# V12 Photon Kernel - 5-Epic Roadmap

## Epic Status Matrix

| Epic | Status | PHS | Greptile | Commits | PR |
|------|--------|-----|----------|---------|-----|
| 1: SIMA Fleet Dispatch | ✅ COMPLETE | - | - | - | - |
| 2: Core State FSM | ✅ COMPLETE | - | - | - | - |
| 3: REAPER Expansion | 🔄 ACTIVE | 95.65% | 5/5 | 7 | #1 |
| 4: Sticky State & IPC | ⏳ PENDING | - | - | - | - |
| 5: V12 Global Adjudication | ⏳ PENDING | - | - | - | - |

## Cross-Epic Technical Debt Register

### Deferred to Epic 4
- [ ] IPC queue monitoring (_photonDispatchRing.Count observability)
- [ ] Entries quantity validation (secondary dispatch methods)

### Deferred to Epic 5
- [ ] Qlty code quality hardening (306 code smells)
- [ ] Method complexity reduction (UpdateComplianceDisplay: 25→15)
- [ ] Parameter object refactoring (ExecuteOrderSync: 7 params)
- [ ] Python script linting cleanup

## Jane Street Compliance Checklist

- [x] Lock-free actor pattern (Epic 1-2)
- [x] Atomic state transitions (Epic 2)
- [x] Bounded queues (Epic 3)
- [ ] Bounded latency verification (Epic 5)
- [ ] Wait-free progress guarantee (Epic 5)

## Session Transition Protocol

**At Epic Boundary**:
1. Create `EPIC-{N}-COMPLETE.md` in current session
2. Update `EPIC-{N+1}-BACKLOG.md` with discovered issues
3. Update this roadmap with metrics
4. Commit handoff documents to main
5. Start new session with handoff context loaded