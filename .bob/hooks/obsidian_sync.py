"""
Obsidian Knowledge Management Integration for Bob CLI

Automatically syncs Bob CLI sessions to Obsidian vault for knowledge management.
Creates structured notes with metadata, learnings, and cross-references.

Author: V12 Universal OR Strategy
Version: 1.0.0
"""

import os
import json
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Optional, Any
import hashlib


class ObsidianSync:
    """Syncs Bob CLI sessions to Obsidian vault"""
    
    def __init__(self, vault_path: Optional[str] = None):
        """
        Initialize Obsidian sync
        
        Args:
            vault_path: Path to Obsidian vault (default: ~/Obsidian/V12-Knowledge)
        """
        if vault_path:
            self.vault_path = Path(vault_path)
        else:
            home = Path.home()
            self.vault_path = home / "Obsidian" / "V12-Knowledge"
        
        # Create vault structure
        self.sessions_dir = self.vault_path / "Sessions"
        self.learnings_dir = self.vault_path / "Learnings"
        self.agents_dir = self.vault_path / "Agents"
        self.files_dir = self.vault_path / "Files"
        self.tags_dir = self.vault_path / "Tags"
        
        self._ensure_vault_structure()
        
        # Session state
        self.session_id: Optional[str] = None
        self.session_start: Optional[datetime] = None
        self.session_data: Dict[str, Any] = {}
    
    def _ensure_vault_structure(self):
        """Create Obsidian vault directory structure"""
        for directory in [
            self.vault_path,
            self.sessions_dir,
            self.learnings_dir,
            self.agents_dir,
            self.files_dir,
            self.tags_dir
        ]:
            directory.mkdir(parents=True, exist_ok=True)
        
        # Create index files
        self._create_index_if_missing(self.vault_path / "README.md", self._get_vault_readme())
        self._create_index_if_missing(self.sessions_dir / "README.md", self._get_sessions_readme())
        self._create_index_if_missing(self.learnings_dir / "README.md", self._get_learnings_readme())
    
    def _create_index_if_missing(self, path: Path, content: str):
        """Create index file if it doesn't exist"""
        if not path.exists():
            path.write_text(content, encoding='utf-8')
    
    def _get_vault_readme(self) -> str:
        """Get vault README content"""
        return """# V12 Universal OR Strategy - Knowledge Vault

This Obsidian vault contains all knowledge accumulated by Bob CLI and other agents.

## Structure

- **Sessions/**: Individual agent session notes
- **Learnings/**: Extracted insights and patterns
- **Agents/**: Agent-specific knowledge bases
- **Files/**: File-specific notes and history
- **Tags/**: Tag-based organization

## Navigation

Use Obsidian's graph view to explore connections between:
- Sessions and learnings
- Files and modifications
- Agents and their specialties
- Tags and topics

## Automatic Sync

This vault is automatically updated by Bob CLI's `obsidian_sync.py` hook.
Do not manually edit files in Sessions/ - they are regenerated on each session.

## Search Tips

- Use `tag:#phoenix` to find Phoenix tracing sessions
- Use `tag:#refactoring` to find refactoring sessions
- Use `[[filename]]` to see all sessions that touched a file
- Use graph view to find related learnings
"""
    
    def _get_sessions_readme(self) -> str:
        """Get sessions README content"""
        return """# Agent Sessions

Each file represents one Bob CLI session with:
- Session metadata (agent, timestamp, duration)
- Task description and goals
- Tools used and files modified
- Learnings extracted
- Cross-references to related sessions

## Naming Convention

`YYYY-MM-DD_HHmmss_<agent>_<task-hash>.md`

Example: `2026-05-28_120000_bob_a3f2c1.md`

## Tags

- `#bob-cli` - Bob CLI sessions
- `#refactoring` - Code refactoring tasks
- `#phoenix` - Phoenix tracing integration
- `#firebase` - Firebase/Compound Intelligence
- `#testing` - Testing and verification
"""
    
    def _get_learnings_readme(self) -> str:
        """Get learnings README content"""
        return """# Learnings

Extracted insights and patterns from agent sessions.

## Types

- **Patterns**: Recurring code patterns or architectural decisions
- **Gotchas**: Common pitfalls and how to avoid them
- **Best Practices**: Proven approaches that work well
- **Anti-Patterns**: Approaches to avoid

## Naming Convention

`<category>_<topic>_<hash>.md`

Example: `pattern_fsm-actor_b4e1f2.md`

## Tags

- `#pattern` - Design patterns
- `#gotcha` - Common pitfalls
- `#best-practice` - Recommended approaches
- `#anti-pattern` - Approaches to avoid
"""
    
    def start_session(self, agent_name: str, task_description: str, session_id: Optional[str] = None):
        """
        Start tracking a new session
        
        Args:
            agent_name: Name of the agent (e.g., 'bob', 'codex')
            task_description: Description of the task
            session_id: Optional session ID (generated if not provided)
        """
        self.session_start = datetime.now()
        self.session_id = session_id or self._generate_session_id(agent_name, task_description)
        
        self.session_data = {
            'agent': agent_name,
            'task': task_description,
            'start_time': self.session_start.isoformat(),
            'tools_used': [],
            'files_modified': [],
            'files_read': [],
            'learnings': [],
            'tags': [f'#{agent_name}'],
            'related_sessions': []
        }
    
    def _generate_session_id(self, agent_name: str, task_description: str) -> str:
        """Generate unique session ID"""
        content = f"{agent_name}_{task_description}_{datetime.now().isoformat()}"
        hash_obj = hashlib.md5(content.encode())
        return hash_obj.hexdigest()[:6]
    
    def log_tool_use(self, tool_name: str, parameters: Dict[str, Any]):
        """Log tool usage"""
        if not self.session_data:
            return
        
        self.session_data['tools_used'].append({
            'tool': tool_name,
            'params': parameters,
            'timestamp': datetime.now().isoformat()
        })
    
    def log_file_modification(self, file_path: str, operation: str):
        """
        Log file modification
        
        Args:
            file_path: Path to modified file
            operation: Type of operation (create, modify, delete)
        """
        if not self.session_data:
            return
        
        self.session_data['files_modified'].append({
            'path': file_path,
            'operation': operation,
            'timestamp': datetime.now().isoformat()
        })
        
        # Create file note if it doesn't exist
        self._create_file_note(file_path)
    
    def log_file_read(self, file_path: str):
        """Log file read"""
        if not self.session_data:
            return
        
        if file_path not in self.session_data['files_read']:
            self.session_data['files_read'].append(file_path)
    
    def add_learning(self, learning: str, category: str = 'general'):
        """
        Add a learning from this session
        
        Args:
            learning: The learning/insight
            category: Category (pattern, gotcha, best-practice, anti-pattern)
        """
        if not self.session_data:
            return
        
        self.session_data['learnings'].append({
            'content': learning,
            'category': category,
            'timestamp': datetime.now().isoformat()
        })
        
        # Create learning note
        self._create_learning_note(learning, category)
    
    def add_tag(self, tag: str):
        """Add a tag to this session"""
        if not self.session_data:
            return
        
        if not tag.startswith('#'):
            tag = f'#{tag}'
        
        if tag not in self.session_data['tags']:
            self.session_data['tags'].append(tag)
    
    def finalize_session(self, success: bool = True, notes: Optional[str] = None):
        """
        Finalize session and write to Obsidian
        
        Args:
            success: Whether session completed successfully
            notes: Optional additional notes
        """
        if not self.session_data or not self.session_id or not self.session_start:
            return
        
        end_time = datetime.now()
        duration = (end_time - self.session_start).total_seconds()
        
        self.session_data['end_time'] = end_time.isoformat()
        self.session_data['duration_seconds'] = duration
        self.session_data['success'] = success
        if notes:
            self.session_data['notes'] = notes
        
        # Write session note
        self._write_session_note()
        
        # Update agent profile
        self._update_agent_profile()
        
        # Clear session state
        self.session_data = {}
        self.session_id = None
        self.session_start = None
    
    def _write_session_note(self):
        """Write session note to Obsidian"""
        if not self.session_id or not self.session_data:
            return
        
        # Generate filename
        timestamp = datetime.fromisoformat(self.session_data['start_time'])
        filename = f"{timestamp.strftime('%Y-%m-%d_%H%M%S')}_{self.session_data['agent']}_{self.session_id}.md"
        filepath = self.sessions_dir / filename
        
        # Generate content
        content = self._generate_session_content()
        
        # Write file
        filepath.write_text(content, encoding='utf-8')
    
    def _generate_session_content(self) -> str:
        """Generate session note content in Obsidian format"""
        data = self.session_data
        
        # Header with metadata
        content = f"""---
agent: {data['agent']}
session_id: {self.session_id}
start_time: {data['start_time']}
end_time: {data.get('end_time', 'N/A')}
duration: {data.get('duration_seconds', 0):.1f}s
success: {data.get('success', True)}
tags: {', '.join(data['tags'])}
---

# Session: {data['task']}

**Agent**: [[{data['agent']}]]
**Status**: {'✅ Success' if data.get('success', True) else '❌ Failed'}
**Duration**: {data.get('duration_seconds', 0):.1f}s

## Task Description

{data['task']}

"""
        
        # Tools used
        if data['tools_used']:
            content += "## Tools Used\n\n"
            for tool in data['tools_used']:
                content += f"- `{tool['tool']}` at {tool['timestamp']}\n"
            content += "\n"
        
        # Files modified
        if data['files_modified']:
            content += "## Files Modified\n\n"
            for file in data['files_modified']:
                content += f"- [[{file['path']}]] ({file['operation']})\n"
            content += "\n"
        
        # Files read
        if data['files_read']:
            content += "## Files Read\n\n"
            for file in data['files_read']:
                content += f"- [[{file}]]\n"
            content += "\n"
        
        # Learnings
        if data['learnings']:
            content += "## Learnings\n\n"
            for learning in data['learnings']:
                content += f"### {learning['category'].title()}\n\n"
                content += f"{learning['content']}\n\n"
        
        # Notes
        if data.get('notes'):
            content += f"## Notes\n\n{data['notes']}\n\n"
        
        # Related sessions
        if data.get('related_sessions'):
            content += "## Related Sessions\n\n"
            for session in data['related_sessions']:
                content += f"- [[{session}]]\n"
            content += "\n"
        
        # Tags
        content += f"\n{' '.join(data['tags'])}\n"
        
        return content
    
    def _create_file_note(self, file_path: str):
        """Create or update file note"""
        # Sanitize filename for Obsidian
        safe_name = file_path.replace('/', '_').replace('\\', '_').replace(':', '_')
        filepath = self.files_dir / f"{safe_name}.md"
        
        if filepath.exists():
            # Append to existing note
            content = filepath.read_text(encoding='utf-8')
            content += f"\n- Modified in session [[{self.session_id}]] at {datetime.now().isoformat()}\n"
        else:
            # Create new note
            content = f"""# {file_path}

## Modification History

- Modified in session [[{self.session_id}]] at {datetime.now().isoformat()}

#file
"""
        
        filepath.write_text(content, encoding='utf-8')
    
    def _create_learning_note(self, learning: str, category: str):
        """Create learning note"""
        # Generate hash for unique filename
        hash_obj = hashlib.md5(learning.encode())
        learning_id = hash_obj.hexdigest()[:6]
        
        # Create filename
        topic = learning.split()[0].lower() if learning else 'general'
        filename = f"{category}_{topic}_{learning_id}.md"
        filepath = self.learnings_dir / filename
        
        if not filepath.exists():
            content = f"""---
category: {category}
created: {datetime.now().isoformat()}
session: {self.session_id}
---

# {category.title()}: {topic.title()}

{learning}

## Related Sessions

- [[{self.session_id}]]

#{category}
"""
            filepath.write_text(content, encoding='utf-8')
    
    def _update_agent_profile(self):
        """Update agent profile with session stats"""
        agent_name = self.session_data['agent']
        filepath = self.agents_dir / f"{agent_name}.md"
        
        if filepath.exists():
            content = filepath.read_text(encoding='utf-8')
            # Append session reference
            content += f"\n- [[{self.session_id}]] - {self.session_data['task'][:50]}...\n"
        else:
            # Create new agent profile
            content = f"""# Agent: {agent_name}

## Recent Sessions

- [[{self.session_id}]] - {self.session_data['task'][:50]}...

#agent #{agent_name}
"""
        
        filepath.write_text(content, encoding='utf-8')


# Global instance
_obsidian_sync: Optional[ObsidianSync] = None


def get_obsidian_sync() -> ObsidianSync:
    """Get or create global ObsidianSync instance"""
    global _obsidian_sync
    if _obsidian_sync is None:
        vault_path = os.getenv('OBSIDIAN_VAULT_PATH')
        _obsidian_sync = ObsidianSync(vault_path)
    return _obsidian_sync


# Convenience functions for hooks
def start_session(agent_name: str, task_description: str, session_id: Optional[str] = None):
    """Start tracking session"""
    sync = get_obsidian_sync()
    sync.start_session(agent_name, task_description, session_id)


def log_tool_use(tool_name: str, parameters: Dict[str, Any]):
    """Log tool usage"""
    sync = get_obsidian_sync()
    sync.log_tool_use(tool_name, parameters)


def log_file_modification(file_path: str, operation: str):
    """Log file modification"""
    sync = get_obsidian_sync()
    sync.log_file_modification(file_path, operation)


def log_file_read(file_path: str):
    """Log file read"""
    sync = get_obsidian_sync()
    sync.log_file_read(file_path)


def add_learning(learning: str, category: str = 'general'):
    """Add learning"""
    sync = get_obsidian_sync()
    sync.add_learning(learning, category)


def add_tag(tag: str):
    """Add tag"""
    sync = get_obsidian_sync()
    sync.add_tag(tag)


def finalize_session(success: bool = True, notes: Optional[str] = None):
    """Finalize session"""
    sync = get_obsidian_sync()
    sync.finalize_session(success, notes)


if __name__ == '__main__':
    # Test the sync
    print("Testing Obsidian sync...")
    
    sync = ObsidianSync()
    print(f"Vault path: {sync.vault_path}")
    print(f"Vault exists: {sync.vault_path.exists()}")
    
    # Test session
    sync.start_session('bob', 'Test Obsidian integration')
    sync.log_tool_use('read_file', {'path': 'test.py'})
    sync.log_file_modification('test.py', 'modify')
    sync.add_learning('Obsidian integration works!', 'pattern')
    sync.add_tag('testing')
    sync.finalize_session(success=True, notes='Test completed successfully')
    
    print("✅ Test session created successfully!")
    print(f"Check: {sync.sessions_dir}")

# Made with Bob
