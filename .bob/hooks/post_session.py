#!/usr/bin/env python3
"""Post-session hook for Bob CLI.

This hook runs after Bob CLI completes a session.
It finalizes Phoenix tracing and logs session data to Compound Intelligence.
"""

import sys
import os
from pathlib import Path

# Add scripts to path
repo_root = Path(__file__).parent.parent.parent
sys.path.insert(0, str(repo_root / 'scripts'))
sys.path.insert(0, str(repo_root / '.bob' / 'hooks'))

# Import Phoenix tracer
try:
    from phoenix_tracer import finalize_tracing
    PHOENIX_AVAILABLE = True
except ImportError:
    PHOENIX_AVAILABLE = False
    print("[Bob Post-Session] Warning: Phoenix tracer not available")

# Import Compound Intelligence logger
try:
    from compound_intelligence_logger import log_session_completion
    COMPOUND_AVAILABLE = True
except ImportError:
    COMPOUND_AVAILABLE = False
    print("[Bob Post-Session] Warning: Compound Intelligence logger not available")

# Import Obsidian sync
try:
    from obsidian_sync import finalize_session as obs_finalize_session
    OBSIDIAN_AVAILABLE = True
except ImportError:
    OBSIDIAN_AVAILABLE = False
    print("[Bob Post-Session] Warning: Obsidian sync not available")


def main():
    """Finalize Bob CLI session."""
    try:
        # Get session status from environment
        status = os.getenv('BOB_SESSION_STATUS', 'success')
        error = os.getenv('BOB_SESSION_ERROR', None)
        
        print(f"[Bob Post-Session] Finalizing session (status: {status})")
        
        # Finalize Phoenix tracing
        if PHOENIX_AVAILABLE:
            try:
                finalize_tracing(status, error)
                print("[Bob Post-Session] Phoenix tracing finalized")
            except Exception as e:
                print(f"[Bob Post-Session] Warning: Failed to finalize Phoenix tracing: {e}")
        
        # Log to Compound Intelligence
        if COMPOUND_AVAILABLE:
            try:
                log_session_completion(status, error)
                print("[Bob Post-Session] ✅ Compound Intelligence logged")
            except Exception as e:
                print(f"[Bob Post-Session] ⚠️ Failed to log to Compound Intelligence: {e}")
        
        # Finalize Obsidian sync
        if OBSIDIAN_AVAILABLE:
            try:
                success = (status == 'success')
                notes = error if error else None
                obs_finalize_session(success=success, notes=notes)
                print("[Bob Post-Session] ✅ Obsidian session note created")
            except Exception as e:
                print(f"[Bob Post-Session] ⚠️ Failed to finalize Obsidian sync: {e}")
        
        print("[Bob Post-Session] 🏁 Session finalized successfully")
        
    except Exception as e:
        print(f"[Bob Post-Session] Warning: Post-session hook failed: {e}")
        # Don't fail the session


if __name__ == "__main__":
    main()

# Made with Bob
