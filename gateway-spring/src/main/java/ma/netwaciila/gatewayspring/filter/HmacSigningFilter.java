package ma.netwaciila.gatewayspring.filter;

import ma.netwaciila.gatewayspring.security.HmacSignatureVerifier;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.cloud.gateway.filter.GatewayFilterChain;
import org.springframework.cloud.gateway.filter.GlobalFilter;
import org.springframework.core.Ordered;
import org.springframework.core.io.buffer.DataBuffer;
import org.springframework.core.io.buffer.DataBufferUtils;
import org.springframework.http.server.reactive.ServerHttpRequest;
import org.springframework.http.server.reactive.ServerHttpRequestDecorator;
import org.springframework.stereotype.Component;
import org.springframework.web.server.ServerWebExchange;
import reactor.core.publisher.Flux;
import reactor.core.publisher.Mono;

import java.nio.charset.StandardCharsets;
import java.time.Instant;

/**
 * Filtre global cote Gateway : signe chaque requete avant transfert mTLS vers le backend.
 *
 * NOTE naming: correspond a "HmacSignatureVerifier"/filtre HMAC prevu dans le plan initial.
 * Renomme HmacSigningFilter ici car ce filtre SIGNE (cote Gateway) ; la VERIFICATION
 * (recalcul + comparaison) se fait cote backend .NET, dans un middleware separe.
 *
 * ORDRE CRITIQUE : ce filtre doit s'executer APRES les filtres de route
 * (SetPath / RewritePath / StripPrefix definis dans application-secure.yaml)
 * mais AVANT le forward reseau reel vers le backend (NettyRoutingFilter,
 * qui s'execute a Ordered.LOWEST_PRECEDENCE - 1, donc en tout dernier).
 *
 * Pourquoi : les routes /api/finance/health -> SetPath=/health,
 * /api/finance/** -> RewritePath=/api/finance/(...) -> /api/${segment},
 * /api/rhpaie/** -> StripPrefix=2, changent le path AVANT que la requete
 * n'atteigne le backend. Si on signe le path original (client) au lieu du
 * path reecrit, le backend recalculera un HMAC different et rejettera
 * TOUTES les requetes -- echec silencieux, aucune vraie faille de securite,
 * juste une chaine canonique qui ne correspond jamais des deux cotes.
 *
 * Valeur choisie : tres superieure aux filtres de route (ordre ~1,2,3...)
 * mais tres inferieure a LOWEST_PRECEDENCE-1 (forward reseau, ~Integer.MAX-1).
 *
 * RoutingLogFilter reste a LOWEST_PRECEDENCE (log apres reponse), pas de conflit.
 */
@Component
public class HmacSigningFilter implements GlobalFilter, Ordered {

    private static final Logger log = LoggerFactory.getLogger(HmacSigningFilter.class);

    // TODO phase secret management: remplacer par Vault. Pour l'instant: variable d'environnement.
    @Value("${security.hmac.secret:CHANGE_ME_DEV_SECRET}")
    private String hmacSecret;

    @Override
    public Mono<Void> filter(ServerWebExchange exchange, GatewayFilterChain chain) {
        ServerHttpRequest request = exchange.getRequest();

        return DataBufferUtils.join(request.getBody())
                .defaultIfEmpty(exchange.getResponse().bufferFactory().wrap(new byte[0]))
                .flatMap(dataBuffer -> {
                    byte[] bodyBytes = new byte[dataBuffer.readableByteCount()];
                    dataBuffer.read(bodyBytes);
                    DataBufferUtils.release(dataBuffer);

                    String timestamp = String.valueOf(Instant.now().toEpochMilli());
                    String method = request.getMethod().name();
                    String path = request.getURI().getRawPath();

                    String canonicalString = HmacSignatureVerifier.buildCanonicalString(
                            method, path, timestamp, bodyBytes);
                    String signature = HmacSignatureVerifier.computeHmac(canonicalString, hmacSecret);

                    log.info("[HMAC] {} {} -> timestamp={} signature={}",
                            method, path, timestamp, signature);

                    ServerHttpRequest mutatedRequest = new ServerHttpRequestDecorator(request) {
                        @Override
                        public Flux<DataBuffer> getBody() {
                            // Re-injecte le corps (deja lu une fois pour le hash)
                            DataBuffer buffer = exchange.getResponse()
                                    .bufferFactory()
                                    .wrap(bodyBytes);
                            return Flux.just(buffer);
                        }
                    };

                    ServerHttpRequest requestWithHeaders = mutatedRequest.mutate()
                            .header("X-Timestamp", timestamp)
                            .header("X-Signature", signature)
                            .build();

                    ServerWebExchange mutatedExchange = exchange.mutate()
                            .request(requestWithHeaders)
                            .build();

                    return chain.filter(mutatedExchange);
                });
    }

    @Override
    public int getOrder() {
        // Doit s'executer apres SetPath/RewritePath/StripPrefix (ordre bas)
        // et avant le forward reseau reel (LOWEST_PRECEDENCE - 1, tres eleve).
        return 10_000;
    }
}
