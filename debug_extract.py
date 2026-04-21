import re
import html
import os

SUBMISSIONS_DIR = r"c:\tmp\arena_round_22"
sub = "hardware-striped-affinity-implementation"
index_html = os.path.join(SUBMISSIONS_DIR, sub, "index.html")

def get_method_body(full_code, method_name):
    # Find the start of the method and its first brace
    start_match = re.search(method_name + r'[\s\S]*?\{', full_code)
    if not start_match: return "NOT FOUND START"
    
    start_pos = start_match.end()
    brace_count = 1
    end_pos = start_pos
    
    while brace_count > 0 and end_pos < len(full_code):
        if full_code[end_pos] == '{':
            brace_count += 1
        elif full_code[end_pos] == '}':
            brace_count -= 1
        end_pos += 1
        
    if brace_count == 0:
        return full_code[start_pos:end_pos-1].strip()
    return f"BRACE COUNT ERROR: {brace_count}"

with open(index_html, 'r', encoding='utf-8') as f: 
    raw_html = f.read()
    content = html.unescape(re.sub(r'<[^>]+>', '', raw_html))
    print(f"Content length: {len(content)}")
    e_raw = get_method_body(content, "TryEnqueue")
    print("--- TryEnqueue Body ---")
    print(e_raw[:500])
    print("--- End ---")
