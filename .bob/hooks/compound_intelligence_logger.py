"""
Compound Intelligence Logger for Bob CLI

Logs sessions, tool usage, file modifications, and learnings to Firebase Firestore.
Accumulates knowledge across sessions for cross-session intelligence.

Author: V12 Universal OR Strategy
Version: 1.0.0
"""

import os
import json
from datetime import datetime
from typing import Dict, List, Optional, Any
import hashlib


class CompoundIntelligenceLogger:
    """Logs Bob CLI sessions to Firebase Firestore for compound intelligence"""
    
    def __init__(self):
        """Initialize Firestore connection"""
        self.db = None
        self.session_id: Optional[str] = None
        self.session_data: Dict[str, Any] = {}
        self.session_start: Optional[datetime] = None
        
        # Initialize Firebase
        try:
            import firebase_admin
            from firebase_admin import credentials, firestore
            
            # Check if already initialized
            try:
                self.db = firestore.client()
            except ValueError:
                # Initialize Firebase
                cred_path = os.getenv('GOOGLE_APPLICATION_CREDENTIALS')
                if cred_path and os.path.exists(cred_path):
                    cred = credentials.Certificate(cred_path)
                    firebase_admin.initialize_app(cred)
                    self.db = firestore.client()
                else:
                    print("[Compound Intelligence] Warning: Firebase credentials not found")
                    self.db = None
        except ImportError:
            print("[Compound Intelligence] Warning: firebase_admin not installed")
            self.db = None
    
    def start_session(self, agent_name: str, task_description: str, session_id: Optional[str] = None):
        """
        Start tracking a new session
        
        Args:
            agent_name: Name of the agent (e.g., 'bob', 'codex')
            task_description: Description of the task
            session_id: Optional session ID (generated if not provided)
        """
        if not self.db:
            return
        
        self.session_start = datetime.now()
        self.session_id = session_id or self._generate_session_id(agent_name, task_description)
        
        self.session_data = {
            'agent': agent_name,
            'task': task_description,
            'start_time': self.session_start,
            'tools_used': [],
            'files_modified': [],
            'files_read': [],
            'learnings': [],
            'status': 'in_progress'
        }
    
    def _generate_session_id(self, agent_name: str, task_description: str) -> str:
        """Generate unique session ID"""
        content = f"{agent_name}_{task_description}_{datetime.now().isoformat()}"
        hash_obj = hashlib.md5(content.encode())
        return hash_obj.hexdigest()[:12]
    
    def log_tool_use(self, tool_name: str, parameters: Dict[str, Any]):
        """Log tool usage"""
        if not self.db or not self.session_data:
            return
        
        self.session_data['tools_used'].append({
            'tool': tool_name,
            'params': parameters,
            'timestamp': datetime.now()
        })
    
    def log_file_modification(self, file_path: str, operation: str):
        """
        Log file modification
        
        Args:
            file_path: Path to modified file
            operation: Type of operation (create, modify, delete)
        """
        if not self.db or not self.session_data:
            return
        
        self.session_data['files_modified'].append({
            'path': file_path,
            'operation': operation,
            'timestamp': datetime.now()
        })
    
    def log_file_read(self, file_path: str):
        """Log file read"""
        if not self.db or not self.session_data:
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
        if not self.db or not self.session_data:
            return
        
        learning_data = {
            'content': learning,
            'category': category,
            'timestamp': datetime.now(),
            'session_id': self.session_id
        }
        
        self.session_data['learnings'].append(learning_data)
        
        # Also save to learnings collection
        try:
            learning_id = hashlib.md5(learning.encode()).hexdigest()[:8]
            self.db.collection('learnings').document(learning_id).set({
                **learning_data,
                'agent': self.session_data['agent'],
                'created_at': datetime.now()
            })
        except Exception as e:
            print(f"[Compound Intelligence] Warning: Failed to save learning: {e}")
    
    def log_session_completion(self, status: Optional[str] = None, error: Optional[str] = None):
        """
        Finalize session and upload to Firestore
        
        Args:
            status: Session status (success, error, cancelled)
            error: Error message if status is error
        """
        if not self.db or not self.session_data or not self.session_id:
            return
        
        try:
            end_time = datetime.now()
            duration = (end_time - self.session_start).total_seconds() if self.session_start else 0
            
            self.session_data['end_time'] = end_time
            self.session_data['duration_seconds'] = duration
            self.session_data['status'] = status or 'success'
            if error:
                self.session_data['error'] = error
            
            # Auto-generate learnings from patterns
            auto_learnings = self._generate_auto_learnings()
            self.session_data['auto_learnings'] = auto_learnings
            
            # Upload to Firestore
            self.db.collection('agent_sessions').document(self.session_id).set(self.session_data)
            
            print(f"[Compound Intelligence] Session logged: {self.session_id}")
            
            # Clear session state
            self.session_data = {}
            self.session_id = None
            self.session_start = None
            
        except Exception as e:
            print(f"[Compound Intelligence] Warning: Failed to log session: {e}")
    
    def _generate_auto_learnings(self) -> List[Dict[str, str]]:
        """Generate automatic learnings from session patterns"""
        learnings = []
        
        # Pattern: Frequent tool use
        tool_counts = {}
        for tool_use in self.session_data.get('tools_used', []):
            tool_name = tool_use['tool']
            tool_counts[tool_name] = tool_counts.get(tool_name, 0) + 1
        
        for tool, count in tool_counts.items():
            if count >= 5:
                learnings.append({
                    'content': f"Heavy use of {tool} ({count} times) - consider optimization",
                    'category': 'pattern'
                })
        
        # Pattern: File modification patterns
        modified_files = self.session_data.get('files_modified', [])
        if len(modified_files) > 10:
            learnings.append({
                'content': f"Large-scale refactoring: {len(modified_files)} files modified",
                'category': 'pattern'
            })
        
        # Pattern: Read-heavy vs write-heavy
        read_count = len(self.session_data.get('files_read', []))
        write_count = len(modified_files)
        if read_count > write_count * 3:
            learnings.append({
                'content': "Read-heavy session - mostly exploration/analysis",
                'category': 'pattern'
            })
        
        return learnings


# Global instance
_logger: Optional[CompoundIntelligenceLogger] = None


def get_logger() -> CompoundIntelligenceLogger:
    """Get or create global logger instance"""
    global _logger
    if _logger is None:
        _logger = CompoundIntelligenceLogger()
    return _logger


# Convenience functions for hooks
def start_session(agent_name: str, task_description: str, session_id: Optional[str] = None):
    """Start tracking session"""
    logger = get_logger()
    logger.start_session(agent_name, task_description, session_id)


def log_tool_use(tool_name: str, parameters: Dict[str, Any]):
    """Log tool usage"""
    logger = get_logger()
    logger.log_tool_use(tool_name, parameters)


def log_file_modification(file_path: str, operation: str):
    """Log file modification"""
    logger = get_logger()
    logger.log_file_modification(file_path, operation)


def log_file_read(file_path: str):
    """Log file read"""
    logger = get_logger()
    logger.log_file_read(file_path)


def add_learning(learning: str, category: str = 'general'):
    """Add learning"""
    logger = get_logger()
    logger.add_learning(learning, category)


def log_session_completion(status: Optional[str] = None, error: Optional[str] = None):
    """Finalize session"""
    logger = get_logger()
    logger.log_session_completion(status, error)


if __name__ == '__main__':
    # Test the logger
    print("Testing Compound Intelligence logger...")
    
    logger = CompoundIntelligenceLogger()
    if logger.db:
        print("[OK] Firebase connection successful")
        
        # Test session
        logger.start_session('test-agent', 'Test compound intelligence integration')
        logger.log_tool_use('read_file', {'path': 'test.py'})
        logger.log_file_modification('test.py', 'modify')
        logger.add_learning('Compound Intelligence integration works!', 'pattern')
        logger.log_session_completion(status='success')
        
        print("[OK] Test session logged successfully!")
    else:
        print("[WARNING] Firebase not configured - set GOOGLE_APPLICATION_CREDENTIALS")

# Made with Bob
