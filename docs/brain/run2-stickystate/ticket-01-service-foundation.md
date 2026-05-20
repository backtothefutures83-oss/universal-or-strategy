# Ticket 01: Service Foundation

**Epic:** run2-stickystate  
**Phase:** A - Service Foundation  
**Assignee:** v12-engineer  
**Estimated Complexity:** LOW  
**Dependencies:** None

---

## OBJECTIVE

Create the foundational service infrastructure: interfaces, DTOs, and unit test stub. This ticket establishes the pure C# service layer with zero NinjaTrader dependencies.

---

## SCOPE

### Files to Create

1. **src/Services/IStickyStateService.cs** (~40 lines)
   - IStickyStateService interface
   - IStickyStateLogger interface

2. **src/Services/StickyStateService.cs** (~100 lines - shell only)
   - StickyStateSnapshot struct
   - StickyStateData class
   - PositionTrailState struct
   - StickyStateService class (empty methods)

3. **tests/Services/StickyStateServiceTests.cs** (~20 lines)
   - Unit test stub proving dotnet test works

### Files to Modify

None (this ticket only creates new files)

---

## IMPLEMENTATION STEPS

### Step 1: Create IStickyStateService Interface

**File:** `src/Services/IStickyStateService.cs`

```csharp
// V12 Services: IStickyStateService -- Pure C# state persistence interface
// Zero NinjaTrader dependencies - enables dotnet test without NT runtime
using System;
using System.Collections.Generic;

namespace NinjaTrader.NinjaScript.Strategies.Services
{
    /// <summary>
    /// Pure C# service for StickyState serialization and deserialization.
    /// Accepts all state via method parameters (no global statics).
    /// </summary>
    public interface IStickyStateService
    {
        /// <summary>
        /// Serializes a state snapshot into INI format.
        /// Thread-safe: accepts immutable snapshot created on strategy thread.
        /// </summary>
        string Serialize(StickyStateSnapshot snapshot);

        /// <summary>
        /// Deserializes INI file into structured data.
        /// Returns null if file doesn't exist or parsing fails.
        /// </summary>
        StickyStateData Deserialize(string filePath);

        /// <summary>
        /// Atomic file write: write to .tmp, then rename over target.
        /// Prevents corruption if process is killed mid-write.
        /// </summary>
        void AtomicWrite(string targetPath, string content);
    }

    /// <summary>
    /// Logging abstraction for service (injected by Strategy).
    /// </summary>
    public interface IStickyStateLogger
    {
        void Log(string message);
    }
}
```

### Step 2: Create StickyStateService Shell

**File:** `src/Services/StickyStateService.cs`

```csharp
// V12 Services: StickyStateService -- Pure C# state persistence implementation
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NinjaTrader.NinjaScript.Strategies.Services
{
    /// <summary>
    /// Immutable snapshot of all state to be persisted.
    /// Created on strategy thread to prevent torn reads (H18-FIX).
    /// </summary>
    public struct StickyStateSnapshot
    {
        public HeaderConfigSnapshot HeaderConfig;
        public Dictionary<string, ModeConfigProfile> ModeProfiles;
        public Dictionary<string, bool> FleetAccounts;
        public KeyValuePair<string, PositionInfo>[] Positions;
        public string LeaderAccount;
        public RmaAnchorType CurrentAnchor;
        public double CachedManualPrice;
        public string InstrumentName;
        public string BuildTag;
    }

    /// <summary>
    /// Deserialized state data returned to Strategy for application.
    /// </summary>
    public class StickyStateData
    {
        public Dictionary<string, object> ConfigValues = new Dictionary<string, object>();
        public Dictionary<string, ModeConfigProfile> ModeProfiles = new Dictionary<string, ModeConfigProfile>();
        public Dictionary<string, bool> FleetToggles = new Dictionary<string, bool>();
        public Dictionary<string, PositionTrailState> PositionStates = new Dictionary<string, PositionTrailState>();
        public string LeaderAccount;
        public RmaAnchorType Anchor;
        public double ManualPrice;
    }

    /// <summary>
    /// Position trailing stop state (subset of PositionInfo).
    /// </summary>
    public struct PositionTrailState
    {
        public double ExtremePriceSinceEntry;
        public int CurrentTrailLevel;
        public bool ManualBreakevenArmed;
        public bool ManualBreakevenTriggered;
        public int InitialTargetCount;
    }

    /// <summary>
    /// Pure C# service for StickyState persistence.
    /// Zero NinjaTrader dependencies.
    /// </summary>
    public class StickyStateService : IStickyStateService
    {
        private readonly IStickyStateLogger _logger;

        public StickyStateService(IStickyStateLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Serialize(StickyStateSnapshot snapshot)
        {
            // TODO: Implement in ticket-02
            throw new NotImplementedException();
        }

        public StickyStateData Deserialize(string filePath)
        {
            // TODO: Implement in ticket-03
            throw new NotImplementedException();
        }

        public void AtomicWrite(string targetPath, string content)
        {
            // TODO: Implement in ticket-02
            throw new NotImplementedException();
        }
    }
}
```

### Step 3: Create Unit Test Stub

**File:** `tests/Services/StickyStateServiceTests.cs`

```csharp
using Xunit;
using NinjaTrader.NinjaScript.Strategies.Services;

namespace V12.Tests.Services
{
    public class StickyStateServiceTests
    {
        [Fact]
        public void CanInstantiateWithoutNinjaTrader()
        {
            // Proves dotnet test works without NinjaTrader runtime
            var logger = new TestLogger();
            var service = new StickyStateService(logger);
            Assert.NotNull(service);
        }

        private class TestLogger : IStickyStateLogger
        {
            public void Log(string message) { }
        }
    }
}
```

---

## VERIFICATION CHECKLIST

### Build Verification
- [ ] `dotnet build` succeeds (no compilation errors)
- [ ] No NinjaTrader dependencies in service files
- [ ] All using statements are pure .NET (System.*, no NinjaTrader.*)

### Test Verification
- [ ] `dotnet test` runs successfully
- [ ] CanInstantiateWithoutNinjaTrader test passes
- [ ] Test proves service can be created without NinjaTrader runtime

### Code Quality
- [ ] All classes/interfaces have XML doc comments
- [ ] ASCII-only compliance (no Unicode characters)
- [ ] Namespace follows V12 convention

---

## ACCEPTANCE CRITERIA

1. ✅ IStickyStateService interface created with 3 methods
2. ✅ IStickyStateLogger interface created
3. ✅ StickyStateSnapshot struct created with all required fields
4. ✅ StickyStateData class created with all required properties
5. ✅ PositionTrailState struct created
6. ✅ StickyStateService class created (shell with NotImplementedException)
7. ✅ Unit test stub created and passes
8. ✅ `dotnet test` runs without NinjaTrader
9. ✅ Zero compilation errors
10. ✅ Zero NinjaTrader dependencies in service layer

---

## NOTES

- This ticket creates the foundation only - methods throw NotImplementedException
- Serialization implementation comes in ticket-02
- Deserialization implementation comes in ticket-03
- Service remains testable throughout (dotnet test always works)

---

## ESTIMATED TIME

**30 minutes** (straightforward file creation, no complex logic)