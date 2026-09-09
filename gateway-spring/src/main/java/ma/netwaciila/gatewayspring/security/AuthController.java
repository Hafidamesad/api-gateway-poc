package ma.netwaciila.gatewayspring.security;

import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;
import reactor.core.publisher.Mono;

import java.util.HashMap;
import java.util.Map;

/**
 * Controleur d'authentification. Utilisateurs FACTICES en memoire pour
 * l'instant (phase de developpement) -- a remplacer par une verification
 * reelle (Etudiants.Api ou service d'identite dedie) dans une phase future.
 *
 * Endpoint expose: POST /auth/login
 * Ce endpoint DOIT etre exclu du filtre JwtAuthenticationFilter, sinon
 * impossible de se connecter (boucle: il faut un token pour obtenir un token).
 */
@RestController
public class AuthController {

    private final JwtUtil jwtUtil;

    // Utilisateurs de test -- en dur, uniquement pour developpement/demo.
    // username -> [password, role]
    private static final Map<String, String[]> FAKE_USERS = new HashMap<>();
    static {
        FAKE_USERS.put("admin", new String[]{"admin123", "ADMIN"});
        FAKE_USERS.put("etudiant", new String[]{"etudiant123", "ETUDIANT"});
        FAKE_USERS.put("finance", new String[]{"finance123", "FINANCE"});
        FAKE_USERS.put("rh", new String[]{"rh123", "RH"});  
        FAKE_USERS.put("transport", new String[]{"transport123", "TRANSPORT"});
        FAKE_USERS.put("academique", new String[]{"academique123", "ACADEMIQUE"});
}

    public AuthController(JwtUtil jwtUtil) {
        this.jwtUtil = jwtUtil;
    }

    public record LoginRequest(String username, String password) {}
    public record LoginResponse(String token, String tokenType, long expiresInMs) {}
    public record ErrorResponse(String error) {}

    @PostMapping("/auth/login")
    public Mono<ResponseEntity<Object>> login(@RequestBody LoginRequest request) {
        String[] userRecord = FAKE_USERS.get(request.username());

        if (userRecord == null || !userRecord[0].equals(request.password())) {
            return Mono.just(ResponseEntity
                    .status(HttpStatus.UNAUTHORIZED)
                    .body(new ErrorResponse("Identifiants invalides.")));
        }

        String role = userRecord[1];
        String token = jwtUtil.generateToken(request.username(), role);

        return Mono.just(ResponseEntity.ok(new LoginResponse(token, "Bearer", 3600000)));
    }
}
