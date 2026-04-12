import os
import re

def get_method_body(full_code, method_name):
    match = re.search(method_name + r'[\s\S]*?\{', full_code)
    if not match: 
        print(f"DEBUG: No match for {method_name}")
        return ""
    start_idx = match.end() - 1
    brace_count = 0
    for i in range(start_idx, len(full_code)):
        if full_code[i] == '{': brace_count += 1
        elif full_code[i] == '}':
            brace_count -= 1
            if brace_count == 0:
                body = full_code[start_idx+1:i].strip()
                print(f"DEBUG: Successfully extracted {method_name} body ({len(body)} chars)")
                return body
    print(f"DEBUG: Brace mismatch for {method_name}")
    return ""

def test():
    app_tsx = r"c:\tmp\arena_round_today\submission_21\src\App.tsx"
    with open(app_tsx, 'r', encoding='utf-8') as f: content = f.read()
    literals = re.findall(r'`([\s\S]*?)`', content)
    candidates = [lit.strip() for lit in literals if "TryEnqueue" in lit]
    print(f"DEBUG: Found {len(candidates)} candidates for TryEnqueue")
    if candidates:
        code = max(candidates, key=len)
        print(f"DEBUG: Selected candidate of length {len(code)}")
        e_raw = get_method_body(code, "TryEnqueue")
        d_raw = get_method_body(code, "TryDequeue")
        print("======== ENQUEUE BODY ========")
        print(e_raw)
        print("======== DEQUEUE BODY ========")
        print(d_raw)

if __name__ == "__main__": test()
