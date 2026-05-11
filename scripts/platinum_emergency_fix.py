import subprocess
import os
import re

def get_rev_content(rev, path):
    try:
        # Use binary to avoid any encoding issues
        return subprocess.check_output(['git', 'show', rev + ':' + path])
    except Exception as e:
        print(f"Error fetching {path} from {rev}: {e}")
        return None

def apply_platinum_fixes(path, content_bytes):
    # Standardize to LF and remove BOM
    text = content_bytes.decode('utf-8-sig', errors='replace').replace('\r\n', '\n')
    
    if 'Trailing.cs' in path:
        # 1. Restore ASCII marker
        text = text.replace('? MANUAL BREAKEVEN', '(!) MANUAL BREAKEVEN')
        # 2. Restore E1 Print
        text = text.replace('// Print(string.Format("TREND E1 TRAIL', 'Print(string.Format("TREND E1 TRAIL')
        
        # 3. Task B: Thread-Safe Snapshot (Surgically replace the old one-liner)
        old_sync = 'if (EnableSIMA) ManageTrail_RunFleetSymmetrySync(positionSnapshot);'
        new_sync = """// [LD-003] Thread-Safety: Use a fresh snapshot for fleet sync to prevent stale stop synchronization.
            if (EnableSIMA)
            {
                var updatedSnapshot = activePositions.ToArray();
                ManageTrail_RunFleetSymmetrySync(updatedSnapshot);
            }"""
        text = text.replace(old_sync, new_sync)
        
        # 4. Hygiene: Ternary for extreme price
        old_if = r'if\s*\(pos\.Direction\s*==\s*MarketPosition\.Long\)\s+pos\.ExtremePriceSinceEntry\s*=\s*Math\.Max\(pos\.ExtremePriceSinceEntry,\s*Close\[0\]\);\s+else\s+pos\.ExtremePriceSinceEntry\s*=\s*Math\.Min\(pos\.ExtremePriceSinceEntry,\s*Close\[0\]\);'
        new_ternary = 'pos.ExtremePriceSinceEntry = pos.Direction == MarketPosition.Long ? Math.Max(pos.ExtremePriceSinceEntry, Close[0]) : Math.Min(pos.ExtremePriceSinceEntry, Close[0]);'
        text = re.sub(old_if, new_ternary, text)

    if 'SIMA.Dispatch.cs' in path:
        # 1. Timezone: UTC Ticks
        text = text.replace('DateTime.Now.Ticks', 'DateTime.UtcNow.Ticks')
        # 2. Hygiene: Remove unused var
        text = re.sub(r'\s*bool useRmaForFollower = true;', '', text)
        
        # 3. Gitar's Catch Block Null-Guard
        # Find the TryRemove call in the catch block
        old_tryremove = '_followerBrackets.TryRemove(fleetEntryName, out _);'
        new_tryremove = """if (!string.IsNullOrEmpty(fleetEntryName))
                            _followerBrackets.TryRemove(fleetEntryName, out _);"""
        # We need to be careful with the exact string in ff858ae
        if old_tryremove in text:
            text = text.replace(old_tryremove, new_tryremove)

    if 'Execution.cs' in path:
        # 1. Hygiene: Nullable simplification
        text = re.sub(
            r'pos\.ExecutingAccount\s*!=\s*null\s*&&\s*pos\.ExecutingAccount\.Name\s*==\s*flatAcctName',
            'pos.ExecutingAccount?.Name == flatAcctName',
            text
        )
        text = re.sub(
            r'kvp\.Value\.ExecutingAccount\s*!=\s*null\s*&&\s*kvp\.Value\.ExecutingAccount\.Name\s*==\s*flatAcctName',
            'kvp.Value.ExecutingAccount?.Name == flatAcctName',
            text
        )

    return text

rev = 'ff858ae'
files = ['src/V12_002.Trailing.cs', 'src/V12_002.SIMA.Dispatch.cs', 'src/V12_002.Orders.Callbacks.Execution.cs']

for f_path in files:
    content_bytes = get_rev_content(rev, f_path)
    if content_bytes:
        fixed_text = apply_platinum_fixes(f_path, content_bytes)
        # Write with LF
        with open(f_path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(fixed_text)
        print(f'PLATINUM RESTORED: {f_path}')

# Also restore check_ascii.py
try:
    ca_bytes = get_rev_content('main', 'check_ascii.py')
    if ca_bytes:
        with open('check_ascii.py', 'wb') as f:
            f.write(ca_bytes)
        print('Restored check_ascii.py')
except:
    pass
