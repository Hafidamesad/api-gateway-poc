package ma.netwaciila.gatewayspring.security;

import io.jsonwebtoken.Claims;
import io.jsonwebtoken.ExpiredJwtException;
import io.jsonwebtoken.Jwts;
import io.jsonwebtoken.MalformedJwtException;
import io.jsonwebtoken.SignatureException;
import io.jsonwebtoken.security.Keys;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import javax.crypto.SecretKey;
import java.nio.charset.StandardCharsets;
import java.util.Date;

/**
 * Utilitaire JWT (HS256). Genere et valide les tokens.
 * Meme secret partage entre Gateway (emission + validation) et backends .NET
 * (validation uniquement) -- variable d'environnement JWT_SECRET.
 *
 * NOTE: le secret HS256 doit faire au moins 256 bits (32 caracteres) sinon
 * la librairie JJWT rejette la cle au demarrage (WeakKeyException).
 */
@Component
public class JwtUtil {

    private static final Logger log = LoggerFactory.getLogger(JwtUtil.class);

    // TODO phase secret management: migrer vers Vault. Env var pour l'instant.
    @Value("${security.jwt.secret:CHANGE_ME_DEV_JWT_SECRET_MIN_32_CHARS}")
    private String jwtSecret;

    // Duree de validite du token: 1 heure (3600000 ms)
    @Value("${security.jwt.expiration-ms:3600000}")
    private long expirationMs;

    private SecretKey signingKey;

    private SecretKey getSigningKey() {
        if (signingKey == null) {
            signingKey = Keys.hmacShaKeyFor(jwtSecret.getBytes(StandardCharsets.UTF_8));
        }
        return signingKey;
    }

    /**
     * Genere un JWT signe pour un utilisateur donne.
     */
    public String generateToken(String username, String role) {
        Date now = new Date();
        Date expiry = new Date(now.getTime() + expirationMs);

        return Jwts.builder()
                .subject(username)
                .claim("role", role)
                .issuedAt(now)
                .expiration(expiry)
                .signWith(getSigningKey(), Jwts.SIG.HS256)
                .compact();
    }

    /**
     * Valide un token et retourne ses claims si valide.
     * Retourne null si invalide (signature, expiration, format).
     */
    public Claims validateAndParse(String token) {
        try {
            return Jwts.parser()
                    .verifyWith(getSigningKey())
                    .build()
                    .parseSignedClaims(token)
                    .getPayload();
        } catch (ExpiredJwtException e) {
            log.warn("[JWT] Token expire: {}", e.getMessage());
            return null;
        } catch (SignatureException e) {
            log.warn("[JWT] Signature invalide: {}", e.getMessage());
            return null;
        } catch (MalformedJwtException e) {
            log.warn("[JWT] Token malforme: {}", e.getMessage());
            return null;
        } catch (Exception e) {
            log.warn("[JWT] Erreur validation token: {}", e.getMessage());
            return null;
        }
    }
}
