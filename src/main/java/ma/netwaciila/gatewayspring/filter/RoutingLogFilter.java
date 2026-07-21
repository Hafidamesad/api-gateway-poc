package ma.netwaciila.gatewayspring.filter;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.cloud.gateway.filter.GatewayFilterChain;
import org.springframework.cloud.gateway.filter.GlobalFilter;
import org.springframework.cloud.gateway.route.Route;
import org.springframework.cloud.gateway.support.ServerWebExchangeUtils;
import org.springframework.core.Ordered;
import org.springframework.http.server.reactive.ServerHttpRequest;
import org.springframework.stereotype.Component;
import org.springframework.web.server.ServerWebExchange;
import reactor.core.publisher.Mono;

import java.net.URI;

/**
 * Filtre de traçabilité pour le prototype #1 (variante réactive/WebFlux).
 * Objectif : valider visuellement (logs) que chaque requête est bien routée
 * vers le bon backend, et mesurer le temps passé côté Gateway.
 * A remplacer/étendre par les filtres de sécurité d'Imane (OAuth2/mTLS) à l'étape 4.
 */
@Component
public class RoutingLogFilter implements GlobalFilter, Ordered {

    private static final Logger log = LoggerFactory.getLogger(RoutingLogFilter.class);
    private static final String START_TIME_ATTR = "gw.startTimeMillis";

    @Override
    public Mono<Void> filter(ServerWebExchange exchange, GatewayFilterChain chain) {
        exchange.getAttributes().put(START_TIME_ATTR, System.currentTimeMillis());
        ServerHttpRequest request = exchange.getRequest();

        return chain.filter(exchange).then(Mono.fromRunnable(() -> {
            long start = (long) exchange.getAttributes().getOrDefault(START_TIME_ATTR, System.currentTimeMillis());
            long elapsedMs = System.currentTimeMillis() - start;

            Route route = exchange.getAttribute(ServerWebExchangeUtils.GATEWAY_ROUTE_ATTR);
            URI targetUri = route != null ? route.getUri() : null;
            Integer statusCode = exchange.getResponse().getStatusCode() != null
                    ? exchange.getResponse().getStatusCode().value() : null;

            log.info("[GATEWAY] {} {} -> route={} target={} status={} latenceGatewayMs={}",
                    request.getMethod(),
                    request.getPath(),
                    route != null ? route.getId() : "AUCUNE",
                    targetUri,
                    statusCode,
                    elapsedMs);

            exchange.getResponse().getHeaders().add("X-Gateway-Latency-Ms", String.valueOf(elapsedMs));
        }));
    }

    @Override
    public int getOrder() {
        return Ordered.LOWEST_PRECEDENCE;
    }
}