import os
import json
import glob
import re

def extract_rag_metadata(filepath):
    """
    Extracts the JSON RAG Metadata block from a processed markdown file.
    """
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Locate the markdown code block containing JSON metadata
    match = re.search(r'```json\s*(\{.*?\})\s*```', content, re.DOTALL)
    if not match:
        print(f"[-] No JSON metadata block found in: {filepath}")
        return None
        
    try:
        data = json.loads(match.group(1))
        return data
    except json.JSONDecodeError as e:
        print(f"[!] JSON parsing error in {filepath}: {e}")
        return None

def sync_to_firestore():
    processed_dir = os.path.join("experts", "jane_street", "processed")
    md_files = glob.glob(os.path.join(processed_dir, "*.md"))
    
    if not md_files:
        print("[-] No processed markdown files found to sync.")
        return

    print(f"[*] Found {len(md_files)} processed files. Extracting metadata...")
    records = []
    for filepath in md_files:
        data = extract_rag_metadata(filepath)
        if data:
            records.append((filepath, data))
            
    if not records:
        print("[-] No valid metadata records extracted.")
        return

    # Check for Firebase credentials
    cred_path = os.environ.get("FIREBASE_CREDENTIALS")
    default_cred_path = "firebase-credentials.json"
    
    if not cred_path and os.path.exists(default_cred_path):
        cred_path = default_cred_path

    # Initialize Firebase if credentials are found
    db = None
    if cred_path:
        try:
            import firebase_admin
            from firebase_admin import credentials
            from firebase_admin import firestore
            
            print(f"[+] Initializing Firebase with credentials from: {cred_path}")
            cred = credentials.Certificate(cred_path)
            firebase_admin.initialize_app(cred)
            db = firestore.client()
            print("[+] Firebase Admin SDK initialized successfully.")
        except Exception as e:
            print(f"[!] Failed to initialize Firebase: {e}")
            db = None
    else:
        print("\n" + "="*80)
        print("[-] FIREBASE CREDENTIALS NOT FOUND")
        print("="*80)
        print("To upload this distilled intelligence to your Firestore Knowledge Base:")
        print("1. Go to your Firebase Console -> Project Settings -> Service Accounts.")
        print("2. Click 'Generate new private key' and download the JSON file.")
        print(f"3. Save the file as '{default_cred_path}' in the root directory,")
        print("   OR set the path in your environment variables: FIREBASE_CREDENTIALS=<path_to_json>.")
        print("="*80 + "\n")

    # Process uploads or print preview
    for filepath, data in records:
        doc_id = data.get("document_id")
        title = data.get("title", "Untitled")
        
        if not doc_id:
            basename = os.path.basename(filepath)
            doc_id = os.path.splitext(basename)[0]
            data["document_id"] = doc_id

        if db:
            try:
                # Store in collection 'jane_street_knowledge_base'
                doc_ref = db.collection("jane_street_knowledge_base").document(doc_id)
                doc_ref.set(data)
                print(f"[+] Synced: '{title}' -> document_id: '{doc_id}'")
            except Exception as e:
                print(f"[!] Error uploading document '{doc_id}': {e}")
        else:
            # Print preview of what would be uploaded
            print(f"[*] Previewing Record (ID: {doc_id}):")
            print(f"    Title: {title}")
            print(f"    Source: {data.get('source_url', 'N/A')}")
            print(f"    Categories: {', '.join(data.get('categories', []))}")
            print(f"    Takeaways: {len(data.get('key_takeaways', []))} points extracted")
            print(f"    V12 Patterns: {list(data.get('v12_csharp_patterns', {}).keys())}")
            print("-" * 50)

if __name__ == "__main__":
    sync_to_firestore()
