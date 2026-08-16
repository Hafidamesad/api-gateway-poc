package ma.netwaciila.gatewayspring.filter;

import org.springframework.cloud.gateway.filter.ratelimit.KeyResolver;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import reactor.core.publisher.Mono;

@Configuration
public class RateLimiterConfig {

    @Bean
    public KeyResolver clientKeyResolver() {
        return exchange -> {
            String clientId = exchange.getRequest()
                    .getHeaders()
                    .getFirst("X-Client-Id");

            if (clientId != null && !clientId.isBlank()) {
                return Mono.just(clientId);
            }

            // Fallback : utiliser l'adresse IP si X-Client-Id n'est pas présent
            String remoteAddress = exchange.getRequest()
                    .getRemoteAddress() != null
                    ? exchange.getRequest()
                        .getRemoteAddress()
                        .getAddress()
                        .getHostAddress()
                    : "unknown";

            return Mono.just(remoteAddress);
        };
    }
}
