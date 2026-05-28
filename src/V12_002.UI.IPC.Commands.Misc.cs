// Build 971: UI.IPC.Commands.Misc -- TryHandleConfigCommand, TryHandleComplianceCommand, HandleFleetCommand, SendResponseToRemote, FlattenSpecificTarget, ToggleStrategyMode
// V12 UI.IPC Module (Extracted)
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
        #region IPC Commands Misc

        private bool TryHandleConfigCommand(string action, string[] parts)
        {
            if (action == "CONFIG")
            {
                HandleConfigCommand(parts);
                return true;
            }
            // V12: GET_LAYOUT handler (primary response is in ListenForRemote, this is fallback logging)
            if (action == "GET_LAYOUT")
            {
                string mode = GetCurrentConfigMode();
                Print(
                    string.Format(
                        "V12 GET_LAYOUT: Mode={0} Count={1} T1={2}({3}) T2={4}({5}) T3={6}({7}) T4={8}({9}) T5={10}({11})",
                        mode,
                        activeTargetCount,
                        Target1Value,
                        T1Type,
                        Target2Value,
                        T2Type,
                        Target3Value,
                        T3Type,
                        Target4Value,
                        T4Type,
                        Target5Value,
                        T5Type
                    )
                );
                return true;
            }
            return false;
        }

        /// <summary>
        /// Stub for future compliance commands (GET_COMPLIANCE, etc.).
        /// Build 943: Established per router architecture.
        /// </summary>
        private bool TryHandleComplianceCommand(string action, string[] parts)
        {
            return false;
        }

        /// <summary>
        /// Handles fleet-level commands: GET_FLEET, SET_SIMA, DIAG_FLEET,
        /// SET_LEADER_ACCOUNT, REQUEST_FLEET_STATE.
        /// </summary>
        private void HandleFleetCommand(string action, string[] parts)
        {
            if (HandleFleet_GetFleet(action))
                return;
            if (HandleFleet_SetSima(action, parts))
                return;
            if (HandleFleet_DiagFleet(action))
                return;
            if (HandleFleet_SetLeader(action, parts))
                return;
            HandleFleet_RequestFleetState(action);
        }

        private bool HandleFleet_GetFleet(string action)
        {
            if (!action.StartsWith("GET_FLEET", StringComparison.OrdinalIgnoreCase))
                return false;

            var fleetAccounts = GetFleetAccountsSnapshot();
            var aliasMap = BuildFleetAliasMap(fleetAccounts);
            StringBuilder sb = new StringBuilder("CONFIG|FLEET");
            sb.Append("|COUNT:").Append(fleetAccounts.Count);
            foreach (var acct in fleetAccounts)
                sb.Append('|').Append(GetIpcFleetIdentity(acct.Name, aliasMap));
            SendResponseToRemote(sb.ToString());
            Print("[SIMA] GET_FLEET -> Responded with account list");
            return true;
        }

        private bool HandleFleet_SetSima(string action, string[] parts)
        {
            if (action != "SET_SIMA")
                return false;

            if (parts.Length > 1)
            {
                bool enable = parts[1].Trim().ToUpperInvariant() == "ON";
                ApplySimaState(enable);
                Print($"V12.Phase6: SET_SIMA = {enable} (lifecycle applied)");
            }

            return true;
        }

        private bool HandleFleet_DiagFleet(string action)
        {
            if (action != "DIAG_FLEET")
                return false;

            // T-Q1: Toggle catch logging flag
            _diagFleet = !_diagFleet;
            Print("[DIAG_FLEET] Catch logging: " + (_diagFleet ? "ENABLED" : "DISABLED"));

            Print("[DIAG] ##################################################");
            Print($"[DIAG] EnableSIMA = {EnableSIMA}");
            Print($"[DIAG] AccountPrefix = \"{AccountPrefix}\"");
            int total = 0;
            int active = 0;
            foreach (Account acct in Account.All)
            {
                if (IsFleetAccount(acct))
                {
                    total++;
                    bool isActive = false;
                    activeFleetAccounts.TryGetValue(acct.Name, out isActive);
                    if (isActive)
                        active++;
                    Print($"[DIAG]   {acct.Name} -> {(isActive ? "? ACTIVE" : "[X] INACTIVE")}");
                }
            }
            Print($"[DIAG] TOTAL: {total} accounts | {active} ACTIVE");
            Print("[DIAG] ##################################################");
            return true;
        }

        private bool HandleFleet_SetLeader(string action, string[] parts)
        {
            if (action != "SET_LEADER_ACCOUNT")
                return false;

            if (parts.Length > 1)
            {
                string newLeader = parts[1].Trim();
                _stickyLeaderAccount = newLeader; // Build 1103: Store for persistence
                Print($"V12.25 IPC: Leader Account synced to [{newLeader}]");
                MarkStickyDirty(); // Build 1103: Persist leader
            }

            return true;
        }

        private void HandleFleet_RequestFleetState(string action)
        {
            if (action != "REQUEST_FLEET_STATE")
                return;

            StringBuilder fsb = new StringBuilder("FLEET_STATE|");
            fsb.Append(Instrument.FullName).Append("|");
            fsb.Append(Position.MarketPosition).Append("|");

            var fleetAccounts = GetFleetAccountsSnapshot();
            var aliasMap = BuildFleetAliasMap(fleetAccounts);
            List<string> acctStates = new List<string>();
            foreach (Account acct in fleetAccounts)
            {
                var bPos = acct.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
                int act = 0;
                if (bPos != null && bPos.MarketPosition != MarketPosition.Flat)
                {
                    act = (bPos.MarketPosition == MarketPosition.Long) ? (int)bPos.Quantity : -(int)bPos.Quantity;
                }
                int exp = 0;
                expectedPositions?.TryGetValue(ExpKey(acct.Name), out exp);
                acctStates.Add($"{GetIpcFleetIdentity(acct.Name, aliasMap)}:{act}:{exp}");
            }
            fsb.Append(string.Join(";", acctStates));
            SendResponseToRemote(fsb.ToString());
            if (!string.IsNullOrEmpty(_stickyLeaderAccount))
                SendResponseToRemote("SET_LEADER_ACCOUNT|" + _stickyLeaderAccount);
        }

        // =========================================================================

        private void SendResponseToRemote(string response)
        {
            if (connectedClients == null)
                return;

            // Diagnostic: Log what we are sending and to how many clients
            if (response.Contains("SYNC_TARGET_STATE"))
                Print($"V14 IPC: Broadcasting SYNC_TARGET_STATE to {connectedClients.Count} clients");

            byte[] responseBytes = Encoding.UTF8.GetBytes(response + "\n");
            List<int> disconnectedClientIds = new List<int>();

            foreach (var kvp in connectedClients.ToArray())
            {
                int clientId = kvp.Key;
                IpcClientSession session = kvp.Value;
                try
                {
                    if (session.Client.Connected && session.Stream.CanWrite)
                    {
                        session.Stream.Write(responseBytes, 0, responseBytes.Length);
                        session.Stream.Flush();
                    }
                    else
                    {
                        disconnectedClientIds.Add(clientId);
                    }
                }
                catch (System.IO.IOException ex)
                {
                    // Network I/O error - client disconnected
                    Print($"V14 IPC: Send Error - {ex.Message}");
                    disconnectedClientIds.Add(clientId);
                }
                catch (Exception ex)
                {
                    // Unexpected IPC error - log and fail fast
                    Print($"V14 IPC: CRITICAL Send Error - {ex.Message}");
                    disconnectedClientIds.Add(clientId);
                    throw;
                }
            }

            foreach (int clientId in disconnectedClientIds)
            {
                if (connectedClients.TryRemove(clientId, out var staleClient))
                {
                    // V12.EPIC-7-QUALITY-006: Explicit stale client cleanup
                    try
                    {
                        staleClient.Client.Close();
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref _ipcCleanupFailures);
                        Print($"[IPC_CLEANUP] Stale client close failed [id={clientId}]: {ex.Message}");
                        // Continue cleanup - non-fatal
                    }
                }
            }
        }

        // V12.13-D: SendToExternalApp REMOVED -- it connected to port 5001 (the strategy's own listener),
        // causing infinite flood loops. All callers now use SendResponseToRemote() or direct client stream writes.
        // V12.44: MoveStopsToBreakevenPlusOne() removed -- dead code, replaced by MoveStopsToBreakevenWithOffset()

        /// <summary>
        /// V10.3: Close a specific target (T1..T5) at market for all active positions
        /// Cancels working limit order and submits market order to close
        /// </summary>
        private void FlattenSpecificTarget(int targetNumber)
        {
            try
            {
                foreach (var kvp in activePositions.ToArray())
                {
                    if (!activePositions.ContainsKey(kvp.Key))
                        continue;
                    PositionInfo pos = kvp.Value;
                    string entryName = kvp.Key;

                    if (!pos.EntryFilled || pos.RemainingContracts <= 0)
                        continue;

                    if (
                        !FlattenSpecificTarget_ResolveTarget(
                            targetNumber,
                            pos,
                            out int qtyToClose,
                            out ConcurrentDictionary<string, Order> targetDict,
                            out string targetName
                        )
                    )
                        return;

                    if (qtyToClose <= 0)
                    {
                        Print(string.Format("V10.3: {0} has no contracts to close for {1}", targetName, entryName));
                        continue;
                    }

                    FlattenSpecificTarget_CancelLimit(entryName, pos, targetName, targetDict);

                    // Build 1108.003 [D1]: Pre-cancel stop when closing the entire remaining position.
                    // Without this, follower accounts can have a working stop + market exit simultaneously.
                    if (qtyToClose >= pos.RemainingContracts)
                        FlattenSpecificTarget_RequestStopCancel(entryName);

                    FlattenSpecificTarget_SubmitMarketExit(entryName, pos, qtyToClose, targetName);
                }
            }
            catch (InvalidOperationException ex)
            {
                // Flatten operation failed - non-critical
                Print("ERROR FlattenSpecificTarget: " + ex.Message);
            }
            catch (Exception ex)
            {
                // Unexpected error in flatten - log and fail fast
                Print("CRITICAL FlattenSpecificTarget: " + ex.Message);
                throw;
            }
        }

        private bool FlattenSpecificTarget_ResolveTarget(
            int targetNumber,
            PositionInfo pos,
            out int qtyToClose,
            out ConcurrentDictionary<string, Order> targetDict,
            out string targetName
        )
        {
            qtyToClose = 0;
            targetDict = null;
            targetName = "";

            switch (targetNumber)
            {
                case 1:
                    qtyToClose = pos.T1Contracts;
                    targetDict = target1Orders;
                    targetName = "T1";
                    return true;
                case 2:
                    qtyToClose = pos.T2Contracts;
                    targetDict = target2Orders;
                    targetName = "T2";
                    return true;
                case 3:
                    qtyToClose = pos.T3Contracts;
                    targetDict = target3Orders;
                    targetName = "T3";
                    return true;
                case 4:
                    qtyToClose = pos.T4Contracts;
                    targetDict = target4Orders;
                    targetName = "T4";
                    return true;
                case 5:
                    qtyToClose = pos.T5Contracts;
                    targetDict = target5Orders;
                    targetName = "T5";
                    return true;
                default:
                    Print(string.Format("V10.3: Invalid target number {0}", targetNumber));
                    return false;
            }
        }

        private void FlattenSpecificTarget_CancelLimit(
            string entryName,
            PositionInfo pos,
            string targetName,
            ConcurrentDictionary<string, Order> targetDict
        )
        {
            // Cancel existing limit order if working
            if (targetDict != null && targetDict.TryGetValue(entryName, out Order targetOrder))
            {
                if (
                    targetOrder != null
                    && (
                        targetOrder.OrderState == OrderState.Working
                        || targetOrder.OrderState == OrderState.Accepted
                        || targetOrder.OrderState == OrderState.Submitted
                    )
                )
                {
                    CancelOrderSafe(targetOrder, pos);
                    Print(string.Format("V10.3: Cancelled {0} limit order for {1}", targetName, entryName));
                }
            }
        }

        private void FlattenSpecificTarget_RequestStopCancel(string entryName)
        {
            RequestStopCancelLifecycleSafe(entryName);
            Print(string.Format("V10.3: Full close -- requested stop cancel for {0}", entryName));
        }

        private void FlattenSpecificTarget_SubmitMarketExit(
            string entryName,
            PositionInfo pos,
            int qtyToClose,
            string targetName
        )
        {
            // Submit market order to close the target contracts
            Order closeOrder = SubmitExitOrderForPosition(
                pos,
                qtyToClose,
                OrderType.Market,
                0,
                string.Format("Close{0}_{1}", targetName, entryName)
            );
            if (closeOrder != null)
                Print(
                    string.Format(
                        "V10.3: Closing {0} ({1} contracts) at market for {2}",
                        targetName,
                        qtyToClose,
                        entryName
                    )
                );
            else
                Print(
                    string.Format(
                        "V10.3: FAILED to close {0} ({1} contracts) at market for {2}",
                        targetName,
                        qtyToClose,
                        entryName
                    )
                );
        }

        private void ToggleStrategyMode(string action)
        {
            ToggleStrategyMode_SetFlags(action);
            ToggleStrategyMode_ExecuteModeAction(action);
            ToggleStrategyMode_PublishSnapshot(action);
        }

        private void ToggleStrategyMode_SetFlags(string action)
        {
            // MP0: Dictionary dispatch (CYC=2)
            if (_modeSetFlagsDispatch != null && _modeSetFlagsDispatch.TryGetValue(action, out Action handler))
            {
                handler();
            }
        }

        private void ToggleStrategyMode_ExecuteModeAction(string action)
        {
            // MP0: Dictionary dispatch (CYC=2)
            if (_modeExecDispatch != null && _modeExecDispatch.TryGetValue(action, out Action handler))
            {
                handler();
            }
        }

        private void ToggleStrategyMode_PublishSnapshot(string action)
        {
            if (action == "MODE_RMA" || action == "MODE_MOMO" || action == "MODE_FFMA" || action == "FFMA_DISARM")
            {
                BumpUiConfigRevision();
            }

            PublishUiSnapshot();

            Print(
                string.Format(
                    "IPC Mode Toggle: {0} | RMA={1} MOMO={2} TrendRMA={3} RetestRMA={4} FFMA={5}",
                    action,
                    isRMAModeActive,
                    isMOMOModeActive,
                    isTrendRmaMode,
                    isRetestRmaMode,
                    isFFMAModeArmed
                )
            );
        }

        #endregion
    }
}
