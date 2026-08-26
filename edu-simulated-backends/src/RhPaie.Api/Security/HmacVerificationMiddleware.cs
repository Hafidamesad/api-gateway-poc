using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace RhPaie.Api.Security;

public class HmacVerificationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HmacVerificationMiddleware> _logger;
    private readonly string _hmacSecret;
    private readonly string _jwtSecret;
    private readonly IConnectionMultiplexer _redis;

    private const string TimestampHeader = "X-Timestamp";
    private const string NonceHeader = "X-Nonce";
    private const string SignatureHeader = "X-Signature";
    private const string AuthHeader = "Authorization";
    private const string BearerPrefix = "Bearer ";

    private static readonly TimeSpan TimestampFreshnessWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan NonceTtl = TimeSpan.FromMinutes(5);

    public HmacVerificationMiddleware(
        RequestDelegate next,
        ILogger<HmacVerificationMiddleware> logger,
        IConfiguration configuration,
        IConnectionMultiplexer redis)
    {
        _next = next;
        _logger = logger;
        _redis = redis;

        _hmacSecret = configuration["HMAC_SECRET"]
            ?? Environment.GetEnvironmentVariable("HMAC_SECRET")
            ?? "CHANGE_ME_DEV_SECRET";

        _jwtSecret = configuration["JWT_SECRET"]
            ?? Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? "CHANGE_ME_DEV_JWT_SECRET_MIN_32_CHARS";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/RhPaieService.asmx"))
        {
            await _next(context);
            return;
        }

        if (context.Request.Query.ContainsKey("wsdl"))
        {
            await _next(context);
            return;
        }

        // --- JWT (vérifié en premier, avant tout traitement HMAC/body) ---
        string? authHeader = context.Request.Headers[AuthHeader].ToString();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith(BearerPrefix))
        {
            await RejectAsync(context, "Token JWT manquant.");
            return;
        }

        string token = authHeader.Substring(BearerPrefix.Length);

        if (!TryValidateJwt(token, out var principal))
        {
            _logger.LogWarning("[JWT] Token invalide ou expiré pour {Path}", context.Request.Path);
            await RejectAsync(context, "Token JWT invalide ou expiré.");
            return;
        }

        // --- Headers HMAC ---
        if (!context.Request.Headers.TryGetValue(TimestampHeader, out var timestampValues) ||
            !context.Request.Headers.TryGetValue(NonceHeader, out var nonceValues) ||
            !context.Request.Headers.TryGetValue(SignatureHeader, out var signatureValues))
        {
            await RejectAsync(context, "Headers de sécurité manquants : X-Signature, X-Timestamp, X-Nonce requis.");
            return;
        }

        string timestamp = timestampValues.ToString();
        string nonce = nonceValues.ToString();
        string providedSignature = signatureValues.ToString();

        // --- Fraîcheur du timestamp ---
        if (!long.TryParse(timestamp, out long timestampMs))
        {
            await RejectAsync(context, "Timestamp invalide.");
            return;
        }

        var requestTime = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);
        var age = DateTimeOffset.UtcNow - requestTime;

        if (age > TimestampFreshnessWindow || age < -TimestampFreshnessWindow)
        {
            _logger.LogWarning(
                "[HMAC] Requête rejetée : timestamp hors fenêtre ({Age}) pour {Method} {Path}",
                age, context.Request.Method, context.Request.Path);

            await RejectAsync(context, "Timestamp expiré ou invalide.");
            return;
        }

        // --- Lecture du corps ---
        context.Request.EnableBuffering();

        byte[] bodyBytes;
        using (var memoryStream = new MemoryStream())
        {
            await context.Request.Body.CopyToAsync(memoryStream);
            bodyBytes = memoryStream.ToArray();
        }
        context.Request.Body.Position = 0;

        string method = context.Request.Method.ToUpperInvariant();
        string path = context.Request.Path.Value ?? "/";

        string canonicalString = BuildCanonicalString(method, path, timestamp, nonce, bodyBytes);
        string expectedSignature = ComputeHmac(canonicalString, _hmacSecret);

        if (!IsValidSignature(providedSignature, expectedSignature))
        {
            _logger.LogWarning(
                "[HMAC] Signature invalide pour {Method} {Path} (timestamp={Timestamp}, nonce={Nonce})",
                method, path, timestamp, nonce);

            await RejectAsync(context, "Signature HMAC invalide.");
            return;
        }

        // --- Anti-rejeu par nonce ---
        IDatabase db = _redis.GetDatabase();
        string redisKey = $"nonce:rhpaie:{nonce}";

        bool nonceIsNew = await db.StringSetAsync(redisKey, "1", NonceTtl, When.NotExists);

        if (!nonceIsNew)
        {
            _logger.LogWarning(
                "[HMAC] Rejeu détecté : nonce déjà utilisé ({Nonce}) pour {Method} {Path}",
                nonce, method, path);

            await RejectAsync(context, "Nonce déjà utilisé (rejeu détecté).");
            return;
        }

        _logger.LogInformation(
            "[HMAC] Signature valide, nonce accepté pour {Method} {Path} (timestamp={Timestamp}, nonce={Nonce})",
            method, path, timestamp, nonce);

        await _next(context);
    }

    private bool TryValidateJwt(string token, out System.Security.Claims.ClaimsPrincipal? principal)
    {
        principal = null;
        var handler = new JwtSecurityTokenHandler();

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            principal = handler.ValidateToken(token, validationParameters, out _);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task RejectAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            error = message,
            code = "SECURITY_VERIFICATION_FAILED",
            timestamp = DateTime.UtcNow
        });
    }

    private static string BuildCanonicalString(string method, string path, string timestamp, string nonce, byte[] body)
    {
        string bodyHash = Sha256Hex(body);
        return $"{method}\n{path}\n{timestamp}\n{nonce}\n{bodyHash}";
    }

    private static string ComputeHmac(string canonicalString, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        byte[] rawHmac = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonicalString));
        return Convert.ToBase64String(rawHmac);
    }

    private static bool IsValidSignature(string provided, string expected)
    {
        if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected)) return false;

        byte[] a = Encoding.UTF8.GetBytes(provided);
        byte[] b = Encoding.UTF8.GetBytes(expected);

        if (a.Length != b.Length) return false;

        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static string Sha256Hex(byte[] body)
    {
        byte[] hash = SHA256.HashData(body ?? Array.Empty<byte>());
        var sb = new StringBuilder();
        foreach (byte b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
