package ma.netwaciila.gatewayspring.filter;

import io.jsonwebtoken.Claims;
import ma.netwaciila.gatewayspring.security.JwtUtil;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.core.Ordered;
import org.springframework.http.HttpStatus;
import org.springframework.http.server.reactive.ServerHttpRequest;
import org.springframework.stereotype.Component;
import org.springframework.web.server.ServerWebExchange;
import org.springframework.web.server.WebFilter;
import org.springframework.web.server.WebFilterChain;
import reactor.core.publisher.Mono;

import java.nio.charset.StandardCharsets;

/**
 * Filtre d'authentification JWT. S'execute AVANT le routage ET avant la
 * signature HMAC (inutile de signer une requete non authentifiee).
 *
 * IMPORTANT -- type de filtre: WebFilter (pas GlobalFilter comme les autres).
 * Raison: /auth/login est gere par un @RestController Spring MVC classique
 * (AuthController), pas par une route Gateway. Les GlobalFilter de Spring
 * Cloud Gateway ne s'appliquent qu'aux requetes qui matchent une route
 * definie dans application(-secure).yaml. Un WebFilter s'applique a TOUTE
 * requete entrante dans l'application, y compris /auth/login lui-meme,
 * ce qui nous permet de l'exclure explicitement ici.
 *
 * Ordre: HIGHEST_PRECEDENCE + 1 -- doit s'executer avant HmacSigningFilter
 * (order=10_000) et avant tout traitement de route.
 */
@Component
public class JwtAuthenticationFilter implements WebFilter, Ordered {

    private static final Logger log = LoggerFactory.getLogger(JwtAuthenticationFilter.class);

    private static final String AUTH_HEADER = "Authorization";
    private static final String BEARER_PREFIX = "Bearer ";

    // Chemins publics, exemptes de JWT (login, health checks actuator)
    private static final String[] PUBLIC_PATHS = {
        "/auth/login",
        "/actuator/health",
        "/api/finance/health",
        "/api/etudiants/health",
        "/api/transport/health",
        "/api/academique/health",
        "/api/rhpaie/health"
};

    private final JwtUtil jwtUtil;

    public JwtAuthenticationFilter(JwtUtil jwtUtil) {
        this.jwtUtil = jwtUtil;
    }

    @Override
    public Mono<Void> filter(ServerWebExchange exchange, WebFilterChain chain) {
        ServerHttpRequest request = exchange.getRequest();
        String path = request.getURI().getRawPath();

        for (String publicPath : PUBLIC_PATHS) {
            if (path.equals(publicPath)) {
                return chain.filter(exchange);
            }
        }

        String authHeader = request.getHeaders().getFirst(AUTH_HEADER);

        if (authHeader == null || !authHeader.startsWith(BEARER_PREFIX)) {
            log.warn("[JWT] Requete rejetee: en-tete Authorization Bearer manquant pour {} {}",
                    request.getMethod(), path);
            return reject(exchange, "Token JWT manquant.");
        }

        String token = authHeader.substring(BEARER_PREFIX.length());
        Claims claims = jwtUtil.validateAndParse(token);

        if (claims == null) {
            log.warn("[JWT] Requete rejetee: token invalide ou expire pour {} {}",
                    request.getMethod(), path);
            return reject(exchange, "Token JWT invalide ou expire.");
        }

        String username = claims.getSubject();
        String role = claims.get("role", String.class);
        log.info("[JWT] Authentifie: user={} role={} -> {} {}", username, role, request.getMethod(), path);

        // Propage l'identite vers les filtres suivants (utile pour audit/logging futur)
        ServerHttpRequest mutatedRequest = request.mutate()
                .header("X-Authenticated-User", username)
                .header("X-Authenticated-Role", role)
                .build();

        ServerWebExchange mutatedExchange = exchange.mutate()
                .request(mutatedRequest)
                .build();

        return chain.filter(mutatedExchange);
    }

    private Mono<Void> reject(ServerWebExchange exchange, String message) {
        exchange.getResponse().setStatusCode(HttpStatus.UNAUTHORIZED);
        exchange.getResponse().getHeaders().add("Content-Type", "text/plain");
        byte[] bytes = message.getBytes(StandardCharsets.UTF_8);
        return exchange.getResponse().writeWith(
                Mono.just(exchange.getResponse().bufferFactory().wrap(bytes)));
    }

    @Override
    public int getOrder() {
        return Ordered.HIGHEST_PRECEDENCE + 1;
    }
}
