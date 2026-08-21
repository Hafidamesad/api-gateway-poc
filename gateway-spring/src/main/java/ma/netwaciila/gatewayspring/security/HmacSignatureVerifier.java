package ma.netwaciila.gatewayspring.security;

import javax.crypto.Mac;
import javax.crypto.spec.SecretKeySpec;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.Base64;

/**
 * Utilitaire HMAC-SHA256.
 *
 * Calcule une signature à partir d'une chaîne canonique + secret partagé.
 *
 * Format de la chaîne canonique:
 *
 * METHOD
 * PATH
 * TIMESTAMP
 * NONCE
 * SHA256(BODY)
 */
public final class HmacSignatureVerifier {

    private static final String HMAC_ALGO = "HmacSHA256";

    private HmacSignatureVerifier() {
        // utilitaire statique, pas d'instanciation
    }

    /**
     * Construit la chaîne canonique.
     *
     * Format:
     * METHOD\n
     * PATH\n
     * TIMESTAMP\n
     * NONCE\n
     * SHA256(BODY)
     */
    public static String buildCanonicalString(
            String method,
            String path,
            String timestamp,
            String nonce,
            byte[] body) {

        String bodyHash = sha256Hex(body);

        return method + "\n"
                + path + "\n"
                + timestamp + "\n"
                + nonce + "\n"
                + bodyHash;
    }

    /**
     * Calcule HMAC-SHA256 de la chaîne canonique,
     * encodé en Base64.
     */
    public static String computeHmac(
            String canonicalString,
            String secret) {

        try {
            Mac mac = Mac.getInstance(HMAC_ALGO);

            SecretKeySpec keySpec = new SecretKeySpec(
                    secret.getBytes(StandardCharsets.UTF_8),
                    HMAC_ALGO
            );

            mac.init(keySpec);

            byte[] rawHmac = mac.doFinal(
                    canonicalString.getBytes(StandardCharsets.UTF_8)
            );

            return Base64.getEncoder().encodeToString(rawHmac);

        } catch (Exception e) {
            throw new IllegalStateException(
                    "Erreur calcul HMAC",
                    e
            );
        }
    }

    /**
     * Compare deux signatures en temps constant
     * (protection contre les timing attacks).
     */
    public static boolean isValidSignature(
            String provided,
            String expected) {

        if (provided == null || expected == null) {
            return false;
        }

        byte[] a = provided.getBytes(StandardCharsets.UTF_8);
        byte[] b = expected.getBytes(StandardCharsets.UTF_8);

        return MessageDigest.isEqual(a, b);
    }

    /**
     * Vérifie directement une signature HMAC.
     *
     * Reconstruit la chaîne canonique avec:
     * METHOD + PATH + TIMESTAMP + NONCE + SHA256(BODY)
     *
     * puis compare la signature reçue avec la signature calculée.
     */
    public static boolean verify(
            String method,
            String path,
            String timestamp,
            String nonce,
            byte[] body,
            String secret,
            String providedSignature) {

        String canonicalString = buildCanonicalString(
                method,
                path,
                timestamp,
                nonce,
                body
        );

        String expectedSignature = computeHmac(
                canonicalString,
                secret
        );

        return isValidSignature(
                providedSignature,
                expectedSignature
        );
    }

    /**
     * SHA-256 du corps de requête, en hexadécimal.
     */
    public static String sha256Hex(byte[] body) {

        try {
            MessageDigest digest =
                    MessageDigest.getInstance("SHA-256");

            byte[] hash = digest.digest(
                    body != null ? body : new byte[0]
            );

            StringBuilder sb = new StringBuilder();

            for (byte b : hash) {
                sb.append(String.format("%02x", b));
            }

            return sb.toString();

        } catch (NoSuchAlgorithmException e) {
            throw new IllegalStateException(
                    "SHA-256 non disponible",
                    e
            );
        }
    }
}
