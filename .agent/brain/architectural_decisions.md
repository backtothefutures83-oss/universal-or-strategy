# Architectural Decision Record (ADR): Thread-Safety & Termination Protocols
## Date: 2026-03-15
## Decision ID: ADR-001-TERMINATION-SAFETY

### Status: Supersedes \"Strict Actor Model\" (Build 966)

### Context:
Prior to Build 981, tracking latency during shutdown created \"Ghost Orders.\"

### Decision:
Use **Direct Writes** for stopOrders during bracket submission. **No Internal Locks** (lock(stateLock) remains BANNED).
