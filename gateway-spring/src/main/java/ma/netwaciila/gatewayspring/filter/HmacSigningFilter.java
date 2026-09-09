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

import java.time.Instant;
import java.util.UUID;

/**
 * Filtre global cote Gateway : signe chaque requete avant transfert mTLS vers le backend.
 *
 * La signature HMAC protege la chaine canonique suivante :
 *
 * METHOD
 * PATH
 * TIMESTAMP
 * NONCE
 * SHA256(BODY)
 *
 * Le nonce est inclus dans la signature afin qu'il ne puisse pas etre
 * modifie sans rendre la signature invalide.
 *
 * ORDRE CRITIQUE : ce filtre doit s'executer APRES les filtres de route
 * (SetPath / RewritePath / StripPrefix definis dans application-secure.yaml)
 * mais AVANT le forward reseau reel vers le backend.
 */
@Component
public class HmacSigningFilter implements GlobalFilter, Ordered {

    private static final Logger log =
            LoggerFactory.getLogger(HmacSigningFilter.class);

    // TODO phase secret management: remplacer par Vault.
    // Pour l'instant: variable d'environnement/configuration.
    @Value("${security.hmac.secret}")
    private String hmacSecret;

    @Override
    public Mono<Void> filter(
            ServerWebExchange exchange,
            GatewayFilterChain chain) {

        ServerHttpRequest request = exchange.getRequest();

        return DataBufferUtils.join(request.getBody())
                .defaultIfEmpty(
                        exchange.getResponse()
                                .bufferFactory()
                                .wrap(new byte[0])
                )
                .flatMap(dataBuffer -> {

                    byte[] bodyBytes =
                            new byte[dataBuffer.readableByteCount()];

                    dataBuffer.read(bodyBytes);
                    DataBufferUtils.release(dataBuffer);

                    // 1. Informations de la requete
                    String timestamp =
                            String.valueOf(Instant.now().toEpochMilli());

                    String nonce =
                            UUID.randomUUID().toString();

                    String method =
                            request.getMethod().name();

                    String path =
                            request.getURI().getRawPath();

                    // 2. Construire la chaine canonique
                    // METHOD + PATH + TIMESTAMP + NONCE + SHA256(BODY)
                    String canonicalString =
                            HmacSignatureVerifier.buildCanonicalString(
                                    method,
                                    path,
                                    timestamp,
                                    nonce,
                                    bodyBytes
                            );

                    // 3. Calculer la signature HMAC
                    String signature =
                            HmacSignatureVerifier.computeHmac(
                                    canonicalString,
                                    hmacSecret
                            );

                    log.info(
                            "[HMAC] {} {} -> timestamp={} nonce={} signature={}",
                            method,
                            path,
                            timestamp,
                            nonce,
                            signature
                    );

                    // 4. Re-injecter le body car il a deja ete lu
                    ServerHttpRequest mutatedRequest =
                            new ServerHttpRequestDecorator(request) {

                                @Override
                                public Flux<DataBuffer> getBody() {

                                    DataBuffer buffer =
                                            exchange.getResponse()
                                                    .bufferFactory()
                                                    .wrap(bodyBytes);

                                    return Flux.just(buffer);
                                }
                            };

                    // 5. Ajouter les headers de securite
                    ServerHttpRequest requestWithHeaders =
                            mutatedRequest.mutate()
                                    .header(
                                            "X-Timestamp",
                                            timestamp
                                    )
                                    .header(
                                            "X-Nonce",
                                            nonce
                                    )
                                    .header(
                                            "X-Signature",
                                            signature
                                    )
                                    .build();

                    ServerWebExchange mutatedExchange =
                            exchange.mutate()
                                    .request(requestWithHeaders)
                                    .build();

                    return chain.filter(mutatedExchange);
                });
    }

    @Override
    public int getOrder() {
        // Apres les filtres de route et avant le forward reseau.
        return 10_000;
    }
}
