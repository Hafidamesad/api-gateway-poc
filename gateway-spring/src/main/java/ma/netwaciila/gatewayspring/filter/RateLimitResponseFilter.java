package ma.netwaciila.gatewayspring.filter;

import org.springframework.cloud.gateway.filter.GlobalFilter;
import org.springframework.core.Ordered;
import org.springframework.core.io.buffer.DataBuffer;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.stereotype.Component;
import reactor.core.publisher.Mono;

import java.nio.charset.StandardCharsets;

@Component
public class RateLimitResponseFilter implements GlobalFilter, Ordered {

    @Override
    public Mono<Void> filter(
            org.springframework.web.server.ServerWebExchange exchange,
            org.springframework.cloud.gateway.filter.GatewayFilterChain chain) {

        return chain.filter(exchange)
                .then(Mono.defer(() -> {

                    if (exchange.getResponse().getStatusCode()
                            == HttpStatus.TOO_MANY_REQUESTS) {

                        var response = exchange.getResponse();

                        if (response.isCommitted()) {
                            return Mono.empty();
                        }

                        String json = """
                                {
                                  "error": "rate_limit_exceeded",
                                  "message": "Too many requests. Please try again later.",
                                  "status": 429
                                }
                                """;

                        response.setStatusCode(HttpStatus.TOO_MANY_REQUESTS);
                        response.getHeaders().setContentType(MediaType.APPLICATION_JSON);

                        DataBuffer buffer = response.bufferFactory()
                                .wrap(json.getBytes(StandardCharsets.UTF_8));

                        return response.writeWith(Mono.just(buffer));
                    }

                    return Mono.empty();
                }));
    }

    @Override
    public int getOrder() {
        return Ordered.LOWEST_PRECEDENCE;
    }
}
