import os
import zipfile
from datetime import datetime, date

downloads_path = os.path.expanduser("~/Downloads")
extract_base = "c:/WSGTA/universal-or-strategy/tmp/battle_results"
os.makedirs(extract_base, exist_ok=True)

today = date.today()
files = []
for f in os.listdir(downloads_path):
    full_path = os.path.join(downloads_path, f)
    if os.path.isfile(full_path) and f.endswith(".zip"):
        mtime = datetime.fromtimestamp(os.path.getmtime(full_path)).date()
        if mtime == today:
            files.append((f, full_path, os.path.getmtime(full_path)))

files.sort(key=lambda x: x[2], reverse=True)

for i, (f, full_path, mtime) in enumerate(files[:5]):
    target_dir = os.path.join(extract_base, f"battle_{i}")
    os.makedirs(target_dir, exist_ok=True)
    try:
        with zipfile.ZipFile(full_path, 'r') as zip_ref:
            zip_ref.extractall(target_dir)
            print(f"Extracted {f} to {target_dir}")
    except Exception as e:
        print(f"Failed to extract {f}: {e}")
