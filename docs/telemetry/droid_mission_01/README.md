# Droid Mission 01: Telemetry Evidence

This folder serves as the central repository for raw technical evidence captured during Droid Mission 01. This data will be synthesized into the final engineering report for Eno Reyes (Factory AI).

## 📡 Live Telemetry Links

- **LangSmith Traces**: [Sovereign-Multi-Agent Project](https://smith.langchain.com/o/e5e9b88c-7f52-4a7b-8b5d-9c4c1a2d3e4f/projects/p/85a7d76c-3c6f-4c54-9d41-6c2e3919876e)
- **Sentry Issues**: [V12-Sovereign-Agents Project](https://v12-os.sentry.io/projects/v12-sovereign-agents/)

## 📄 Logs & Artifacts

- `shell_output.log`: Raw CLI output from the Droid mission.
- `crash_dump.json`: Captured Sentry events (exported).
- `trace_summary.md`: Highlighted LangSmith traces with latency and cost analysis.

## 🎯 Target Evidence

We are specifically looking for:

1.  **Windows Path Friction**: Log any "Access Denied" or path truncation issues.
2.  **MCP Timeout Proofs**: Captured LangSmith traces showing agent-tool latency.
3.  **Validation Drift**: Documentation of YAML schema friction vs. actual behavior.
