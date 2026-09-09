package ma.netwaciila.gatewayspring.config;
import org.springframework.core.io.ClassPathResource;
import java.io.InputStream;
import io.netty.handler.ssl.SslContextBuilder;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import reactor.netty.http.client.HttpClient;

import javax.net.ssl.KeyManagerFactory;
import javax.net.ssl.TrustManagerFactory;
import java.security.KeyStore;

import reactor.netty.resources.ConnectionProvider;
import java.time.Duration;

@Configuration
public class GatewaySslConfig {

    @Value("${gateway.ssl.key-store}")
    private String keyStorePath;

    @Value("${gateway.ssl.key-store-password}")
    private String keyStorePassword;

    @Value("${gateway.ssl.trust-store}")
    private String trustStorePath;

    @Value("${gateway.ssl.trust-store-password}")
    private String trustStorePassword;
private javax.net.ssl.SSLParameters disableHostnameVerification(javax.net.ssl.SSLParameters params) {
    params.setEndpointIdentificationAlgorithm(null);
    return params;
}
    @Bean
    public HttpClient httpClient() throws Exception {
        // Charge l'identité du Gateway (ce qu'il présente aux backends)
        KeyStore keyStore = KeyStore.getInstance("PKCS12");
String keyStoreResource = keyStorePath.replace("classpath:", "");

try (InputStream is = new ClassPathResource(keyStoreResource).getInputStream()) {
    keyStore.load(is, keyStorePassword.toCharArray());
}
        KeyManagerFactory keyManagerFactory = KeyManagerFactory.getInstance(
                KeyManagerFactory.getDefaultAlgorithm());
        keyManagerFactory.init(keyStore, keyStorePassword.toCharArray());

        // Charge le truststore (les CA en qui le Gateway a confiance côté backend)
        KeyStore trustStore = KeyStore.getInstance("PKCS12");
String trustStoreResource = trustStorePath.replace("classpath:", "");

try (InputStream is = new ClassPathResource(trustStoreResource).getInputStream()) {
    trustStore.load(is, trustStorePassword.toCharArray());
}
        TrustManagerFactory trustManagerFactory = TrustManagerFactory.getInstance(
                TrustManagerFactory.getDefaultAlgorithm());
        trustManagerFactory.init(trustStore);

var sslContext = SslContextBuilder.forClient()
        .keyManager(keyManagerFactory)
        .trustManager(trustManagerFactory)
        .build();

ConnectionProvider provider = ConnectionProvider.builder("gateway-backend-pool")
        .maxConnections(100)
        .pendingAcquireTimeout(Duration.ofSeconds(15))
        .maxIdleTime(Duration.ofSeconds(30))
        .build();

return HttpClient.create(provider)
        .secure(sslSpec -> sslSpec
                .sslContext(sslContext)
                .handshakeTimeout(Duration.ofSeconds(30))
                .handlerConfigurator(sslHandler ->
                        sslHandler.engine().setSSLParameters(disableHostnameVerification(
                                sslHandler.engine().getSSLParameters()))));
    }
}
