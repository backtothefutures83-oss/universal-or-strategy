import os
import sys
import re
import json
from datetime import datetime, timezone
from langsmith import traceable
from dotenv import load_dotenv

# Load environment variables from .env
load_dotenv()

# Configuration (Mandatory via Env)
# export LANGSMITH_TRACING=true
# export LANGSMITH_API_KEY=ls_...
# export LANGSMITH_PROJECT="Sovereign-Multi-Agent"

@traceable(run_type="chain")
def trace_agent_handoff(from_agent, to_agent, mission_id, payload):
    """
    Traces the handoff between two agents in the Sovereign fleet.
    """
    print(f"[*] Tracing Handoff: {from_agent} -> {to_agent} (Mission: {mission_id})")
    return {
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "from": from_agent,
        "to": to_agent,
        "mission_id": mission_id,
        "payload_size": len(str(payload))
    }

@traceable(run_type="llm")
def trace_forensic_run(submission_id, metrics):
    """
    Traces an AMAL forensic run and attaches performance metadata.
    """
    latency = metrics.get('latency', 'N/A')
    alloc = metrics.get('alloc', 'N/A')
    print(f"[*] Tracing Forensic Run: {submission_id} ({latency}ns)")
    return {
        "submission_id": submission_id,
        "latency_ns": latency,
        "allocation_b": alloc,
        "status": "PASS" if latency != 'N/A' and float(latency) < 5.0 else "FAIL"
    }

def main():
    if "--test" in sys.argv:
        print("[*] Running LangSmith Connectivity Test...")
        try:
            trace_agent_handoff("Antigravity", "Claude", "MISN-001", {"goal": "sub-1ns"})
            print("[+] Trace emitted successfully.")
        except Exception as e:
            print(f"[-] LangSmith Trace Failed: {e}")
            sys.exit(1)

if __name__ == "__main__":
    main()
