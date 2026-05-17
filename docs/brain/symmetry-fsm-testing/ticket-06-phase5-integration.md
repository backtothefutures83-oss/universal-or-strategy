# Ticket 06: Phase 5 Tests - Integration

**Epic**: Symmetry FSM Testing  
**Phase**: Phase 5 (P2 - Integration)  
**Priority**: P2 (Medium)  
**Complexity**: M (Medium - 4-6 hours)  
**Owner**: Bob CLI (`v12-engineer`)

---

## Objective

Implement integration tests verifying FSM interaction with REAPER (position calculation), SIMA (lifecycle management), Orders (two-phase replace), and metadata validation.

---

## Scope

### In Scope
- T18: REAPER Integration (GetFsmExpectedPosition)
- T19: SIMA Integration (FSM creation/removal)
- T20: Orders Integration (Two-phase replace with Replacing state)

---

## Test Scenarios

### T18: REAPER Integration - GetFsmExpectedPosition
```csharp
[Fact]
public void T18_REAPER_GetFsmExpectedPosition_Aggregates()
{
    // Arrange: Multiple FSMs for same account
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    
    var fsm1 = CreateTestFsm("Sim101", "Fleet_Apex_1");
    fsm1.State = FollowerBracketState.Active;
    fsm1.EntryOrder = new MockOrder("ORD001", "Entry_Fleet_Apex_1", 
                                    OrderAction.Buy, 2);
    
    var fsm2 = CreateTestFsm("Sim101", "Fleet_Apex_2");
    fsm2.State = FollowerBracketState.Active;
    fsm2.EntryOrder = new MockOrder("ORD002", "Entry_Fleet_Apex_2", 
                                    OrderAction.Buy, 3);
    
    mockFsm.AddBracket("Fleet_Apex_1", fsm1);
    mockFsm.AddBracket("Fleet_Apex_2", fsm2);
    
    // Act: Calculate expected position
    int expectedPos = mockFsm.GetFsmExpectedPosition("Sim101");
    
    // Assert: Aggregates both FSMs (2 + 3 = 5)
    Assert.Equal(5, expectedPos);
}

[Fact]
public void T18_REAPER_GetFsmExpectedPosition_Short_Position()
{
    // Arrange: Short position
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    
    var fsm = CreateTestFsm("Sim101", "Fleet_Apex_1");
    fsm.State = FollowerBracketState.Active;
    fsm.EntryOrder = new MockOrder("ORD001", "Entry_Fleet_Apex_1", 
                                    OrderAction.SellShort, 2);
    
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    
    // Act
    int expectedPos = mockFsm.GetFsmExpectedPosition("Sim101");
    
    // Assert: Negative for short (-2)
    Assert.Equal(-2, expectedPos);
}

[Fact]
public void T18_REAPER_GetFsmExpectedPosition_Terminal_States_Excluded()
{
    // Arrange: Mix of active and terminal FSMs
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    
    var fsm1 = CreateTestFsm("Sim101", "Fleet_Apex_1");
    fsm1.State = FollowerBracketState.Active;
    fsm1.EntryOrder = new MockOrder("ORD001", "Entry_Fleet_Apex_1", 
                                    OrderAction.Buy, 2);
    
    var fsm2 = CreateTestFsm("Sim101", "Fleet_Apex_2");
    fsm2.State = FollowerBracketState.Filled; // Terminal
    fsm2.EntryOrder = new MockOrder("ORD002", "Entry_Fleet_Apex_2", 
                                    OrderAction.Buy, 3);
    
    mockFsm.AddBracket("Fleet_Apex_1", fsm1);
    mockFsm.AddBracket("Fleet_Apex_2", fsm2);
    
    // Act
    int expectedPos = mockFsm.GetFsmExpectedPosition("Sim101");
    
    // Assert: Only active FSM counted (2, not 5)
    Assert.Equal(2, expectedPos);
}
```

### T19: SIMA Integration - FSM Lifecycle
```csharp
[Fact]
public void T19_SIMA_FSM_Creation_And_Removal()
{
    // Arrange
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    
    // Act: Create FSM
    var fsm = CreateTestFsm("Sim101", "Fleet_Apex_1");
    fsm.State = FollowerBracketState.PendingSubmit;
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    
    // Assert: FSM exists
    var retrieved = mockFsm.GetBracket("Fleet_Apex_1");
    AssertFsmNotNull(retrieved, "FSM created");
    
    // Act: Remove FSM
    bool removed = mockFsm.RemoveBracket("Fleet_Apex_1");
    
    // Assert: FSM removed
    Assert.True(removed, "FSM removed");
    var afterRemoval = mockFsm.GetBracket("Fleet_Apex_1");
    AssertFsmNull(afterRemoval, "FSM no longer exists");
}

[Fact]
public void T19_SIMA_FSM_OrderId_Mappings_Cleaned()
{
    // Arrange
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    var fsm = CreateTestFsm("Sim101", "Fleet_Apex_1");
    fsm.EntryOrder = new MockOrder("ORD001", "Entry_Fleet_Apex_1", 
                                   OrderAction.Buy, 2);
    fsm.StopOrder = new MockOrder("ORD002", "Stop_Fleet_Apex_1", 
                                  OrderAction.Sell, 2);
    
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    mockFsm.MapOrderId("ORD001", "Fleet_Apex_1", fsm.Generation);
    mockFsm.MapOrderId("ORD002", "Fleet_Apex_1", fsm.Generation);
    
    // Act: Remove FSM
    mockFsm.RemoveBracket("Fleet_Apex_1");
    
    // Assert: OrderId mappings cleaned
    var resolved1 = mockFsm.ResolveFsm_ByOrderId("ORD001");
    var resolved2 = mockFsm.ResolveFsm_ByOrderId("ORD002");
    AssertFsmNull(resolved1, "Entry mapping cleaned");
    AssertFsmNull(resolved2, "Stop mapping cleaned");
}
```

### T20: Orders Integration - Two-Phase Replace
```csharp
[Fact]
public void T20_Orders_TwoPhase_Replace_Replacing_State()
{
    // Arrange
    var time = new MockTime(1000000L);
    var mockFsm = new MockSymmetryFsm(time);
    var fsm = CreateTestFsm("Sim101", "Fleet_Apex_1");
    fsm.State = FollowerBracketState.Active;
    fsm.StopOrder = new MockOrder("ORD002", "Stop_Fleet_Apex_1", 
                                  OrderAction.Sell, 2);
    mockFsm.AddBracket("Fleet_Apex_1", fsm);
    mockFsm.MapOrderId("ORD002", "Fleet_Apex_1", fsm.Generation);
    
    // Act: Phase 1 - Cancel old stop (enter Replacing state)
    mockFsm.SetFsmReplacing("Fleet_Apex_1", "ORD002");
    AssertFsmState(fsm, FollowerBracketState.Replacing, 
                   "Phase 1: Replacing");
    Assert.Equal("ORD002", fsm.ReplacingCancelOrderId);
    
    // Act: Phase 2 - Cancel confirmed
    var cancelEvent = CreateCancelledEvent("ORD002", "Stop_Fleet_Apex_1");
    mockFsm.EnqueueEvent(cancelEvent);
    mockFsm.DrainMailbox();
    
    // Assert: Still in Replacing (cancel absorbed)
    AssertFsmState(fsm, FollowerBracketState.Replacing, 
                   "Cancel absorbed, stays Replacing");
    
    // Act: Phase 3 - New stop submitted and accepted
    fsm.StopOrder = new MockOrder("ORD003", "Stop_Fleet_Apex_1", 
                                  OrderAction.Sell, 2);
    mockFsm.MapOrderId("ORD003", "Fleet_Apex_1", fsm.Generation);
    fsm.State = FollowerBracketState.Active;
    fsm.ReplacingCancelOrderId = null;
    
    // Assert: Back to Active with new stop
    AssertFsmState(fsm, FollowerBracketState.Active, 
                   "Replace complete");
    Assert.Equal("ORD003", fsm.StopOrder.OrderId);
}
```

---

## Verification Criteria

### Test Execution
- [ ] All 3 test groups pass (T18, T19, T20)
- [ ] Each test completes in <100ms
- [ ] Zero flaky failures
- [ ] No exceptions thrown

### Integration Coverage
- [ ] REAPER position calculation tested
- [ ] SIMA FSM lifecycle tested
- [ ] Orders two-phase replace tested
- [ ] OrderId mapping cleanup tested

### Position Calculation
- [ ] Long positions aggregate correctly
- [ ] Short positions aggregate correctly
- [ ] Terminal states excluded
- [ ] Null order references handled

### FSM Lifecycle
- [ ] Creation works
- [ ] Removal works
- [ ] OrderId mappings cleaned on removal
- [ ] Generation counter increments

### Two-Phase Replace
- [ ] Replacing state entered
- [ ] Cancel absorbed during replace
- [ ] New order accepted
- [ ] Back to Active after replace

### V12 DNA Compliance
- [ ] Zero `lock()` statements
- [ ] Atomic operations
- [ ] ASCII-only strings
- [ ] MockTime used

---

## Dependencies

### Prerequisites
- Ticket 01 (Mock Infrastructure) COMPLETE
- Ticket 02 (Phase 1 Tests) COMPLETE
- Ticket 03 (Phase 2 Tests) COMPLETE
- Ticket 04 (Phase 3 Tests) COMPLETE
- Ticket 05 (Phase 4 Tests) COMPLETE

### Blocks
- None (final ticket)

---

## Estimated Complexity

**Size**: M (Medium)  
**Time**: 4-6 hours  
**Risk**: Low (integration testing)

---

## Final Deliverables

Upon completion of this ticket:
1. All 20 test scenarios implemented (T01-T20)
2. >90% branch coverage achieved
3. Coverage report generated
4. All tests passing
5. Zero lock usage verified
6. Documentation complete

---

**END OF TICKET 06**
**END OF EPIC**