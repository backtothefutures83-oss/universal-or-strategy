import os
import sys
import json
import firebase_admin
from firebase_admin import credentials
from firebase_admin import firestore

def init_firestore():
    """
    Initializes Firebase using local or env credentials.
    """
    cred_path = os.environ.get("FIREBASE_CREDENTIALS")
    default_cred_path = "firebase-credentials.json"
    
    if not cred_path and os.path.exists(default_cred_path):
        cred_path = default_cred_path
        
    if not cred_path:
        print("[!] firebase-credentials.json not found in root or FIREBASE_CREDENTIALS env var.")
        sys.exit(1)
        
    try:
        cred = credentials.Certificate(cred_path)
        firebase_admin.initialize_app(cred)
        return firestore.client()
    except Exception as e:
        print(f"[!] Failed to initialize Firebase: {e}")
        sys.exit(1)

def match_document(doc_data, query):
    """
    Performs case-insensitive substring search across different document fields.
    """
    query = query.lower()
    
    # 1. Title match
    if query in doc_data.get("title", "").lower():
        return True
        
    # 2. Categories match
    for category in doc_data.get("categories", []):
        if query in category.lower():
            return True
            
    # 3. Key Takeaways match
    for takeaway in doc_data.get("key_takeaways", []):
        if query in takeaway.lower():
            return True
            
    # 4. C# Patterns match
    for key, value in doc_data.get("v12_csharp_patterns", {}).items():
        if query in key.lower() or query in value.lower():
            return True
            
    return False

def query_kb():
    if len(sys.argv) < 2 or sys.argv[1] in ("-h", "--help"):
        print("Usage: python scripts/query_kb.py <query_string>")
        print("Example: python scripts/query_kb.py zero-allocation")
        sys.exit(0)
        
    query_str = sys.argv[1]
    db = init_firestore()
    
    print(f"[*] Fetching documents from Firestore...")
    docs_ref = db.collection("jane_street_knowledge_base").stream()
    
    matches = []
    total_docs = 0
    for doc in docs_ref:
        total_docs += 1
        data = doc.to_dict()
        if match_document(data, query_str):
            matches.append((doc.id, data))
            
    print(f"[*] Searched {total_docs} documents. Found {len(matches)} matches for '{query_str}':\n")
    
    if not matches:
        print("[-] No matching documents found.")
        return
        
    for doc_id, data in matches:
        print("=" * 60)
        print(f"TITLE: {data.get('title')}")
        print(f"DOCUMENT ID: {doc_id}")
        print(f"SOURCE URL: {data.get('source_url', 'N/A')}")
        print(f"CATEGORIES: {', '.join(data.get('categories', []))}")
        print("KEY TAKEAWAYS:")
        for idx, takeaway in enumerate(data.get("key_takeaways", []), 1):
            print(f"  {idx}. {takeaway}")
        print("V12 C# PATTERNS:")
        for pattern_name, pattern_desc in data.get("v12_csharp_patterns", {}).items():
            print(f"  - {pattern_name}: {pattern_desc}")
        print("=" * 60 + "\n")

if __name__ == "__main__":
    query_kb()
