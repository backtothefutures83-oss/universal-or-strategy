# Ticket 03: Phase 2 Tests - Event Processing

**Epic**: Symmetry FSM Testing  
**Phase**: Phase 2 (P1 - Event Processing)  
**Priority**: P1 (High)  
**Complexity**: M (Medium - 4-6 hours)  
**Owner**: Bob CLI (`v12-engineer`)

---

## Objective

Implement tests for the 3-tier FSM resolution strategy (OrderId → SignalName → Scan) including backfill verification, idempotency, and out-of-order event handling.

---

## Scope

### In Scope
- T05: Tier 1 - OrderId Hit (O(1) lookup)
- T06: Tier 2 - SignalName Hit with Backfill
- T07: Tier 3 - Scan Hit with Backfill (O(N) fallback)
- T08: Duplicate Events (Idempotency)
- T09: Out-of-Order Events (Filled before Accepted)

### Out of Scope
- Contract tracking logic (Phase 3)
- Edge cases (Phase 4)
- Integration scenarios (Phase 5)

---

## Test Scenarios

### T05: Tier 1 - OrderId Hit
```csharp
[Fact]
public void T05_Tier1_OrderId_Hit_Primary_Path()
{
    // Arrange: OrderId already mapped
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    var fsm = CreateTestFsm("Sim101", "Fleet_Apex_1");
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    mockFsm.MapOrderId("ORD001", "Fleet_Apex_1", fsm.Generation);
    
    // Act: Resolve via OrderId
    var evt = CreateAcceptedEvent("ORD001", "Entry_Fleet_Apex_1");
    var resolved = mockFsm.ResolveFsm_ByOrderId("ORD001");
    
    // Assert: O(1) hit
    AssertFsmNotNull(resolved, "Tier 1 hit");
    Assert.Equal("Fleet_Apex_1", resolved.EntryName);
}
```

### T06: Tier 2 - SignalName Hit with Backfill
```csharp
[Fact]
public void T06_Tier2_SignalName_Hit_With_Backfill()
{
    // Arrange: OrderId NOT mapped, but SignalName parseable
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    var fsm = CreateTestFsm("Sim101", "Fleet_Apex_1");
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    
    // Act: Resolve via SignalName (Entry_Fleet_Apex_1 -> Fleet_Apex_1)
    var evt = CreateAcceptedEvent("ORD002", "Entry_Fleet_Apex_1");
    var resolved = mockFsm.ResolveFsmFromEvent(evt);
    
    // Assert: Tier 2 hit + backfill
    AssertFsmNotNull(resolved, "Tier 2 hit");
    Assert.Equal("Fleet_Apex_1", resolved.EntryName);
    
    // Verify backfill occurred
    var backfilled = mockFsm.ResolveFsm_ByOrderId("ORD002");
    AssertFsmNotNull(backfilled, "Backfill successful");
}
```

### T07: Tier 3 - Scan Hit with Backfill
```csharp
[Fact]
public void T07_Tier3_Scan_Hit_With_Backfill()
{
    // Arrange: OrderId NOT mapped, SignalName unparseable
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    var fsm = CreateTestFsm("Sim101", "Fleet_Apex_1");
    fsm.StopOrder = new MockOrder("ORD003", "Stop_Fleet_Apex_1", 
                                  OrderAction.Sell, 2);
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    
    // Act: Resolve via O(N) scan (no OrderId, no parseable SignalName)
    var evt = CreateAcceptedEvent("ORD003", null);
    evt.AccountAlias = "Sim101";
    var resolved = mockFsm.ResolveFsm_ByScan("Sim101", "ORD003");
    
    // Assert: Tier 3 hit + backfill
    AssertFsmNotNull(resolved, "Tier 3 scan hit");
    Assert.Equal("Fleet_Apex_1", resolved.EntryName);
    
    // Verify backfill occurred
    var backfilled = mockFsm.ResolveFsm_ByOrderId("ORD003");
    AssertFsmNotNull(backfilled, "Backfill successful");
}
```

### T08: Duplicate Events (Idempotency)
```csharp
[Fact]
public void T08_Duplicate_Events_Idempotent()
{
    // Arrange
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    var fsm = CreateTestFsm("Sim101", "Fleet_Apex_1");
    fsm.State = FollowerBracketState.Submitted;
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    mockFsm.MapOrderId("ORD001", "Fleet_Apex_1", fsm.Generation);
    
    // Act: Process same Accepted event twice
    var acceptEvent = CreateAcceptedEvent("ORD001", "Entry_Fleet_Apex_1");
    mockFsm.EnqueueEvent(acceptEvent);
    mockFsm.DrainMailbox();
    AssertFsmState(fsm, FollowerBracketState.Accepted, "First event");
    
    mockFsm.EnqueueEvent(acceptEvent);
    mockFsm.DrainMailbox();
    
    // Assert: State unchanged (idempotent)
    AssertFsmState(fsm, FollowerBracketState.Accepted, "Duplicate ignored");
}
```

### T09: Out-of-Order Events
```csharp
[Fact]
public void T09_OutOfOrder_Filled_Before_Accepted()
{
    // Arrange
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    var fsm = CreateTestFsm("Sim101", "Fleet_Apex_1");
    fsm.State = FollowerBracketState.Submitted;
    fsm.RemainingContracts = 2;
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    mockFsm.MapOrderId("ORD001", "Fleet_Apex_1", fsm.Generation);
    
    // Act: Filled arrives before Accepted (race condition)
    var fillEvent = CreateFilledEvent("ORD001", "Entry_Fleet_Apex_1", 
                                      2, 4500.0);
    mockFsm.EnqueueEvent(fillEvent);
    mockFsm.DrainMailbox();
    
    // Assert: FSM handles gracefully (transitions to Active)
    AssertFsmState(fsm, FollowerBracketState.Active, 
                   "Out-of-order fill handled");
}
```

---

## Verification Criteria

### Test Execution
- [ ] All 5 tests pass (T05-T09)
- [ ] Each test completes in <100ms
- [ ] Zero flaky failures
- [ ] No exceptions thrown

### Resolution Coverage
- [ ] Tier 1 (OrderId) tested
- [ ] Tier 2 (SignalName) tested
- [ ] Tier 3 (Scan) tested
- [ ] Backfill verified for Tier 2
- [ ] Backfill verified for Tier 3

### Event Handling
- [ ] Idempotency verified
- [ ] Out-of-order events handled
- [ ] Duplicate events ignored
- [ ] Race conditions tested

### V12 DNA Compliance
- [ ] Zero `lock()` statements
- [ ] ConcurrentDictionary for OrderId map
- [ ] ASCII-only strings
- [ ] MockTime used

---

## Dependencies

### Prerequisites
- Ticket 01 (Mock Infrastructure) COMPLETE
- Ticket 02 (Phase 1 Tests) COMPLETE

### Blocks
- Ticket 04 (Phase 3 Tests)
- Ticket 05 (Phase 4 Tests)
- Ticket 06 (Phase 5 Tests)

---

## Estimated Complexity

**Size**: M (Medium)  
**Time**: 4-6 hours  
**Risk**: Medium (3-tier resolution logic complexity)

---

**END OF TICKET 03**