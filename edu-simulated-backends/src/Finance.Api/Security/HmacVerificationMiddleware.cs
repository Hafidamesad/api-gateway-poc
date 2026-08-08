using System.Security.Cryptography;
using System.Text;

namespace Finance.Api.Security;

/// <summary>
/// Middleware de verification HMAC-SHA256.
/// Recalcule la signature envoyee par le Gateway (X-Timestamp, X-Signature)
/// et la compare a celle recue. Rejette avec 401 si absente ou invalide.
///
/// IMPORTANT: doit correspondre EXACTEMENT au format cote Gateway
/// (HmacSignatureVerifier.java / HmacSigningFilter.java):
///     canonicalString = METHOD + "\n" + PATH + "\n" + TIMESTAMP + "\n" + SHA256(BODY)
///
/// Le PATH utilise ici est celui recu par CE service (donc DEJA reecrit par le
/// Gateway via SetPath/RewritePath/StripPrefix). Ne pas reconstruire le path
/// original cote client -- le Gateway signe le path final, pas le path client.
///
/// Ordre d'enregistrement dans Program.cs: APRES app.UseHttpsRedirection() (si
/// present, avec sa logique conditionnelle existante) mais AVANT
/// app.MapControllers() / app.UseAuthorization(), pour bloquer les requetes
/// non signees avant qu'elles n'atteignent la logique metier.
/// </summary>
public class HmacVerificationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HmacVerificationMiddleware> _logger;
    private readonly string _hmacSecret;

    private const string TimestampHeader = "X-Timestamp";
    private const string SignatureHeader = "X-Signature";

    public HmacVerificationMiddleware(RequestDelegate next, ILogger<HmacVerificationMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;

        // Meme secret partage que le Gateway (HMAC_SECRET), lu via variable d'environnement.
        // IConfiguration lit automatiquement les variables d'environnement en ASP.NET Core.
        _hmacSecret = configuration["HMAC_SECRET"]
            ?? Environment.GetEnvironmentVariable("HMAC_SECRET")
            ?? "CHANGE_ME_DEV_SECRET";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // NOTE: /health n'est PAS exempte. Le Gateway signe deja les requetes
        // de health check (confirme dans les logs), donc /health sert de banc
        // de test naturel pour le protocole negatif/positif (comme pour mTLS):
        // requete sans signature -> 401, requete avec mauvaise signature -> 401,
        // requete correctement signee -> 200.

        if (!context.Request.Headers.TryGetValue(TimestampHeader, out var timestampValues) ||
            !context.Request.Headers.TryGetValue(SignatureHeader, out var signatureValues))
        {
            _logger.LogWarning("[HMAC] Requete rejetee: en-tetes X-Timestamp/X-Signature manquants pour {Method} {Path}",
                context.Request.Method, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Signature HMAC manquante.");
            return;
        }

        string timestamp = timestampValues.ToString();
        string providedSignature = signatureValues.ToString();

        // Permet de relire le corps plus tard (dans le controleur)
        context.Request.EnableBuffering();

        byte[] bodyBytes;
        using (var memoryStream = new MemoryStream())
        {
            await context.Request.Body.CopyToAsync(memoryStream);
            bodyBytes = memoryStream.ToArray();
        }
        context.Request.Body.Position = 0; // reset pour le controleur

        string method = context.Request.Method.ToUpperInvariant();
        string path = context.Request.Path.Value ?? "/";

        string canonicalString = BuildCanonicalString(method, path, timestamp, bodyBytes);
        string expectedSignature = ComputeHmac(canonicalString, _hmacSecret);

        if (!IsValidSignature(providedSignature, expectedSignature))
        {
            _logger.LogWarning("[HMAC] Signature invalide pour {Method} {Path} (timestamp={Timestamp})",
                method, path, timestamp);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Signature HMAC invalide.");
            return;
        }

        _logger.LogInformation("[HMAC] Signature valide pour {Method} {Path} (timestamp={Timestamp})",
            method, path, timestamp);

        await _next(context);
    }

    private static string BuildCanonicalString(string method, string path, string timestamp, byte[] body)
    {
        string bodyHash = Sha256Hex(body);
        return $"{method}\n{path}\n{timestamp}\n{bodyHash}";
    }

    private static string ComputeHmac(string canonicalString, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        byte[] rawHmac = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonicalString));
        return Convert.ToBase64String(rawHmac);
    }

    private static bool IsValidSignature(string provided, string expected)
    {
        if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected))
        {
            return false;
        }
        byte[] a = Encoding.UTF8.GetBytes(provided);
        byte[] b = Encoding.UTF8.GetBytes(expected);
        if (a.Length != b.Length)
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static string Sha256Hex(byte[] body)
    {
        byte[] hash = SHA256.HashData(body ?? Array.Empty<byte>());
        var sb = new StringBuilder();
        foreach (byte b in hash)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}
