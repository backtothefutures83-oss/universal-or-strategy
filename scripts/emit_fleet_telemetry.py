import sys
import os
import json
from datetime import datetime, timezone
from langsmith import traceable
from dotenv import load_dotenv

# Load global observability environment
load_dotenv()

@traceable(run_type="chain", name="Fleet Global Monitor")
def emit_agent_telemetry(agent_name, action, status="PASS", metadata=None):
    """
    Unified sink for all agent telemetry (Gemini, Qwen, Jules, Codex, Droid, Bob).
    Ensures every action is visible in the LangSmith dashboard.
    """
    print(f"[*] [TELEMETRY] {agent_name} -> {action} ({status})")
    return {
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "agent": agent_name,
        "action": action,
        "status": status,
        "payload": metadata or {}
    }

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python emit_fleet_telemetry.py <agent> <action> [status] [metadata_json]")
        sys.exit(1)
    
    agent = sys.argv[1]
    act = sys.argv[2]
    stat = sys.argv[3] if len(sys.argv) > 3 else "PASS"
    meta = json.loads(sys.argv[4]) if len(sys.argv) > 4 else {}
    
    emit_agent_telemetry(agent, act, stat, meta)
