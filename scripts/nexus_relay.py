import sys
import os
import json
from datetime import datetime, timezone
from langsmith import traceable
from dotenv import load_dotenv

# Load tracing config
load_dotenv()

@traceable(run_type="chain", name="A2A Relay")
def relay_to_agent(to_agent, instructions):
    """
    Formalizes the handoff to a sub-agent and emits a LangSmith trace.
    """
    mission_id = "V14.2" # Default to current mission
    blackboard_path = r"c:\WSGTA\universal-or-strategy\docs\brain\nexus_a2a.json"
    
    print(f"[*] Relaying Mission to {to_agent}...")
    
    # Update Blackboard
    with open(blackboard_path, "r") as f:
        blackboard = json.load(f)
    
    blackboard["last_relay"] = {
        "agent": to_agent,
        "time": datetime.now(timezone.utc).isoformat(),
        "status": "IN_PROGRESS"
    }
    
    with open(blackboard_path, "w") as f:
        json.dump(blackboard, f, indent=2)
        
    return {
        "relay_agent": to_agent,
        "instruction_hash": hash(instructions),
        "target": "Sovereign-Core-V23"
    }

def main():
    if len(sys.argv) < 3:
        print("Usage: python nexus_relay.py <agent_name> <instructions>")
        sys.exit(1)
        
    agent = sys.argv[1]
    instr = sys.argv[2]
    relay_to_agent(agent, instr)
    print(f"[+] Relay to {agent} documented and traced.")

if __name__ == "__main__":
    main()
