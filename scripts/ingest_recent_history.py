import os
import sys
import json
from datetime import datetime, timezone
from langsmith import Client
from dotenv import load_dotenv

# Load tracing config
load_dotenv()

def ingest_history(limit=5):
    """
    Fetches the most recent agent traces from LangSmith to provide 
    short-term "Temporal Memory" for the current agent.
    """
    print(f"[*] Ingesting Temporal Memory from LangSmith (Limit: {limit})...")
    
    if os.getenv("LANGSMITH_TRACING") != "true":
        print("[!] LangSmith Tracing is not enabled. Skipping history ingestion.")
        return

    try:
        client = Client()
        project_name = os.getenv("LANGSMITH_PROJECT", "Sovereign-Multi-Agent")
        
        # Fetch recent runs
        runs = client.list_runs(
            project_name=project_name,
            limit=limit,
            execution_order=1 # Descending
        )
        
        history = []
        for run in runs:
            history.append({
                "time": run.start_time.isoformat() if run.start_time else "Unknown",
                "agent": run.name,
                "action": run.run_type,
                "inputs": str(run.inputs)[:200] + "..." if run.inputs else "N/A"
            })
            
        # Write to local brain cache
        memory_path = "docs/brain/temporal_memory.json"
        with open(memory_path, "w") as f:
            json.dump(history, f, indent=2)
            
        print(f"[+] Successfully cached {len(history)} synapses to {memory_path}")
        
    except Exception as e:
        print(f"[-] History Ingestion Failed: {e}")

if __name__ == "__main__":
    ingest_history()
