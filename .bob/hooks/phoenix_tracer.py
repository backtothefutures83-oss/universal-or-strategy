#!/usr/bin/env python3
"""Phoenix OpenTelemetry tracer for Bob CLI.

This module provides automatic tracing of Bob CLI operations to Phoenix.
It wraps Bob's tool calls and session lifecycle with OpenTelemetry spans.
"""

import os
import sys
import time
from datetime import datetime, timezone
from typing import Optional, Dict, Any
from pathlib import Path

# OpenTelemetry imports
try:
    from opentelemetry import trace
    from opentelemetry.sdk.trace import TracerProvider
    from opentelemetry.sdk.trace.export import BatchSpanProcessor
    from opentelemetry.exporter.otlp.proto.http.trace_exporter import OTLPSpanExporter
    from opentelemetry.sdk.resources import Resource
    from opentelemetry.trace import Status, StatusCode
    OTEL_AVAILABLE = True
except ImportError:
    OTEL_AVAILABLE = False
    print("[Phoenix Tracer] Warning: OpenTelemetry not installed. Run: pip install opentelemetry-api opentelemetry-sdk opentelemetry-exporter-otlp-proto-http")


class PhoenixTracer:
    """Manages Phoenix tracing for Bob CLI sessions."""
    
    def __init__(self):
        self.tracer = None
        self.session_span = None
        self.session_id = None
        self.enabled = False
        
        if not OTEL_AVAILABLE:
            return
            
        # Get Phoenix endpoint from environment or use default
        phoenix_endpoint = os.getenv(
            'PHOENIX_COLLECTOR_ENDPOINT',
            'http://localhost:6006/v1/traces'
        )
        
        # Also check OTEL standard env var
        if not phoenix_endpoint or phoenix_endpoint == 'http://localhost:6006/v1/traces':
            phoenix_endpoint = os.getenv(
                'OTEL_EXPORTER_OTLP_ENDPOINT',
                'http://localhost:6006/v1/traces'
            )
        
        try:
            # Create resource with service info
            resource = Resource.create({
                "service.name": "bob-cli",
                "service.version": "v12",
                "deployment.environment": "development"
            })
            
            # Set up tracer provider
            provider = TracerProvider(resource=resource)
            
            # Configure OTLP exporter for Phoenix
            otlp_exporter = OTLPSpanExporter(
                endpoint=phoenix_endpoint,
                timeout=5
            )
            
            # Add batch span processor
            span_processor = BatchSpanProcessor(otlp_exporter)
            provider.add_span_processor(span_processor)
            
            # Set as global tracer provider
            trace.set_tracer_provider(provider)
            
            # Get tracer instance
            self.tracer = trace.get_tracer("bob-cli", "v12")
            self.enabled = True
            
            print(f"[Phoenix Tracer] Initialized - sending traces to {phoenix_endpoint}")
            
        except Exception as e:
            print(f"[Phoenix Tracer] Warning: Failed to initialize: {e}")
            self.enabled = False
    
    def start_session(self, mode: str, task_description: str = "") -> Optional[str]:
        """Start a new Bob session span."""
        if not self.enabled or not self.tracer:
            return None
        
        try:
            # Generate session ID
            self.session_id = f"bob-{mode}-{int(time.time())}"
            
            # Start session span
            self.session_span = self.tracer.start_span(
                "bob_session",
                attributes={
                    "session.id": self.session_id,
                    "session.mode": mode,
                    "session.task": task_description,
                    "session.start_time": datetime.now(timezone.utc).isoformat(),
                    "agent.name": "Bob",
                    "agent.version": "v12"
                }
            )
            
            print(f"[Phoenix Tracer] Session started: {self.session_id}")
            return self.session_id
            
        except Exception as e:
            print(f"[Phoenix Tracer] Warning: Failed to start session: {e}")
            return None
    
    def end_session(self, status: str = "success", error: Optional[str] = None):
        """End the current Bob session span."""
        if not self.enabled or not self.session_span:
            return
        
        try:
            # Set span status
            if status == "success":
                self.session_span.set_status(Status(StatusCode.OK))
            else:
                self.session_span.set_status(
                    Status(StatusCode.ERROR, error or "Session failed")
                )
            
            # Add end time
            self.session_span.set_attribute(
                "session.end_time",
                datetime.now(timezone.utc).isoformat()
            )
            
            # End span
            self.session_span.end()
            
            print(f"[Phoenix Tracer] Session ended: {self.session_id} ({status})")
            
        except Exception as e:
            print(f"[Phoenix Tracer] Warning: Failed to end session: {e}")
    
    def trace_tool_call(self, tool_name: str, parameters: Dict[str, Any]) -> Any:
        """Create a span for a tool call. Returns a context manager."""
        if not self.enabled or not self.tracer:
            return _NoOpSpan()
        
        try:
            span = self.tracer.start_span(
                f"tool.{tool_name}",
                attributes={
                    "tool.name": tool_name,
                    "tool.parameters": str(parameters),
                    "session.id": self.session_id or "unknown"
                }
            )
            return span
            
        except Exception as e:
            print(f"[Phoenix Tracer] Warning: Failed to create tool span: {e}")
            return _NoOpSpan()
    
    def trace_file_operation(self, operation: str, file_path: str, details: Optional[Dict] = None) -> Any:
        """Create a span for a file operation."""
        if not self.enabled or not self.tracer:
            return _NoOpSpan()
        
        try:
            attributes = {
                "file.operation": operation,
                "file.path": file_path,
                "session.id": self.session_id or "unknown"
            }
            
            if details:
                for key, value in details.items():
                    attributes[f"file.{key}"] = str(value)
            
            span = self.tracer.start_span(
                f"file.{operation}",
                attributes=attributes
            )
            return span
            
        except Exception as e:
            print(f"[Phoenix Tracer] Warning: Failed to create file span: {e}")
            return _NoOpSpan()
    
    def add_event(self, name: str, attributes: Optional[Dict] = None):
        """Add an event to the current session span."""
        if not self.enabled or not self.session_span:
            return
        
        try:
            self.session_span.add_event(name, attributes or {})
        except Exception as e:
            print(f"[Phoenix Tracer] Warning: Failed to add event: {e}")


class _NoOpSpan:
    """No-op span for when tracing is disabled."""
    def __enter__(self):
        return self
    
    def __exit__(self, *args):
        pass
    
    def set_status(self, *args, **kwargs):
        pass
    
    def set_attribute(self, *args, **kwargs):
        pass
    
    def add_event(self, *args, **kwargs):
        pass
    
    def end(self):
        pass


# Global tracer instance
_tracer_instance: Optional[PhoenixTracer] = None


def get_tracer() -> PhoenixTracer:
    """Get or create the global Phoenix tracer instance."""
    global _tracer_instance
    if _tracer_instance is None:
        _tracer_instance = PhoenixTracer()
    return _tracer_instance


def initialize_tracing(mode: str, task: str = "") -> Optional[str]:
    """Initialize Phoenix tracing for a Bob session."""
    tracer = get_tracer()
    return tracer.start_session(mode, task)


def finalize_tracing(status: str = "success", error: Optional[str] = None):
    """Finalize Phoenix tracing for a Bob session."""
    tracer = get_tracer()
    tracer.end_session(status, error)


if __name__ == "__main__":
    # Test the tracer
    print("Testing Phoenix Tracer...")
    
    tracer = PhoenixTracer()
    
    if tracer.enabled:
        # Start session
        session_id = tracer.start_session("test", "Testing Phoenix integration")
        
        # Simulate tool call
        with tracer.trace_tool_call("read_file", {"path": "test.py"}) as span:
            time.sleep(0.1)
            span.set_attribute("result", "success")
        
        # Simulate file operation
        with tracer.trace_file_operation("write", "output.txt", {"lines": 10}) as span:
            time.sleep(0.05)
            span.set_attribute("bytes_written", 1024)
        
        # Add event
        tracer.add_event("checkpoint", {"step": "validation"})
        
        # End session
        time.sleep(0.2)
        tracer.end_session("success")
        
        print("Test complete! Check Phoenix UI at http://localhost:6006")
    else:
        print("Tracer not enabled - check dependencies and Phoenix server")

# Made with Bob
