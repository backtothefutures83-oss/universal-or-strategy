// V12.44 MODULAR: IPC Integration Module (Split from UI.cs)
// Contains: TCP IPC server, command dispatcher, remote signal handling
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;
using System.Net;
using System.Net.Sockets;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class UniversalORStrategyV12_002_Dev : Strategy
    {
        #region IPC Integration (V9.1.8)

        private void StartIpcServer()
        {
            if (isIpcRunning) return;

            try
            {
                StopIpcServer(); // Ensure clean start

                isIpcRunning = true;
                ipcCommandQueue = new ConcurrentQueue<string>();

                // V12.2: Multi-Client Support
                connectedClients = new ConcurrentBag<TcpClient>();

                ipcThread = new Thread(ListenForRemote);
                ipcThread.IsBackground = true;
                ipcThread.Name = "V10_IPC_Server";
                ipcThread.Start();

                Print(string.Format("IPC SERVER SUCCESS: Listening on Port {0} (Multi-Client)", IpcPort));
            }
            catch (Exception ex)
            {
                Print("ERROR StartIpcServer: " + ex.Message);
            }
        }

        private void ListenForRemote()
        {
            try
            {
                ipcListener = new TcpListener(IPAddress.Any, IpcPort);
                ipcListener.Start();

                while (isIpcRunning)
                {
                    if (!ipcListener.Pending())
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    // Accept new client
                    TcpClient client = ipcListener.AcceptTcpClient();
                    connectedClients.Add(client);
                    Print("V12 IPC: New Client Connected");

                    // V12.13-D: Send REQUEST_FLEET_STATE directly to the newly connected client
                    // (Previously called SendToExternalApp which connected back to port 5001 = self, causing infinite flood loop)
                    try
                    {
                        byte[] reqBytes = Encoding.UTF8.GetBytes("REQUEST_FLEET_STATE|ALL\n");
                        NetworkStream ns = client.GetStream();
                        ns.Write(reqBytes, 0, reqBytes.Length);
                        ns.Flush();
                        Print("V12 IPC: Sent REQUEST_FLEET_STATE to new client");
                    }
                    catch (Exception ex)
                    {
                        Print("V12 IPC: Failed to send fleet state request: " + ex.Message);
                    }

                    // Handle client in a separate task
                    Task.Run(() => HandleClient(client));
                }
            }
            catch (Exception)
            {
                isIpcRunning = false;
                Print("[V12.2] IPC Listener Status: Stopped/Error");
            }
            finally
            {
                if (ipcListener != null)
                {
                    try { ipcListener.Stop(); } catch { }
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                using (NetworkStream stream = client.GetStream())
                {
                    System.Text.StringBuilder lineBuffer = new System.Text.StringBuilder();
                    byte[] buffer = new byte[4096];

                    while (isIpcRunning && client.Connected)
                    {
                         if (!stream.DataAvailable)
                        {
                            Thread.Sleep(50);
                            continue;
                        }

                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break; // Disconnected

                        string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        lineBuffer.Append(chunk);

                        string accumulated = lineBuffer.ToString();
                        int lastNewline = accumulated.LastIndexOf('\n');
                        if (lastNewline < 0) continue;

                        string completeLines = accumulated.Substring(0, lastNewline);
                        lineBuffer.Clear();
                        if (lastNewline + 1 < accumulated.Length)
                        {
                            lineBuffer.Append(accumulated.Substring(lastNewline + 1));
                        }

                        string[] commands = completeLines.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string rawCmd in commands)
                        {
                            string message = rawCmd.Trim();
                            if (string.IsNullOrEmpty(message)) continue;

                            // Handle GET_LAYOUT (Synchronous Response to THIS client only)
                            if (message.StartsWith("GET_LAYOUT"))
                            {
                                // V12.Hardening: Read strategy state under stateLock to prevent data race with strategy thread
                                string configResponse;
                                lock (stateLock)
                                {
                                    string mode = isRMAModeActive ? "RMA" : "OR";
                                    double stopValue = isRMAModeActive ? RMAStopATRMultiplier : StopMultiplier;
                                    configResponse = string.Format(
                                        "CONFIG|{0}|COUNT:{1};T1:{2};T1TYPE:{3};T2:{4};T2TYPE:{5};T3:{6};T3TYPE:{7};T4:{8};T4TYPE:{9};T5:{10};T5TYPE:{11};STR:{12};STRTYPE:ATR;MAX:{13};CIT:{14};OT:Limit;TRMA:{15};RRMA:{16};\n",
                                        mode, activeTargetCount, Target1Value, ToIpcTargetMode(T1Type),
                                        Target2Value, ToIpcTargetMode(T2Type),
                                        Target3Value, ToIpcTargetMode(T3Type),
                                        Target4Value, ToIpcTargetMode(T4Type),
                                        Target5Value, ToIpcTargetMode(T5Type),
                                        stopValue, MaxRiskAmount, ChaseIfTouchPoints ?? "0",
                                        isTrendRmaMode ? "1" : "0", isRetestRmaMode ? "1" : "0");
                                }
                                byte[] responseBytes = Encoding.UTF8.GetBytes(configResponse);
                                stream.Write(responseBytes, 0, responseBytes.Length);
                                stream.Flush();
                                continue;
                            }

                            // Enqueue for processing
                            ipcCommandQueue.Enqueue(message);
                            Print(string.Format("V12.1 IPC ENQUEUE: {0}", message));

                            // Trigger processing
                            try
                            {
                                TriggerCustomEvent(o => ProcessIpcCommands(), null);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Print("V12 IPC Client Error: " + ex.Message);
            }
            finally
            {
                // Remove client from bag (rebuild bag exclusion) - ConcurrentBag doesn't have Remove,
                // effectively we just let it connect/disconnect. The SendResponse will handle dead clients.
                Print("V12 IPC: Client Disconnected");
                client.Close();
            }
        }

        private void StopIpcServer()
        {
            try
            {
                isIpcRunning = false;
                if (ipcListener != null)
                {
                    ipcListener.Stop();
                    ipcListener = null;
                }
                if (ipcThread != null && ipcThread.IsAlive)
                {
                    ipcThread.Join(500);
                }
            }
            catch { }
        }

        private static string ToIpcTargetMode(TargetMode mode)
        {
            return mode == TargetMode.Points ? "Points" : mode.ToString();
        }

        private static bool TryParseTargetMode(string raw, out TargetMode mode)
        {
            mode = TargetMode.ATR;
            if (string.IsNullOrWhiteSpace(raw)) return false;

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
        private static bool ValidateIpcMultiplier(double v, out string reason,
            double min = 0.01, double max = 50.0)
        {
            if (v < min) { reason = $"below minimum ({min})"; return false; }
            if (v > max) { reason = $"exceeds maximum ({max})"; return false; }
            reason = null;
            return true;
        }

        private void HandleExternalSignal(object sender, SignalBroadcaster.ExternalCommandSignal e)
        {
            // V10.3: Only non-winners (secondary charts) need to handle the broadcast
            // The port winner already enqueued the message locally in ListenForRemote
            if (ipcCommandQueue != null && !isIpcRunning)
            {
                Print(string.Format("V10.3 DEBUG: {0} received broadcast: {1}", Instrument.MasterInstrument.Name, e.Command));
                ipcCommandQueue.Enqueue(e.Command);

                // Force instant processing for secondary charts (so they don't wait for a tick)
                try { TriggerCustomEvent(o => ProcessIpcCommands(), null); } catch { }
            }
        }

        private void ProcessIpcCommands()
        {
            if (ipcCommandQueue == null || ipcCommandQueue.IsEmpty) return;

            while (ipcCommandQueue.TryDequeue(out string command))
            {
                try
                {
                    string[] parts = command.Split('|');
                    string action = parts[0];
                    string targetSymbol = parts.Length > 1 ? parts[1] : "Global";

                    // V12.9: Global commands bypass symbol filter entirely — these are account/fleet-level, not instrument-level
                    // [1102Z-F] MOVE_TARGET and LOCK_50 use parts[1] for parameters (not symbol), so they must bypass
                    // the symbol filter. Each handler internally filters by activePositions so only charts with live
                    // positions act. This is the correct fix for the "For Me? False [target=T1]" rejection.
                    bool isGlobalCommand = action == "TOGGLE_ACCOUNT" || action == "SET_SIMA" ||
                                           action == "GET_FLEET" || action == "DIAG_FLEET" || action == "CANCEL_ALL" ||
                                           action == "FLATTEN" || action == "SYNC_ALL" || action == "MKT_SYNC" ||
                                           action == "REQUEST_FLEET_STATE" || action == "RESET_MEMORY" ||
                                           action.StartsWith("MOVE_TARGET") || action == "LOCK_50"; // [1102Z-F]

                    // V10.3: Robust Symbol Matching (Matches MGC to GC/MGC, MES to ES/MES, etc.)
                    string mySym = Instrument.MasterInstrument.Name.ToUpper();
                    string myFull = Instrument.FullName.ToUpper();
                    string target = targetSymbol.Trim().ToUpper();

                    bool isForMe = isGlobalCommand ||  // V12.9: SIMA/Fleet commands always pass through
                                   target == "GLOBAL" ||
                                   target == "ALL" ||  // V12.13: Universal broadcast target (FLATTEN|ALL, REQUEST_FLEET_STATE|ALL)
                                   target == "ON" || target == "OFF" ||  // V12.4: Mode toggle commands (SET_RMA_MODE|ON)
                                   target == "RMA" || target == "ORB" || target == "OR" || target == "MOMO" || // V12.6: Mode-switch keywords are global
                                   mySym == target ||
                                   mySym.StartsWith(target) || // "MES" matches "MES 03-26"
                                   target.StartsWith(mySym) || // "GC" matches "GC/MGC"
                                   myFull.Contains(target) ||
                                   (target == "MES" && mySym.Contains("ES")) || // Robustness for MES/ES
                                   (target == "MYM" && mySym.Contains("YM")) || // Robustness for MYM/YM
                                   (target == "MGC" && mySym.Contains("GC"));   // Robustness for MGC/GC

                    // V12.2: Global IPC Diagnostic Log
                    Print(string.Format("V12 IPC: Received '{0}' for '{1}'. For Me? {2} (My Symbol: {3}){4}",
                        action, target, isForMe, mySym, isGlobalCommand ? " [GLOBAL CMD]" : ""));

                    if (!isForMe)
                    {
                        // Quiet ignore if it's clearly for another instrument
                        continue;
                    }

                    Print(string.Format("{0:HH:mm:ss} | IPC Executing {1} for {2}", DateTime.Now, action, Instrument.MasterInstrument.Name));

            if (action == "TRIM_25" || action == "TRIM_50")
            {
                double percent = action == "TRIM_50" ? 0.5 : 0.25;
                // V12.1101E [A-3/SK-02]: Snapshot .Values before iterating.
                // [1102Z-F]: TRIM now routes to pos.ExecutingAccount for fleet followers.
                foreach (var pos in activePositions.Values.ToArray())
                {
                    if (pos.RemainingContracts > 1)
                    {
                        // V10.3.1 FIX: Math.Max(1, ...) ensures we always trim at least 1 contract.
                        int rawQty = Math.Max(1, (int)Math.Floor(pos.RemainingContracts * percent));
                        int remainingAfterTrim = pos.RemainingContracts - rawQty;

                        // Safety: never flatten via trim
                        if (remainingAfterTrim < 1)
                            rawQty = pos.RemainingContracts - 1;

                        if (rawQty >= 1 && (pos.RemainingContracts - rawQty) >= 1)
                        {
                            OrderAction trimAction = pos.Direction == MarketPosition.Long
                                ? OrderAction.Sell : OrderAction.BuyToCover;

                            // [1102Z-F]: Route to fleet follower account when applicable
                            if (EnableSIMA && pos.IsFollower && pos.ExecutingAccount != null)
                            {
                                string trimSig = "Trim_" + pos.SignalName;
                                if (trimSig.Length > 50) trimSig = trimSig.Substring(0, 50);
                                Order trimOrder = pos.ExecutingAccount.CreateOrder(
                                    Instrument, trimAction, OrderType.Market, TimeInForce.Gtc,
                                    rawQty, 0, 0, "", trimSig, null);
                                pos.ExecutingAccount.Submit(new[] { trimOrder });
                                Print(string.Format("[SIMA] TRIM {0}%: Follower {1} → {2} closing {3} contracts",
                                    (int)(percent * 100), pos.SignalName, pos.ExecutingAccount.Name, rawQty));
                            }
                            else
                            {
                                Print(string.Format("IPC Trim: Closing {0} of {1} contracts for {2} ({3:P0})",
                                    rawQty, pos.RemainingContracts, pos.SignalName, percent));

                                if (pos.Direction == MarketPosition.Long)
                                    SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, rawQty, 0, 0, "", "Trim_" + pos.SignalName);
                                else
                                    SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, rawQty, 0, 0, "", "Trim_" + pos.SignalName);
                            }
                        }
                        else
                        {
                            Print(string.Format("IPC Trim SKIPPED: {0} contracts for {1} - cannot satisfy {2:P0} trim with 1+ remaining",
                                pos.RemainingContracts, pos.SignalName, percent));
                        }
                    }
                    else
                    {
                        Print(string.Format("IPC Trim SKIPPED: {0} has only 1 contract - use FLATTEN to close", pos.SignalName));
                    }
                }
            }
                    else if (action == "CONFIG")
                    {
                        // V12 PRO: Parse the full config sync from side panel
                        // Format: CONFIG|Mode|COUNT:3;T1:1.0;T1TYPE:Points;T2:0.5;T2TYPE:ATR;...
                        if (parts.Length > 2)
                        {
                            string configMode = parts[1];
                            string configContent = parts[2];
                            string[] settingsItems = configContent.Split(';');
                            foreach (string setting in settingsItems)
                            {
                                if (string.IsNullOrEmpty(setting)) continue;
                                string[] kv = setting.Split(':');
                                if (kv.Length < 2) continue;
                                string key = kv[0].ToUpper();
                                string val = kv[1];

                                if (key == "T1") { if (double.TryParse(val, out double v)) Target1Value = v; }
                                else if (key == "CIT") { ChaseIfTouchPoints = val; }
                                else if (key == "T2") {
                                    if (double.TryParse(val, out double v)) {
                                        string vmReason;
                                        if (!ValidateIpcMultiplier(v, out vmReason))
                                            Print($"[IPC REJECT] T2 value {v} rejected: {vmReason}");
                                        else Target2Value = v;
                                    }
                                }
                                else if (key == "T3") {
                                    if (double.TryParse(val, out double v)) {
                                        string vmReason;
                                        if (!ValidateIpcMultiplier(v, out vmReason))
                                            Print($"[IPC REJECT] T3 value {v} rejected: {vmReason}");
                                        else Target3Value = v;
                                    }
                                }
                                else if (key == "T4")
                                {
                                    if (double.TryParse(val, out double v)) {
                                        string vmReason;
                                        if (!ValidateIpcMultiplier(v, out vmReason))
                                            Print($"[IPC REJECT] T4 value {v} rejected: {vmReason}");
                                        else Target4Value = v;
                                    }
                                }
                                else if (key == "T5")
                                {
                                    if (double.TryParse(val, out double v)) {
                                        string vmReason;
                                        if (!ValidateIpcMultiplier(v, out vmReason))
                                            Print($"[IPC REJECT] T5 value {v} rejected: {vmReason}");
                                        else Target5Value = v;
                                    }
                                }
                                else if (key == "T1TYPE")
                                {
                                    if (TryParseTargetMode(val, out var parsed)) T1Type = parsed;
                                }
                                else if (key == "T2TYPE")
                                {
                                    if (TryParseTargetMode(val, out var parsed)) T2Type = parsed;
                                }
                                else if (key == "T3TYPE")
                                {
                                    if (TryParseTargetMode(val, out var parsed)) T3Type = parsed;
                                }
                                else if (key == "T4TYPE")
                                {
                                    if (TryParseTargetMode(val, out var parsed)) T4Type = parsed;
                                }
                                else if (key == "T5TYPE")
                                {
                                    if (TryParseTargetMode(val, out var parsed)) T5Type = parsed;
                                }
                                else if (key == "STR") {
                                    if (double.TryParse(val, out double v)) {
                                        string vmReason;
                                        if (!ValidateIpcMultiplier(v, out vmReason))
                                            Print($"[IPC REJECT] STR multiplier {v} rejected: {vmReason}");
                                        else if (configMode == "RMA") RMAStopATRMultiplier = v; else StopMultiplier = v;
                                    }
                                }
                                else if (key == "MAX") {
                                    if (double.TryParse(val, out double v)) {
                                        MaxRiskAmount = v;
                                        RiskPerTrade = v;
                                    }
                                }
                                else if (key == "COUNT") {
                                    if (int.TryParse(val, out int v)) {
                                        // FIX-B [Build 1102Z]: Clamp + lock to prevent IPC race with SIMA dispatch loop.
                                        int clamped = Math.Max(1, Math.Min(5, v));
                                        lock (stateLock) { activeTargetCount = clamped; }
                                    }
                                }
                                else if (key == "TRMA") { isTrendRmaMode = (val == "1"); }
                                else if (key == "RRMA") { isRetestRmaMode = (val == "1"); }
                            }
                            Print(string.Format("[V12] Sync All CONFIG ({0}) Applied: {1}", configMode, configContent));
                        }
                    }
                    else if (action == "SET_TRAIL")
                    {
                        // V12 PRO: Dynamic trail - move stop to current price +/- distance
                        if (parts.Length >= 2 && double.TryParse(parts[1], out double trailDistance))
                        {
                            if (activePositions.Count == 0)
                            {
                                Print("[V12] SET_TRAIL: No active positions");
                            }
                            else
                            {
                                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                                int trailCount = 0;

                                foreach (var kvp in activePositions.ToArray())
                                {
                                    if (!activePositions.ContainsKey(kvp.Key)) continue;
                                    PositionInfo pos = kvp.Value;
                                    string entryName = kvp.Key;

                                    if (!pos.EntryFilled) continue;

                                    // Calculate new stop: Longs = Price - Distance, Shorts = Price + Distance
                                    double newStopPrice = pos.Direction == MarketPosition.Long
                                        ? currentPrice - trailDistance
                                        : currentPrice + trailDistance;

                                    newStopPrice = Instrument.MasterInstrument.RoundToTickSize(newStopPrice);
                                    UpdateStopOrder(entryName, pos, newStopPrice, pos.CurrentTrailLevel);
                                    trailCount++;
                                    Print(string.Format("[V12] SET_TRAIL: {0} → Stop @ {1:F2} (Price: {2:F2}, Dist: {3})",
                                        entryName, newStopPrice, currentPrice, trailDistance));
                                }

                                Print(string.Format("[V12] SET_TRAIL COMPLETE: Updated {0} position(s) with {1} pt trail", trailCount, trailDistance));
                            }
                        }
                        else
                        {
                            Print("[V12] SET_TRAIL: Invalid distance parameter");
                        }
                    }
                    else if (action == "SET_CIT")
                    {
                        if (parts.Length >= 2)
                        {
                            ChaseIfTouchPoints = parts[1].Trim();
                            Print($"[V12] CIT updated: {ChaseIfTouchPoints}");
                        }
                    }
                    else if (action == "LOCK_50")
                    {
                        // [1102Z-F]: IPC LOCK_50 — Lock 50% of unrealized profit on all active positions.
                        // Delegates to ExecuteRunnerAction which already handles all account routing.
                        Print("[IPC LOCK_50] Received — routing to ExecuteRunnerAction(lock50)");
                        ExecuteRunnerAction("lock50");
                    }
                    else if (action == "BE" || action == "BE_CUSTOM" || action == "BE_PLUS_2" || action == "BE_PLUS_1") // V12.23: +BE_CUSTOM with dynamic ticks
                    {
                        double beOffset;
                        if (action == "BE_CUSTOM" && parts.Length >= 2)
                        {
                            // V12.23: Dynamic ticks from panel input — syncs auto-trail BE too
                            int customTicks;
                            if (!int.TryParse(parts[1].Trim(), out customTicks) || customTicks < 0)
                                customTicks = BreakEvenOffsetTicks; // fallback to default
                            BreakEvenOffsetTicks = customTicks; // V12.23: Sync auto-trail + fleet symmetry
                            beOffset = customTicks * tickSize;
                        }
                        else if (action == "BE" || action == "BE_PLUS_2")
                            beOffset = BreakEvenOffsetTicks * tickSize;
                        else
                            beOffset = 1 * tickSize; // Legacy BE_PLUS_1
                        MoveStopsToBreakevenWithOffset(beOffset);
                    }
                    else if (action == "FLATTEN_ONLY")
                    {
                        // V12.21: Flatten Only (Close Positions) - preserve pending orders
                        if (EnableSIMA)
                        {
                            Print("[SIMA] IPC FLATTEN_ONLY → Closing all open positions (Pending orders preserved)");
                            ClosePositionsOnlyApexAccounts(); // V12.21: Use new non-cancelling helper
                        }
                        else
                        {
                            Print("[V12] FLATTEN_ONLY → Closing all open positions (Pending orders preserved)");
                            // CloseAllPositions(); // Native NT8 method closes positions and cancels orders usually?
                            // NT8 Flatten() cancels orders. We must use Close() on each position instead.

                            foreach (Position pos in Account.Positions)
                            {
                                if (pos.Instrument.FullName == Instrument.FullName && pos.MarketPosition != MarketPosition.Flat)
                                {
                                    if (pos.MarketPosition == MarketPosition.Long)
                                        SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, pos.Quantity, 0, 0, "", "FlattenOnly_ExitLong");
                                    else
                                        SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, pos.Quantity, 0, 0, "", "FlattenOnly_ExitShort");
                                }
                            }
                        }
                    }
                    else if (action == "FLATTEN")
                    {
                        // V12 SIMA: Use multi-account flatten when enabled
                        if (EnableSIMA)
                        {
                            Print("[SIMA] IPC FLATTEN → Broadcasting to all Apex accounts");
                            FlattenAllApexAccounts();
                        }
                        else
                        {
                            FlattenAll();
                        }
                    }
                    else if (action == "CANCEL_ALL")
                    {
                        // V12.13c: Only cancels pending entry orders (stops/targets on active positions are preserved)
                        if (EnableSIMA)
                        {
                            int cancelled = 0;

                            // ── V12.10: Cancel local account orders FIRST ──
                            foreach (Order order in Account.Orders)
                            {
                                if (order != null && order.Instrument.FullName == Instrument.FullName &&
                                    (order.OrderState == OrderState.Working ||
                                     order.OrderState == OrderState.Accepted ||
                                     order.OrderState == OrderState.Submitted))
                                {
                                    // V12.13c: Skip stops and targets on active positions — only cancel pending entries
                                    string oName = order.Name;
                                    if (oName.StartsWith("Stop_") || oName.StartsWith("S_") ||
                                        oName.StartsWith("T1_") || oName.StartsWith("T2_") ||
                                        oName.StartsWith("T3_") || oName.StartsWith("T4_") || oName.StartsWith("T5_"))
                                        continue;

                                    CancelOrder(order);
                                    cancelled++;
                                }
                            }

                            // ── Fleet accounts ──
                            foreach (Account acct in Account.All)
                            {
                                if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    if (acct == this.Account) continue; // already cancelled above
                                    foreach (Order order in acct.Orders)
                                    {
                                        if (order != null && order.Instrument.FullName == Instrument.FullName &&
                                            (order.OrderState == OrderState.Working ||
                                             order.OrderState == OrderState.Accepted ||
                                             order.OrderState == OrderState.Submitted))
                                        {
                                            // V12.13c: Skip stops and targets — only cancel pending entries
                                            string oName = order.Name;
                                            if (oName.StartsWith("Stop_") || oName.StartsWith("S_") ||
                                                oName.StartsWith("T1_") || oName.StartsWith("T2_") ||
                                                oName.StartsWith("T3_") || oName.StartsWith("T4_") || oName.StartsWith("T5_"))
                                                continue;

                                            acct.Cancel(new[] { order });
                                            cancelled++;
                                        }
                                    }
                                }
                            }
                            Print($"[SIMA] CANCEL_ALL → Cancelled {cancelled} pending entry orders (local + fleet)");
                        }
                        else
                        {
                            int cancelled = 0;
                            foreach (Order order in Account.Orders)
                            {
                                if (order != null && order.Instrument.FullName == Instrument.FullName &&
                                    (order.OrderState == OrderState.Working ||
                                     order.OrderState == OrderState.Accepted ||
                                     order.OrderState == OrderState.Submitted))
                                {
                                    // V12.13c: Skip stops and targets — only cancel pending entries
                                    string oName = order.Name;
                                    if (oName.StartsWith("Stop_") || oName.StartsWith("S_") ||
                                        oName.StartsWith("T1_") || oName.StartsWith("T2_") ||
                                        oName.StartsWith("T3_") || oName.StartsWith("T4_") || oName.StartsWith("T5_"))
                                        continue;

                                    CancelOrder(order);
                                    cancelled++;
                                }
                            }
                            Print($"[V12] CANCEL_ALL → Cancelled {cancelled} pending entry orders");
                        }

                        // V1102Z-HARDEN: Ghost Memory Teardown
                        // We must sweep ALL matching accounts and zero their expectedPositions for THIS instrument.
                        // Relying on activePositions.Values iteration is insufficient as failed dispatches leave entries in
                        // expectedPositions with no corresponding activePositions object.
                        int resetAcctCount = 0;
                        foreach (Account acct in Account.All)
                        {
                            if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0 || acct == this.Account)
                            {
                                SetExpectedPositionLocked(ExpKey(acct.Name), 0);
                                resetAcctCount++;
                            }
                        }
                        Print($"[V1102Z] Ghost Memory Purge: Zeroed expectedPositions for {resetAcctCount} accounts on {Instrument.FullName}");

                        // Clean up local position objects for anything not filled
                        foreach (var kvp in activePositions.ToArray())
                        {
                            if (!kvp.Value.EntryFilled)
                            {
                                CleanupPosition(kvp.Key);
                                Print(string.Format("V12.13b: CANCEL_ALL cleaned unfilled memory entry: {0}", kvp.Key));
                            }
                        }
                    }
                    else if (action == "RESET_MEMORY")
                    {
                        // V1102Z: Manual emergency reset of all expectedPositions for this instrument
                        int resetAcctCount = 0;
                        foreach (Account acct in Account.All)
                        {
                            if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0 || acct == this.Account)
                            {
                                SetExpectedPositionLocked(ExpKey(acct.Name), 0);
                                resetAcctCount++;
                            }
                        }
                        Print($"[V1102Z] RESET_MEMORY: Zeroed all fleet/master expectedPositions for {Instrument.FullName} across {resetAcctCount} accounts.");
                        SendResponseToRemote("MSG|Memory Reset Complete");
                    }
                    else if (action == "LONG" || action == "SHORT")
                    {
                        // V12.2: Handle Sync Mode
                        if (isTosSyncMode)
                        {
                            bool armed = (action == "LONG") ? isLongArmed : isShortArmed;
                            if (!armed)
                            {
                                Print($"[SYNC] ToS Signal IGNORED: {action} received but {action} is not ARMED locally.");
                                continue;
                            }
                            else
                            {
                                Print($"[SYNC] ToS Handshake Received -> Executing {action} Fleet Entry");
                                // Reset armed flag after firing
                                if (action == "LONG") isLongArmed = false; else isShortArmed = false;
                            }
                        }

                        // V12 SIMA: Broadcast to all Apex accounts when enabled
                        if (EnableSIMA)
                        {
                            OrderAction orderAction = action == "LONG" ? OrderAction.Buy : OrderAction.SellShort;

                            // [Phase 8.2 Part 3 - IPC SIZING]: Calculate ATR-sized quantity to match
                            // what ExecuteRMAEntryV2 would use, instead of defaulting to minContracts (= 1).
                            // This ensures manual LONG/SHORT button entries enter at the correct fleet size.
                            int qty;
                            try
                            {
                                // [Phase 8.2 Part 4 - IPC SIZING FIX]: Use RMAStopATRMultiplier to match
                                // the actual RMA engine risk model. StopMultiplier caused incorrect stop
                                // distances on high-value instruments (ES/NQ), flooring qty to 1.
                                double stopDist = CalculateATRStopDistance(RMAStopATRMultiplier);
                                if (stopDist <= 0)
                                {
                                    stopDist = MinimumStop;
                                    Print($"[IPC SIZING] ATR latency detected. Falling back to MinimumStop={MinimumStop:F4}");
                                }
                                qty = stopDist > 0 ? CalculatePositionSize(stopDist) : Math.Max(1, minContracts);
                                Print($"[IPC SIZING] Calculation: StopDist={stopDist:F4}, Risk={MaxRiskAmount}, TargetQty={qty}");
                            }
                            catch
                            {
                                qty = Math.Max(1, minContracts);
                            }
                            qty = Math.Max(1, qty); // safety floor

                            if (EnablePathB)
                            {
                                Print($"[SIMA] PATH B {action} → Broadcasting {qty} contracts with FIXED BRACKETS to all Apex accounts");
                                ExecuteMultiAccountBracket(orderAction, qty, "PATHB_" + action, PathBStopPoints, PathBTargetPoints);
                            }
                            else
                            {
                                Print($"[SIMA] IPC {action} → Broadcasting {qty} contracts to all Apex accounts");
                                ExecuteMultiAccountMarket(orderAction, qty, "SIMA_" + action);
                            }
                        }
                        else
                        {
                            // Original single-account logic
                            MarketPosition direction = action == "LONG" ? MarketPosition.Long : MarketPosition.Short;
                            double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                            double stopDist  = CalculateATRStopDistance(RMAStopATRMultiplier);
                            int contracts    = CalculatePositionSize(stopDist);
                            ExecuteRMAEntryV2(currentPrice, direction, contracts);
                        }
                    }
                    // V10.3: OR Breakout Entry Commands
                    else if (action == "OR_LONG")
                    {
                        // V12.2: Handle Sync Mode
                        if (isTosSyncMode)
                        {
                            if (isLongArmed)
                            {
                                Print("[SYNC] ToS Handshake Received -> Executing OR_LONG");
                                double orStopDist = CalculateORStopDistance();
                                int orContracts   = CalculatePositionSize(orStopDist);
                                ExecuteLong(orContracts);
                                isLongArmed = false;
                            }
                            else
                            {
                                Print("[SYNC] ToS Signal IGNORED: OR_LONG received but Long is not ARMED locally.");
                            }
                        }
                        else
                        {
                            double orStopDist = CalculateORStopDistance();
                            int orContracts   = CalculatePositionSize(orStopDist);
                            ExecuteLong(orContracts);
                            Print("V10.3: OR_LONG executed via IPC");
                        }
                    }
                    else if (action == "OR_SHORT")
                    {
                        // V12.2: Handle Sync Mode
                        if (isTosSyncMode)
                        {
                            if (isShortArmed)
                            {
                                Print("[SYNC] ToS Handshake Received -> Executing OR_SHORT");
                                double orStopDist = CalculateORStopDistance();
                                int orContracts   = CalculatePositionSize(orStopDist);
                                ExecuteShort(orContracts);
                                isShortArmed = false;
                            }
                            else
                            {
                                Print("[SYNC] ToS Signal IGNORED: OR_SHORT received but Short is not ARMED locally.");
                            }
                        }
                        else
                        {
                            double orStopDist = CalculateORStopDistance();
                            int orContracts   = CalculatePositionSize(orStopDist);
                            ExecuteShort(orContracts);
                            Print("V10.3: OR_SHORT executed via IPC");
                        }
                    }
                    // V10.3: Target-Specific Close Commands
                    else if (action.StartsWith("CLOSE_T"))
                    {
                        int targetNum = 0;
                        if (action.Length > 7 && int.TryParse(action.Substring(7, 1), out targetNum))
                        {
                            FlattenSpecificTarget(targetNum);
                        }
                    }
                    // V14: MOVE_TARGET command - Surgical target price adjustment
                    else if (action.StartsWith("MOVE_TARGET"))
                    {
                        // Format: MOVE_TARGET|T1|1pt  or  MOVE_TARGET|T2|2pt
                        if (parts.Length >= 3)
                        {
                            string targetId = parts[1].Trim().ToUpper(); // "T1", "T2", etc.
                            string distance = parts[2].Trim().ToLower(); // "1pt" or "2pt"

                            // Parse distance
                            double profitPoints = 0;
                            if (distance == "1pt") profitPoints = 1.0;
                            else if (distance == "2pt") profitPoints = 2.0;
                            else
                            {
                                Print($"[V14] MOVE_TARGET: Invalid distance '{distance}' - expected '1pt' or '2pt'");
                                continue;
                            }

                            // Extract target number (T1 -> 1, T2 -> 2, etc.)
                            int targetNum = 0;
                            if (targetId.Length >= 2 && targetId.StartsWith("T"))
                            {
                                if (!int.TryParse(targetId.Substring(1), out targetNum) || targetNum < 1 || targetNum > 5)
                                {
                                    Print($"[V14] MOVE_TARGET: Invalid target '{targetId}' - expected T1-T5");
                                    continue;
                                }
                            }
                            else
                            {
                                Print($"[V14] MOVE_TARGET: Invalid target format '{targetId}'");
                                continue;
                            }

                            Print($"[V14] MOVE_TARGET: Command received for {targetId} to +{profitPoints}pt profit");

                            // Call the move handler (implemented in Orders.cs)
                            MoveSpecificTarget(targetNum, profitPoints);
                        }
                        else
                        {
                            Print("[V14] MOVE_TARGET: Invalid format - expected MOVE_TARGET|TX|1pt or MOVE_TARGET|TX|2pt");
                        }
                    }
                    else if (action.StartsWith("GET_FLEET"))
                    {
                        // Broadcast account names to the Remote App for UI mapping
                        StringBuilder sb = new StringBuilder("CONFIG|FLEET");
                        foreach (var acct in Account.All)
                        {
                            if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                sb.Append($"|{acct.Name}");
                            }
                        }
                        SendResponseToRemote(sb.ToString());
                        Print("[SIMA] GET_FLEET → Responded with account list");
                    }
                    else if (action.StartsWith("SET_MAX_RISK"))
                    {
                        if (parts.Length > 2 && double.TryParse(parts[2], out double val))
                        {
                            MaxRiskAmount = val;
                            RiskPerTrade = val; // Sync legacy property
                            Print($"[V12.2] SET_MAX_RISK: {val}");
                        }
                    }
                    else if (action.StartsWith("TOGGLE_ACCOUNT"))
                    {
                        if (parts.Length > 2)
                        {
                            string acctName = parts[1];
                            bool active = parts[2] == "1";
                            // V12.1101E [A-2]: Lock IPC writes to activeFleetAccounts — this dict is also
                            // read by the strategy thread (ExecuteMultiAccountMarket) without a lock.
                            lock (stateLock)
                            {
                                activeFleetAccounts[acctName] = active;
                            }
                            Print($"[V12.2] TOGGLE_ACCOUNT: {acctName} | Active={active}");
                        }
                    }
                    // V12.6: SET_SIMA|ON or SET_SIMA|OFF - Remote SIMA toggle from external panel
                    // V12.Phase6 [LIFECYCLE]: Uses centralized ApplySimaState for full lifecycle management
                    else if (action == "SET_SIMA")
                    {
                        if (parts.Length > 1)
                        {
                            bool enable = parts[1].Trim().ToUpper() == "ON";
                            ApplySimaState(enable);
                            Print($"V12.Phase6: SET_SIMA = {enable} (lifecycle applied)");
                        }
                    }
                    // V12.2: Diagnostic command to check fleet state
                    else if (action == "DIAG_FLEET")
                    {
                        Print("[DIAG] ══════════════════════════════════════════════════");
                        Print($"[DIAG] EnableSIMA = {EnableSIMA}");
                        Print($"[DIAG] AccountPrefix = \"{AccountPrefix}\"");
                        int total = 0;
                        int active = 0;
                        foreach (Account acct in Account.All)
                        {
                            if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                total++;
                                bool isActive = false;
                                activeFleetAccounts.TryGetValue(acct.Name, out isActive);
                                if (isActive) active++;
                                Print($"[DIAG]   {acct.Name} -> {(isActive ? "✓ ACTIVE" : "✗ INACTIVE")}");
                            }
                        }
                        Print($"[DIAG] TOTAL: {total} accounts | {active} ACTIVE");
                        Print("[DIAG] ══════════════════════════════════════════════════");
                    }
                    else if (action.StartsWith("SET_ANCHOR"))
                    {
                        // V11: SET_ANCHOR|EMA30|Global
                        if (parts.Length > 2)
                        {
                            string anchorStr = parts[1];
                            SetRmaAnchorFromIpc(anchorStr);
                        }
                    }
                    // V12.4: SET_RMA_MODE|ON or SET_RMA_MODE|OFF - Toggle chart-click RMA mode from Panel
                    else if (action == "SET_RMA_MODE")
                    {
                        if (parts.Length > 1)
                        {
                            bool enable = parts[1].Trim().ToUpper() == "ON";
                            isRMAModeActive = enable;
                            isRMAButtonClicked = enable;
                            Print(string.Format("V12.4: SET_RMA_MODE = {0} (Chart-Click RMA {1})", enable, enable ? "ENABLED" : "DISABLED"));
                        }
                    }
                    // V12.2: SYNC_MODE|{MODE} - Relay mode sync from chart panel to external app
                    else if (action == "SYNC_MODE")
                    {
                        if (parts.Length > 1)
                        {
                            string syncMode = parts[1].Trim().ToUpper();
                            // V12.13-D: Broadcast SYNC_MODE to all connected panel clients
                            SendResponseToRemote($"SYNC_MODE|{syncMode}");
                            Print(string.Format("V12.2: SYNC_MODE Relay -> {0}", syncMode));
                        }
                    }
                    // V12.5: SET_TARGETS|count - Panel is sole source of truth
                    // V12.Phase8.3: Now writes to activeTargetCount — minContracts is symbol-specific risk floor only
                    else if (action == "SET_TARGETS")
                    {
                        if (parts.Length > 1 && int.TryParse(parts[1], out int targetCount))
                        {
                            // FIX-B [Build 1102Z]: Clamp + lock to prevent IPC race with SIMA dispatch loop.
                            int clamped = Math.Max(1, Math.Min(5, targetCount));
                            lock (stateLock) { activeTargetCount = clamped; }
                            Print(string.Format("V12.Phase8.3: SET_TARGETS = {0} targets (clamped from {1}; minContracts preserved at {2})", clamped, targetCount, minContracts));
                            // V12.25: CONFIG broadcast REMOVED — Panel is sole source of truth.
                            // Sending CONFIG back here caused the Ping-Pong overwrite bug.
                            // Build 1102Y [U-02]: Immediately sync panel visibility — panel needs the count, not a CONFIG echo.
                            SendResponseToRemote($"SYNC_TARGET_STATE|{clamped}");
                        }
                    }
                    // Phase 9.1: MKT_SYNC — Toggle ToS Armed Mode (Top button)
                    else if (action == "MKT_SYNC")
                    {
                        isTosSyncMode = !isTosSyncMode;
                        Print(string.Format("[SYNC] ToS Sync Mode: {0}", isTosSyncMode));
                    }
                    // Phase 9.1: SYNC_ALL — Refresh active target orders to match current panel config (Bottom button)
                    else if (action == "SYNC_ALL")
                    {
                        Print("[SYNC_ALL] Refresh triggered — recalculating active target orders");
                        RefreshActivePositionOrders();
                    }
                    // V12.5: SET_MODE|mode - Panel is sole source of truth
                    else if (action == "SET_MODE")
                    {
                        if (parts.Length > 1)
                        {
                            string newMode = parts[1].Trim().ToUpper();

                            // V12.20: Atomic mode transition — prevents partial state reads during switch
                            lock (stateLock)
                            {
                                isRMAModeActive = false;
                                isRMAButtonClicked = false;
                                isRetestModeActive = false;
                                isTRENDModeActive = false;
                                isMOMOModeActive = false;
                                isFFMAModeArmed = false;

                                if (newMode == "RMA")
                                {
                                    isRMAModeActive = true;
                                    isRMAButtonClicked = true;
                                }
                                else if (newMode == "RETEST")
                                {
                                    isRetestModeActive = true;
                                }
                                else if (newMode == "TREND")
                                {
                                    isTRENDModeActive = true;
                                }
                                else if (newMode == "MOMO")
                                {
                                    isMOMOModeActive = true;
                                }
                                else if (newMode == "FFMA")
                                {
                                    isFFMAModeArmed = true;
                                }
                                // ORB/OR = all modes off (already deactivated above)
                            }

                            Print(string.Format("V12.25: SET_MODE = {0} | RMA={1} RETEST={2} TREND={3} MOMO={4} FFMA={5} (no CONFIG echo)",
                                newMode, isRMAModeActive, isRetestModeActive, isTRENDModeActive, isMOMOModeActive, isFFMAModeArmed));

                            // V12.25: CONFIG broadcast REMOVED — Panel is sole source of truth.
                            // Sending CONFIG back here caused the Ping-Pong overwrite bug.
                        }
                    }
                    // V12.25: SET_LEADER_ACCOUNT|accountName — Panel tells strategy which account is the leader
                    else if (action == "SET_LEADER_ACCOUNT")
                    {
                        if (parts.Length > 1)
                        {
                            string newLeader = parts[1].Trim();
                            Print($"V12.25 IPC: Leader Account synced to [{newLeader}]");
                        }
                    }
                    else if (action == "REQUEST_FLEET_STATE")
                    {
                        StringBuilder fsb = new StringBuilder("FLEET_STATE|");
                        fsb.Append(Instrument.FullName).Append("|");
                        fsb.Append(Position.MarketPosition).Append("|");
                        
                        List<string> acctStates = new List<string>();
                        foreach (Account acct in Account.All)
                        {
                            if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                var bPos = acct.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
                                int act = (bPos != null && bPos.MarketPosition != MarketPosition.Flat) 
                                    ? (bPos.MarketPosition == MarketPosition.Long ? (int)bPos.Quantity : -(int)bPos.Quantity) : 0;
                                int exp = 0;
                                if (expectedPositions != null) expectedPositions.TryGetValue(ExpKey(acct.Name), out exp);
                                acctStates.Add($"{acct.Name}:{act}:{exp}");
                            }
                        }
                        fsb.Append(string.Join(";", acctStates));
                        SendResponseToRemote(fsb.ToString());
                    }
                    else if (action == "SET_MANUAL_PRICE")
                    {
                        // Format: SET_MANUAL_PRICE|<price>|<symbol> - price is in parts[1] (after split by |)
                        // Note: The command comes as "SET_MANUAL_PRICE" with price in parts[1] if sent as SET_MANUAL_PRICE|1234.50|MGC
                        if (parts.Length > 1 && double.TryParse(parts[1], out double manualPrice))
                        {
                            cachedMnlPrice = manualPrice;
                            currentRmaAnchor = RmaAnchorType.Manual;
                            // V12.1101E [D-02]: Legacy isMnlArmed flag purged; cachedMnlPrice + anchor state is authoritative.

                            Print(string.Format("IPC SET_MANUAL_PRICE: {0:F2} | Anchor set to MANUAL", manualPrice));
                        }
                        else
                        {
                            Print(string.Format("IPC SET_MANUAL_PRICE: Invalid price format in command: {0}", command));
                        }
                    }
                    // V12.27: Manual entry commands from Contextual UI Submit button
                    else if (action == "TREND_MANUAL_LIMIT")
                    {
                        // Format: TREND_MANUAL_LIMIT|LONG|1234.50
                        if (parts.Length > 2)
                        {
                            string dir = parts[1].Trim().ToUpper();
                            MarketPosition mp = dir == "LONG" ? MarketPosition.Long : MarketPosition.Short;
                            if (double.TryParse(parts[2], out double price) && price > 0)
                            {
                                Print(string.Format("V12.27 IPC: TREND_MANUAL_LIMIT {0} @ {1:F2}", dir, price));
                                double trendDist   = CalculateTRENDStopDistance();
                                int trendContracts = CalculatePositionSize(trendDist);
                                ExecuteTRENDManualEntry(price, mp, trendContracts);
                            }
                            else
                            {
                                Print(string.Format("V12.27 IPC: TREND_MANUAL_LIMIT invalid price: {0}", command));
                            }
                        }
                    }
                    else if (action == "RETEST_MANUAL_LIMIT")
                    {
                        // Format: RETEST_MANUAL_LIMIT|LONG|1234.50
                        if (parts.Length > 2)
                        {
                            string dir = parts[1].Trim().ToUpper();
                            MarketPosition mp = dir == "LONG" ? MarketPosition.Long : MarketPosition.Short;
                            if (double.TryParse(parts[2], out double price) && price > 0)
                            {
                                Print(string.Format("V12.27 IPC: RETEST_MANUAL_LIMIT {0} @ {1:F2}", dir, price));
                                double retestDist   = CalculateRetestStopDistance();
                                int retestContracts = CalculatePositionSize(retestDist);
                                ExecuteRetestManualEntry(price, mp, retestContracts);
                            }
                            else
                            {
                                Print(string.Format("V12.27 IPC: RETEST_MANUAL_LIMIT invalid price: {0}", command));
                            }
                        }
                    }
                    else if (action == "FFMA_MANUAL_LIMIT")
                    {
                        // Format: FFMA_MANUAL_LIMIT|LONG|1234.50
                        if (parts.Length > 2)
                        {
                            string dir = parts[1].Trim().ToUpper();
                            MarketPosition mp = dir == "LONG" ? MarketPosition.Long : MarketPosition.Short;
                            if (double.TryParse(parts[2], out double price) && price > 0)
                            {
                                Print(string.Format("V12.27 IPC: FFMA_MANUAL_LIMIT {0} @ {1:F2}", dir, price));
                                ExecuteFFMALimitEntry(price, mp);
                            }
                            else
                            {
                                Print(string.Format("V12.27 IPC: FFMA_MANUAL_LIMIT invalid price: {0}", command));
                            }
                        }
                    }
                    else if (action == "FFMA_MANUAL_MARKET")
                    {
                        // V12.27: M.FFMA button — instant market, direction toward 9 EMA
                        Print("V12.27 IPC: FFMA_MANUAL_MARKET — auto-direction toward EMA9");
                        ExecuteFFMAManualMarketEntry();
                    }
                    else if (action.StartsWith("MODE_") || action.StartsWith("EXEC_") || action == "FFMA_DISARM")
                    {
                        ToggleStrategyMode(action);
                    }
                    // V12: GET_LAYOUT handler (primary response is in ListenForRemote, this is fallback logging)
                    else if (action == "GET_LAYOUT")
                    {
                        string mode = isRMAModeActive ? "RMA" : "OR";
                        Print(string.Format("V12 GET_LAYOUT: Mode={0} Count={1} T1={2}({3}) T2={4}({5}) T3={6}({7}) T4={8}({9}) T5={10}({11})",
                            mode, activeTargetCount,
                            Target1Value, T1Type,
                            Target2Value, T2Type,
                            Target3Value, T3Type,
                            Target4Value, T4Type,
                            Target5Value, T5Type));
                    }
                }
                catch (Exception ex)
                {
                    Print("Error ProcessIpcCommands: " + ex.Message);
                }
            }
        }

        private void SendResponseToRemote(string response)
        {
            if (connectedClients == null) return;

            // Diagnostic: Log what we are sending and to how many clients
            if (response.Contains("SYNC_TARGET_STATE"))
                 Print($"V14 IPC: Broadcasting {response} to {connectedClients.Count} clients");

            byte[] responseBytes = Encoding.UTF8.GetBytes(response + "\n");
            List<TcpClient> disconnectedClients = new List<TcpClient>();

            foreach (var client in connectedClients)
            {
                try
                {
                    if (client.Connected && client.GetStream().CanWrite)
                    {
                        client.GetStream().Write(responseBytes, 0, responseBytes.Length);
                        client.GetStream().Flush();
                    }
                    else
                    {
                        Print("V14 IPC: Client disconnected or stream not writable.");
                    }
                }
                catch (Exception ex)
                {
                    Print($"V14 IPC: Send Error - {ex.Message}");
                    // Client likely disconnected, will be handled by reading loop or next cleanup
                }
            }
        }

        // V12.13-D: SendToExternalApp REMOVED — it connected to port 5001 (the strategy's own listener),
        // causing infinite flood loops. All callers now use SendResponseToRemote() or direct client stream writes.
        // V12.44: MoveStopsToBreakevenPlusOne() removed — dead code, replaced by MoveStopsToBreakevenWithOffset()

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
                    if (!activePositions.ContainsKey(kvp.Key)) continue;
                    PositionInfo pos = kvp.Value;
                    string entryName = kvp.Key;

                    if (!pos.EntryFilled || pos.RemainingContracts <= 0) continue;

                    int qtyToClose = 0;
                    ConcurrentDictionary<string, Order> targetDict = null;
                    string targetName = "";

                    switch (targetNumber)
                    {
                        case 1: qtyToClose = pos.T1Contracts; targetDict = target1Orders; targetName = "T1"; break;
                        case 2: qtyToClose = pos.T2Contracts; targetDict = target2Orders; targetName = "T2"; break;
                        case 3: qtyToClose = pos.T3Contracts; targetDict = target3Orders; targetName = "T3"; break;
                        case 4: qtyToClose = pos.T4Contracts; targetDict = target4Orders; targetName = "T4"; break;
                        case 5: qtyToClose = pos.T5Contracts; targetDict = target5Orders; targetName = "T5"; break;
                        default:
                            Print(string.Format("V10.3: Invalid target number {0}", targetNumber));
                            return;
                    }

                    if (qtyToClose <= 0)
                    {
                        Print(string.Format("V10.3: {0} has no contracts to close for {1}", targetName, entryName));
                        continue;
                    }

                    // Cancel existing limit order if working
                    if (targetDict != null && targetDict.TryGetValue(entryName, out Order targetOrder))
                    {
                        if (targetOrder != null && (targetOrder.OrderState == OrderState.Working ||
                            targetOrder.OrderState == OrderState.Accepted ||
                            targetOrder.OrderState == OrderState.Submitted))
                        {
                            CancelOrder(targetOrder);
                            Print(string.Format("V10.3: Cancelled {0} limit order for {1}", targetName, entryName));
                        }
                    }

                    // Submit market order to close the target contracts
                    if (pos.Direction == MarketPosition.Long)
                        SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, qtyToClose, 0, 0, "",
                            string.Format("Close{0}_{1}", targetName, entryName));
                    else
                        SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, qtyToClose, 0, 0, "",
                            string.Format("Close{0}_{1}", targetName, entryName));

                    Print(string.Format("V10.3: Closing {0} ({1} contracts) at market for {2}", targetName, qtyToClose, entryName));
                }
            }
            catch (Exception ex)
            {
                Print("ERROR FlattenSpecificTarget: " + ex.Message);
            }
        }

        private void ToggleStrategyMode(string action)
        {
             // V12.20: Atomic flag mutations
             lock (stateLock)
             {
                 if (action == "MODE_RMA") isRMAModeActive = !isRMAModeActive;
                 else if (action == "MODE_MOMO") isMOMOModeActive = !isMOMOModeActive;
                 else if (action == "MODE_FFMA")
                 {
                     isFFMAModeArmed = true;
                     Print("V12.24: FFMA AUTO armed — reversal scanner active");
                 }
                 else if (action == "MODE_M")
                 {
                     Print("V12.24: MODE_M received — immediate FFMA entry pending");
                 }
                 else if (action == "FFMA_DISARM")
                 {
                     isFFMAModeArmed = false;
                     Print("V12.24: FFMA disarmed via panel ResetExecutionMode");
                 }
                 else if (action == "MODE_TREND_RMA")
                 {
                     isTrendRmaMode = true;
                     Print("IPC: TREND RMA Mode Enabled");
                 }
                 else if (action == "MODE_TREND_STD")
                 {
                     isTrendRmaMode = false;
                     Print("IPC: TREND Standard Mode Enabled");
                 }
                 else if (action == "MODE_RETEST_RMA")
                 {
                     isRetestRmaMode = true;
                     Print("IPC: RETEST RMA Mode Enabled");
                 }
                 else if (action == "MODE_RETEST_STD")
                 {
                     isRetestRmaMode = false;
                     Print("IPC: RETEST Standard Mode Enabled");
                 }
             }

             // Execution calls stay outside lock (they do their own order management)
             if (action == "EXEC_TREND" || action == "EXEC_TREND_RMA")
             {
                 double trendDist   = CalculateTRENDStopDistance();
                 int trendContracts = CalculatePositionSize(trendDist);
                 ExecuteTRENDEntry(trendContracts);
             }
             else if (action == "EXEC_RETEST" || action == "EXEC_RETEST_PLUS" || action == "EXEC_RETEST_MINUS")
             {
                 double retestDist   = CalculateRetestStopDistance();
                 int retestContracts = CalculatePositionSize(retestDist);
                 ExecuteRetestEntry(retestContracts);
             }
             else if (action == "EXEC_MOMO")
            {
                double momoStopDist = Math.Min(MOMOStopPoints, MaximumStop);
                int momoContracts   = CalculatePositionSize(momoStopDist);
                ExecuteMOMOEntry(lastKnownPrice, momoContracts);
            }
             else if (action == "MODE_M")
             {
                 // V12.24: Immediate market entry using FFMA trade DNA
                 double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                 double ema9Value = _ema9Val;
                 MarketPosition direction = currentPrice > ema9Value ? MarketPosition.Short : MarketPosition.Long;
                 Print(string.Format("V12.24: MODE_M firing — Price={0:F2} vs EMA9={1:F2} → {2}", currentPrice, ema9Value, direction));
                 ExecuteFFMAEntry(direction);
             }

             Print(string.Format("IPC Mode Toggle: {0} | RMA={1} MOMO={2} TrendRMA={3} RetestRMA={4} FFMA={5}",
                action, isRMAModeActive, isMOMOModeActive, isTrendRmaMode, isRetestRmaMode, isFFMAModeArmed));
        }


        #endregion
    }
}
