// V12 Services: StickyStateService -- Pure C# state persistence implementation
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NinjaTrader.NinjaScript.Strategies.Services
{
    /// <summary>
    /// Target mode enum (matches V12_002.Properties.cs).
    /// </summary>
    public enum TargetMode
    {
        ATR,
        Ticks,
        Points,
        Runner
    }

    /// <summary>
    /// RMA anchor type enum (matches V12_002.cs).
    /// </summary>
    public enum RmaAnchorType
    {
        Ema30,
        Ema65,
        Ema200,
        OrHigh,
        OrLow,
        Manual
    }

    /// <summary>
    /// Per-mode config profile for sticky memory across mode switches.
    /// </summary>
    public class ModeConfigProfile
    {
        public int TargetCount = 1;
        public double T1, T2, T3, T4, T5;
        public TargetMode T1Type, T2Type, T3Type, T4Type, T5Type;
        public double StopMult;
        public double MaxRisk;
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
    /// Immutable snapshot of all state to be persisted.
    /// Created on strategy thread to prevent torn reads (H18-FIX).
    /// </summary>
    public struct StickyStateSnapshot
    {
        public string InstrumentFullName;
        public string BuildTag;
        
        // Mode flags
        public bool IsRMAModeActive;
        public bool IsTRENDModeActive;
        public bool IsRetestModeActive;
        public bool IsMOMOModeActive;
        public bool IsFFMAModeArmed;
        
        // Config section
        public int ActiveTargetCount;
        public double Target1Value;
        public double Target2Value;
        public double Target3Value;
        public double Target4Value;
        public double Target5Value;
        public TargetMode T1Type;
        public TargetMode T2Type;
        public TargetMode T3Type;
        public TargetMode T4Type;
        public TargetMode T5Type;
        public double StopMultiplier;
        public double RMAStopATRMultiplier;
        public double MaxRiskAmount;
        public string ChaseIfTouchPoints;
        public bool IsTrendRmaMode;
        public bool IsRetestRmaMode;
        
        // Fleet section
        public string LeaderAccount;
        public Dictionary<string, bool> FleetToggles;
        
        // Anchor section
        public RmaAnchorType Anchor;
        public double ManualPrice;
        
        // Mode profiles
        public Dictionary<string, ModeConfigProfile> ModeProfiles;
        
        // Position states
        public Dictionary<string, PositionTrailState> PositionStates;
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

        public void Serialize(StickyStateSnapshot snapshot, string filePath)
        {
            // TODO: Implement in ticket-02
            throw new NotImplementedException();
        }

        public StickyStateData Deserialize(string filePath)
        {
            // TODO: Implement in ticket-03
            throw new NotImplementedException();
        }
    }
}

// Made with Bob
