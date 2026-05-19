# LangSmith Operational Guide (V12.16 Indexed)
**Status**: ACTIVE
**Source**: https://docs.smith.langchain.com/
**Purpose**: Official reference for tracing, troubleshooting, and multi-agent observability.

---

## 🛠️ Mandatory Environment Variables
| Variable | Value | Purpose |
| :--- | :--- | :--- |
| `LANGSMITH_TRACING` | `true` | Must be set to `true` to enable any emission. |
| `LANGSMITH_API_KEY` | `lsv2_pt_...` | Personal Access Token or Service Key. |
| `LANGSMITH_PROJECT` | `Sovereign-Multi-Agent` | The destination project in the dashboard. |
| `LANGSMITH_ENDPOINT` | `https://api.smith.langchain.com` | Official cloud endpoint. |

---

## 🔬 Core Tracing Constraints

### 1. The Node.js "Amnesia" Trap (CRITICAL)
- **Problem**: Node.js processes (like Bob IDE) only load environment variables at **startup**.
- **Effect**: If you update the `.env` file while Bob is running, he will continue to use the OLD credentials.
- **Solution**: A **HARD RESTART** of the agent process is mandatory after any `.env` change.

### 2. Background Batching & Flushing
- **Mechanism**: The SDK buffers traces in memory to prevent performance lag.
- **Flushing**: Short-lived scripts must call `client.flush()` before exiting.
- **Limbo**: If a process crashes or is killed abruptly, buffered traces are lost.

### 3. Network Requirements
- **Outbound**: Must allow HTTPS to `api.smith.langchain.com`.
- **TLS**: Standard SSL verification is required. Corporate proxies may block the telemetry stream.

---

## 🛡️ Troubleshooting Checklist
1. **Verify `LANGSMITH_TRACING=true`**: If this is false or unset, no traces are sent.
2. **Check the Project Name**: If `LANGSMITH_PROJECT` doesn't match the dashboard project, traces will go to the "default" bucket.
3. **API Key Permissions**: Ensure the key has "Member" or "Owner" access to the target workspace.
4. **SDK Version**: Outdated `langsmith` libraries may use old endpoints or deprecated headers.

---
**Last Index Update**: 2026-05-18 | Session: 92c12f62
