// <copyright file="V12_002.UI.IPC.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
// V12.44 MODULAR: IPC Integration Module (Split from UI.cs)
// Contains: TCP IPC server, command dispatcher, remote signal handling
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region IPC Integration (V9.1.8)

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private const int IpcMaxBufferedChars = 8192;
        private const int IpcMaxCommandLength = 512;
        private const int IpcMaxQueueDepth = 2000;
        private const int IpcMaxCommandsPerDrain = 500;
        private const int IpcMaxOutboundMessagesPerClient = 128;
        private int ipcQueuedCommandCount = 0;
        private int _ipcClientIdSeed = 0;
        private int _ipcInvalidUtf8Count = 0;
        private int _ipcAllowlistRejectCount = 0;
        private int _ipcQueueDepthPeak = 0;

        private static readonly HashSet<string> AllowedIpcActions = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            "TRIM_25",
            "TRIM_50",
            "CONFIG",
            "SET_TRAIL",
            "SET_CIT",
            "LOCK_50",
            "BE",
            "BE_CUSTOM",
            "BE_PLUS_2",
            "BE_PLUS_1",
            "FLATTEN_ONLY",
            "FLATTEN",
            "CANCEL_ALL",
            "RESET_MEMORY",
            "LONG",
            "SHORT",
            "OR_LONG",
            "OR_SHORT",
            "SET_SIMA",
            "DIAG_FLEET",
            "SET_RMA_MODE",
            "SYNC_MODE",
            "SET_TARGETS",
            "MKT_SYNC",
            "SYNC_ALL",
            "SET_MODE",
            "SET_LEADER_ACCOUNT",
            "REQUEST_FLEET_STATE",
            "SET_MANUAL_PRICE",
            "TREND_MANUAL_LIMIT",
            "RETEST_MANUAL_LIMIT",
            "FFMA_MANUAL_LIMIT",
            "FFMA_MANUAL_MARKET",
            "FFMA_DISARM",
            "GET_LAYOUT",
            "DIAG_IPC",
        };

        // Phase 7 UI: Global IPC command registry for O(1) lookup (T-B: ticket-05)
        private static readonly HashSet<string> _globalIpcCommands = new HashSet<string>
        {
            "TOGGLE_ACCOUNT",
            "SET_SIMA",
            "GET_FLEET",
            "DIAG_FLEET",
            "CANCEL_ALL",
            "FLATTEN",
            "SYNC_ALL",
            "MKT_SYNC",
            "REQUEST_FLEET_STATE",
            "RESET_MEMORY",
            "DIAG_IPC",
            "LOCK_50",
            "SET_TARGETS",
            "SET_TRAIL",
            "SET_CIT",
            "BE_CUSTOM",
        };

        private static string ToIpcTargetMode(TargetMode mode)
        {
            return mode == TargetMode.Points ? "Points" : mode.ToString();
        }

        private static bool TryParseTargetMode(string raw, out TargetMode mode)
        {
            mode = TargetMode.ATR;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string normalized = raw.Trim().ToUpperInvariant();
            switch (normalized)
            {
                case "ATR":
                case "A":
                    mode = TargetMode.ATR;
                    return true;
                case "TICKS":
                case "TICK":
                case "T":
                    mode = TargetMode.Ticks;
                    return true;
                case "POINTS":
                case "POINT":
                case "PTS":
                case "P":
                    mode = TargetMode.Points;
                    return true;
                case "RUNNER":
                case "R":
                    mode = TargetMode.Runner;
                    return true;
                default:
                    return false;
            }
        }

        // FIX-A [Build 1102Z]: IPC Multiplier Validation Gate.
        // All multiplier values arriving over the TCP/IPC channel must pass this domain guard
        // before being written to strategy state. A negative or zero multiplier causes
        // CalculateTargetPrice to produce inverted prices (target on wrong side of entry).
        private static bool ValidateIpcMultiplier(double v, out string reason, double min = 0.01, double max = 50.0)
        {
            if (v < min)
            {
                reason = $"below minimum ({min})";
                return false;
            }
            if (v > max)
            {
                reason = $"exceeds maximum ({max})";
                return false;
            }
            reason = null;
            return true;
        }

        private bool TryEnqueueIpcCommand(string message, out string reason)
        {
            reason = null;
            if (message != null)
                message = message.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                reason = "empty command";
                return false;
            }

            if (message.Length > IpcMaxCommandLength)
            {
                reason = $"command exceeds {IpcMaxCommandLength} chars";
                return false;
            }

            int queueDepth = Interlocked.Increment(ref ipcQueuedCommandCount);
            if (queueDepth > IpcMaxQueueDepth)
            {
                Interlocked.Decrement(ref ipcQueuedCommandCount);
                reason = $"queue depth exceeded ({IpcMaxQueueDepth})";
                return false;
            }

            // Build 941 [FIX-4]: Track peak queue depth for DIAG_IPC telemetry.
            int peak = _ipcQueueDepthPeak;
            while (queueDepth > peak && Interlocked.CompareExchange(ref _ipcQueueDepthPeak, queueDepth, peak) != peak)
                peak = _ipcQueueDepthPeak;

            ipcCommandQueue.Enqueue(message);
            return true;
        }

        private bool IsAllowedIpcAction(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return false;

            if (AllowedIpcActions.Contains(action))
                return true;

            return action.StartsWith("MOVE_TARGET", StringComparison.OrdinalIgnoreCase)
                || action.StartsWith("CLOSE_T", StringComparison.OrdinalIgnoreCase)
                || action.StartsWith("GET_FLEET", StringComparison.OrdinalIgnoreCase)
                || action.StartsWith("SET_MAX_RISK", StringComparison.OrdinalIgnoreCase)
                || action.StartsWith("TOGGLE_ACCOUNT", StringComparison.OrdinalIgnoreCase)
                || action.StartsWith("SET_ANCHOR", StringComparison.OrdinalIgnoreCase)
                || action.StartsWith("MODE_", StringComparison.OrdinalIgnoreCase)
                || action.StartsWith("EXEC_", StringComparison.OrdinalIgnoreCase);
        }

        private List<Account> GetFleetAccountsSnapshot()
        {
            return Account
                .All.Where(a => IsFleetAccount(a))
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private Dictionary<string, string> BuildFleetAliasMap(List<Account> accounts)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (accounts == null)
                return map;
            for (int i = 0; i < accounts.Count; i++)
                map[accounts[i].Name] = "F" + (i + 1).ToString("D2");
            return map;
        }

        private string GetIpcFleetIdentity(string accountName, Dictionary<string, string> aliasMap)
        {
            if (IpcExposeSensitiveFleetIdentity || string.IsNullOrEmpty(accountName))
                return accountName;
            if (aliasMap != null && aliasMap.TryGetValue(accountName, out string alias))
                return alias;
            return "F00";
        }

        /// <summary>
        /// Build 935 [B935-P1]: Reverse alias resolver -- maps a UI alias (F01, F02...) or a raw
        /// account name back to the real broker account name. Returns null if the identity cannot
        /// be matched; callers MUST null-check the return value before passing to broker APIs.
        /// </summary>
        private string ResolveAccountName(string identity)
        {
            if (string.IsNullOrWhiteSpace(identity))
                return null;

            var accounts = GetFleetAccountsSnapshot();

            // Fast path: already a real account name
            var direct = accounts.FirstOrDefault(a =>
                string.Equals(a.Name, identity, StringComparison.OrdinalIgnoreCase)
            );
            if (direct != null)
                return direct.Name;

            // Reverse alias lookup: F01 -> real name via BuildFleetAliasMap
            var aliasMap = BuildFleetAliasMap(accounts);
            foreach (var kv in aliasMap)
            {
                if (string.Equals(kv.Value, identity, StringComparison.OrdinalIgnoreCase))
                    return kv.Key;
            }

            Print($"V12 IPC REJECT: ResolveAccountName could not resolve identity '{identity}'");
            return null;
        }

        private void ProcessIpcCommands()
        {
            if (_isTerminating)
            {
                if (ipcCommandQueue != null)
                {
                    while (ipcCommandQueue.TryDequeue(out string _)) { }
                }
                return;
            }
            if (ipcCommandQueue == null || ipcCommandQueue.IsEmpty)
                return;

            int drainedCount = 0;
            while (ProcessIpc_DrainOneCommand(ref drainedCount, out string command))
            {
                try
                {
                    if (!ProcessIpc_ParseAction(command, out string[] parts, out string action, out long senderTicks))
                        continue;

                    if (!MetadataGuardCommandTimestamp(senderTicks, action))
                        continue;

                    if (!ProcessIpc_ValidateAllowlist(action))
                        continue;

                    if (!ProcessIpc_MatchSymbol(action, parts))
                        continue;

                    ProcessIpc_EnqueueCore(action, parts, senderTicks);
                }
                catch (InvalidOperationException ex)
                {
                    // IPC command processing failed - non-critical
                    Print("Error ProcessIpcCommands: " + ex.Message);
                }
                catch (Exception ex)
                {
                    // Unexpected IPC processing error - log and fail fast
                    Print("CRITICAL ProcessIpcCommands: " + ex.Message);
                    throw;
                }
            }

            if (!ipcCommandQueue.IsEmpty)
            {
                try
                {
                    TriggerCustomEvent(o => ProcessIpcCommands(), null);
                }
                catch (Exception ex)
                {
                    // V12.EPIC-7-QUALITY-006: Log IPC command processing trigger failures
                    Interlocked.Increment(ref _ipcCleanupFailures);
                    Print($"[IPC_CLEANUP] Command processing trigger failed: {ex.Message}");
                    // Continue - non-fatal, commands remain queued for next cycle
                }
            }
        }

        private bool ProcessIpc_DrainOneCommand(ref int drainedCount, out string command)
        {
            command = null;
            if (drainedCount >= IpcMaxCommandsPerDrain)
                return false;

            if (!ipcCommandQueue.TryDequeue(out command))
                return false;

            if (Interlocked.Decrement(ref ipcQueuedCommandCount) < 0)
                Interlocked.Exchange(ref ipcQueuedCommandCount, 0);
            drainedCount++;
            return true;
        }

        private bool ProcessIpc_ParseAction(string command, out string[] parts, out string action, out long senderTicks)
        {
            parts = null;
            action = null;
            senderTicks = 0;

            if (string.IsNullOrWhiteSpace(command) || command.Length > IpcMaxCommandLength)
            {
                Print($"V12 IPC REJECT: malformed/oversize command '{command}'");
                return false;
            }

            parts = command.Split('|');
            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
            {
                Print($"V12 IPC REJECT: empty action in '{command}'");
                return false;
            }
            action = parts[0].Trim().ToUpperInvariant();
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].StartsWith("ts=", StringComparison.OrdinalIgnoreCase))
                {
                    long.TryParse(parts[i].Substring(3), out senderTicks);
                    break;
                }
            }

            return true;
        }

        private bool ProcessIpc_ValidateAllowlist(string action)
        {
            if (!IsAllowedIpcAction(action))
            {
                Interlocked.Increment(ref _ipcAllowlistRejectCount);
                Print($"V12 IPC REJECT: action '{action}' is not allowed");
                return false;
            }

            return true;
        }

        // Phase 7 UI: Symbol matching helper (T-B: ticket-05)
        // Extracted from ProcessIpc_MatchSymbol to reduce CYC 49 -> 3
        // CYC: 15 (acceptable variance from target 12)
        private bool IsSymbolMatch(string targetSymbol)
        {
            string mySym = Instrument.MasterInstrument.Name.ToUpperInvariant();
            string myFull = Instrument.FullName.ToUpperInvariant();
            string target = targetSymbol.Trim().ToUpperInvariant();

            return target == "GLOBAL"
                || target == "ALL"
                || target == "ON"
                || target == "OFF"
                || target == "RMA"
                || target == "ORB"
                || target == "OR"
                || target == "MOMO"
                || mySym == target
                || mySym.StartsWith(target)
                || target.StartsWith(mySym)
                || myFull.Contains(target)
                || (target == "MES" && mySym.Contains("ES"))
                || (target == "MYM" && mySym.Contains("YM"))
                || (target == "MGC" && mySym.Contains("GC"));
        }

        // Phase 7 UI: Residual dispatcher (T-B: ticket-05)
        // Refactored from CYC 49 -> 3 using Command Pattern
        // Uses _globalIpcCommands HashSet for O(1) lookup
        private bool ProcessIpc_MatchSymbol(string action, string[] parts)
        {
            string targetSymbol = parts.Length > 1 ? parts[1] : "Global";

            // Check global command set (O(1) lookup)
            bool isGlobalCommand = _globalIpcCommands.Contains(action) || action.StartsWith("MOVE_TARGET");

            // Symbol matching logic (extracted to helper)
            bool isForMe = isGlobalCommand || IsSymbolMatch(targetSymbol);

            // V12.2: Global IPC Diagnostic Log (format preserved for log parsing)
            Print(
                string.Format(
                    "V12 IPC: Received '{0}' for '{1}'. For Me? {2} (My Symbol: {3}){4}",
                    action,
                    targetSymbol,
                    isForMe,
                    Instrument.MasterInstrument.Name,
                    isGlobalCommand ? " [GLOBAL CMD]" : ""
                )
            );

            return isForMe;
        }

        private void ProcessIpc_EnqueueCore(string action, string[] parts, long senderTicks)
        {
            string queuedAction = action;
            string[] queuedParts = parts;
            long queuedSenderTicks = senderTicks;
            Enqueue(ctx => ctx.ProcessIpcCommandCore(queuedAction, queuedParts, queuedSenderTicks));
        }

        private void ProcessIpcCommandCore(string action, string[] parts, long senderTicks)
        {
            try
            {
                Print(
                    string.Format(
                        "{0:HH:mm:ss} | IPC Executing {1} for {2}",
                        DateTime.UtcNow,
                        action,
                        Instrument.MasterInstrument.Name
                    )
                );

                // EPIC-4 Ticket 03: IPC Hardening validation layer
                ValidationResult validationResult = ValidateIpcCommand(action, parts);

                switch (validationResult)
                {
                    case ValidationResult.Valid:
                        // Proceed with command execution
                        break;

                    case ValidationResult.InvalidSyntax:
                        Print(string.Format("[IPC] Invalid syntax: {0}", action));
                        return;

                    case ValidationResult.RateLimitExceeded:
                        SendBackpressureNack(action);
                        return;

                    case ValidationResult.CircuitBreakerOpen:
                        Print("[IPC] Circuit breaker OPEN - command rejected");
                        return;

                    case ValidationResult.AllowlistBypass:
                        Print(string.Format("[IPC] Security violation: {0}", action));
                        // TODO: Disconnect client (Phase 5)
                        return;

                    default:
                        Print(string.Format("[IPC] Unknown validation result: {0}", validationResult));
                        return;
                }

                // Build 942 [FIX-2]: Diag commands handled here; removes 2 branches from chain below (CS-R1140)
                if (TryHandleDiagCommand(action, parts))
                    return;

                // Build 943: Sub-handler routing -- CS-R1140 complexity reduction
                if (TryHandleModeCommand(action, parts))
                    return;
                if (TryHandleRiskCommand(action, parts))
                    return;
                if (TryHandleFleetCommand(action, parts, senderTicks))
                    return;
                if (TryHandleConfigCommand(action, parts))
                    return;
                if (TryHandleComplianceCommand(action, parts))
                    return;
                Print(
                    string.Format(
                        "[IPC] WARNING: Unhandled IPC action '{0}' -- parts: {1}",
                        action,
                        parts != null ? string.Join("|", parts) : "<none>"
                    )
                );
            }
            catch (InvalidOperationException ex)
            {
                // IPC command core processing failed - non-critical
                Print("Error ProcessIpcCommandCore: " + ex.Message);
            }
            catch (Exception ex)
            {
                // Unexpected IPC core error - log and fail fast
                Print("CRITICAL ProcessIpcCommandCore: " + ex.Message);
                throw;
            }
        }

        // Build 935 [B935-P2]: Extracted IPC sub-handlers

        /// <summary>
        /// Get current photon dispatch ring depth for monitoring.
        /// </summary>
        internal int GetPhotonDispatchRingDepth()
        {
            return _photonDispatchRing?.Count ?? 0;
        }

        /// <summary>
        /// Handles TRIM_25 / TRIM_50 -- partial position close by percentage.
        /// </summary>
        #endregion
    }
}
