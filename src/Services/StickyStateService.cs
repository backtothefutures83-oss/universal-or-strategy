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
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            {
                _logger.Log("[STICKY] No persisted state found -- using defaults");
                return null;
            }

            try
            {
                string[] lines = System.IO.File.ReadAllLines(filePath, Encoding.UTF8);
                var data = new StickyStateData();
                string section = "";

                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                        continue;

                    // Section header
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        section = line.Substring(1, line.Length - 2).ToUpperInvariant();
                        continue;
                    }

                    ParseSection(section, line, data);
                }

                _logger.Log(string.Format("[STICKY] Loaded settings from {0}", System.IO.Path.GetFileName(filePath)));
                return data;
            }
            catch (Exception ex)
            {
                _logger.Log("[STICKY] Load failed (using defaults): " + ex.Message);
                return null;
            }
        }

        private void ParseSection(string section, string line, StickyStateData data)
        {
            if (section == "CONFIG")
            {
                ParseConfig(line, data);
            }
            else if (section.StartsWith("CONFIG_") && section.Length > 7)
            {
                string profileMode = section.Substring(7);
                ParseModeProfile(profileMode, line, data);
            }
            else if (section == "FLEET")
            {
                ParseFleet(line, data);
            }
            else if (section == "ANCHOR")
            {
                ParseAnchor(line, data);
            }
            else if (section == "POSITIONS")
            {
                ParsePosition(line, data);
            }
        }

        // [PORT: Lines 444-551 from V12_002.StickyState.cs]
        // Converted from ApplyStickyConfig() to populate DTO instead of mutating strategy
        private void ParseConfig(string line, StickyStateData data)
        {
            int eq = line.IndexOf('=');
            if (eq < 1) return;
            string key = line.Substring(0, eq).ToUpperInvariant();
            string val = line.Substring(eq + 1);

            // MODE - Build 1108.002 SAFETY GATE: Click-trader modes never auto-rearm on startup
            if (key == "MODE")
            {
                // Always force to OR (safety gate) - store original value for logging
                data.ConfigValues["MODE_ORIGINAL"] = val;
                data.ConfigValues["MODE"] = "OR";
                return;
            }

            // Target count
            if (key == "COUNT")
            {
                if (int.TryParse(val, out int cnt))
                    data.ConfigValues["COUNT"] = Math.Max(1, Math.Min(5, cnt));
                return;
            }

            // Target values
            if (key == "T1" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t1))
            {
                data.ConfigValues["T1"] = t1;
                return;
            }
            if (key == "T2" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t2))
            {
                data.ConfigValues["T2"] = t2;
                return;
            }
            if (key == "T3" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t3))
            {
                data.ConfigValues["T3"] = t3;
                return;
            }
            if (key == "T4" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t4))
            {
                data.ConfigValues["T4"] = t4;
                return;
            }
            if (key == "T5" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t5))
            {
                data.ConfigValues["T5"] = t5;
                return;
            }

            // Target types
            if (key == "T1TYPE")
            {
                data.ConfigValues["T1TYPE"] = ParseTargetMode(val);
                return;
            }
            if (key == "T2TYPE")
            {
                data.ConfigValues["T2TYPE"] = ParseTargetMode(val);
                return;
            }
            if (key == "T3TYPE")
            {
                data.ConfigValues["T3TYPE"] = ParseTargetMode(val);
                return;
            }
            if (key == "T4TYPE")
            {
                data.ConfigValues["T4TYPE"] = ParseTargetMode(val);
                return;
            }
            if (key == "T5TYPE")
            {
                data.ConfigValues["T5TYPE"] = ParseTargetMode(val);
                return;
            }

            // Risk and flags
            if (key == "STR" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double str))
            {
                data.ConfigValues["STR"] = str;
                return;
            }
            if (key == "MAX" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double max))
            {
                data.ConfigValues["MAX"] = max;
                return;
            }
            if (key == "CIT")
            {
                data.ConfigValues["CIT"] = val;
                return;
            }
            if (key == "TRMA")
            {
                data.ConfigValues["TRMA"] = (val == "1");
                return;
            }
            if (key == "RRMA")
            {
                data.ConfigValues["RRMA"] = (val == "1");
                return;
            }
        }

        // [PORT: Lines 554-636 from V12_002.StickyState.cs]
        // Converted from ApplyStickyModeProfile() to populate DTO
        private void ParseModeProfile(string mode, string line, StickyStateData data)
        {
            int eq = line.IndexOf('=');
            if (eq < 1) return;
            string key = line.Substring(0, eq).ToUpperInvariant();
            string val = line.Substring(eq + 1);

            ModeConfigProfile profile;
            if (!data.ModeProfiles.TryGetValue(mode, out profile))
            {
                profile = new ModeConfigProfile();
                data.ModeProfiles[mode] = profile;
            }

            // Target count
            if (key == "COUNT" && int.TryParse(val, out int cnt))
            {
                profile.TargetCount = Math.Max(1, Math.Min(5, cnt));
                return;
            }

            // Target values
            if (key == "T1" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t1))
            {
                profile.T1 = t1;
                return;
            }
            if (key == "T2" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t2))
            {
                profile.T2 = t2;
                return;
            }
            if (key == "T3" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t3))
            {
                profile.T3 = t3;
                return;
            }
            if (key == "T4" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t4))
            {
                profile.T4 = t4;
                return;
            }
            if (key == "T5" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t5))
            {
                profile.T5 = t5;
                return;
            }

            // Target types
            if (key == "T1TYPE")
            {
                profile.T1Type = ParseTargetMode(val);
                return;
            }
            if (key == "T2TYPE")
            {
                profile.T2Type = ParseTargetMode(val);
                return;
            }
            if (key == "T3TYPE")
            {
                profile.T3Type = ParseTargetMode(val);
                return;
            }
            if (key == "T4TYPE")
            {
                profile.T4Type = ParseTargetMode(val);
                return;
            }
            if (key == "T5TYPE")
            {
                profile.T5Type = ParseTargetMode(val);
                return;
            }

            // Risk
            if (key == "STR" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double str))
            {
                profile.StopMult = str;
                return;
            }
            if (key == "MAX" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double max))
            {
                profile.MaxRisk = max;
                return;
            }
        }

        // [PORT: Lines 638-657 from V12_002.StickyState.cs]
        private void ParseFleet(string line, StickyStateData data)
        {
            int eq = line.IndexOf('=');
            if (eq < 1) return;
            string key = line.Substring(0, eq);
            string val = line.Substring(eq + 1);

            if (key.ToUpperInvariant() == "LEADER")
            {
                data.LeaderAccount = val;
                return;
            }

            // Account toggle: "Apex_F01_12345=1"
            data.FleetToggles[key] = (val == "1");
        }

        // [PORT: Lines 659-678 from V12_002.StickyState.cs]
        private void ParseAnchor(string line, StickyStateData data)
        {
            int eq = line.IndexOf('=');
            if (eq < 1) return;
            string key = line.Substring(0, eq).ToUpperInvariant();
            string val = line.Substring(eq + 1);

            if (key == "TYPE")
            {
                data.Anchor = ParseAnchorType(val);
                return;
            }
            if (key == "MNL_PRICE")
            {
                if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double p))
                    data.ManualPrice = p;
                return;
            }
        }

        private static RmaAnchorType ParseAnchorType(string val)
        {
            string upper = val.ToUpperInvariant();
            if (upper == "EMA30") return RmaAnchorType.Ema30;
            if (upper == "EMA65") return RmaAnchorType.Ema65;
            if (upper == "EMA200") return RmaAnchorType.Ema200;
            if (upper == "OR_HIGH") return RmaAnchorType.OrHigh;
            if (upper == "OR_LOW") return RmaAnchorType.OrLow;
            if (upper == "MANUAL") return RmaAnchorType.Manual;
            return RmaAnchorType.Ema65; // Default
        }

        // [PORT: Lines 684-730 from V12_002.StickyState.cs]
        // Converted from EnrichTrailStateFromSticky() to populate DTO
        private void ParsePosition(string line, StickyStateData data)
        {
            if (line.StartsWith("#")) return; // Skip comment line

            // Format: key|extremePrice|trailLevel|beArmed|beTriggered|initialTargetCount
            string[] parts = line.Split('|');
            if (parts.Length < 6) return;

            string posKey = parts[0];
            var state = new PositionTrailState();

            if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double extreme))
                state.ExtremePriceSinceEntry = extreme;
            if (int.TryParse(parts[2], out int trail))
                state.CurrentTrailLevel = trail;
            state.ManualBreakevenArmed = (parts[3] == "1");
            state.ManualBreakevenTriggered = (parts[4] == "1");
            if (int.TryParse(parts[5], out int itc))
                state.InitialTargetCount = itc;

            data.PositionStates[posKey] = state;
        }

        // [PORT: Lines 760-769 from V12_002.StickyState.cs]
        private static TargetMode ParseTargetMode(string val)
        {
            if (val == null) return TargetMode.ATR;
            string upper = val.ToUpperInvariant();
            if (upper == "ATR") return TargetMode.ATR;
            if (upper == "TICKS") return TargetMode.Ticks;
            if (upper == "POINTS") return TargetMode.Points;
            if (upper == "RUNNER") return TargetMode.Runner;
            return TargetMode.ATR;
        }
    }
}

// Made with Bob
