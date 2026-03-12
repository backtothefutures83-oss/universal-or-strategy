import os
import re

candidates = {
    "ExecuteRMAEntry": ("V12_002.Entries.RMA.cs", 185),
    "ExecuteRMAEntryCustom": ("V12_002.Entries.RMA.cs", 356),
    "HandleExternalSignal": ("V12_002.UI.IPC.cs", 220),
    "ListenForRemote": ("V12_002.UI.IPC.Server.cs", 65),
    "OnAccountExecutionUpdate": ("V12_002.UI.Compliance.cs", 273),
    "OnAccountOrderUpdate": ("V12_002.Orders.Callbacks.AccountOrders.cs", 37),
    "OnBreakevenButtonClick": ("V12_002.Trailing.Breakeven.cs", 35),
    "OnChartClick": ("V12_002.UI.Callbacks.cs", 76),
    "OnKeyDown": ("V12_002.UI.Callbacks.cs", 172),
    "ReaperLoop": ("V12_002.REAPER.cs", 122)
}

src_dir = r"c:\WSGTA\universal-or-strategy\src"
root_dir = r"c:\WSGTA\universal-or-strategy"

def scan():
    results = {cand: [] for cand in candidates}
    for root, dirs, files in os.walk(root_dir):
        if ".git" in root or "node_modules" in root or "ARCHIVE" in root or "bin" in root or "obj" in root:
            continue
        for file in files:
            if not file.endswith(".cs"):
                continue
            path = os.path.join(root, file)
            try:
                with open(path, "r", encoding="utf-8-sig") as f:
                    lines = f.readlines()
                for i, line in enumerate(lines):
                    for cand in candidates:
                        # Ignore the definition itself roughly
                        if "void " + cand in line or "Task " + cand in line or "bool " + cand in line:
                            continue
                        # Look for candidate without opening parenthesis after it (ignoring whitespace)
                        # or cases where it's used as an event handler: Object.Event += Candidate;
                        if cand in line:
                            match = re.search(r'\b' + cand + r'\b(?!\s*\()', line)
                            if match:
                                results[cand].append((os.path.basename(path), i+1, line.strip()))
                            elif "override" in line and cand in line:
                                results[cand].append((os.path.basename(path), i+1, line.strip() + " [OVERRIDE]"))
            except Exception as e:
                pass

    with open(r"c:\WSGTA\universal-or-strategy\scripts\trace_results.md", "w", encoding="utf-8") as out:
        out.write("--- EVENT / DELEGATE SCAN RESULTS ---\n")
        for cand, hits in results.items():
            if not hits:
                out.write(f"{cand}: NO EVENT DELEGATE FOUND\n")
            else:
                out.write(f"{cand}:\n")
                for h in hits:
                    out.write(f"  -> {h[0]}:{h[1]} | {h[2]}\n")

if __name__ == "__main__":
    scan()
