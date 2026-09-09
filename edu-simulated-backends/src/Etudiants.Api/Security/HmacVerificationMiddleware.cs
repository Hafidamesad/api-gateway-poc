using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;

namespace Etudiants.Api.Security;

public class HmacVerificationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HmacVerificationMiddleware> _logger;
    private readonly string _hmacSecret;
    private readonly IConnectionMultiplexer _redis;

    private const string TimestampHeader = "X-Timestamp";
    private const string NonceHeader = "X-Nonce";
    private const string SignatureHeader = "X-Signature";

    private static readonly TimeSpan TimestampFreshnessWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan NonceTtl = TimeSpan.FromMinutes(5); // match freshness window

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
            ?? throw new InvalidOperationException(
                "HMAC_SECRET is not set. Export HMAC_SECRET before starting this service (see .env.example at repo root).");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(TimestampHeader, out var timestampValues) ||
            !context.Request.Headers.TryGetValue(NonceHeader, out var nonceValues) ||
            !context.Request.Headers.TryGetValue(SignatureHeader, out var signatureValues))
        {
            _logger.LogWarning(
                "[HMAC] Requete rejetee: en-tetes manquants pour {Method} {Path}",
                context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Signature HMAC ou nonce manquant.");
            return;
        }

        string timestamp = timestampValues.ToString();
        string nonce = nonceValues.ToString();
        string providedSignature = signatureValues.ToString();

        // --- Timestamp freshness check ---
        if (!long.TryParse(timestamp, out long timestampMs))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Timestamp invalide.");
            return;
        }

        var requestTime = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);
        var age = DateTimeOffset.UtcNow - requestTime;

        if (age > TimestampFreshnessWindow || age < -TimestampFreshnessWindow)
        {
            _logger.LogWarning(
                "[HMAC] Requete rejetee: timestamp hors fenetre ({Age}) pour {Method} {Path}",
                age, context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Timestamp expire ou invalide.");
            return;
        }

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

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Signature HMAC invalide.");
            return;
        }

        // --- Nonce replay check (only after signature is confirmed valid) ---
        IDatabase db = _redis.GetDatabase();
        string redisKey = $"nonce:etudiants:{nonce}";

        bool nonceIsNew = await db.StringSetAsync(
            redisKey,
            "1",
            NonceTtl,
            When.NotExists);

        if (!nonceIsNew)
        {
            _logger.LogWarning(
                "[HMAC] Rejeu detecte: nonce deja utilise ({Nonce}) pour {Method} {Path}",
                nonce, method, path);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Nonce deja utilise (rejeu detecte).");
            return;
        }

        _logger.LogInformation(
            "[HMAC] Signature valide, nonce accepte pour {Method} {Path} (timestamp={Timestamp}, nonce={Nonce})",
            method, path, timestamp, nonce);

        await _next(context);
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
