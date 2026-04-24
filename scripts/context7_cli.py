import sys
import os
import json
import subprocess
import argparse

def get_api_key():
    api_key = os.environ.get("CONTEXT7_API_KEY")
    if not api_key:
        raise RuntimeError("CONTEXT7_API_KEY is required")
    return api_key

def call_context7_mcp(method, params):
    """
    Simulates a JSON-RPC call to the Context7 MCP server over stdin/stdout.
    Performs the standard MCP initialization handshake.
    """
    try:
        cmd = ["npx", "-y", "@upstash/context7-mcp"]
        api_key = get_api_key()
        env = os.environ.copy()
        env["CONTEXT7_API_KEY"] = api_key

        process = subprocess.Popen(
            cmd,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            shell=True,
            env=env
        )
        
        # 1. Initialize
        init_req = {
            "jsonrpc": "2.0",
            "id": 1,
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {"name": "context7-sovereign-cli", "version": "1.0.0"}
            }
        }
        process.stdin.write(json.dumps(init_req) + "\n")
        process.stdin.flush()
        
        # 2. Initialized (notification)
        initialized_notif = {
            "jsonrpc": "2.0",
            "method": "notifications/initialized"
        }
        process.stdin.write(json.dumps(initialized_notif) + "\n")
        process.stdin.flush()

        # 3. The actual Tool Call
        request = {
            "jsonrpc": "2.0",
            "id": 2,
            "method": method,
            "params": params
        }
        process.stdin.write(json.dumps(request) + "\n")
        process.stdin.flush()

        # 4. Read response
        # We read until we get a response with id=2
        for line in iter(process.stdout.readline, ""):
            if not line: break
            try:
                response = json.loads(line)
                if response.get("id") == 2:
                    return response.get("result", response.get("error"))
            except json.JSONDecodeError:
                continue
                
        stdout, stderr = process.communicate(timeout=5)
        return {"error": "Timeout or failed to get response", "stderr": stderr}
    except Exception as e:
        return {"error": str(e)}

def main():
    parser = argparse.ArgumentParser(description="Context7 Sovereign CLI")
    subparsers = parser.add_subparsers(dest="command", help="Commands")

    # Query command
    query_parser = subparsers.add_parser("query", help="Search library documentation")
    query_parser.add_argument("lib_id", help="Exact library ID (e.g., /upstash/context7-mcp)")
    query_parser.add_argument("query", help="What you're looking for")

    # Resolve command
    resolve_parser = subparsers.add_parser("resolve", help="Resolve library name to ID")
    resolve_parser.add_argument("library", help="Library name (e.g., React)")

    args = parser.parse_args()

    if args.command == "query":
        # MCP Tool name mapping: tools/call is the standard MCP method
        params = {
            "name": "query-docs",
            "arguments": {"libraryId": args.lib_id, "query": args.query}
        }
        result = call_context7_mcp("tools/call", params)
        print(json.dumps(result, indent=2))
        return 1 if isinstance(result, dict) and result.get("error") else 0
    elif args.command == "resolve":
        params = {
            "name": "resolve-library-id",
            "arguments": {"libraryName": args.library, "query": f"Resolve {args.library}"}
        }
        result = call_context7_mcp("tools/call", params)
        print(json.dumps(result, indent=2))
        return 1 if isinstance(result, dict) and result.get("error") else 0
    else:
        parser.print_help()
        return 1

if __name__ == "__main__":
    raise SystemExit(main())
