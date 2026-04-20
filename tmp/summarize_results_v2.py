import os
import re

extract_base = "c:/WSGTA/universal-or-strategy/tmp/battle_results"
results = {}

def get_findings(text):
    # Regex to find the pattern findings array and its title/status/summary
    # title: '...', status: '...', summary: '...'
    findings = []
    # Search for patternFindings or PATTERN_FINDINGS
    match = re.search(r"(?:patternFindings|PATTERN_FINDINGS)\s*=\s*\[(.*?)\];", text, re.DOTALL)
    if match:
        raw = match.group(1)
        # Each finding is an object
        blocks = re.split(r'\}\s*,\s*\{', raw)
        for block in blocks:
            title = re.search(r"title:\s*['\"](.*?)['\"]", block)
            status = re.search(r"(?:status|badge):\s*['\"](.*?)['\"]", block)
            summary = re.search(r"summary:\s*['\"](.*?)['\"]", block)
            if title and status:
                findings.append({
                    "title": title.group(1),
                    "status": status.group(1),
                    "summary": summary.group(1) if summary else ""
                })
    return findings

for i in range(5):
    folder = os.path.join(extract_base, f"battle_{i}")
    if not os.path.exists(folder): continue
    
    agent = f"Battle {i}"
    findings = []
    
    # Check index.html
    html_path = os.path.join(folder, "index.html")
    if os.path.exists(html_path):
        with open(html_path, 'r', encoding='utf-8') as f:
            content = f.read()
            title_tag = re.search(r"<title>(.*?)</title>", content)
            if title_tag:
                agent = title_tag.group(1).split('|')[0].strip()
            findings = get_findings(content)
            
    # If findings empty, check src/App.tsx
    if not findings:
        app_path = os.path.join(folder, "src", "App.tsx")
        if os.path.exists(app_path):
            with open(app_path, 'r', encoding='utf-8') as f:
                content = f.read()
                # App.tsx might not have the agent name in it, but look for it in comments or constants
                agent_match = re.search(r"Claude (.*?) Visualizer", content)
                if agent_match:
                    agent = f"Claude {agent_match.group(1)}"
                findings = get_findings(content)
                
    results[i] = {"agent": agent, "findings": findings}

for i, res in results.items():
    print(f"--- BATTLE {i} ---")
    print(f"Agent: {res['agent']}")
    for f in res['findings']:
        print(f"  [{f['status']}] {f['title']}")
