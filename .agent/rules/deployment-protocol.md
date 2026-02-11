# NOTIFICATION: Deployment Protocol Hardened

## Protocol: ONE SOURCE OF TRUTH

**Context:** NinjaTrader 8 (NT8) and GitHub Repository synchronization.

### 🛡️ Mandatory Rule for All Agents
- All agents MUST run `.\deploy-sync.ps1 -Link` immediately after modifying any `.cs` file or development script.
- All agents MUST run `.\verify-desync.ps1` after the sync command to confirm successful hard link propagation.
- **ZERO-TOUCH PROTOCOL**: Agents MUST NOT request the user to run terminal commands or scripts. Command execution is the sole responsibility of the active AI Agent via `run_command` tools.
- Failure to run the sync script and verify results is considered a **CRITICAL PROTOCOL VIOLATION**.

### 🚀 Workflow
- **Repo** = The Source
- **NinjaTrader** = The Live Target (via Hard Links)
- **Deployment** = Automated via `deploy-sync.ps1`

Failure to follow this protocol will result in a code desync and compilation errors for the USER.
