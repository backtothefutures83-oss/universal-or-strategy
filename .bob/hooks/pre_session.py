#!/usr/bin/env python3
"""Pre-session hook for Bob CLI to load bootstrap context.

This hook runs automatically when Bob CLI starts a session.
It loads context from Jane Street KB, Graphify, and Compound Intelligence.
It also initializes Phoenix tracing.
"""

import sys
import os
from pathlib import Path

# Add scripts to path
repo_root = Path(__file__).parent.parent.parent
sys.path.insert(0, str(repo_root / 'scripts'))
sys.path.insert(0, str(repo_root / '.bob' / 'hooks'))

try:
    from agent_bootstrap import bootstrap_agent
except ImportError as e:
    print(f"[Bob Bootstrap] Error: Failed to import agent_bootstrap: {e}")
    sys.exit(0)  # Don't fail the session, just skip bootstrap

try:
    from query_kb import init_firestore
except ImportError:
    init_firestore = None

# Import Phoenix tracer
try:
    from phoenix_tracer import initialize_tracing
    PHOENIX_AVAILABLE = True
except ImportError:
    PHOENIX_AVAILABLE = False
    print("[Bob Bootstrap] Warning: Phoenix tracer not available")

# Import Compound Intelligence logger
try:
    from compound_intelligence_logger import start_session as ci_start_session
    CI_AVAILABLE = True
except ImportError:
    CI_AVAILABLE = False
    print("[Bob Bootstrap] Warning: Compound Intelligence logger not available")

# Import Obsidian sync
try:
    from obsidian_sync import start_session as obs_start_session
    OBS_AVAILABLE = True
except ImportError:
    OBS_AVAILABLE = False
    print("[Bob Bootstrap] Warning: Obsidian sync not available")


def generate_jane_street_rules(context):
    """Generate mandatory rules file from Jane Street KB."""
    jane_street_patterns = context.get('jane_street', [])
    
    if not jane_street_patterns:
        return None
    
    lines = [
        "# Jane Street Principles (Auto-Generated)",
        "",
        "**Source**: Loaded from Jane Street Knowledge Base on session start",
        "**Status**: MANDATORY - These are architectural constraints, not suggestions",
        "",
        "## Core Principles",
        ""
    ]
    
    for pattern in jane_street_patterns:
        title = pattern.get('title', 'Unknown')
        lines.append(f"### {title}")
        lines.append("")
        
        # Add key takeaways
        takeaways = pattern.get('key_takeaways', [])
        if takeaways:
            lines.append("**Key Takeaways**:")
            for takeaway in takeaways:
                lines.append(f"- {takeaway}")
            lines.append("")
        
        # Add V12 C# patterns if available
        v12_patterns = pattern.get('v12_csharp_patterns', {})
        if v12_patterns:
            lines.append("**V12 C# Patterns**:")
            for key, value in v12_patterns.items():
                lines.append(f"- **{key}**: {value}")
            lines.append("")
    
    lines.extend([
        "## Enforcement",
        "",
        "- These principles MUST be applied to all architectural decisions",
        "- Violations should be flagged during code review",
        "- When in doubt, query the Jane Street KB: `python scripts/query_kb.py <term>`",
        ""
    ])
    
    return '\n'.join(lines)


def main():
    """Load bootstrap context for Bob CLI session."""
    try:
        # Detect task type from Bob mode
        mode = os.getenv('BOB_MODE', 'v12-engineer')
        
        # Map mode to task type
        task_type_map = {
            'v12-engineer': 'architecture',
            'v12-epic-planner': 'architecture',
            'code': 'refactoring',
            'advanced': 'refactoring',
            'plan': 'architecture',
            'ask': 'debugging'
        }
        
        task_type = task_type_map.get(mode, 'architecture')
        
        print(f"[Bob Bootstrap] Loading context for mode: {mode} (task type: {task_type})")
        
        # Get task description
        task_desc = os.getenv('BOB_TASK_DESCRIPTION', f"{mode} session")
        session_id = None
        
        # Initialize Phoenix tracing
        if PHOENIX_AVAILABLE:
            try:
                session_id = initialize_tracing(mode, task_desc)
                if session_id:
                    print(f"[Bob Bootstrap] ✅ Phoenix tracing initialized: {session_id}")
            except Exception as e:
                print(f"[Bob Bootstrap] ⚠️ Failed to initialize Phoenix tracing: {e}")
        
        # Initialize Compound Intelligence logger
        if CI_AVAILABLE:
            try:
                ci_start_session(mode, task_desc, session_id)
                print(f"[Bob Bootstrap] ✅ Compound Intelligence logger initialized")
            except Exception as e:
                print(f"[Bob Bootstrap] ⚠️ Failed to initialize Compound Intelligence: {e}")
        
        # Initialize Obsidian sync
        if OBS_AVAILABLE:
            try:
                obs_start_session(mode, task_desc, session_id)
                print(f"[Bob Bootstrap] ✅ Obsidian sync initialized")
            except Exception as e:
                print(f"[Bob Bootstrap] ⚠️ Failed to initialize Obsidian sync: {e}")
        
        # Load context
        result = bootstrap_agent('Bob', task_type)
        
        # Save bootstrap summary to Bob's context directory
        context_dir = repo_root / '.bob' / 'context'
        context_dir.mkdir(parents=True, exist_ok=True)
        
        context_path = context_dir / 'bootstrap.md'
        context_path.write_text(result['summary'], encoding='utf-8')
        
        # Generate mandatory Jane Street rules file
        rules_content = generate_jane_street_rules(result['context'])
        if rules_content:
            rules_dir = repo_root / '.bob' / 'rules-v12-engineer'
            rules_dir.mkdir(parents=True, exist_ok=True)
            
            rules_path = rules_dir / '99-jane-street-auto.md'
            rules_path.write_text(rules_content, encoding='utf-8')
            
            print(f"[Bob Bootstrap] Jane Street rules generated: {rules_path}")
        
        print(f"[Bob Bootstrap] Context loaded successfully")
        print(f"[Bob Bootstrap] Saved to: {context_path}")
        print(f"[Bob Bootstrap] Jane Street patterns: {len(result['context']['jane_street'])}")
        print(f"[Bob Bootstrap] Graphify nodes: {len(result['context']['graphify'].get('nodes', []))}")
        print(f"[Bob Bootstrap] Learnings: {len(result['context']['learnings'])}")
        
    except Exception as e:
        print(f"[Bob Bootstrap] Warning: Bootstrap failed: {e}")
        print(f"[Bob Bootstrap] Continuing without bootstrap context...")


if __name__ == "__main__":
    main()