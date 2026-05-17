# Ticket 02: Phase 1 Tests - Core State Machine

**Epic**: Symmetry FSM Testing  
**Phase**: Phase 1 (P0 - Core State Machine)  
**Priority**: P0 (Critical Path)  
**Complexity**: M (Medium - 4-6 hours)  
**Owner**: Bob CLI (`v12-engineer`)

---

## Objective

Implement comprehensive tests for the core FSM state transitions covering the primary happy path, rejection handling, cancellation, and partial fill scenarios. These tests validate the fundamental state machine behavior that all other functionality depends on.

---

## Scope

### In Scope
- T01: Happy Path (None → PendingSubmit → Submitted → Accepted → Active → Filled)
- T02: Rejection Path (Submitted → Rejected)
- T03: Cancel Path (Active → Cancelled)
- T04: Partial Fill (Active → PartFilled → Active → Filled)

### Out of Scope
- Event resolution logic (Phase 2)
- Contract tracking details (Phase 3)
- Edge cases (Phase 4)
- Integration scenarios (Phase 5)

---

## Implementation Steps

### Test T01: Happy Path - None to Filled

**Scenario**: Complete lifecycle from strategic intent to filled state

```csharp
[Fact]
public void T01_HappyPath_None_To_Filled()
{
    // Arrange
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    
    var fsm = new MockFollowerBracketFSM
    {
        AccountName = "Sim101",
        EntryName = "Fleet_Apex_1",
        State = FollowerBracketState.None,
        RemainingContracts = 2,
        EntryOrder = new MockOrder("ORD001", "Entry_Fleet_Apex_1", 
                                   OrderAction.Buy, 2)
    };
    
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    mockFsm.MapOrderId("ORD001", "Fleet_Apex_1", fsm.Generation);
    
    // Act: Transition through states
    // Step 1: None -> PendingSubmit
    fsm.State = FollowerBracketState.PendingSubmit;
    AssertFsmState(fsm, FollowerBracketState.PendingSubmit, 
                   "Strategic intent set");
    
    // Step 2: PendingSubmit -> Submitted
    fsm.State = FollowerBracketState.Submitted;
    AssertFsmState(fsm, FollowerBracketState.Submitted, 
                   "Order submitted to broker");
    
    // Step 3: Submitted -> Accepted (broker ack)
    var acceptEvent = CreateAcceptedEvent("ORD001", "Entry_Fleet_Apex_1");
    mockFsm.EnqueueEvent(acceptEvent);
    mockFsm.DrainMailbox();
    AssertFsmState(fsm, FollowerBracketState.Accepted, 
                   "Broker accepted order");
    
    // Step 4: Accepted -> Active (entry filled)
    var fillEvent = CreateFilledEvent("ORD001", "Entry_Fleet_Apex_1", 
                                      2, 4500.0);
    mockFsm.EnqueueEvent(fillEvent);
    mockFsm.DrainMailbox();
    AssertFsmState(fsm, FollowerBracketState.Active, 
                   "Entry filled, bracket active");
    AssertRemainingContracts(fsm, 2);
    
    // Step 5: Active -> Filled (stop filled)
    fsm.StopOrder = new MockOrder("ORD002", "Stop_Fleet_Apex_1", 
                                  OrderAction.Sell, 2);
    mockFsm.MapOrderId("ORD002", "Fleet_Apex_1", fsm.Generation);
    
    var stopFillEvent = CreateFilledEvent("ORD002", "Stop_Fleet_Apex_1", 
                                          2, 4480.0);
    mockFsm.EnqueueEvent(stopFillEvent);
    mockFsm.DrainMailbox();
    AssertFsmState(fsm, FollowerBracketState.Filled, 
                   "Stop filled, position closed");
    AssertRemainingContracts(fsm, 0);
}
```

### Test T02: Rejection Path

**Scenario**: Broker rejects order during submission

```csharp
[Fact]
public void T02_RejectionPath_Submitted_To_Rejected()
{
    // Arrange
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    
    var fsm = new MockFollowerBracketFSM
    {
        AccountName = "Sim101",
        EntryName = "Fleet_Apex_1",
        State = FollowerBracketState.Submitted,
        EntryOrder = new MockOrder("ORD001", "Entry_Fleet_Apex_1", 
                                   OrderAction.Buy, 2)
    };
    
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    mockFsm.MapOrderId("ORD001", "Fleet_Apex_1", fsm.Generation);
    
    // Act: Broker rejects order
    var rejectEvent = CreateRejectedEvent("ORD001", "Entry_Fleet_Apex_1",
                                          "Insufficient margin");
    mockFsm.EnqueueEvent(rejectEvent);
    mockFsm.DrainMailbox();
    
    // Assert
    AssertFsmState(fsm, FollowerBracketState.Rejected, 
                   "Order rejected by broker");
    Assert.Equal("Insufficient margin", fsm.LastBrokerError);
}
```

### Test T03: Cancel Path

**Scenario**: User cancels active bracket

```csharp
[Fact]
public void T03_CancelPath_Active_To_Cancelled()
{
    // Arrange
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    
    var fsm = new MockFollowerBracketFSM
    {
        AccountName = "Sim101",
        EntryName = "Fleet_Apex_1",
        State = FollowerBracketState.Active,
        RemainingContracts = 2,
        StopOrder = new MockOrder("ORD002", "Stop_Fleet_Apex_1", 
                                  OrderAction.Sell, 2)
    };
    
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    mockFsm.MapOrderId("ORD002", "Fleet_Apex_1", fsm.Generation);
    
    // Act: Cancel stop order
    var cancelEvent = CreateCancelledEvent("ORD002", "Stop_Fleet_Apex_1");
    mockFsm.EnqueueEvent(cancelEvent);
    mockFsm.DrainMailbox();
    
    // Assert
    AssertFsmState(fsm, FollowerBracketState.Cancelled, 
                   "Bracket cancelled");
}
```

### Test T04: Partial Fill Path

**Scenario**: Multi-step partial fills leading to complete fill

```csharp
[Fact]
public void T04_PartialFill_Active_To_PartFilled_To_Filled()
{
    // Arrange
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    
    var fsm = new MockFollowerBracketFSM
    {
        AccountName = "Sim101",
        EntryName = "Fleet_Apex_1",
        State = FollowerBracketState.Active,
        RemainingContracts = 5,
        StopOrder = new MockOrder("ORD002", "Stop_Fleet_Apex_1", 
                                  OrderAction.Sell, 5)
    };
    
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    mockFsm.MapOrderId("ORD002", "Fleet_Apex_1", fsm.Generation);
    
    // Act: First partial fill (2 contracts)
    var partFill1 = CreatePartFilledEvent("ORD002", "Stop_Fleet_Apex_1", 
                                          2, 4480.0);
    mockFsm.EnqueueEvent(partFill1);
    mockFsm.DrainMailbox();
    
    // Assert: Still active with reduced contracts
    AssertFsmState(fsm, FollowerBracketState.Active, 
                   "First partial fill");
    AssertRemainingContracts(fsm, 3);
    
    // Act: Second partial fill (2 more contracts)
    var partFill2 = CreatePartFilledEvent("ORD002", "Stop_Fleet_Apex_1", 
                                          2, 4481.0);
    mockFsm.EnqueueEvent(partFill2);
    mockFsm.DrainMailbox();
    
    // Assert: Still active with 1 contract remaining
    AssertFsmState(fsm, FollowerBracketState.Active, 
                   "Second partial fill");
    AssertRemainingContracts(fsm, 1);
    
    // Act: Final fill (1 contract)
    var finalFill = CreateFilledEvent("ORD002", "Stop_Fleet_Apex_1", 
                                      1, 4482.0);
    mockFsm.EnqueueEvent(finalFill);
    mockFsm.DrainMailbox();
    
    // Assert: Fully filled
    AssertFsmState(fsm, FollowerBracketState.Filled, 
                   "All contracts filled");
    AssertRemainingContracts(fsm, 0);
}
```

---

## Verification Criteria

### Test Execution
- [ ] All 4 tests pass (T01-T04)
- [ ] Each test completes in <100ms
- [ ] Zero flaky failures (run 10 times)
- [ ] No exceptions thrown

### State Coverage
- [ ] None state tested
- [ ] PendingSubmit state tested
- [ ] Submitted state tested
- [ ] Accepted state tested
- [ ] Active state tested
- [ ] Filled state tested
- [ ] Rejected state tested
- [ ] Cancelled state tested

### Transition Coverage
- [ ] None → PendingSubmit
- [ ] PendingSubmit → Submitted
- [ ] Submitted → Accepted
- [ ] Submitted → Rejected
- [ ] Accepted → Active
- [ ] Active → Filled
- [ ] Active → Cancelled
- [ ] Active → PartFilled → Active

### Contract Tracking
- [ ] RemainingContracts decrements on fill
- [ ] RemainingContracts reaches zero on complete fill
- [ ] Partial fills maintain Active state
- [ ] Final fill transitions to Filled state

### V12 DNA Compliance
- [ ] Zero `lock()` statements
- [ ] All state updates atomic
- [ ] ASCII-only strings
- [ ] MockTime used (no Thread.Sleep)

---

## Dependencies

### Prerequisites
- Ticket 01 (Mock Infrastructure) **MUST BE COMPLETE**
- All mock classes implemented
- Event builders functional
- Assertion helpers functional

### Blocks
- Ticket 03 (Phase 2 Tests)
- Ticket 04 (Phase 3 Tests)
- Ticket 05 (Phase 4 Tests)
- Ticket 06 (Phase 5 Tests)

---

## Estimated Complexity

**Size**: M (Medium)  
**Time**: 4-6 hours  
**Risk**: Low (straightforward state machine testing)

**Breakdown**:
- T01 implementation: 90 minutes
- T02 implementation: 45 minutes
- T03 implementation: 45 minutes
- T04 implementation: 90 minutes
- Testing and debugging: 60 minutes
- Documentation: 30 minutes

---

## Notes

- Focus on clear, readable test code
- Each test should be independently runnable
- Use descriptive assertion messages
- Follow Red-Green-Refactor workflow
- Commit after each passing test

---

**END OF TICKET 02**