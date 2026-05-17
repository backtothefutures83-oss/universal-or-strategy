# Living Document Registry
**Mission**: Universal OR Strategy | V12 Photon Kernel
**Purpose**: Centralized index for all active "living" documents, protocols, and mission artifacts.

---

## 🏛️ Sovereign Agent Protocols (Root)
These files define the identity, rules, and operational boundaries of our agent fleet.

*   [AGENTS.md](../../AGENTS.md) - The Master Sovereign Agent Protocol (Hierarchy & DNA).
*   [CLAUDE.md](../../CLAUDE.md) - Architect-specific guidelines and structural design rules.
*   [CODEX.md](../../CODEX.md) - Engineer-specific logic hardening and implementation rules.
*   [GEMINI.md](../../GEMINI.md) - Utility Specialist & Token Conservation protocol.
*   [JULES.md](../../JULES.md) - Auditor-specific adversarial review rules.

---

## 📜 Core Manifestos & Strategy
Long-term strategic documents governing the codebase and workflow.

*   [V12_Workflow_Manifesto.md](V12_Workflow_Manifesto.md) - The 7-Stage Recursive Protocol (Bob-First/Traycer-Next).
*   [master_roadmap.md](master_roadmap.md) - The high-level multi-phase refactoring roadmap.
*   [stack_registry.md](stack_registry.md) - Technical stack and library version tracking.
*   [INFRASTRUCTURE_PROTOCOL.md](../../INFRASTRUCTURE_PROTOCOL.md) - CI/CD and deployment safety rules.

---

## 🎯 Active Mission Intelligence
Dynamic documents used for the current implementation cycle.

*   [task.md](task.md) - The current active mission status and ticket tracking.
*   [T2: ExecuteOnExecutionUpdate_CIT_Repair](phase7_sprint5_t2.md) - COMPLETE
*   [T3: ExecuteSmartDispatchEntry Extraction](phase7_sprint5_t03_ExecuteSmartDispatchEntry.md) - COMPLETE
*   [T4: SubmitBracketOrders Extraction](phase7_sprint5_t04_SubmitBracketOrders.md) - COMPLETE
*   [T5: MoveSpecificTarget CYC Reduction](phase7_sprint5_t05_MoveSpecificTarget.md) - PARKED (superseded by Epic Phase 7 ticket queue)
*   [T13: SweepBrokerOrders Extraction](phase7_sprint5_t13_SweepBrokerOrders.md) - COMPLETE (CYC 28→15)
*   [T13: SweepBrokerOrders Acceptance](phase7_sprint5_t13_ACCEPTANCE_REPORT.md) - ACCEPTED
*   [T14: BuildUiLivePositionSnapshot Extraction](phase7_sprint5_t14_BuildUiLivePositionSnapshot.md) - COMPLETE (CYC 20→2)
*   [T14: BuildUiLivePositionSnapshot Acceptance](phase7_sprint5_t14_ACCEPTANCE_REPORT.md) - ACCEPTED
*   [T15: ExecuteWatchdogDirectFallback Extraction](phase7_sprint5_t15_ExecuteWatchdogDirectFallback.md) - COMPLETE (CYC 20→3)
*   [T15: ExecuteWatchdogDirectFallback Acceptance](phase7_sprint5_t15_ACCEPTANCE_REPORT.md) - ACCEPTED
*   [T16: CreateNewStopOrder Extraction](phase7_sprint5_t16_CreateNewStopOrder.md) - COMPLETE (CYC 21->6)
*   [implementation_plan.md](implementation_plan.md) - Surgical implementation steps for the active engineer.
*   [forensics_report.md](forensics_report.md) - Root cause analysis and technical evidence.
*   [mini-spec.md](mini-spec.md) - Technical requirements and metabolic design for the active mission.
*   [walkthrough.md](walkthrough.md) - Step-by-step verification and logic walkthrough for reviewers.

### MP-0: Dictionary Dispatch Conversion (COMPLETE 2026-05-15)
*   [forensics_mp0_dispatch.md](forensics_mp0_dispatch.md) - Source-verified audit: 14 candidates reviewed, 2 confirmed, 12 disqualified with reasoning.
*   [mp0_implementation_plan.md](mp0_implementation_plan.md) - Dict dispatch pattern spec (Action delegates, Init_Services init, zero-alloc constraints).
*   [mp0_completion_report.md](mp0_completion_report.md) - Mission acceptance: CYC 30->6, F5 PASS, BUILD_TAG 1111.007-mphase-mp0.

### MP-1: SIMA Lifecycle Cluster (COMPLETE 2026-05-15)
*   [mp1_sima_lifecycle_bob_prompt.md](../../../brain/87ca7479-83b5-4a9b-bcb3-ae6327b87852/artifacts/mp1_sima_lifecycle_bob_prompt.md) - Source-verified mission brief: 3 tickets confirmed, 7 disqualified.
*   Tickets: MP1-A HydrateFSM_LinkBracketOrders (loop consolidation), MP1-B RecoverFSM_LinkRecoveredBrackets (loop consolidation), MP1-C HydrateExpectedPositionsFromBroker (helper extraction).
*   F5 PASS 2026-05-15 11:58 Eastern | Logic Audit 1-9 PASS | Deploy-sync 29,938 chars.

### Phase 7 Testing Epic - Batch B (S5, S6, S7) - COMPLETE 2026-05-17

**Integration Test Suite (273 tests across 7 clusters)**

#### Cluster S1: SIMA Integration Tests (30 tests)
*   [SIMAIntegrationTests.cs](../../tests/SIMAIntegrationTests.cs) - SIMA orchestration, lifecycle, dispatch, fleet, and execution tests
*   [implementation_plan_cluster_s1.md](implementation_plan_cluster_s1.md) - S1 implementation plan
*   [verification_report_cluster_s1.md](verification_report_cluster_s1.md) - S1 verification report

#### Cluster S2: Execution Engine Integration Tests (30 tests)
*   [ExecutionEngineIntegrationTests.cs](../../tests/ExecutionEngineIntegrationTests.cs) - Order callbacks, symmetry FSM, trailing stops, and order management tests
*   [forensics_report_cluster_s2.md](forensics_report_cluster_s2.md) - S2 forensic analysis
*   [implementation_plan_cluster_s2.md](implementation_plan_cluster_s2.md) - S2 implementation plan
*   [verification_report_cluster_s2.md](verification_report_cluster_s2.md) - S2 verification report

#### Cluster S3: UI Photon IO Integration Tests (30 tests)
*   [UIPhotonIOIntegrationTests.cs](../../tests/UIPhotonIOIntegrationTests.cs) - IPC server, UI callbacks, panel construction, and state sync tests
*   [adjudicator_audit_cluster_s3.md](adjudicator_audit_cluster_s3.md) - S3 adjudicator audit
*   [implementation_plan_cluster_s3.md](implementation_plan_cluster_s3.md) - S3 implementation plan
*   [verification_report_cluster_s3.md](verification_report_cluster_s3.md) - S3 verification report

#### Cluster S4: REAPER Defense Integration Tests (30 tests)
*   [REAPERDefenseIntegrationTests.cs](../../tests/REAPERDefenseIntegrationTests.cs) - REAPER audit, repair, watchdog, and safety tests
*   [forensics_report_cluster_s4.md](forensics_report_cluster_s4.md) - S4 forensic analysis
*   [adjudicator_audit_cluster_s4.md](adjudicator_audit_cluster_s4.md) - S4 adjudicator audit
*   [implementation_plan_cluster_s4.md](implementation_plan_cluster_s4.md) - S4 implementation plan
*   [verification_report_cluster_s4.md](verification_report_cluster_s4.md) - S4 verification report

#### Cluster S5: Configuration Integration Tests (30 tests)
*   [ConfigurationIntegrationTests.cs](../../tests/ConfigurationIntegrationTests.cs) - Kernel state, lifecycle, telemetry, and configuration tests
*   [implementation_plan_cluster_s5.md](implementation_plan_cluster_s5.md) - S5 implementation plan
*   [verification_report_cluster_s5.md](verification_report_cluster_s5.md) - S5 verification report

#### Cluster S6: Metrics Integration Tests (22 tests)
*   [MetricsIntegrationTests.cs](../../tests/MetricsIntegrationTests.cs) - Entry signals, indicators, trend logic, and FSM tests
*   [implementation_plan_cluster_s6.md](implementation_plan_cluster_s6.md) - S6 implementation plan
*   [verification_report_cluster_s6.md](verification_report_cluster_s6.md) - S6 verification report

#### Cluster S7: Orchestration Integration Tests (28 tests)
*   [OrchestrationIntegrationTests.cs](../../tests/OrchestrationIntegrationTests.cs) - Infrastructure, drawing helpers, account updates, and ATM tests
*   [implementation_plan_cluster_s7.md](implementation_plan_cluster_s7.md) - S7 implementation plan
*   [verification_report_cluster_s7.md](verification_report_cluster_s7.md) - S7 verification report

**Test Coverage Summary:**
- Total Tests: 273 (200 core + 73 edge cases)
- Test Distribution: S1-S5 (30 each), S6 (22), S7 (28)
- Mock Infrastructure: Comprehensive NT8 API mocks with V12 DNA compliance
- Verification: All clusters PASS with zero lock violations, ASCII compliance, and atomic state patterns

---

## 🛡️ Specialized Protocols & Audits
Security, forensic, and adversarial review documentation.

*   [adversarial_audit_protocol.md](adversarial_audit_protocol.md) - Rules for P3/P6 adversarial consensus.
*   [audit_v28_1_platinum.md](audit_v28_1_platinum.md) - Forensic logic audit results for the V28.1 Platinum kernel.
*   [arena_forensics_synthesis.md](arena_forensics_synthesis.md) - Cross-agent synthesis of forensic findings.
*   [pr76_final_audit_report.md](pr76_final_audit_report.md) - Validation report for the stable V12 baseline.

---

## 🤖 Qwen & Agent Integration Guides
*   [qwen_agent_arena_doc.md](qwen_agent_arena_doc.md) - Agent Arena: side-by-side model execution.
*   [qwen_approval_mode_doc.md](qwen_approval_mode_doc.md) - Permission levels (Plan, Default, Auto-Edit, YOLO).
*   [qwen_channels_doc.md](qwen_channels_doc.md) - Chat platform adapters and custom channel plugins.
*   [qwen_checkpointing_doc.md](qwen_checkpointing_doc.md) - Checkpointing user guide: automatic shadow Git snapshots and /restore recovery.
*   [qwen_code_review_doc.md](qwen_code_review_doc.md) - Deterministic and parallel 9-agent code review pipelines.
*   [qwen_dual_output_doc.md](qwen_dual_output_doc.md) - Event streams & bi-directional IDE/CI pipeline synchronization.
*   [qwen_headless_mode_doc.md](qwen_headless_mode_doc.md) - Non-interactive scripting, automation, and session resumption.
*   [qwen_model_providers_doc.md](qwen_model_providers_doc.md) - Configuration settings, API provider keys, and thinking tokens.
*   [qwen_subagents_doc.md](qwen_subagents_doc.md) - Focused task delegation and context division.
*   [workflow_pilot.md](workflow_pilot.md) - Sovereign agent workflow pilot checklist ($workflow-pilot command guide).

---

## 🏗️ Architecture & Knowledge
Design decisions and inspiration for the project's evolution.

*   [ADR-019.md](ADR-019.md) - Architectural Decision Record for the Photon Kernel.
*   [inspiration_gallery.md](inspiration_gallery.md) - UI/UX and architectural patterns for future iterations.
*   [IDE_GUIDE.md](../../IDE_GUIDE.md) - Developer environment and tooling setup instructions.

---
**Registry Status**: MAINTAINED
**Last Update**: 2026-05-17 (Phase 7 Testing Epic Batch B complete; 273 integration tests across 7 clusters; BUILD_TAG 1111.007-phase7-tQ1_S7_ORCHESTRATION_TESTS_COMPLETE)
