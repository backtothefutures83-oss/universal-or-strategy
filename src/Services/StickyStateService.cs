// V12 Services: StickyStateService -- Pure C# state persistence implementation
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
            var sb = new StringBuilder(1024);
            SerializeSticky_WriteHeaderConfig(sb, snapshot);
            SerializeSticky_WriteFleetAnchor(sb, snapshot);
            SerializeSticky_WriteModeProfiles(sb, snapshot);
            SerializeSticky_WritePositions(sb, snapshot);
            AtomicWrite(filePath, sb.ToString());
        }

        private void SerializeSticky_WriteHeaderConfig(StringBuilder sb, StickyStateSnapshot snapshot)
        {
            // Header
            sb.AppendLine("# V12 StickyState v1");
            sb.AppendLine("# Symbol: " + (snapshot.InstrumentFullName ?? "unknown"));
            sb.AppendLine("# Updated: " + DateTime.UtcNow.ToString("o"));
            sb.AppendLine("# Build: " + snapshot.BuildTag);
            sb.AppendLine();

            // [CONFIG] - H18-FIX: Read from snapshot instead of live properties
            sb.AppendLine("[CONFIG]");
            string mode = "OR";
            if (snapshot.IsRMAModeActive) mode = "RMA";
            else if (snapshot.IsTRENDModeActive) mode = "TREND";
            else if (snapshot.IsRetestModeActive) mode = "RETEST";
            else if (snapshot.IsMOMOModeActive) mode = "MOMO";
            else if (snapshot.IsFFMAModeArmed) mode = "FFMA";
            sb.AppendLine("MODE=" + mode);
            sb.AppendLine("COUNT=" + snapshot.ActiveTargetCount.ToString());
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T1={0}", snapshot.Target1Value));
            sb.AppendLine("T1TYPE=" + snapshot.T1Type.ToString());
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T2={0}", snapshot.Target2Value));
            sb.AppendLine("T2TYPE=" + snapshot.T2Type.ToString());
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T3={0}", snapshot.Target3Value));
            sb.AppendLine("T3TYPE=" + snapshot.T3Type.ToString());
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T4={0}", snapshot.Target4Value));
            sb.AppendLine("T4TYPE=" + snapshot.T4Type.ToString());
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T5={0}", snapshot.Target5Value));
            sb.AppendLine("T5TYPE=" + snapshot.T5Type.ToString());
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "STR={0}",
                snapshot.IsRMAModeActive ? snapshot.RMAStopATRMultiplier : snapshot.StopMultiplier));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "MAX={0}", snapshot.MaxRiskAmount));
            sb.AppendLine("CIT=" + (snapshot.ChaseIfTouchPoints ?? "0"));
            sb.AppendLine("TRMA=" + (snapshot.IsTrendRmaMode ? "1" : "0"));
            sb.AppendLine("RRMA=" + (snapshot.IsRetestRmaMode ? "1" : "0"));
            sb.AppendLine();
        }

        private void SerializeSticky_WriteFleetAnchor(StringBuilder sb, StickyStateSnapshot snapshot)
        {
            // [FLEET] - H18-FIX: Use snapshot instead of live dictionary
            sb.AppendLine("[FLEET]");
            sb.AppendLine("LEADER=" + (snapshot.LeaderAccount ?? ""));
            if (snapshot.FleetToggles != null)
            {
                foreach (var kvp in snapshot.FleetToggles)
                    sb.AppendLine(kvp.Key + "=" + (kvp.Value ? "1" : "0"));
            }
            sb.AppendLine();

            // [ANCHOR]
            sb.AppendLine("[ANCHOR]");
            sb.AppendLine("TYPE=" + AnchorTypeToString(snapshot.Anchor));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "MNL_PRICE={0}", snapshot.ManualPrice));
            sb.AppendLine();
        }

        private void SerializeSticky_WriteModeProfiles(StringBuilder sb, StickyStateSnapshot snapshot)
        {
            // Build 1106: [CONFIG_*] -- per-mode profile snapshots
            // H18-FIX: Use snapshot instead of mutating live _modeProfiles dictionary
            string activeMode = "OR";
            if (snapshot.IsRMAModeActive) activeMode = "RMA";
            else if (snapshot.IsTRENDModeActive) activeMode = "TREND";
            else if (snapshot.IsRetestModeActive) activeMode = "RETEST";
            else if (snapshot.IsMOMOModeActive) activeMode = "MOMO";
            else if (snapshot.IsFFMAModeArmed) activeMode = "FFMA";
            
            // Capture current config into snapshot (not live dictionary)
            var modeProfilesSnapshot = snapshot.ModeProfiles ?? new Dictionary<string, ModeConfigProfile>();
            modeProfilesSnapshot[activeMode] = new ModeConfigProfile
            {
                TargetCount = snapshot.ActiveTargetCount,
                T1 = snapshot.Target1Value,
                T2 = snapshot.Target2Value,
                T3 = snapshot.Target3Value,
                T4 = snapshot.Target4Value,
                T5 = snapshot.Target5Value,
                T1Type = snapshot.T1Type,
                T2Type = snapshot.T2Type,
                T3Type = snapshot.T3Type,
                T4Type = snapshot.T4Type,
                T5Type = snapshot.T5Type,
                StopMult = snapshot.IsRMAModeActive ? snapshot.RMAStopATRMultiplier : snapshot.StopMultiplier,
                MaxRisk = snapshot.MaxRiskAmount
            };

            foreach (var kvp in modeProfilesSnapshot)
            {
                ModeConfigProfile p = kvp.Value;
                if (p == null) continue;
                sb.AppendLine("[CONFIG_" + kvp.Key + "]");
                sb.AppendLine("COUNT=" + p.TargetCount.ToString());
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T1={0}", p.T1));
                sb.AppendLine("T1TYPE=" + p.T1Type.ToString());
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T2={0}", p.T2));
                sb.AppendLine("T2TYPE=" + p.T2Type.ToString());
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T3={0}", p.T3));
                sb.AppendLine("T3TYPE=" + p.T3Type.ToString());
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T4={0}", p.T4));
                sb.AppendLine("T4TYPE=" + p.T4Type.ToString());
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "T5={0}", p.T5));
                sb.AppendLine("T5TYPE=" + p.T5Type.ToString());
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "STR={0}", p.StopMult));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "MAX={0}", p.MaxRisk));
                sb.AppendLine();
            }
        }

        private void SerializeSticky_WritePositions(StringBuilder sb, StickyStateSnapshot snapshot)
        {
            // [POSITIONS] -- trailing stop state for active positions
            // H18-FIX: Use snapshot instead of live dictionary
            sb.AppendLine("[POSITIONS]");
            sb.AppendLine("# key|extremePrice|trailLevel|beArmed|beTriggered|initialTargetCount");
            if (snapshot.PositionStates != null)
            {
                foreach (var kvp in snapshot.PositionStates)
                {
                    var pi = kvp.Value;
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}|{1}|{2}|{3}|{4}|{5}",
                        kvp.Key,
                        pi.ExtremePriceSinceEntry,
                        pi.CurrentTrailLevel,
                        pi.ManualBreakevenArmed ? "1" : "0",
                        pi.ManualBreakevenTriggered ? "1" : "0",
                        pi.InitialTargetCount));
                }
            }
        }

        private static string AnchorTypeToString(RmaAnchorType t)
        {
            switch (t)
            {
                case RmaAnchorType.Ema30:  return "EMA30";
                case RmaAnchorType.Ema65:  return "EMA65";
                case RmaAnchorType.Ema200: return "EMA200";
                case RmaAnchorType.OrHigh: return "OR_HIGH";
                case RmaAnchorType.OrLow:  return "OR_LOW";
                case RmaAnchorType.Manual: return "MANUAL";
                default: return "EMA65";
            }
        }

        /// <summary>
        /// Atomic file write: write to .tmp, then rename over target.
        /// Prevents corruption if process is killed mid-write.
        /// </summary>
        private void AtomicWrite(string targetPath, string content)
        {
            if (string.IsNullOrEmpty(targetPath)) return;
            string tmpPath = targetPath + ".tmp";
            File.WriteAllText(tmpPath, content, Encoding.UTF8);
            // File.Move on Windows is atomic on NTFS when same volume
            if (File.Exists(targetPath))
                File.Delete(targetPath);
            File.Move(tmpPath, targetPath);
        }

        public StickyStateData Deserialize(string filePath)
        {
            // TODO: Implement in ticket-03
            throw new NotImplementedException();
        }
    }
}

// Made with Bob
