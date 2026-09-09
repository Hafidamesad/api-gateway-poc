package ma.netwaciila.gatewayspring.filter;

import ma.netwaciila.gatewayspring.config.RoleMatrixProperties;
import org.springframework.core.Ordered;
import org.springframework.http.HttpStatus;
import org.springframework.http.server.reactive.ServerHttpRequest;
import org.springframework.http.server.reactive.ServerHttpResponse;
import org.springframework.stereotype.Component;
import org.springframework.util.AntPathMatcher;
import org.springframework.web.server.ServerWebExchange;
import org.springframework.web.server.WebFilter;
import org.springframework.web.server.WebFilterChain;
import reactor.core.publisher.Mono;

import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.Map;

/**
 * Gateway-level role-based authorization.
 *
 * Runs after JwtAuthenticationFilter so that the JWT has already been
 * validated and X-Authenticated-Role has been populated from the
 * validated JWT claims.
 */
@Component
public class RoleAuthorizationFilter implements WebFilter, Ordered {

    private static final String ROLE_HEADER = "X-Authenticated-Role";

    private final AntPathMatcher pathMatcher = new AntPathMatcher();
    private final RoleMatrixProperties roleMatrixProperties;

    public RoleAuthorizationFilter(RoleMatrixProperties roleMatrixProperties) {
        this.roleMatrixProperties = roleMatrixProperties;
    }

    @Override
    public Mono<Void> filter(ServerWebExchange exchange, WebFilterChain chain) {

        ServerHttpRequest request = exchange.getRequest();
        String path = request.getURI().getRawPath();

        /*
         * Public endpoints must not require role authorization.
         * JWT filter already handles these paths too.
         */
        if (isExempt(path)) {
            return chain.filter(exchange);
        }

        /*
         * Find the role rule corresponding to the requested path.
         */
        String matchedPattern = findMatchingPattern(path);

        /*
         * Fail closed:
         * if a protected path has no role rule, reject it.
         */
        if (matchedPattern == null) {
            return reject(
                    exchange,
                    "Aucune regle de role definie pour ce chemin."
            );
        }

        /*
         * Get allowed roles for the matched route.
         */
        List<String> allowedRoles =
                roleMatrixProperties.getRoleMatrix().get(matchedPattern);

        /*
         * The role was created by JwtAuthenticationFilter after
         * successful JWT validation.
         */
        String role = request.getHeaders().getFirst(ROLE_HEADER);

        if (role == null || role.isBlank()) {
            return reject(
                    exchange,
                    "Aucun role authentifie trouve."
            );
        }

        /*
         * Check whether the authenticated role is allowed.
         */
        if (allowedRoles == null || !allowedRoles.contains(role)) {
            return reject(
                    exchange,
                    "Role '" + role + "' non autorise pour cette ressource."
            );
        }

        /*
         * Role is authorized -> continue to the next filter.
         */
        return chain.filter(exchange);
    }

    /**
     * Finds the first configured path pattern matching the request path.
     */
    private String findMatchingPattern(String path) {

        Map<String, List<String>> roleMatrix =
                roleMatrixProperties.getRoleMatrix();

        for (String pattern : roleMatrix.keySet()) {
            if (pathMatcher.match(pattern, path)) {
                return pattern;
            }
        }

        return null;
    }

    /**
     * Endpoints that do not require role authorization.
     */
    private boolean isExempt(String path) {

        return path.equals("/auth/login")
                || path.equals("/actuator/health")
                || path.endsWith("/health")
                || path.contains("wsdl");
    }

    /**
     * Reject unauthorized requests with HTTP 403.
     */
    private Mono<Void> reject(
            ServerWebExchange exchange,
            String message) {

        ServerHttpResponse response = exchange.getResponse();
        response.getHeaders().add("X-Auth-Reject-Reason", message);
        response.setStatusCode(HttpStatus.FORBIDDEN);
        response.getHeaders().add(
                "Content-Type",
                "text/plain"
        );

        byte[] bytes = message.getBytes(StandardCharsets.UTF_8);

        return response.writeWith(
                Mono.just(
                        response.bufferFactory().wrap(bytes)
                )
        );
    }

    /**
     * JWTAuthenticationFilter:
     * HIGHEST_PRECEDENCE + 1
     *
     * We run immediately after it.
     */
    @Override
    public int getOrder() {
        return Ordered.HIGHEST_PRECEDENCE + 2;
    }
}
