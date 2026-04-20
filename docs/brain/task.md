# Task: Post-Hardening Oversight & Next Phase Activation

- [x] Load project baseline and historical context
  - [x] Read `DIRECTOR_SESSION_LOG.md`
  - [x] Read previous session's `task.md` and `walkthrough.md`
  - [x] Verify `src/` directory contents
- [x] Summarize immediate next steps for regression testing
- [x] Explain "Regression Testing" and "Weekend Tasks" to user
- [x] **PHASE 5: Modularization (Refactor First)**
  - [x] Design "Modular Split" map (e.g., UI → IPC, Compliance, Sizing)
  - [x] Split `UniversalORStrategyV12_002_Dev.UI.cs` (2200+ lines)
  - [x] Split `UniversalORStrategyV12_002_Dev.Orders.cs` (2000+ lines)
- [x] **PHASE 6: Forensic Audit (Small-Node Scan)**
  - [x] Identify unmarshalled callback race (SIMA)
- [x] **PHASE 7: Concurrency Hardening (The "Final Green Light")**
  - [x] Implement `InvokeAsync` / `TriggerCustomEvent` marshalling
  - [x] Verify thread-safety in simulation
- [x] **PHASE 8: Unmanaged Exit & Sizing Logic Fix**
  - [x] Replace `ExitLong/Short` in `SIMA.cs` with `SubmitOrderUnmanaged`
  - [x] Allow 1-contract trades to have T1 Target if configured
  - [x] Re-verify stability after logic shift
- [x] **STABILITY CERTIFIED: Safe to Trade Build 1101E (Modular)**

## Sovereign Infrastructure Hardening (V12.15 Platinum)

- [x] Consolidate MCP fleet in `.gemini/settings.json` (V12.15 unified access)
- [x] Migrate Context7 to `scripts/context7_cli.py` (Stateless RPC documentation service)
- [x] Harden all 17 Gemini CLI subagent definitions (Wildcard tool access + YAML repair)
- [x] Deploy `@v12-graphifier` subagent for autonomous structural indexing

### Phase 7: Forensic Consensus (P5 Red Team Battle)

- [x] P3 Architect drafts ADR-019 structural plan (32-site kernel superset + 3 infra + portability bundle) -- 2026-04-18
- [ ] Execute P5 Arena Red Team audit of `docs/brain/implementation_plan.md` (P4 handoff SUSPENDED until gate clears)
- [ ] Achieve 100% consensus across Codex, Gemini, and Jules (task-splitting forbidden; simulation banned)
- [ ] Run `scripts/amal_harness.py` for Zero-Allocation verification
- [ ] Deploy via Codex (P4) after Arena APPROVED and perform Claude UltraReview (P5)
