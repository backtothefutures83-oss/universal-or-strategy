// <copyright file="V12_002.Lifecycle.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
// Build 971: V12_002 Lifecycle -- OnStateChange, OnConnectionStatusUpdate, OnMarketData
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security;
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
        #region OnStateChange

        protected override void OnStateChange()
        {
            State state = State;
            if (state != State.SetDefaults)
                RefreshActorOwnerThread();
            ProcessOnStateChange(state);
        }

        private void ProcessOnStateChange(State state)
        {
            if (state == State.SetDefaults)
                OnStateChangeSetDefaults();
            else if (state == State.Configure)
                OnStateChangeConfigure();
            else if (state == State.DataLoaded)
                OnStateChangeDataLoaded();
            else if (state == State.Realtime)
                OnStateChangeRealtime();
            else if (state == State.Terminated)
                OnStateChangeTerminated();
        }

        private void DrainQueuesForShutdown()
        {
            try
            {
                Print("[SHUTDOWN] Draining queues...");
                int ipcDrained = 0;
                if (ipcCommandQueue != null)
                {
                    while (ipcDrained < 100 && ipcCommandQueue.TryDequeue(out string _))
                    {
                        ipcDrained++;
                    }
                }

                int actorDrained = 0;
                int actorOverflow = 0;
                while (actorDrained < 50 && _cmdQueue.TryDequeue(out StrategyCommand cmd))
                {
                    try
                    {
                        cmd.Execute(this);
                    }
                    catch (Exception exCmd)
                    {
                        Print("[SHUTDOWN] Actor cmd failed during drain: " + exCmd.ToString());
                    }
                    actorDrained++;
                }
                StrategyCommand overflowCmd;
                while (_cmdQueue.TryDequeue(out overflowCmd))
                    actorOverflow++;

                Print(
                    string.Format(
                        "[SHUTDOWN] Drained {0} IPC cmds, {1} Actor cmds. Overflow discarded: {2}.",
                        ipcDrained,
                        actorDrained,
                        actorOverflow
                    )
                );
            }
            catch (Exception exOuter)
            {
                Print("[SHUTDOWN] DrainQueuesForShutdown outer exception: " + exOuter.ToString());
            }
        }

        // INV-7.1, INV-7.2: Critical termination ordering -- _isTerminating MUST be first,
        // StopWatchdog MUST be second. Atomic cluster prevents watchdog from firing during teardown.
        // DEVIATION-T8-A: 8 LOC < 15 LOC target, pre-authorized by ticket (safety-critical, cannot decompose).
        private void SetTerminatingAndStopWatchdog()
        {
            _isTerminating = true;
            StopWatchdog();
        }

        private void ShutdownUiAndServices()
        {
            _configureComplete = false;
            _dataLoadedComplete = false;
            Interlocked.Exchange(ref _startupReadinessLogEmitted, 0);

            StopPanelRefresh();

            if (ChartControl != null)
            {
                ChartControl.Dispatcher.InvokeAsync(() =>
                {
                    // B984-F07: _isTerminating guard ensures no re-entrant panel ops if invoked late.
                    if (!_isTerminating)
                        return;
                    DetachHotkeys();
                    DetachChartClickHandler();
                    DestroyPanel();
                });
            }

            // [BUILD 984] GTC Cancel Sweep -- cancel all tracked/broker V12 orders before teardown.
            // Must run while dicts are still populated and accounts still subscribed.
            // force=false: soft terminate, protects brackets for open positions.
            // B984-F08: Log entry count before sweep for post-mortem tracing.
            Print(
                string.Format(
                    "[SHUTDOWN] GTC sweep: cancelling {0} tracked + broker-scanned orders",
                    (entryOrders?.Count ?? 0) + (stopOrders?.Count ?? 0)
                )
            );
            CancelAllV12GtcOrders(false);

            DrainQueuesForShutdown();
            EmitMetricsSummary();

            // Stop IPC Server
            StopIpcServer();

            // V12 SIMA: Stop Reaper audit thread
            StopReaperAudit();

            // V12.7: Always unsubscribe from account updates (subscribed for fleet bracket management)
            // V12.1101E [A-4]: Use shared UnsubscribeFromFleetAccounts() -- unconditional (no EnableSIMA guard)
            // to handle cases where flag was toggled OFF mid-session while handlers were still subscribed.
            UnsubscribeFromFleetAccounts();
        }

        // DEVIATION-T8-A amendment: CleanupResourcesAndReferences split into two helpers
        // to meet CYC <=19 constraint. Original had CYC=22 due to 20+ null-conditional operators.
        private void CleanupMmioAndEvents()
        {
            // v28.0 MMIO mirror teardown
            if (_photonMmioMirror != null)
            {
                try
                {
                    _photonMmioMirror.Dispose();
                }
                catch (ObjectDisposedException ex)
                {
                    // Known issue - MMIO already disposed
                    Print("[SHUTDOWN_ERROR] MMIO mirror already disposed: " + ex.Message);
                }
                catch (Exception ex)
                {
                    // Unexpected exception during shutdown - log but continue cleanup
                    Print("[SHUTDOWN_ERROR] CRITICAL: MMIO mirror dispose failed: " + ex.ToString());
                    throw;
                }
                _photonMmioMirror = null;
            }

            // V12.Phase7 [C-08]: Clear ALL static SignalBroadcaster event handlers on termination.
            // Static events survive instance disposal -- without this, dead instance handlers accumulate
            // and fire into garbage-collected strategy contexts on reload, causing phantom order submissions.
            try
            {
                SignalBroadcaster.ClearAllSubscribers();
            }
            finally
            {
                // V12.Phase7 [GAP-4]: No disposal needed for lock-free int gate (_simaToggleState).
                // Interlocked primitives have no OS handles to release.
            }
        }

        private void CleanupDictionaries()
        {
            // Clear all order tracking dictionaries and compliance state.
            // CYC optimization: remove null-conditional operators inside grouped block to stay under CYC=19.
            activePositions?.Clear();
            entryOrders?.Clear();
            stopOrders?.Clear();
            target1Orders?.Clear();
            target2Orders?.Clear();
            target3Orders?.Clear(); // v5.13
            target4Orders?.Clear();
            target5Orders?.Clear();
            _followerBrackets?.Clear();
            if (_accountMailbox != null)
            {
                while (_accountMailbox.TryDequeue(out var _))
                    ;
            }

            // Compliance tracking dictionaries - grouped with unconditional Clear() to reduce CYC
            if (accountDailyProfit != null)
            {
                accountDailyProfit.Clear();
                accountTotalProfit.Clear();
                accountTradeCount.Clear();
                accountDailyTradeCount.Clear();
                accountEquityPeak.Clear();
                accountMaxDrawdown.Clear();
                accountTradingDays.Clear();
                accountLastSummaryDate.Clear();
            }
        }

        #endregion

        private void OnStateChangeSetDefaults()
        {
            _configureComplete = false;
            _dataLoadedComplete = false;
            Interlocked.Exchange(ref _startupReadinessLogEmitted, 0);
            ResetTelemetry();
            Description = "Universal OR Strategy V12.12 - Build " + BUILD_TAG;
            Name = "V12_002";
            Calculate = Calculate.OnPriceChange; // CRITICAL FIX: Updates on every price tick for real-time trailing
            EntriesPerDirection = 10;
            EntryHandling = EntryHandling.UniqueEntries;
            IsExitOnSessionCloseStrategy = false;
            IsFillLimitOnTouch = false;
            MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
            OrderFillResolution = OrderFillResolution.Standard;
            StartBehavior = StartBehavior.ImmediatelySubmit;
            TimeInForce = TimeInForce.Gtc;
            StopTargetHandling = StopTargetHandling.PerEntryExecution;
            IsUnmanaged = true;

            // EPIC-4 P0 Fix #4: Remove generic path - will be set in DataLoaded with symbol-specific path
            _stickyStateEnabled = true;
            _stickyStatePath = string.Empty; // Will be set in DataLoaded

            // Session defaults (NY Open)
            SessionStart = DateTime.Parse("09:30", System.Globalization.CultureInfo.InvariantCulture);
            SessionEnd = DateTime.Parse("16:00", System.Globalization.CultureInfo.InvariantCulture);
            ORTimeframe = ORTimeframeType.Minutes_5;
            SelectedTimeZone = "Eastern";

            // Risk defaults
            RiskPerTrade = 200;
            StopThresholdPoints = 5.0;
            SlippageCushionPoints = 1.0; // SLIP-01: 1pt default cushion for follower slippage
            MESMinimum = 1;
            MESMaximum = 30;
            MGCMinimum = 1;
            MGCMaximum = 15;

            // Stop defaults
            StopMultiplier = 0.5;
            MinimumStop = 4.0; // 1102Z-A F2: raised floor from 1.0 to 4.0 for current volatility
            MaximumStop = 15.0; // V8.31: Increased from 8.0
            IpcPort = 5001;
            IpcExposeSensitiveFleetIdentity = false;

            // V12.1101E: 5-target system with configurable runner selection
            Target1Value = 1.0;
            Target2Value = 0.5;
            Target3Value = 1.0;
            Target4Value = 1.5;
            Target5Value = 2.0;
            ConfiguredTargetCount = 5;
            T1Type = TargetMode.Points;
            T2Type = TargetMode.ATR;
            T3Type = TargetMode.ATR;
            T4Type = TargetMode.ATR;
            T5Type = TargetMode.Runner;

            // Trailing stop defaults
            BreakEvenTriggerPoints = 2.0;
            BreakEvenOffsetTicks = 2; // BE stop offset in ticks (0 = exact entry)
            Trail1TriggerPoints = 3.0;
            Trail1DistancePoints = 2.0;
            Trail2TriggerPoints = 4.0;
            Trail2DistancePoints = 1.5;
            Trail3TriggerPoints = 5.0;
            Trail3DistancePoints = 1.0;

            // Display
            ShowMidLine = true;
            BoxOpacity = 20;

            // RMA defaults
            RMAEnabled = true;
            RMAATRPeriod = 14;
            RMAStopATRMultiplier = 1.1;

            // V8.2: TREND defaults (V8.31: E1 now uses ATR from live EMA9)
            TRENDEnabled = true;
            TRENDEntry1ATRMultiplier = 1.1; // V8.31: 1.1x ATR stop from live 9 EMA (was fixed 2pt)
            TRENDEntry2ATRMultiplier = 1.1; // 1.1x ATR trailing for 15 EMA entry

            // V8.4: RETEST defaults
            RetestEnabled = true;
            RetestATRMultiplier = 1.1; // 1.1x ATR for both stop and trail

            // V8.6: MOMO defaults
            MOMOEnabled = true;
            MOMOStopPoints = 0.5; // Fixed 0.5pt stop for MOMO trades

            // V8.7: FFMA defaults
            FFMAEnabled = true;
            FFMAEMADistance = 10.0; // 10 points from 9 EMA
            FFMARSIOverbought = 80;
            FFMARSIOversold = 20;

            // V12 SIMA defaults
            AccountPrefix = "Apex";
            EnableSIMA = false; // SAFETY: Default to OFF
            ReaperAuditEnabled = true;
            ReaperIntervalMs = 1000; // 1 second audit cycle
            NakedPositionGraceSec = 5; // Build 1104: extend naked-position grace to 5s for stop replace round-trips
            EnablePathB = false;
            AutoFlattenDesync = false;
            RepairTickFence = 8;
            FleetParityMultiplier = 1; // V12.Phase8.7 [PARITY-01]: Set to 10 for ES/MES fleet parity
            ShadowModeEnabled = false; // Build 1105: Shadow Mode opt-in, default OFF for safe rollout
            PathBStopPoints = 10.0;
            PathBTargetPoints = 15.0;
            ChaseIfTouchPoints = "0";

            // Apex Compliance defaults
            EnableComplianceHub = true;
            ConsistencyThreshold = 30;
            EnableConsistencyLock = false;
            MaxDailyProfitCap = 1500; // Default $1500 cap for consistency
            PayoutMinTradingDays = 10;
            PayoutMinProfit = 2600; // Common Apex 50K payout threshold (adjust per account)
            TrailingDrawdownLimit = 2500; // Common Apex 50K trailing DD
            // RMA Intelligence defaults (Phase 9.2)
            RmaIntelligenceEnabled = false; // Default to isolated/OFF
            RmaProximityTicks = 2;
            RmaCancellationTicks = 4;
            RmaMaxProbeCount = 3; // Phase 9.2: 3 probes before exhaustion
            RmaExhaustionEnabled = false; // Phase 9.2: Off by default, opt-in
            EnablePhotonAffinityBind = false;
            CpuAffinityMask = 0;
        }

        private void OnStateChangeConfigure()
        {
            _configureComplete = false;
            _dataLoadedComplete = false;
            // P0 CRITICAL: Set BarsRequiredToTrade for safe bar access (Jane Street: Correctness by construction)
            // EMA 200 requires 200 bars minimum
            BarsRequiredToTrade = 200;

            // B984-F03: AddDataSeries FIRST -- NT8 requires early registration before any throwing code.
            // Index 1 = 5-min (ATR), Index 2 = 10-min, Index 3 = 15-min (MTF RMA Intelligence Phase 9.2)
            AddDataSeries(BarsPeriodType.Minute, 5);
            AddDataSeries(BarsPeriodType.Minute, 10);
            AddDataSeries(BarsPeriodType.Minute, 15);

            // V8.30: Initialize thread-safe collections
            // ConcurrentDictionary(concurrencyLevel, initialCapacity)
            activePositions = new ConcurrentDictionary<string, PositionInfo>(2, 4);
            entryOrders = new ConcurrentDictionary<string, Order>(2, 4);
            stopOrders = new ConcurrentDictionary<string, Order>(2, 4);
            target1Orders = new ConcurrentDictionary<string, Order>(2, 4);
            target2Orders = new ConcurrentDictionary<string, Order>(2, 4);
            target3Orders = new ConcurrentDictionary<string, Order>(2, 4); // v5.13
            target4Orders = new ConcurrentDictionary<string, Order>(2, 4);
            target5Orders = new ConcurrentDictionary<string, Order>(2, 4);

            // V8.2: TREND linked entries tracking
            // V8.30: Thread-safe dictionary
            linkedTRENDEntries = new ConcurrentDictionary<string, string>(2, 4);

            // V8.11: Initialize pending stop replacements tracking
            // V8.30: Thread-safe dictionary
            pendingStopReplacements = new ConcurrentDictionary<string, PendingStopReplacement>(2, 4);

            // IPC Queue
            ipcCommandQueue = new ConcurrentQueue<string>();
            connectedClients = new ConcurrentDictionary<int, IpcClientSession>(); // Build 935 [Fix-1]: prevent NullReferenceException in StopIpcServer

            // EPIC-4 P0 Fix #1: Initialize IPC hardening layer to prevent NullReferenceException
            InitializeIpcHardening();

            // V12 SIMA: Initialize expected positions tracking
            expectedPositions = new ConcurrentDictionary<string, int>(2, 20); // Up to 20 accounts

            // v28.0 Sovereign Photon [ADR-012 + ADR-016]: pool + ring + sideband + salt + MMIO mirror
            // Capacity 64: 5 concurrent signals x 12 accounts = 60 < 64
            _photonPool = new PhotonOrderPool(PhotonPoolCapacity);
            _photonDispatchRing = new SPSCRing<FleetDispatchSlot>(PhotonPoolCapacity);
            _photonSideband = new FleetDispatchSideband[PhotonPoolCapacity];
            _photonShadowSalt = unchecked((ulong)Guid.NewGuid().GetHashCode() * 0x9E3779B97F4A7C15UL);

            // Static assert: Shadow must be the last 8 bytes of FleetDispatchSlot (ADR-016)
            // B984-F01: Degrade gracefully instead of crashing Configure cold.
            {
                int _slotSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(FleetDispatchSlot));
                int _shadowOffset = System
                    .Runtime.InteropServices.Marshal.OffsetOf(typeof(FleetDispatchSlot), "Shadow")
                    .ToInt32();
                if (_slotSize != 64 || _shadowOffset != 56)
                {
                    Print(
                        string.Format(
                            "[PHOTON CRITICAL] FleetDispatchSlot layout invariant violated: size={0}, shadowOffset={1}; expected size=64, offset=56. Photon MMIO disabled.",
                            _slotSize,
                            _shadowOffset
                        )
                    );
                    _photonPool = null;
                    _photonDispatchRing = null;
                }
            }

            // Optional MMIO mirror. Named per-process so multiple NT instances do not collide.
            // Failure is non-fatal: hot path runs against the heap ring even if the mirror fails.
            try
            {
                string _mmfName =
                    "V12_FleetDispatch_"
                    + System.Diagnostics.Process.GetCurrentProcess().Id.ToString()
                    + "_"
                    + _photonShadowSalt.ToString("X16");
                _photonMmioMirror = new MmioDispatchMirror(_mmfName, PhotonPoolCapacity, 64, _photonShadowSalt);
                Print(string.Format("[PHOTON MMIO] mirror online: {0}", _mmfName));
            }
            catch (Exception _mmioEx)
            {
                _photonMmioMirror = null;
                Print("[PHOTON MMIO] mirror unavailable (hot path unaffected): " + _mmioEx.ToString());
            }

            // V14.2 Sovereign Photon [ADR-011]: Pre-allocate execution ID dedup rings
            _executionIdRing = new ExecutionIdRing(512, 1024);
            _executionIdFallbackRing = new ExecutionIdRing(512, 1024);

            // [EPIC-5-PERF T04] Initialize order array pool for zero-allocation SIMA propagation
            _orderArrayPool = new OrderArrayPool();

            // V12.1: Initialize Compliance Hub -- create log directory early (idempotent).
            // Build 935 [Fix-2/3]: Symbol-specific log paths and LogicAudit moved to DataLoaded.
            string logsDirInit = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "NinjaTrader 8",
                "SIMA_Logs"
            );

            try
            {
                // EPIC-7-QUALITY-010: Validate directory path before creation
                string validLogDir = PathValidation.ValidateDirectoryPath(logsDirInit, "CreateLogDirectory");

                if (!System.IO.Directory.Exists(validLogDir))
                    System.IO.Directory.CreateDirectory(validLogDir);
            }
            catch (SecurityException ex)
            {
                Print(string.Format("[IO_SECURITY] {0}", ex.Message));
                // Continue execution - log directory creation is not critical for strategy operation
            }

            _configureComplete = true;
        }

        private void OnStateChangeDataLoaded()
        {
            // CRITICAL: Initialization sequence MUST be preserved exactly.
            // Order: InstrumentConfig -> TargetConfig -> Indicators -> SessionLogging -> Services -> SnapshotPool
            _dataLoadedComplete = false;

            string symbol = Instrument.MasterInstrument.Name;
            Init_InstrumentConfig(symbol);
            Init_TargetConfiguration();
            Init_Indicators();
            Init_SessionLogging(symbol);
            Init_Services(symbol);

            // [EPIC-5-PERF T03] Pre-warm UI snapshot pool for zero-allocation publishing
            PreWarmSnapshotPool();

            _dataLoadedComplete = true;
        }

        private void Init_InstrumentConfig(string symbol)
        {
            tickSize = Instrument.MasterInstrument.TickSize;
            pointValue = Instrument.MasterInstrument.PointValue;
            lastKnownPrice = 0; // V11 FIX: Reset price on load to prevent stale data (e.g. MES->MGC switch)

            if (symbol.Contains("MES") || symbol.Contains("ES"))
            {
                minContracts = MESMinimum;
                maxContracts = MESMaximum; // V12.1101E [B-9]: Upper bound for ATR sizer
            }
            else if (symbol.Contains("MGC") || symbol.Contains("GC"))
            {
                minContracts = MGCMinimum;
                maxContracts = MGCMaximum; // V12.1101E [B-9]
            }
            else
            {
                minContracts = 1;
                maxContracts = 20; // V12.1101E [B-9]: Conservative default for unknown instruments
            }
        }

        private void Init_TargetConfiguration()
        {
            int persistedTargetCount = Math.Max(0, Math.Min(5, ConfiguredTargetCount));
            if (persistedTargetCount >= 1)
            {
                activeTargetCount = persistedTargetCount;
            }
            else
            {
                // Backward compatibility for templates saved before ConfiguredTargetCount existed.
                int loadedTargetCount =
                    (Target1Value > 0 ? 1 : 0)
                    + (Target2Value > 0 ? 1 : 0)
                    + (Target3Value > 0 ? 1 : 0)
                    + (Target4Value > 0 ? 1 : 0)
                    + (Target5Value > 0 ? 1 : 0);
                activeTargetCount = Math.Max(1, Math.Min(5, loadedTargetCount));
                // B984-F04: Log backward-compat override so users know why target count changed.
                Print(
                    string.Format(
                        "[COMPAT] ConfiguredTargetCount was 0 -- auto-detected {0} targets from TargetValue fields.",
                        activeTargetCount
                    )
                );
                ConfiguredTargetCount = activeTargetCount;
            }
        }

        private void Init_Indicators()
        {
            // B984-F02: Guard BarsArray[1] -- only valid if AddDataSeries completed in Configure.
            // Audit marker: BarsArray.Length >= 2 (use .Length, not .Count -- ISeries<T> has no .Count)
            if (BarsArray != null && BarsArray.Length >= 2)
            {
                atrIndicator = this.ATR(BarsArray[1], RMAATRPeriod);
            }
            else
            {
                Print(
                    "[CRITICAL] BarsArray[1] unavailable -- ATR will use primary series. Check AddDataSeries in Configure."
                );
                atrIndicator = this.ATR(RMAATRPeriod);
            }

            // V8.2: Initialize EMA indicators for TREND trades
            // Using simple form - default is primary bars series
            ema9 = this.EMA(9);
            ema15 = this.EMA(15);
            // V11: Telemetry & Multi-Anchor EMAs
            ema30 = this.EMA(30);
            ema65 = this.EMA(65);
            ema200 = this.EMA(200);

            // V8.7: Initialize RSI for FFMA trades
            rsiIndicator = this.RSI(14, 3);

            // V8.2 DEBUG: Verify EMA periods are correct
            Print(string.Format("EMA INIT DEBUG: ema9.Period={0} ema15.Period={1}", ema9.Period, ema15.Period));
        }

        private void Init_SessionLogging(string symbol)
        {
            ResetOR();

            Print(
                string.Format(
                    "UniversalORStrategy {0} | {1} | Tick: {2} | PV: ${3}",
                    BUILD_TAG,
                    symbol,
                    tickSize,
                    pointValue
                )
            );
            Print(
                string.Format(
                    "Session: {0} - {1} {2} | OR: {3} min",
                    SessionStart.ToString("HH:mm"),
                    SessionEnd.ToString("HH:mm"),
                    SelectedTimeZone,
                    (int)ORTimeframe
                )
            );
            Print(
                string.Format(
                    "Targets: T1={0}({1}) T2={2}({3}) T3={4}({5}) T4={6}({7}) T5={8}({9}) | Stop={10}xOR",
                    Target1Value,
                    T1Type,
                    Target2Value,
                    T2Type,
                    Target3Value,
                    T3Type,
                    Target4Value,
                    T4Type,
                    Target5Value,
                    T5Type,
                    StopMultiplier
                )
            );
            Print(
                string.Format("RMA: Enabled={0} ATR({1}) Stop={2}xATR", RMAEnabled, RMAATRPeriod, RMAStopATRMultiplier)
            );
            Print(
                string.Format(
                    "TREND: Enabled={0} E1Stop={1}xATR E2Trail={2}xATR",
                    TRENDEnabled,
                    TRENDEntry1ATRMultiplier,
                    TRENDEntry2ATRMultiplier
                )
            );
            Print(
                string.Format(
                    "FFMA: Enabled={0} Distance={1}pt RSI={2}/{3}",
                    FFMAEnabled,
                    FFMAEMADistance,
                    FFMARSIOversold,
                    FFMARSIOverbought
                )
            );
            Print(
                string.Format(
                    "V12 SIMA: {0} | AccountPrefix: \"{1}\"",
                    EnableSIMA ? "ENABLED - Fleet mode" : "DISABLED - Single account",
                    AccountPrefix
                )
            );

            // Build 935 [Fix-2]: Symbol-specific log paths prevent file-lock collisions
            // when MES and MCL instances run concurrently on the same machine.
            string logsDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "NinjaTrader 8",
                "SIMA_Logs"
            );
            complianceLogPath = System.IO.Path.Combine(logsDir, $"ApexPerformance_{symbol}.json");
            dailySummaryCsvPath = System.IO.Path.Combine(logsDir, $"DailySummaries_{symbol}.csv");
            EnsureDailySummaryCsv();

            // Build 935 [Fix-3]: Run Risk Logic Audit here (DataLoaded) so instrument properties
            // (tickSize, pointValue, minContracts, maxContracts) are populated before audit runs.
            ExecuteRiskLogicAudit();

            // MP0: Initialize dictionary dispatch tables for IPC command routing
            InitializeCommandDispatchers();
        }

        private void InitializeCommandDispatchers()
        {
            // MP0-A: SetFlags dispatch (9 entries)
            _modeSetFlagsDispatch = new Dictionary<string, Action>(9, StringComparer.Ordinal)
            {
                ["MODE_RMA"] = () =>
                {
                    isRMAModeActive = !isRMAModeActive;
                    ClearClickTraderBorderIfInactive();
                },
                ["MODE_MOMO"] = () =>
                {
                    isMOMOModeActive = !isMOMOModeActive;
                    ClearClickTraderBorderIfInactive();
                },
                ["MODE_FFMA"] = () =>
                {
                    isFFMAModeArmed = true;
                    Print("V12.24: FFMA AUTO armed -- reversal scanner active");
                },
                ["MODE_M"] = () =>
                {
                    Print("V12.24: MODE_M received -- immediate FFMA entry pending");
                },
                ["FFMA_DISARM"] = () =>
                {
                    isFFMAModeArmed = false;
                    Print("V12.24: FFMA disarmed via panel ResetExecutionMode");
                },
                ["MODE_TREND_RMA"] = () =>
                {
                    isTrendRmaMode = true;
                    Print("IPC: TREND RMA Mode Enabled");
                },
                ["MODE_TREND_STD"] = () =>
                {
                    isTrendRmaMode = false;
                    Print("IPC: TREND Standard Mode Enabled");
                },
                ["MODE_RETEST_RMA"] = () =>
                {
                    isRetestRmaMode = true;
                    Print("IPC: RETEST RMA Mode Enabled");
                },
                ["MODE_RETEST_STD"] = () =>
                {
                    isRetestRmaMode = false;
                    Print("IPC: RETEST Standard Mode Enabled");
                },
            };

            // MP0-B: ExecuteMode dispatch (6 unique handlers, 8 command strings)
            // Shared handler for EXEC_TREND + EXEC_TREND_RMA
            Action execTrendHandler = () =>
            {
                double trendDist = CalculateTRENDStopDistance();
                int trendContracts = CalculatePositionSize(trendDist);
                Enqueue(ctx => ctx.ExecuteTRENDEntry(trendContracts));
            };

            // Shared handler for EXEC_RETEST variants
            Action execRetestHandler = () =>
            {
                double retestDist = CalculateRetestStopDistance();
                int retestContracts = CalculatePositionSize(retestDist);
                Enqueue(ctx => ctx.ExecuteRetestEntry(retestContracts));
            };

            _modeExecDispatch = new Dictionary<string, Action>(8, StringComparer.Ordinal)
            {
                ["EXEC_TREND"] = execTrendHandler,
                ["EXEC_TREND_RMA"] = execTrendHandler,
                ["EXEC_RETEST"] = execRetestHandler,
                ["EXEC_RETEST_PLUS"] = execRetestHandler,
                ["EXEC_RETEST_MINUS"] = execRetestHandler,
                ["EXEC_MOMO"] = () =>
                {
                    double momoStopDist = Math.Min(MOMOStopPoints, MaximumStop);
                    int momoContracts = CalculatePositionSize(momoStopDist);
                    double capturedMomoPrice = lastKnownPrice;
                    Enqueue(ctx => ctx.ExecuteMOMOEntry(capturedMomoPrice, momoContracts));
                },
                ["MODE_M"] = () =>
                {
                    // V12.24: Immediate market entry using FFMA trade DNA
                    double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                    double ema9Value = _ema9Val;
                    MarketPosition direction = currentPrice > ema9Value ? MarketPosition.Short : MarketPosition.Long;
                    Print(
                        string.Format(
                            "V12.24: MODE_M firing -- Price={0:F2} vs EMA9={1:F2} -> {2}",
                            currentPrice,
                            ema9Value,
                            direction
                        )
                    );
                    double stopPrice = direction == MarketPosition.Long ? Low[0] : High[0];
                    double ffmaStopDist = Math.Min(Math.Abs(currentPrice - stopPrice), MaximumStop);
                    if (ffmaStopDist < tickSize * 2)
                        ffmaStopDist = tickSize * 2;
                    int ffmaContracts = CalculatePositionSize(ffmaStopDist);
                    Enqueue(ctx => ctx.ExecuteFFMAEntry(direction, ffmaContracts));
                },
            };
        }

        private void Init_Services(string symbol)
        {
            // B984-F05: StickyState + IPC must complete BEFORE the load-complete gate flips
            // so EnsureStartupReady() gate does not open until services are ready.

            // Build 1103: Initialize sticky state path + hydrate persisted config.
            // MUST run BEFORE IPC startup so GET_LAYOUT serves last-synced state.
            string logsDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "NinjaTrader 8",
                "SIMA_Logs"
            );
            _stickyStatePath = System.IO.Path.Combine(logsDir, string.Format("StickyState_{0}.v12state", symbol));
            bool stickyLoaded = LoadStickyState();
            if (stickyLoaded)
                Print("[STICKY] Persisted state hydrated -- GET_LAYOUT will serve last-synced config");

            // V12.2 HEADLESS SAFETY: Start core services even if ChartControl is null (for background execution)
            // [Build 932]: Start IPC in DataLoaded so Control Surface connects even if market is closed/offline.
            StartIpcServer();
            TouchStrategyHeartbeat();
            PublishUiSnapshot();
        }

        private void OnStateChangeRealtime()
        {
            // B984-F10: Replaced box-drawing chars with ASCII-safe dashes and brackets.
            Print("--------------------------------------------------------------");
            Print("[OK] BMad HARDENED DEPLOYMENT PROTOCOL ACTIVE");
            Print(string.Format("Build: {0} | Sync: ONE SOURCE OF TRUTH", BUILD_TAG));
            Print("--------------------------------------------------------------");
            TouchStrategyHeartbeat();
            PublishUiSnapshot();
            StartWatchdog();

            if (EnableSIMA)
            {
                // Route realtime SIMA startup through the actor queue so lifecycle state
                // mutation and optional REAPER start stay ordered on the strategy thread.
                Enqueue(ctx =>
                {
                    ctx.EnumerateApexAccounts();
                    if (ctx.ReaperAuditEnabled)
                        ctx.StartReaperAudit();
                });
            }

            // V10.3: Subscribe to external signals for multi-chart sync
            // SignalBroadcaster.OnExternalCommand += HandleExternalSignal;

            if (ChartControl != null)
            {
                // Hotkeys attach at Normal priority (fast, no visual tree dependency)
                ChartControl.Dispatcher.InvokeAsync(
                    () =>
                    {
                        if (_isTerminating)
                            return;
                        AttachHotkeys();
                        AttachChartClickHandler();
                    },
                    System.Windows.Threading.DispatcherPriority.Normal
                );

                // Panel creation deferred to Loaded priority (runs AFTER Render pass)
                // This ensures the Chart Trader control is in the visual tree before discovery
                ChartControl.Dispatcher.InvokeAsync(
                    () =>
                    {
                        if (_isTerminating)
                            return;
                        CreatePanel();
                        StartPanelRefresh();
                        Print("REALTIME - Hotkeys: L=Long, S=Short, Shift+Click=RMA, F=Flatten");
                    },
                    System.Windows.Threading.DispatcherPriority.Loaded
                );
            }
        }

        private void OnStateChangeTerminated()
        {
            SetTerminatingAndStopWatchdog();

            if (_stickyStateEnabled)
            {
                SaveStickyState();
            }

            ShutdownUiAndServices();
            CleanupMmioAndEvents();
            CleanupDictionaries();
        }

        #region OnConnectionStatusUpdate - Build 984: Mid-session re-adoption on Rithmic reconnect

        protected override void OnConnectionStatusUpdate(ConnectionStatusEventArgs connectionStatusUpdate)
        {
            base.OnConnectionStatusUpdate(connectionStatusUpdate);
            RefreshActorOwnerThread();

            ConnectionStatus status = connectionStatusUpdate.Status;
            bool enableSima = EnableSIMA;
            State strategyState = State;
            Enqueue(ctx => ctx.ProcessOnConnectionStatusUpdate(status, enableSima, strategyState));
        }

        private void ProcessOnConnectionStatusUpdate(ConnectionStatus status, bool enableSima, State strategyState)
        {
            // B984-F11: Log when guard exits early so operators know reconnect re-adoption was skipped.
            if (!enableSima || strategyState != State.Realtime)
            {
                if (status == ConnectionStatus.Connected)
                    Print(
                        string.Format("[BUILD 984] Reconnect skipped -- SIMA={0}, State={1}", enableSima, strategyState)
                    );
                return;
            }

            if (status == ConnectionStatus.Disconnecting || status == ConnectionStatus.ConnectionLost)
            {
                // Gate REAPER until re-adoption completes after reconnect
                _orderAdoptionComplete = false;
                Print("[BUILD 984] Connection lost -- order adoption gate reset, REAPER paused.");
            }
            else if (status == ConnectionStatus.Connected)
            {
                // Re-adopt working orders after reconnect; runs on strategy thread via TriggerCustomEvent
                Print("[BUILD 984] Reconnected -- scheduling working order re-adoption.");
                try
                {
                    Enqueue(ctx => ctx.HydrateWorkingOrdersFromBroker());
                }
                catch (Exception exReconnect)
                {
                    Print(
                        "[B983-D6] CRITICAL: Reconnect re-adoption Enqueue failed: "
                            + exReconnect.ToString()
                            + " -- orders may not be re-adopted. Manual intervention required."
                    );
                }
            }
        }

        #endregion

        #region OnMarketData - V10.1: Process IPC on every tick for real-time responsiveness

        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {
            // [EPIC-5-PERF] Latency instrumentation
            var probe = LatencyProbe.Start();

            try
            {
                RefreshActorOwnerThread();

                // Only process on primary instrument
                if (marketDataUpdate.MarketDataType == MarketDataType.Last)
                {
                    if (!EnsureStartupReady(nameof(OnMarketData)))
                        return;
                    TouchStrategyHeartbeat();

                    // Update last known price for real-time tracking
                    lastKnownPrice = marketDataUpdate.Price;

                    // B984-F12: Rate-gate UI snapshot -- publish only every 5 ticks to reduce dispatcher pressure.
                    _uiSnapshotTickCounter = (_uiSnapshotTickCounter + 1) % 5;
                    if (_uiSnapshotTickCounter == 0)
                        PublishUiSnapshot();

                    // Process IPC commands immediately on every tick
                    // This ensures Remote App buttons work even outside session time
                    ProcessIpcCommands();
                }
            }
            finally
            {
                // [EPIC-5-PERF] Record latency
                probe = probe.Stop();
                _histOnMarketData.Record(probe);
            }
        }

        #endregion
    }
}
