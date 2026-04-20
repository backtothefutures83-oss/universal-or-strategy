import os
from datetime import datetime, date

downloads_path = os.path.expanduser("~/Downloads")
today = date.today()

files = []
for f in os.listdir(downloads_path):
    full_path = os.path.join(downloads_path, f)
    if os.path.isfile(full_path):
        mtime = datetime.fromtimestamp(os.path.getmtime(full_path)).date()
        if mtime == today and f.endswith(".zip"):
            files.append((f, full_path, os.path.getmtime(full_path)))

# Sort by last modified
files.sort(key=lambda x: x[2], reverse=True)

for f, full_path, mtime in files[:5]:
    print(full_path)
