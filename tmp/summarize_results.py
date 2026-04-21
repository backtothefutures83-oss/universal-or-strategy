import os
import re

extract_base = "c:/WSGTA/universal-or-strategy/tmp/battle_results"
results = []

for i in range(5):
    path = os.path.join(extract_base, f"battle_{i}", "index.html")
    if os.path.exists(path):
        with open(path, 'r', encoding='utf-8') as f:
            content = f.read()
            # Extract Agent name from <title>
            title_match = re.search(r"<title>(.*?) \|", content)
            agent = title_match.group(1) if title_match else f"Battle {i}"
            
            # Extract Findings
            # The PATTERN_FINDINGS is a JS array
            findings_match = re.search(r"const PATTERN_FINDINGS = \[(.*?)\];", content, re.DOTALL)
            if findings_match:
                findings_raw = findings_match.group(1)
                # Look for title and badge
                titles = re.findall(r"title: \"(.*?)\"", findings_raw)
                badges = re.findall(r"badge: \"(.*?)\"", findings_raw)
                findings = list(zip(titles, badges))
            else:
                findings = []
            
            results.append({"agent": agent, "findings": findings})

for res in results:
    print(f"Agent: {res['agent']}")
    for title, badge in res['findings']:
        print(f"  [{badge}] {title}")
