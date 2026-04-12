"""
V25 MPMC AMAL Vetting Gate
Extracts the full MpmcPipeline<T> class body and benchmarks it natively.
"""
import os, re, json, subprocess, sys, html as _html
from datetime import datetime

BASE_DIR = r"c:\WSGTA\universal-or-strategy"
TEMPLATE_FILE = os.path.join(BASE_DIR, "benchmarks", "StandaloneBench_V25.template.txt")
BENCH_FILE    = os.path.join(BASE_DIR, "benchmarks", "StandaloneBench.cs")
RESULTS_JSON  = os.path.join(BASE_DIR, "docs", "battle_results.json")
RESULTS_MD    = os.path.join(BASE_DIR, "docs", "battle_results.md")
SUBMISSIONS   = r"C:\tmp\arena_round_25"

def extract_all_classes(content):
    """Extract all classes, structs, enums, etc. and handle orphan methods in tabbed UIs."""
    # Pre-clean
    content = re.sub(r'<span[^>]*>|</span>', '', content)
    content = _html.unescape(content)
    
    # 1. Identify all class/struct definitions
    class_pat = re.compile(
        r'(?:\[[^\]]+\]\s*)*'
        r'(?:public\s+|internal\s+|private\s+|sealed\s+|unsafe\s+|partial\s+|abstract\s+|static\s+)*'
        r'(?:class|struct|interface|enum)\s+(?!Program)(\w+)[\w\s<>,:]*\{'
    )
    
    results = {} # name -> body
    ranges = []
    
    for m in class_pat.finditer(content):
        name = m.group(1)
        start = m.start()
        if any(r[0] <= start < r[1] for r in ranges): continue
            
        depth = 1
        i = m.end()
        while depth > 0 and i < len(content):
            if content[i] == '{': depth += 1
            elif content[i] == '}':
                depth -= 1
                if depth == 0:
                    body = content[start:i+1].strip()
                    results[name] = body
                    ranges.append((start, i+1))
                    break
            i += 1
            
    # 2. Identify "orphan" methods (outside classes)
    method_pat = re.compile(
        r'(?:\[[^\]]+\]\s*)*'
        r'(?:public\s+|internal\s+|private\s+|static\s+|unsafe\s+|partial\s+|async\s+|virtual\s+|override\s+)*'
        r'(?:\w+)\s+(Try\w+|Steal\w+)\s*\(.*?\)\s*\{'
    )
    
    orphans = []
    for m in method_pat.finditer(content):
        start = m.start()
        if any(r[0] <= start < r[1] for r in ranges): continue
        
        depth = 1
        i = m.end()
        while depth > 0 and i < len(content):
            if content[i] == '{': depth += 1
            elif content[i] == '}':
                depth -= 1
                if depth == 0:
                    orphans.append(content[start:i+1].strip())
                    ranges.append((start, i+1))
                    break
            i += 1
            
    # 3. Inject orphans into MpmcPipeline if exists
    if orphans and "MpmcPipeline" in results:
        main_class = results["MpmcPipeline"]
        # Find the last closing brace and inject before it
        last_brace = main_class.rfind('}')
        if last_brace != -1:
            results["MpmcPipeline"] = main_class[:last_brace] + "\n" + "\n\n".join(orphans) + "\n}"
            
    return "\n\n".join(results.values())

def run_benchmark(sub_id, class_body):
    """Inject class body into V25 template and benchmark."""
    with open(TEMPLATE_FILE, 'r', encoding='utf-8') as f:
        template = f.read()
    injected = template.replace("// [[MPMC_CLASS_BODY]]", class_body)
    with open(BENCH_FILE, 'w', encoding='utf-8') as f:
        f.write(injected)
    
    proj = os.path.join(BASE_DIR, "benchmarks", "SpscRing.Benchmarks.csproj")
    result = subprocess.run(
        ["dotnet", "run", "--project", proj, "-c", "Release"],
        capture_output=True, text=True, timeout=120
    )
    output = result.stdout + result.stderr
    
    # Parse: "| RoundTrip | 4.395 ns | 0 B |"
    m = re.search(r'RoundTrip\s*\|\s*([\d.]+)\s*ns\s*\|\s*([\w\s]+)\s*\|', output)
    if m:
        latency = float(m.group(1))
        alloc = m.group(2).strip()
        print(f"  [PASS] {sub_id}: {latency:.3f}ns | {alloc}")
        return {"id": sub_id, "status": "PASS", "latency": latency, "alloc": alloc}
    elif "ERROR" in output:
        err_m = re.search(r'ERROR: (.+)', output)
        print(f"  [ERROR] {sub_id}: {err_m.group(1) if err_m else 'runtime error'}")
        return {"id": sub_id, "status": "FAIL", "error": "Runtime error"}
    else:
        print(f"  [FAIL] {sub_id}: Build failed")
        print(f"    {output[-300:]}")
        return {"id": sub_id, "status": "FAIL", "error": "Build failed"}

def main():
    target_sub = sys.argv[1] if len(sys.argv) > 1 else None
    
    submissions = sorted([s for s in os.listdir(SUBMISSIONS) if os.path.isdir(os.path.join(SUBMISSIONS, s))])
    if target_sub:
        submissions = [s for s in submissions if s == target_sub]
        if not submissions:
            print(f"Submission {target_sub} not found.")
            return

    results = []
    for sub in submissions:
        print(f"\n[*] Benchmarking {sub}...")
        sub_dir = os.path.join(SUBMISSIONS, sub)
        
        # Find and parse all HTML/TS files
        content = ""
        for root, _, files in os.walk(sub_dir):
            for fname in files:
                ext = os.path.splitext(fname)[1].lower()
                if ext not in ('.ts', '.tsx', '.html', '.cs'):
                    continue
                fpath = os.path.join(root, fname)
                with open(fpath, 'r', encoding='utf-8', errors='ignore') as f:
                    fc = f.read()
                if ext == '.html':
                    # Priority 1: Extract from JS String.raw`...` blocks (multi-block support)
                    raw_blocks = re.findall(r'String\.raw\s*`([\s\S]*?)`', fc)
                    # Priority 2: Extract from <pre> tags (common in tabbed UIs)
                    pre_blocks = re.findall(r'<pre[^>]*>([\s\S]*?)</pre>', fc)
                    
                    if raw_blocks or pre_blocks:
                        fc = "\n".join(raw_blocks + pre_blocks)
                    else:
                        # Priority 3: Strip tags and unescape as fallback
                        fc = _html.unescape(re.sub(r'<[^>]+>', ' ', fc))
                # ASCII gate: eliminate non-ASCII before extraction
                fc = fc.encode('ascii', errors='ignore').decode('ascii')
                content += "\n" + fc
        
        # Try to extract all classes
        class_body = extract_all_classes(content)
        
        if not class_body:
            # Check if TrySend + TryReceive exist (partial extraction)
            has_send = "TrySend" in content or "TryEnqueue" in content
            has_recv = "TryReceive" in content or "TryDequeue" in content
            if has_send and has_recv:
                print(f"  [SKIP] {sub}: Class body not extractable but methods exist")
                results.append({"id": sub, "status": "SKIP", "error": "Class not extractable"})
            else:
                print(f"  [SKIP] {sub}: No MPMC logic found")
                results.append({"id": sub, "status": "SKIP", "error": "Incomplete code"})
        else:
            # Force non-generic specialization
            # 1. Normalize whitespace around generic brackets
            class_body = re.sub(r'<\s*([A-Za-z0-9_?]+)\s*>', r'<\1>', class_body)
            # 2. Replace generic tokens with double
            class_body = re.sub(r'\b(T|TItem|TValue|TKey)\b', 'double', class_body)
            # 3. Strip remaining generic parameters from class/struct names
            class_body = re.sub(r'<(?:double|T|U|TItem|TValue|TKey)>', '', class_body)
            # 4. Remove IDisposable remnants
            class_body = class_body.replace(': IDisposable', '')
            
            results.append(run_benchmark(sub, class_body))
    
    # Save results
    with open(RESULTS_JSON, 'w') as f:
        json.dump(results, f, indent=2)
    
    # Summary
    passes = [r for r in results if r.get('status') == 'PASS']
    fails  = [r for r in results if r.get('status') == 'FAIL']
    skips  = [r for r in results if r.get('status') == 'SKIP']
    
    print(f"\n{'='*60}")
    print(f"ROUND 25 AMAL GATE RESULTS")
    print(f"{'='*60}")
    print(f"  PASS: {len(passes)}")
    print(f"  FAIL: {len(fails)}")
    print(f"  SKIP: {len(skips)}")
    print(f"  TOTAL: {len(results)}")
    
    if passes:
        winner = min(passes, key=lambda x: x['latency'])
        print(f"\nCHAMPION: {winner['id']} ({winner['latency']:.3f}ns | {winner['alloc']})")
    
    with open(RESULTS_MD, 'w') as f:
        f.write("# AMAL Round 25 Gate Results\n\n")
        f.write(f"**Gate Stats**: {len(passes)} Pass | {len(fails)} Fail | {len(skips)} Skip\n\n")
        f.write("| Sub | Status | Latency | Alloc |\n|-----|--------|---------|-------|\n")
        for r in sorted(results, key=lambda x: x.get('latency', 999.0)):
            f.write(f"| {r['id']} | {r.get('status')} | {r.get('latency','N/A')} | {r.get('alloc','N/A')} |\n")

if __name__ == "__main__":
    main()
