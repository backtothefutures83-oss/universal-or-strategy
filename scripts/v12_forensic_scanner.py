import os
import re

# V12 Forensic Scanner: Static Pattern Analysis for Concurrency & Logic Bugs
# Mission: Phase 7 Hardening
# Author: Antigravity (Orchestrator)

RULES = [
    {
        "id": "DNA-001",
        "name": "Banned Lock Usage",
        "pattern": r"lock\s*\(",
        "severity": "CRITICAL",
        "description": "Legacy lock(stateLock) is BANNED. Use Enqueue or Atomic primitives."
    },
    {
        "id": "BUG-010-SCAN",
        "name": "Ghost Order Window (Enqueue on Stops)",
        "pattern": r"Enqueue\(.*stopOrders",
        "severity": "HIGH",
        "description": "Using Enqueue for stop-order updates creates a ghost window. Direct write is MANDATORY (Build 981)."
    },
    {
        "id": "BUG-015-SCAN",
        "name": "Async ID Race Condition",
        "pattern": r"\.Submit\(.*\);[\s\S]{0,200}\.OrderId",
        "severity": "HIGH",
        "description": "Accessing OrderId immediately after Submit() fails on async brokers."
    },
    {
        "id": "PERF-001",
        "name": "Hot-Path Allocation (.ToArray)",
        "pattern": r"(OnBarUpdate|Pump|Process|ShouldSkip).*\n[\s\S]*?\.ToArray\(\)",
        "severity": "MEDIUM",
        "description": "Hidden allocation in hot-path. Eliminates zero-allocation advantage."
    },
    {
        "id": "DNA-002",
        "name": "Missing Finally on Semaphore/Interlocked",
        "pattern": r"Monitor\.Enter|WaitOne|Semaphore[\s\S]*?(?!finally)",
        "severity": "HIGH",
        "description": "Potential semaphore leak. All acquisitions must be paired with a finally block."
    },
    {
        "id": "BUG-001-SCAN",
        "name": "O(N^2) Loop Pattern",
        "pattern": r"foreach.*foreach",
        "severity": "MEDIUM",
        "description": "Nested loops in fleet management cause performance degradation (N^2)."
    },
    {
        "id": "BUG-028-SCAN",
        "name": "Non-Atomic ContainsKey + TryAdd",
        "pattern": r"ContainsKey\s*\(.*?\).*?\n.*?(?:TryAdd|Add)\s*\(",
        "severity": "CRITICAL",
        "description": "TOCTOU race condition. Use GetOrAdd or rely solely on TryAdd return value."
    },
    {
        "id": "BUG-068-SCAN",
        "name": "Banned Generic Catch",
        "pattern": r"catch\s*\(\s*Exception\b[^)]*\)\s*\{\s*(?!.*?(?:throw|Metrics|Crash|Alert))",
        "severity": "HIGH",
        "description": "Generic catch block swallows critical errors (OOM, StackOverflow). Must throw or log strongly."
    },
    {
        "id": "BUG-071-SCAN",
        "name": "Hot-Path String Allocation",
        "pattern": r"Print\(\s*(?:string\.Format|\$\"|\w+\s*\+\s*\w+)",
        "severity": "MEDIUM",
        "description": "Eager string interpolation/concatenation in Print() causes GC pressure."
    },
    {
        "id": "BUG-049-SCAN",
        "name": "Missing Bounds Clamp on Payload",
        "pattern": r"for\s*\(\s*int\s+i\s*=\s*0;\s*i\s*<\s*orderCount;\s*i\+\+\s*\)",
        "severity": "HIGH",
        "description": "Trusts payload orderCount without clamping Math.Min(orderCount, orders.Length)."
    },
    {
        "id": "BUG-078-SCAN",
        "name": "OrderId Sync Race",
        "pattern": r"\[.*?\.OrderId\]\s*=\s*fleetEntryName",
        "severity": "CRITICAL",
        "description": "Mapping OrderId -> FSM outside of OnAccountOrderUpdate races with the broker callback."
    }
]

def scan_files(directory):
    print(f"=== V12 FORENSIC SCANNER: Starting Audit of {directory} ===")
    results = []
    
    for root, _, files in os.walk(directory):
        for file in files:
            if not file.endswith(".cs") or "Morpheus" in root:
                continue
            
            path = os.path.join(root, file)
            with open(path, 'r', encoding='utf-8') as f:
                content = f.read()
                
                for rule in RULES:
                    matches = re.finditer(rule["pattern"], content, re.MULTILINE)
                    for match in matches:
                        line_no = content.count('\n', 0, match.start()) + 1
                        results.append({
                            "file": file,
                            "line": line_no,
                            "rule_id": rule["id"],
                            "rule_name": rule["name"],
                            "severity": rule["severity"],
                            "snippet": content[match.start():match.end()].strip().replace('\n', ' ')[:100]
                        })

    # Sort results by severity
    severity_map = {"CRITICAL": 0, "HIGH": 1, "MEDIUM": 2, "LOW": 3}
    results.sort(key=lambda x: severity_map.get(x["severity"], 99))
    
    print(f"--- SCAN COMPLETE: Found {len(results)} potential issues ---\n")
    
    for r in results:
        print(f"[{r['severity']}] {r['rule_id']} in {r['file']}:L{r['line']}")
        print(f"    Name: {r['rule_name']}")
        print(f"    Match: {r['snippet']}...")
        print("-" * 40)

if __name__ == "__main__":
    src_dir = os.path.join(os.getcwd(), "src")
    scan_files(src_dir)
