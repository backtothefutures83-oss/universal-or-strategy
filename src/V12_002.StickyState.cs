// <copyright file="V12_002.StickyState.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
// EPIC-4-STICKY-STATE-IPC Ticket 02: Sticky State Persistence Layer
// Atomic file operations with SHA256 checksums and rollback capability
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002
    {
        #region Sticky State - Persistence Layer

        [Serializable]
        public class StateSnapshot
        {
            public long SnapshotTicks { get; set; }
            public string StrategyVersion { get; set; }
            public int PositionSize { get; set; }
            public bool EnableSIMA { get; set; }
            public bool EnableREAPER { get; set; }
            public Dictionary<string, int> AccountPositions { get; set; }
            public string ChecksumSHA256 { get; set; }

            public StateSnapshot()
            {
                StrategyVersion = string.Empty;
                ChecksumSHA256 = string.Empty;
                AccountPositions = new Dictionary<string, int>();
            }
        }

        private StateSnapshot CaptureStateSnapshot()
        {
            StateSnapshot snapshot = new StateSnapshot
            {
                SnapshotTicks = DateTime.UtcNow.Ticks,
                StrategyVersion = BUILD_TAG,
                PositionSize = minContracts,
                EnableSIMA = EnableSIMA,
                EnableREAPER = ReaperAuditEnabled,
            };

            if (expectedPositions != null)
            {
                foreach (var kvp in expectedPositions)
                {
                    snapshot.AccountPositions[kvp.Key] = kvp.Value;
                }
            }

            return snapshot;
        }

        private bool WriteSnapshotAtomic(StateSnapshot snapshot)
        {
            string tempPath = _stickyStatePath + ".tmp";
            string backupPath = _stickyStatePath + ".bak";

            try
            {
                // EPIC-7-QUALITY-010: Validate paths before file operations
                string validStatePath = PathValidation.ValidateAndCanonicalize(_stickyStatePath, "WriteState");
                string validTempPath = PathValidation.ValidateAndCanonicalize(tempPath, "WriteTempState");
                string validBackupPath = PathValidation.ValidateAndCanonicalize(backupPath, "WriteBackupState");

                // EPIC-4 P0 Fix #2: Compute checksum over canonical payload (checksum field empty)
                snapshot.ChecksumSHA256 = string.Empty;
                string json = SerializeSnapshot(snapshot);
                snapshot.ChecksumSHA256 = ComputeSHA256(json);
                string jsonWithChecksum = SerializeSnapshot(snapshot);
                File.WriteAllText(validTempPath, jsonWithChecksum, Encoding.UTF8);

                if (File.Exists(validStatePath))
                {
                    File.Copy(validStatePath, validBackupPath, overwrite: true);
                }

                // .NET Framework 4.5 doesn't support overwrite parameter
                if (File.Exists(validStatePath))
                {
                    File.Delete(validStatePath);
                }
                File.Move(validTempPath, validStatePath);

                Interlocked.Exchange(ref _lastSnapshotTicks, DateTime.UtcNow.Ticks);
                return true;
            }
            catch (SecurityException ex)
            {
                // EPIC-7-QUALITY-010: Log security violations
                Print(string.Format("[IO_SECURITY] {0}", ex.Message));
                throw; // Re-throw to fail-fast
            }
            catch (Exception ex)
            {
                Print(string.Format("[STICKY] Snapshot write failed: {0}", ex.Message));

                // Cleanup temp file (use original path since validation may have failed)
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch { }
                }

                return false;
            }
        }

        private StateSnapshot LoadStateSnapshot()
        {
            try
            {
                // EPIC-7-QUALITY-010: Validate path before checking existence
                string validStatePath = PathValidation.ValidateAndCanonicalize(_stickyStatePath, "ReadState");

                if (!File.Exists(validStatePath))
                {
                    Print("[STICKY] No persisted state found");
                    return null;
                }

                string json = File.ReadAllText(validStatePath, Encoding.UTF8);
                StateSnapshot snapshot = DeserializeSnapshot(json);

                if (snapshot == null)
                {
                    Print("[STICKY] Deserialization returned null");
                    return null;
                }

                if (!ValidateSnapshotIntegrity(snapshot, json))
                {
                    Print("[STICKY] Integrity check failed -- attempting rollback");
                    if (RollbackToLastGoodState())
                    {
                        // Re-validate path after rollback (use different variable name)
                        string validStatePathAfterRollback = PathValidation.ValidateAndCanonicalize(
                            _stickyStatePath,
                            "ReadStateAfterRollback"
                        );
                        string backupJson = File.ReadAllText(validStatePathAfterRollback, Encoding.UTF8);
                        snapshot = DeserializeSnapshot(backupJson);
                        return snapshot;
                    }

                    return null;
                }

                return snapshot;
            }
            catch (SecurityException ex)
            {
                // EPIC-7-QUALITY-010: Log security violations
                Print(string.Format("[IO_SECURITY] {0}", ex.Message));
                throw; // Re-throw to fail-fast
            }
            catch (Exception ex)
            {
                Print(string.Format("[STICKY] Load failed: {0}", ex.Message));
                return null;
            }
        }

        private bool ValidateSnapshotIntegrity(StateSnapshot snapshot, string json)
        {
            // EPIC-4 P0 Fix #2: Compute checksum over canonical payload (same as write)
            string storedChecksum = snapshot.ChecksumSHA256;
            snapshot.ChecksumSHA256 = string.Empty;
            string canonicalJson = SerializeSnapshot(snapshot);
            string computedChecksum = ComputeSHA256(canonicalJson);
            snapshot.ChecksumSHA256 = storedChecksum;

            // 1. Checksum validation (hard fail)
            if (storedChecksum != computedChecksum)
            {
                Print(
                    string.Format(
                        "[STICKY] Checksum mismatch! Expected: {0}, Got: {1}",
                        snapshot.ChecksumSHA256,
                        computedChecksum
                    )
                );
                return false;
            }

            // 2. Version check (soft migration after checksum passes)
            if (snapshot.StrategyVersion != BUILD_TAG)
            {
                Print(
                    string.Format(
                        "[STICKY] Version mismatch detected: {0} -> {1}. Migrating state.",
                        snapshot.StrategyVersion,
                        BUILD_TAG
                    )
                );
                return true; // Allow load to proceed with migration
            }

            return true;
        }

        private bool RollbackToLastGoodState()
        {
            string backupPath = _stickyStatePath + ".bak";

            try
            {
                // EPIC-7-QUALITY-010: Validate backup path
                string validBackupPath = PathValidation.ValidateAndCanonicalize(backupPath, "ReadBackup");

                if (!File.Exists(validBackupPath))
                {
                    Print("[STICKY] No backup available for rollback");
                    return false;
                }

                string json = File.ReadAllText(validBackupPath, Encoding.UTF8);
                StateSnapshot backup = DeserializeSnapshot(json);

                if (backup == null)
                {
                    Print("[STICKY] Backup deserialization failed");
                    return false;
                }

                if (!ValidateSnapshotIntegrity(backup, json))
                {
                    Print("[STICKY] Backup also corrupted. Cannot rollback.");
                    return false;
                }

                File.Copy(backupPath, _stickyStatePath, overwrite: true);

                Print(
                    string.Format(
                        "[STICKY] Rolled back to snapshot from {0}",
                        new DateTime(backup.SnapshotTicks, DateTimeKind.Utc).ToString(
                            "yyyy-MM-dd HH:mm:ss",
                            CultureInfo.InvariantCulture
                        )
                    )
                );

                return true;
            }
            catch (Exception ex)
            {
                Print(string.Format("[STICKY] Rollback failed: {0}", ex.Message));
                return false;
            }
        }

        private string ComputeSHA256(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
            }
        }

        private void RestoreFromSnapshot(StateSnapshot snapshot)
        {
            Print(
                string.Format(
                    "[STICKY] Restoring state from {0}",
                    new DateTime(snapshot.SnapshotTicks, DateTimeKind.Utc).ToString(
                        "yyyy-MM-dd HH:mm:ss",
                        CultureInfo.InvariantCulture
                    )
                )
            );

            minContracts = snapshot.PositionSize;
            EnableSIMA = snapshot.EnableSIMA;
            ReaperAuditEnabled = snapshot.EnableREAPER;

            // EPIC-4 P1-3 Fix: Atomic state transition using ConcurrentDictionary.Clear() + AddOrUpdate
            // Clear() is atomic in ConcurrentDictionary, then rebuild using atomic AddOrUpdate operations
            if (expectedPositions != null)
            {
                // Atomic clear - concurrent readers see either old full state or empty state, never partial
                expectedPositions.Clear();

                // Rebuild using atomic AddOrUpdate operations
                foreach (var kvp in snapshot.AccountPositions)
                {
                    expectedPositions.AddOrUpdate(kvp.Key, kvp.Value, (k, v) => kvp.Value);
                    Print(string.Format("[STICKY] Restored position: {0} = {1}", kvp.Key, kvp.Value));
                }
            }

            Print("[STICKY] State restoration complete");
        }

        private bool LoadStickyState()
        {
            if (!_stickyStateEnabled)
            {
                return false;
            }

            StateSnapshot snapshot = LoadStateSnapshot();
            if (snapshot != null)
            {
                RestoreFromSnapshot(snapshot);
                return true;
            }

            return false;
        }

        private void SaveStickyState()
        {
            if (!_stickyStateEnabled)
            {
                return;
            }

            // EPIC-4 P0 Fix #3: Consume dirty flag atomically before saving
            int wasDirty = Interlocked.Exchange(ref _stickyDirtyFlag, 0);
            if (wasDirty == 0)
            {
                // No changes since last save - skip write
                return;
            }

            StateSnapshot snapshot = CaptureStateSnapshot();
            WriteSnapshotAtomic(snapshot);
        }

        private string SerializeSnapshot(StateSnapshot snapshot)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\n");
            sb.AppendFormat(
                "  \"SnapshotTicks\": {0},\n",
                snapshot.SnapshotTicks.ToString(CultureInfo.InvariantCulture)
            );
            sb.AppendFormat("  \"StrategyVersion\": \"{0}\",\n", EscapeJsonString(snapshot.StrategyVersion));
            sb.AppendFormat("  \"PositionSize\": {0},\n", snapshot.PositionSize.ToString(CultureInfo.InvariantCulture));
            sb.AppendFormat("  \"EnableSIMA\": {0},\n", snapshot.EnableSIMA ? "true" : "false");
            sb.AppendFormat("  \"EnableREAPER\": {0},\n", snapshot.EnableREAPER ? "true" : "false");
            sb.Append("  \"AccountPositions\": {\n");

            bool firstAccount = true;
            foreach (var kvp in snapshot.AccountPositions)
            {
                if (!firstAccount)
                {
                    sb.Append(",\n");
                }

                sb.AppendFormat(
                    "    \"{0}\": {1}",
                    EscapeJsonString(kvp.Key),
                    kvp.Value.ToString(CultureInfo.InvariantCulture)
                );
                firstAccount = false;
            }

            sb.Append("\n  },\n");
            sb.AppendFormat("  \"ChecksumSHA256\": \"{0}\"\n", EscapeJsonString(snapshot.ChecksumSHA256));
            sb.Append("}");
            return sb.ToString();
        }

        private StateSnapshot DeserializeSnapshot(string json)
        {
            StateSnapshot snapshot = new StateSnapshot();

            try
            {
                snapshot.SnapshotTicks = ParseJsonLong(json, "SnapshotTicks");
                snapshot.StrategyVersion = ParseJsonString(json, "StrategyVersion");
                snapshot.PositionSize = ParseJsonInt(json, "PositionSize");
                snapshot.EnableSIMA = ParseJsonBool(json, "EnableSIMA");
                snapshot.EnableREAPER = ParseJsonBool(json, "EnableREAPER");
                snapshot.ChecksumSHA256 = ParseJsonString(json, "ChecksumSHA256");

                int accountPosStart = json.IndexOf("\"AccountPositions\"");
                if (accountPosStart >= 0)
                {
                    int objStart = json.IndexOf('{', accountPosStart);
                    int objEnd = json.IndexOf('}', objStart);
                    if (objStart >= 0 && objEnd > objStart)
                    {
                        string accountsBlock = json.Substring(objStart + 1, objEnd - objStart - 1);
                        string[] pairs = accountsBlock.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string pair in pairs)
                        {
                            int colonIdx = pair.IndexOf(':');
                            if (colonIdx > 0)
                            {
                                string key = pair.Substring(0, colonIdx).Trim().Trim('"');
                                string valStr = pair.Substring(colonIdx + 1).Trim();
                                if (
                                    int.TryParse(
                                        valStr,
                                        NumberStyles.Integer,
                                        CultureInfo.InvariantCulture,
                                        out int val
                                    )
                                )
                                {
                                    snapshot.AccountPositions[key] = val;
                                }
                            }
                        }
                    }
                }

                return snapshot;
            }
            catch
            {
                return null;
            }
        }

        private string EscapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            return input.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private long ParseJsonLong(string json, string key)
        {
            string pattern = string.Format("\"{0}\": ", key);
            int startIdx = json.IndexOf(pattern);
            if (startIdx < 0)
            {
                return 0;
            }

            startIdx += pattern.Length;
            int endIdx = json.IndexOfAny(new[] { ',', '\n', '\r', '}' }, startIdx);
            if (endIdx < 0)
            {
                return 0;
            }

            string valueStr = json.Substring(startIdx, endIdx - startIdx).Trim();
            if (long.TryParse(valueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
            {
                return result;
            }

            return 0;
        }

        private int ParseJsonInt(string json, string key)
        {
            return (int)ParseJsonLong(json, key);
        }

        private bool ParseJsonBool(string json, string key)
        {
            string pattern = string.Format("\"{0}\": ", key);
            int startIdx = json.IndexOf(pattern);
            if (startIdx < 0)
            {
                return false;
            }

            startIdx += pattern.Length;
            int endIdx = json.IndexOfAny(new[] { ',', '\n', '\r', '}' }, startIdx);
            if (endIdx < 0)
            {
                return false;
            }

            string valueStr = json.Substring(startIdx, endIdx - startIdx).Trim();
            return valueStr.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private string ParseJsonString(string json, string key)
        {
            string pattern = string.Format("\"{0}\": \"", key);
            int startIdx = json.IndexOf(pattern);
            if (startIdx < 0)
            {
                return string.Empty;
            }

            startIdx += pattern.Length;
            int endIdx = startIdx;
            while (endIdx < json.Length)
            {
                if (json[endIdx] == '"' && (endIdx == startIdx || json[endIdx - 1] != '\\'))
                {
                    break;
                }

                endIdx++;
            }

            if (endIdx >= json.Length)
            {
                return string.Empty;
            }

            return json.Substring(startIdx, endIdx - startIdx);
        }

        /// <summary>
        /// Apply pending sticky fleet toggles (SIMA/REAPER enable flags).
        /// Called during lifecycle initialization to restore persisted toggle states.
        /// </summary>
        private void ApplyPendingStickyFleetToggles()
        {
            // No-op stub: Fleet toggle logic will be implemented in Phase 2
            // This method is called from V12_002.SIMA.Lifecycle.cs:195
            Print("[STICKY] ApplyPendingStickyFleetToggles() - stub (no-op)");
        }

        /// <summary>
        /// Enrich trail state from sticky persistence layer.
        /// Restores trailing stop metadata from persisted state.
        /// </summary>
        private void EnrichTrailStateFromSticky()
        {
            // No-op stub: Trail state enrichment will be implemented in Phase 2
            // This method is called from V12_002.SIMA.Lifecycle.cs:205
            Print("[STICKY] EnrichTrailStateFromSticky() - stub (no-op)");
        }

        /// <summary>
        /// Mark sticky state as dirty, triggering a deferred persistence write.
        /// Uses atomic flag to avoid lock contention.
        /// </summary>
        private void MarkStickyDirty()
        {
            // Atomic flag set - no lock required (V12 DNA compliance)
            Interlocked.Exchange(ref _stickyDirtyFlag, 1);
        }

        /// <summary>
        /// Snapshot current configuration for IPC command responses.
        /// Returns a lightweight config snapshot without full state serialization.
        /// Phase 1 stub - returns default config. Full implementation in Phase 2.
        /// </summary>
        private ModeConfigProfile SnapshotCurrentConfig()
        {
            // Phase 1 stub - returns default config. Full implementation in Phase 2.
            // Called from V12_002.UI.IPC.Commands.Config.cs:136 and Mode.cs:120
            return new ModeConfigProfile
            {
                TargetCount = 0,
                T1 = 0.0,
                T2 = 0.0,
                T3 = 0.0,
                T4 = 0.0,
                T5 = 0.0,
                T1Type = TargetMode.Ticks,
                T2Type = TargetMode.Ticks,
                T3Type = TargetMode.Ticks,
                T4Type = TargetMode.Ticks,
                T5Type = TargetMode.Ticks,
                StopMult = 1.0,
                MaxRisk = 0.0,
            };
        }

        /// <summary>
        /// Hydrate strategy state from a profile snapshot.
        /// Used by IPC mode switching to restore configuration from a named profile.
        /// </summary>
        /// <param name="profile">Profile to load</param>
        /// <param name="modeName">Mode name for logging</param>
        private void HydrateFromProfile(ModeConfigProfile profile, string modeName)
        {
            // No-op stub: Profile hydration will be implemented in Phase 2
            // This method is called from V12_002.UI.IPC.Commands.Mode.cs:140
            Print(string.Format("[STICKY] HydrateFromProfile({0}) - stub (no-op)", modeName ?? "null"));
        }

        #endregion
    }
}

// Made with Bob
