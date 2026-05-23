# EPIC-5-PERF: Ticket T05 - Order Array Pooling

**Goal:** Eliminate `new[] { order }` allocations in Cancel/Submit calls using a lock-free pool.

## Scope
1. **Implement `OrderArrayPool`**:
   - Class location: `src/V12_002.Perf.OrderArrayPool.cs`
   - Data structure: `ConcurrentBag<Order[]>`
   - Fixed size: 1-element arrays only (matching the current usage).
   - Metrics: `RentCount`, `ReturnCount`, `FallbackCount`.

2. **Refactor Propagation Logic**:
   - Target File: `src/V12_002.Orders.Callbacks.Propagation.cs` (4 instances)
   - Pattern: Replace `new[] { order }` with pooled arrays.
   - **MANDATORY**: Use `try/finally` for all rentals to ensure arrays are returned even on exception.
   - **MANDATORY**: Move the `orderArray[0] = order` assignment *inside* the `try` block to prevent stale order references in the pool if an exception occurs during setup.

## Technical Details
```csharp
// Implementation Pattern
var orderArray = _orderArrayPool.Rent();
try 
{
    orderArray[0] = order; // Assign inside try
    CancelOrders(orderArray); 
}
finally 
{
    _orderArrayPool.Return(orderArray);
}
```

## Success Criteria
- [ ] `OrderArrayPool.Rent()` returns a valid 1-element array.
- [ ] ETW trace shows zero allocations at the 4 targeted sites in `Propagation.cs`.
- [ ] Pool metrics show `FallbackCount < 10%`.
- [ ] V12 DNA Audit: 0 `lock()` statements.

## Rollback
- Revert changes in `Propagation.cs` to use `new[] { order }`.
- Delete `src/V12_002.Perf.OrderArrayPool.cs`.
