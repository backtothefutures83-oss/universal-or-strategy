import sys
sys.path.append('scripts')
import amal_harness
import re
import html

with open(r'C:\tmp\battle_antigravity_os\Codex_Mmio\index.html', 'r', encoding='utf-8') as f:
    content = f.read()

csharp = re.search(r'<script[^>]*type="text/x-csharp"[^>]*>(.*?)</script>', content, flags=re.S | re.I)
c = html.unescape(csharp.group(1))

e_raw = amal_harness.get_method_body(c, 'TryEnqueue')
d_raw = amal_harness.get_method_body(c, 'TryDequeue')

# We override the module's function for testing
def test_normalize_body(e_body, d_body):
    import re
    mappings = {
        r'ulong\s+': 'long ',
        r'\*\(ulong\*\)': '*(long*)',
        r'\b_?shadowLength\b': '0',
        r'\b_?SHADOW_SALT\b': '0',
        r'\b_?shadowOffset\b\s*[\^+\-]=\s*.*?;': '',
        r'\b_?shadowOffset\b': '0',
        r'XorShadow\.Compute\(.*?\)(?=\s*;)': '0',
        r'XorShadow\.Validate\(.*?\)(?=\s*\))': 'true',
    }
    for old, new in mappings.items():
        e_body = re.sub(old, new, e_body, flags=re.S)
        d_body = re.sub(old, new, d_body, flags=re.S)
    return e_body, d_body

e_body, d_body = test_normalize_body(e_raw, d_raw)

print("--- TryEnqueue ---")
print(e_body)
print("--- TryDequeue ---")
print(d_body)
