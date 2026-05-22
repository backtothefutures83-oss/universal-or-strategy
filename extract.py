import json
import os

log_path = r'C:\Users\Mohammed Khalid\.gemini\antigravity\brain\98f1f549-e86b-4fcf-aa9a-8499aa63cdfc\.system_generated\logs\overview.txt'

with open(log_path, 'r', encoding='utf-8') as f:
    for line in f:
        data = json.loads(line)
        for tc in data.get('tool_calls', []):
            if tc.get('name') == 'write_to_file':
                args = tc.get('args', {})
                tf = args.get('TargetFile', '')
                if 'query_kb.py' in tf:
                    # Fix escape sequences from the JSON log (tool args are sometimes double escaped stringified json)
                    content = args.get('CodeContent', '')
                    if isinstance(content, str):
                        with open(r'c:\WSGTA\universal-or-strategy\query_kb.extracted.py', 'w', encoding='utf-8') as out:
                            # It might be literal string with \n or actual newlines, let's just write it
                            # The log stores CodeContent as a string, but because of JSON it might be escaped. 
                            # json.loads handles the first unescaping.
                            out.write(content)
                elif 'sync_to_firestore.py' in tf:
                    content = args.get('CodeContent', '')
                    if isinstance(content, str):
                        with open(r'c:\WSGTA\universal-or-strategy\sync_to_firestore.extracted.py', 'w', encoding='utf-8') as out:
                            out.write(content)
