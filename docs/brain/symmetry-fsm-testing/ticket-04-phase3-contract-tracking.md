# Ticket 04: Phase 3 Tests - Contract Tracking

**Epic**: Symmetry FSM Testing  
**Phase**: Phase 3 (P1 - Contract Tracking)  
**Priority**: P1 (High)  
**Complexity**: M (Medium - 4-6 hours)  
**Owner**: Bob CLI (`v12-engineer`)

---

## Objective

Implement tests for contract tracking logic including stop fills, target detection (T1-T5), multi-target scaling, and zero-contract terminal state transitions.

---

## Scope

### In Scope
- T10: Stop Fill (RemainingContracts decrement)
- T11: T1 Detection (Target 1 fill)
- T12: Multi-Target Scaling (T1+T2+T3 partial fills)
- T13: Zero Contracts (Terminal state transition)

---

## Test Scenarios

### T10: Stop Fill Contract Decrement
```csharp
[Fact]
public void T10_StopFill_Decrements_RemainingContracts()
{
    // Arrange
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    var fsm = CreateTestFsm("Sim101", "Fleet_Apex_1");
    fsm.State = FollowerBracketState.Active;
    fsm.RemainingContracts = 2;
    fsm.StopOrder = new MockOrder("ORD002", "Stop_Fleet_Apex_1", 
                                  OrderAction.Sell, 2);
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    mockFsm.MapOrderId("ORD002", "Fleet_Apex_1", fsm.Generation);
    
    // Act: Stop fills completely
    var stopFill = CreateFilledEvent("ORD002", "Stop_Fleet_Apex_1", 
                                     2, 4480.0);
    mockFsm.EnqueueEvent(stopFill);
    mockFsm.DrainMailbox();
    
    // Assert
    AssertFsmState(fsm, FollowerBracketState.Filled, "Stop filled");
    AssertRemainingContracts(fsm, 0);
}
```

### T11: T1 Target Detection
```csharp
[Fact]
public void T11_T1_Target_Detection_And_Decrement()
{
    // Arrange
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    var fsm = CreateTestFsm("Sim101", "Fleet_Apex_1");
    fsm.State = FollowerBracketState.Active;
    fsm.RemainingContracts = 5;
    fsm.Targets[0] = new MockOrder("ORD003", "T1_Fleet_Apex_1", 
                                   OrderAction.Sell, 1);
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    mockFsm.MapOrderId("ORD003", "Fleet_Apex_1", fsm.Generation);
    
    // Act: T1 fills (1 contract)
    var t1Fill = CreateFilledEvent("ORD003", "T1_Fleet_Apex_1", 
                                   1, 4520.0);
    mockFsm.EnqueueEvent(t1Fill);
    mockFsm.DrainMailbox();
    
    // Assert: Still active with 4 contracts
    AssertFsmState(fsm, FollowerBracketState.Active, "T1 filled");
    AssertRemainingContracts(fsm, 4);
}
```

### T12: Multi-Target Scaling
```csharp
[Fact]
public void T12_MultiTarget_Scaling_T1_T2_T3()
{
    // Arrange
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    var fsm = CreateTestFsm("Sim101", "Fleet_Apex_1");
    fsm.State = FollowerBracketState.Active;
    fsm.RemainingContracts = 5;
    
    // Setup T1, T2, T3 targets
    fsm.Targets[0] = new MockOrder("ORD003", "T1_Fleet_Apex_1", 
                                   OrderAction.Sell, 1);
    fsm.Targets[1] = new MockOrder("ORD004", "T2_Fleet_Apex_1", 
                                   OrderAction.Sell, 1);
    fsm.Targets[2] = new MockOrder("ORD005", "T3_Fleet_Apex_1", 
                                   OrderAction.Sell, 1);
    
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    mockFsm.MapOrderId("ORD003", "Fleet_Apex_1", fsm.Generation);
    mockFsm.MapOrderId("ORD004", "Fleet_Apex_1", fsm.Generation);
    mockFsm.MapOrderId("ORD005", "Fleet_Apex_1", fsm.Generation);
    
    // Act: T1 fills
    var t1Fill = CreateFilledEvent("ORD003", "T1_Fleet_Apex_1", 
                                   1, 4520.0);
    mockFsm.EnqueueEvent(t1Fill);
    mockFsm.DrainMailbox();
    AssertRemainingContracts(fsm, 4);
    
    // Act: T2 fills
    var t2Fill = CreateFilledEvent("ORD004", "T2_Fleet_Apex_1", 
                                   1, 4530.0);
    mockFsm.EnqueueEvent(t2Fill);
    mockFsm.DrainMailbox();
    AssertRemainingContracts(fsm, 3);
    
    // Act: T3 fills
    var t3Fill = CreateFilledEvent("ORD005", "T3_Fleet_Apex_1", 
                                   1, 4540.0);
    mockFsm.EnqueueEvent(t3Fill);
    mockFsm.DrainMailbox();
    
    // Assert: Still active with 2 contracts remaining
    AssertFsmState(fsm, FollowerBracketState.Active, "T1+T2+T3 filled");
    AssertRemainingContracts(fsm, 2);
}
```

### T13: Zero Contracts Terminal State
```csharp
[Fact]
public void T13_ZeroContracts_Transitions_To_Filled()
{
    // Arrange
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    var fsm = CreateTestFsm("Sim101", "Fleet_Apex_1");
    fsm.State = FollowerBracketState.Active;
    fsm.RemainingContracts = 1;
    fsm.StopOrder = new MockOrder("ORD002", "Stop_Fleet_Apex_1", 
                                  OrderAction.Sell, 1);
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    mockFsm.MapOrderId("ORD002", "Fleet_Apex_1", fsm.Generation);
    
    // Act: Final contract fills
    var finalFill = CreateFilledEvent("ORD002", "Stop_Fleet_Apex_1", 
                                      1, 4480.0);
    mockFsm.EnqueueEvent(finalFill);
    mockFsm.DrainMailbox();
    
    // Assert: Terminal state reached
    AssertFsmState(fsm, FollowerBracketState.Filled, 
                   "Zero contracts = Filled");
    AssertRemainingContracts(fsm, 0);
}
```

---

## Verification Criteria

### Test Execution
- [ ] All 4 tests pass (T10-T13)
- [ ] Each test completes in <100ms
- [ ] Zero flaky failures
- [ ] No exceptions thrown

### Contract Logic Coverage
- [ ] Stop fill decrements contracts
- [ ] T1 detection works
- [ ] T2 detection works
- [ ] T3 detection works
- [ ] T4 detection works (if implemented)
- [ ] T5 detection works (if implemented)
- [ ] Multi-target scaling verified
- [ ] Zero contracts triggers Filled state

### Signal Name Parsing
- [ ] "Stop_" prefix detected
- [ ] "S_" prefix detected
- [ ] "T1_" prefix detected
- [ ] "T2_" prefix detected
- [ ] "T3_" prefix detected
- [ ] "T4_" prefix detected
- [ ] "T5_" prefix detected

### V12 DNA Compliance
- [ ] Zero `lock()` statements
- [ ] Atomic contract updates
- [ ] ASCII-only strings
- [ ] MockTime used

---

## Dependencies

### Prerequisites
- Ticket 01 (Mock Infrastructure) COMPLETE
- Ticket 02 (Phase 1 Tests) COMPLETE
- Ticket 03 (Phase 2 Tests) COMPLETE

### Blocks
- Ticket 05 (Phase 4 Tests)
- Ticket 06 (Phase 5 Tests)

---

## Estimated Complexity

**Size**: M (Medium)  
**Time**: 4-6 hours  
**Risk**: Low (straightforward arithmetic logic)

---

**END OF TICKET 04**