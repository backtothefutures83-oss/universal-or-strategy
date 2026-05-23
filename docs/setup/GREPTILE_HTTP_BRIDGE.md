# Greptile HTTP Bridge Setup

## Problem
- **Greptile API**: HTTP/REST
- **Bob CLI**: SSE (Server-Sent Events) for MCP
- **Incompatibility**: Bob cannot directly consume Greptile's HTTP API

## Solution: Quarkus MCP Server - HTTP Transport

**Reference**: https://code.quarkus.io/?a=greeting-mcp&extension-search=mcp-server

**VS Code Extension**: Quarkus Tools for Visual Studio Code (redhat.vscode-quarkus) - INSTALLED ✅
- Provides Quarkus and MicroProfile development support
- Hot reload, debugging, and project scaffolding
- Version: 1.23.0

### Available Quarkus MCP Extensions

1. **MCP Server - HTTP** (`quarkus-mcp-server-http`) ✅ USE THIS
   - HTTP/SSE transport for MCP server
   - Perfect for bridging Greptile HTTP → Bob SSE

2. **MCP Server - CLI Adapter** (`quarkus-mcp-server-cli-adapter`)
   - Adapts Quarkus CLI to MCP server

3. **MCP Server - STDIO** (`quarkus-mcp-server-stdio`)
   - STDIO transport (not needed for Greptile)

4. **MCP Server - WebSocket** (`quarkus-mcp-server-websocket`)
   - WebSocket transport (alternative option)

### Implementation Steps

#### 1. Generate Quarkus MCP Server

```bash
# Visit: https://code.quarkus.io/?a=greptile-mcp-bridge&extension-search=mcp-server
# Select: MCP Server - HTTP
# Download and extract to project root
```

#### 2. Configure Greptile Bridge

Create `greptile-mcp-bridge/src/main/java/org/acme/GreptileMcpBridge.java`:

```java
package org.acme;

import jakarta.inject.Inject;
import jakarta.ws.rs.*;
import jakarta.ws.rs.core.MediaType;
import io.smallrye.mutiny.Multi;
import org.eclipse.microprofile.rest.client.inject.RestClient;

@Path("/mcp")
public class GreptileMcpBridge {
    
    @Inject
    @RestClient
    GreptileClient greptileClient;
    
    @GET
    @Path("/search")
    @Produces(MediaType.SERVER_SENT_EVENTS)
    public Multi<String> searchCode(@QueryParam("query") String query) {
        // Call Greptile HTTP API
        String result = greptileClient.search(query);
        
        // Convert to SSE stream for Bob
        return Multi.createFrom().item(result);
    }
    
    @GET
    @Path("/analyze")
    @Produces(MediaType.SERVER_SENT_EVENTS)
    public Multi<String> analyzeCode(
        @QueryParam("repo") String repo,
        @QueryParam("file") String file
    ) {
        String result = greptileClient.analyze(repo, file);
        return Multi.createFrom().item(result);
    }
}
```

Create `greptile-mcp-bridge/src/main/java/org/acme/GreptileClient.java`:

```java
package org.acme;

import jakarta.ws.rs.*;
import jakarta.ws.rs.core.MediaType;
import org.eclipse.microprofile.rest.client.inject.RegisterRestClient;

@RegisterRestClient(configKey = "greptile-api")
public interface GreptileClient {
    
    @GET
    @Path("/search")
    @Produces(MediaType.APPLICATION_JSON)
    String search(@QueryParam("query") String query);
    
    @GET
    @Path("/analyze")
    @Produces(MediaType.APPLICATION_JSON)
    String analyze(
        @QueryParam("repo") String repo,
        @QueryParam("file") String file
    );
}
```

#### 3. Configure Application Properties

Create `greptile-mcp-bridge/src/main/resources/application.properties`:

```properties
# Greptile API Configuration
greptile-api/mp-rest/url=https://api.greptile.com/v1
greptile-api/mp-rest/scope=jakarta.inject.Singleton

# MCP Server Configuration
quarkus.http.port=8080
quarkus.http.host=0.0.0.0

# CORS Configuration (for Bob CLI)
quarkus.http.cors=true
quarkus.http.cors.origins=*
quarkus.http.cors.methods=GET,POST,OPTIONS
```

#### 4. Update .mcp/config.json

Add Greptile bridge to MCP configuration:

```json
{
  "mcpServers": {
    "jcodemunch": { ... },
    "lsp-mcp": { ... },
    "greptile": {
      "command": "java",
      "args": ["-jar", "greptile-mcp-bridge/target/quarkus-app/quarkus-run.jar"],
      "env": {
        "GREPTILE_API_KEY": "${GREPTILE_API_KEY}",
        "QUARKUS_HTTP_PORT": "8080"
      }
    },
    "sequential-thinking": { ... }
  }
}
```

#### 5. Build and Run

```bash
# Navigate to bridge directory
cd greptile-mcp-bridge

# Build the application
./mvnw clean package

# Run in dev mode (hot reload)
./mvnw quarkus:dev

# Or run the packaged JAR
java -jar target/quarkus-app/quarkus-run.jar
```

#### 6. Verify Integration

```bash
# Test SSE endpoint directly
curl -N http://localhost:8080/mcp/search?query="lock-free"

# Test from Bob CLI
# Bob will now see Greptile as an SSE-compatible MCP server
# Use via: /mcp greptile search "lock-free patterns"
```

### Environment Variables

Create `.env` entry for Greptile API key:

```bash
# Greptile API Configuration
GREPTILE_API_KEY=your_api_key_here
GREPTILE_REPO_ID=your_repo_id
```

### Benefits

- ✅ Bob can use Greptile via SSE
- ✅ No Bob code changes required
- ✅ Quarkus handles HTTP → SSE conversion
- ✅ Scalable (Quarkus native compilation)
- ✅ Hot reload during development
- ✅ Production-ready with minimal configuration

### Native Compilation (Optional)

For production deployment with microsecond startup:

```bash
# Build native executable
./mvnw package -Pnative

# Run native executable
./target/greptile-mcp-bridge-1.0.0-SNAPSHOT-runner
```

### Troubleshooting

**Issue**: Connection refused
- **Solution**: Verify Quarkus is running on port 8080
- **Check**: `netstat -an | findstr 8080`

**Issue**: CORS errors
- **Solution**: Verify CORS configuration in `application.properties`
- **Check**: Browser console for CORS policy errors

**Issue**: Greptile API authentication fails
- **Solution**: Verify `GREPTILE_API_KEY` environment variable
- **Check**: `echo $env:GREPTILE_API_KEY` (PowerShell)

### Integration with V12 Workflows

**Before Code Surgery**:
```powershell
# Start Greptile bridge
cd greptile-mcp-bridge
./mvnw quarkus:dev

# Query via Bob CLI
bob /mcp greptile search "FSM state machine patterns"
```

**During Architecture Analysis**:
```powershell
# Analyze specific file
bob /mcp greptile analyze --repo=universal-or-strategy --file=src/V12_002.cs
```

**After Implementation**:
```powershell
# Verify changes don't break patterns
bob /mcp greptile search "lock-free queue usage"
```

### See Also

- [MCP Configuration](MCP_CONFIGURATION.md)
- [Tool Discovery Protocol](../protocol/TOOL_DISCOVERY_PROTOCOL.md)
- [Universal Agent Protocol](../protocol/UNIVERSAL_AGENT_PROTOCOL.md)