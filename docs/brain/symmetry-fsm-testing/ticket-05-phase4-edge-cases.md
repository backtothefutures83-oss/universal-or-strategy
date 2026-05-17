# Ticket 05: Phase 4 Tests - Edge Cases

**Epic**: Symmetry FSM Testing  
**Phase**: Phase 4 (P2 - Edge Cases)  
**Priority**: P2 (Medium)  
**Complexity**: L (Large - 6-8 hours)  
**Owner**: Bob CLI (`v12-engineer`) or Codex CLI (`codex-rescue`)

---

## Objective

Implement tests for edge cases including null order references (restart scenario), mailbox overflow, concurrent modifications, and invalid state transitions.

---

## Scope

### In Scope
- T14: Null Order Reference (Restart/Hydration scenario)
- T15: Mailbox Overflow (>100 events)
- T16: Concurrent Modifications (Thread safety via CAS)
- T17: Invalid Transitions (State validation)

---

## Test Scenarios

### T14: Null Order Reference (Restart Scenario)
```csharp
[Fact]
public void T14_NullOrderReference_Restart_Scenario()
{
    // Arrange: Hydrated Active FSM with null EntryOrder (restart edge case)
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    var fsm = CreateTestFsm("Sim101", "Fleet_Apex_1");
    fsm.State = FollowerBracketState.Active;
    fsm.EntryOrder = null; // Restart scenario - order reference lost
    fsm.RemainingContracts = 2;
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    
    // Act: GetFsmExpectedPosition should handle null gracefully
    int expectedPos = mockFsm.GetFsmExpectedPosition("Sim101");
    
    // Assert: Returns 0 (fallback to broker position)
    Assert.Equal(0, expectedPos);
}
```

### T15: Mailbox Overflow
```csharp
[Fact]
public void T15_MailboxOverflow_Handles_Backpressure()
{
    // Arrange
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    var fsm = CreateTestFsm("Sim101", "Fleet_Apex_1");
    fsm.State = FollowerBracketState.Submitted;
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    mockFsm.MapOrderId("ORD001", "Fleet_Apex_1", fsm.Generation);
    
    // Act: Enqueue 150 events (exceeds MAX_PER_DRAIN = 100)
    for (int i = 0; i < 150; i++)
    {
        var evt = CreateAcceptedEvent("ORD001", "Entry_Fleet_Apex_1");
        mockFsm.EnqueueEvent(evt);
    }
    
    // First drain processes 100
    mockFsm.DrainMailbox();
    
    // Second drain processes remaining 50
    mockFsm.DrainMailbox();
    
    // Assert: All events processed, no exceptions
    AssertFsmState(fsm, FollowerBracketState.Accepted, 
                   "Overflow handled");
}
```

### T16: Concurrent Modifications (Thread Safety)
```csharp
[Fact]
public void T16_ConcurrentModifications_CAS_Retry()
{
    // Arrange
    var time = new MockTime(1000000L);
    var fsm = new MockFollowerBracketFSM
    {
        AccountName = "Sim101",
        EntryName = "Fleet_Apex_1",
        State = FollowerBracketState.None
    };
    
    // Act: Simulate concurrent state transitions
    bool success1 = fsm.TryTransition(FollowerBracketState.PendingSubmit, false);
    bool success2 = fsm.TryTransition(FollowerBracketState.Submitted, false);
    
    // Assert: Both transitions succeed (CAS-based)
    Assert.True(success1, "First transition");
    Assert.True(success2, "Second transition");
    AssertFsmState(fsm, FollowerBracketState.Submitted, "Final state");
}
```

### T17: Invalid Transitions
```csharp
[Fact]
public void T17_InvalidTransition_Rejected_To_Active()
{
    // Arrange
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    var fsm = CreateTestFsm("Sim101", "Fleet_Apex_1");
    fsm.State = FollowerBracketState.Rejected; // Terminal state
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    mockFsm.MapOrderId("ORD001", "Fleet_Apex_1", fsm.Generation);
    
    // Act: Attempt invalid transition (Rejected -> Active)
    var fillEvent = CreateFilledEvent("ORD001", "Entry_Fleet_Apex_1", 
                                      2, 4500.0);
    mockFsm.EnqueueEvent(fillEvent);
    mockFsm.DrainMailbox();
    
    // Assert: State unchanged (invalid transition rejected)
    AssertFsmState(fsm, FollowerBracketState.Rejected, 
                   "Invalid transition blocked");
}
```

---

## Verification Criteria

### Test Execution
- [ ] All 4 tests pass (T14-T17)
- [ ] Each test completes in <200ms (T15 may be slower)
- [ ] Zero flaky failures
- [ ] No exceptions thrown

### Edge Case Coverage
- [ ] Null order reference handled
- [ ] Mailbox overflow handled
- [ ] Concurrent modifications safe
- [ ] Invalid transitions blocked

### Thread Safety
- [ ] CAS-based transitions verified
- [ ] No race conditions detected
- [ ] Atomic state updates confirmed
- [ ] Generation counter prevents ABA

### V12 DNA Compliance
- [ ] Zero `lock()` statements
- [ ] All updates use Interlocked/Volatile
- [ ] ConcurrentQueue for mailbox
- [ ] ASCII-only strings

---

## Dependencies

### Prerequisites
- Ticket 01 (Mock Infrastructure) COMPLETE
- Ticket 02 (Phase 1 Tests) COMPLETE
- Ticket 03 (Phase 2 Tests) COMPLETE
- Ticket 04 (Phase 3 Tests) COMPLETE

### Blocks
- Ticket 06 (Phase 5 Tests)

---

## Estimated Complexity

**Size**: L (Large)  
**Time**: 6-8 hours  
**Risk**: Medium (thread safety testing complexity)

---

**END OF TICKET 05**