package ma.netwaciila.gatewayspring.config;

import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.stereotype.Component;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

@Component
@ConfigurationProperties(prefix = "security")
public class RoleMatrixProperties {

    private Map<String, List<String>> roleMatrix = new LinkedHashMap<>();

    public Map<String, List<String>> getRoleMatrix() {
        return roleMatrix;
    }

    public void setRoleMatrix(Map<String, List<String>> roleMatrix) {
        this.roleMatrix = roleMatrix;
    }
}
