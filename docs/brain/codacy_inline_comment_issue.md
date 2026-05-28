# Inline Comment Formatting Issue (Deferred)

## Problem
Code + inline comments on same line violate V12 DNA formatting standards.

## Examples Found
1. `src/V12_002.Entries.Trend.cs:911`
   ```csharp
   UpdateATRStopDistance(RMAStopATRMultiplier); // V12.30: Ceiling-rounded
   ```

2. `src/V12_002.REAPER.Audit.cs:65`
   ```csharp
   int threshold = 1600; // 80% of 2000 capacity
   ```

3. `src/SignalBroadcaster.cs:22`
   ```csharp
   public string Instrument { get; set; } // V7.1: For instrument filtering
   ```

4. `src/SignalBroadcaster.cs:28`
   ```csharp
   public double Target3Price { get; set; } // V8: T3 price
   ```

## Fix Required
Move inline comments to separate line above:
```csharp
// V12.30: Ceiling-rounded
UpdateATRStopDistance(RMAStopATRMultiplier);
```

## Scope
Need to scan ALL src/*.cs files for this pattern:
```bash
Select-String -Path "src/*.cs" -Pattern ";\s*//" | Measure-Object
```

## Priority
P2 - Fix after P0 ErrorProne issues (73) complete

## Estimated Effort
- Scan: 5 minutes
- Fix: ~52-102 instances (4 new examples found)
- Total: 2-3 hours

## Rationale
- V12 DNA mandates clean separation of code and comments
- Inline comments reduce readability in dense HFT code
- CSharpier does not enforce this (style-only rule)
- Manual fix required across entire codebase

## Affected Files (Updated 2026-05-28)
- V12_002.Entries.Trend.cs
- V12_002.REAPER.Audit.cs
- SignalBroadcaster.cs (2 instances)