#!/usr/bin/env python3
"""
dead_code_scan.py -- V12_002 Dead Code Prevention Gate
Run this before every PR or major commit to catch dead private methods.
Usage: python scripts/dead_code_scan.py
Returns exit code 1 if new dead methods are found (can be used as a CI gate).
"""
import os, re, sys

SRC_DIR = os.path.join(os.path.dirname(__file__), '..', 'src')
SRC_DIR = os.path.realpath(SRC_DIR)

# NinjaTrader framework methods -- always live, never flag these
NT8_LIFECYCLE = {
    'OnStateChange','OnBarUpdate','OnOrderUpdate','OnPositionUpdate',
    'OnExecutionUpdate','OnAccountOrderUpdate','OnConnectionStatusUpdate',
    'OnMarketData','OnRender','OnRestoreValues','OnMarketDepth',
    'OnWindowCreated','OnWindowDestroyed','OnMouseClick','OnMouseMove',
    'OnKeyUp','OnKeyDown','CreateWPFControls','DestroyWPFControls',
    'Initialize','Dispose','ToString','GetHashCode','Equals','Finalize',
}

PRIV_DECL = re.compile(
    r'^\s*private\s+'
    r'(?:static\s+|async\s+|override\s+|virtual\s+|sealed\s+)*'
    r'(?:void|bool|int|double|string|float|long|decimal|Task|List|'
    r'IEnumerable|ConcurrentDictionary|Dictionary|[A-Z]\w*(?:\[\])?(?:<[^>]*>)?)\s+'
    r'([A-Za-z_]\w*)\s*(?:<[^>]+>)?\s*\('
)

# Load all source
all_files = [f for f in os.listdir(SRC_DIR) if f.endswith('.cs')]
raw = {}
for fname in all_files:
    with open(os.path.join(SRC_DIR, fname), encoding='utf-8', errors='ignore') as f:
        raw[fname] = f.readlines()

# Extract declarations
decls = {}
for fname, lines in raw.items():
    for i, line in enumerate(lines, 1):
        m = PRIV_DECL.match(line)
        if m:
            name = m.group(1)
            if name not in NT8_LIFECYCLE and name not in decls:
                decls[name] = (fname, i)

# Find callers (exclude comment lines and declaration lines)
call_pat = re.compile(r'\b([A-Za-z_]\w+)\b')
all_refs = set()
for fname, lines in raw.items():
    for i, line in enumerate(lines, 1):
        stripped = line.strip()
        if stripped.startswith('//') or stripped.startswith('*'):
            continue
        for m in call_pat.finditer(line):
            all_refs.add(m.group(1))

# Find zero-caller candidates
dead = []
for name, (fname, lineno) in sorted(decls.items()):
    # Only flag if name NEVER appears anywhere except potentially its own decl
    # We use a precise per-file search for the name as a call
    call_found = False
    call_pat2 = re.compile(r'\b' + re.escape(name) + r'\s*[(<]')
    for fname2, lines2 in raw.items():
        for i, line in enumerate(lines2, 1):
            stripped = line.strip()
            if stripped.startswith('//'): continue
            if call_pat2.search(line):
                if fname2 == fname and i == lineno: continue  # skip own decl
                call_found = True
                break
        if call_found: break
    if not call_found:
        # Also check for delegate wiring: += Name, new Thread(Name), new ThreadStart(Name)
        delegate_pat = re.compile(
            r'(?:\+\=\s*' + re.escape(name) + r'\b'
            r'|new\s+Thread\s*\(\s*' + re.escape(name) + r'\s*\)'
            r'|new\s+ThreadStart\s*\(\s*' + re.escape(name) + r'\s*\)'
            r'|TriggerCustomEvent\s*\(\s*' + re.escape(name) + r'\s*\)'
            r')'
        )
        for fname2, lines2 in raw.items():
            for line in lines2:
                if delegate_pat.search(line):
                    call_found = True
                    break
            if call_found: break

    if not call_found:
        dead.append((name, fname, lineno))

if dead:
    print(f'[DEAD CODE SCAN] FAIL -- {len(dead)} unreachable private method(s) found:')
    for name, fname, lineno in dead:
        print(f'  {fname}:L{lineno} -- {name}()')
    print()
    print('Action: Remove dead methods or add a caller before merging.')
    sys.exit(1)
else:
    print(f'[DEAD CODE SCAN] PASS -- {len(decls)} private methods checked, 0 dead.')
    sys.exit(0)
