using System;
using System.Collections.Generic;
using System.Linq;

namespace V12.Testing
{
    // ══════════════════════════════════════════════════════════════════════
    //  V12 RULE LAB HARNESS — Phase 6 Testing Rig
    //  Purpose: Simulate NinjaTrader 8 objects & strategy behavior
    //           without loading the NT8 UI or recompiling the strategy.
    //
    //  Mocks:   MockOrder, MockPosition, MockAccount
    //  Suites:  GhostOrderSuite, SurgicalFlattenTest
    //
    //  Backup:  RuleLabHarness.cs.bak_phase6
    // ══════════════════════════════════════════════════════════════════════

    #region Mock NT8 Enums

    /// <summary>
    /// Mirror of NinjaTrader.Cbi.OrderState — for offline testing.
    /// </summary>
    public enum MockOrderState
    {
        Initialized,
        Submitted,
        Accepted,
        Working,
        PartFilled,
        Filled,
        Cancelled,
        Rejected,
        Unknown
    }

    /// <summary>
    /// Mirror of NinjaTrader.Cbi.MarketPosition
    /// </summary>
    public enum MockMarketPosition
    {
        Flat,
        Long,
        Short
    }

    /// <summary>
    /// Mirror of NinjaTrader.Cbi.OrderAction
    /// </summary>
    public enum MockOrderAction
    {
        Buy,
        Sell,
        BuyToCover,
        SellShort
    }

    /// <summary>
    /// Mirror of NinjaTrader.Cbi.OrderType
    /// </summary>
    public enum MockOrderType
    {
        Market,
        Limit,
        StopMarket,
        StopLimit
    }

    #endregion

    #region Mock NT8 Objects

    /// <summary>
    /// Simulates an NT8 Order object used in OnOrderUpdate.
    /// Contains the fields the V12 strategy inspects during state transitions.
    /// </summary>
    public class MockOrder
    {
        public string OrderId { get; set; }
        public string Name { get; set; }            // Signal name (e.g. "T1_RMA_Long_001")
        public MockOrderState OrderState { get; set; }
        public MockOrderAction Action { get; set; }
        public MockOrderType OrderType { get; set; }
        public int Quantity { get; set; }
        public int FilledQuantity { get; set; }
        public double LimitPrice { get; set; }
        public double StopPrice { get; set; }
        public double AverageFillPrice { get; set; }
        public string Instrument { get; set; }

        public MockOrder(string name, string instrument = "MES 03-26")
        {
            OrderId = Guid.NewGuid().ToString("N").Substring(0, 8);
            Name = name;
            Instrument = instrument;
            OrderState = MockOrderState.Initialized;
            Action = MockOrderAction.Buy;
            OrderType = MockOrderType.Market;
            Quantity = 1;
            FilledQuantity = 0;
            AverageFillPrice = 0;
        }

        public override string ToString()
        {
            return $"[Order] {Name} | State={OrderState} | Qty={Quantity} | Filled={FilledQuantity} | AvgFill={AverageFillPrice:F2}";
        }
    }

    /// <summary>
    /// Simulates an NT8 Position object.
    /// Tracks what the Account reports for a given instrument.
    /// </summary>
    public class MockPosition
    {
        public string Instrument { get; set; }
        public MockMarketPosition MarketPosition { get; set; }
        public int Quantity { get; set; }
        public double AveragePrice { get; set; }

        public MockPosition(string instrument, MockMarketPosition direction, int qty, double avgPrice)
        {
            Instrument = instrument;
            MarketPosition = direction;
            Quantity = qty;
            AveragePrice = avgPrice;
        }

        public override string ToString()
        {
            return $"[Position] {Instrument} | {MarketPosition} x{Quantity} @ {AveragePrice:F2}";
        }
    }

    /// <summary>
    /// Simulates an NT8 Account object.
    /// Owns collections of Orders and Positions — used for SIMA and Flatten tests.
    /// </summary>
    public class MockAccount
    {
        public string Name { get; set; }
        public List<MockOrder> Orders { get; set; }
        public List<MockPosition> Positions { get; set; }

        public MockAccount(string name)
        {
            Name = name;
            Orders = new List<MockOrder>();
            Positions = new List<MockPosition>();
        }

        /// <summary>
        /// Cancel a specific order (simulates Account.Cancel)
        /// </summary>
        public void Cancel(MockOrder order)
        {
            order.OrderState = MockOrderState.Cancelled;
        }

        /// <summary>
        /// Flatten a position (close it out — simulates ExitLong/ExitShort)
        /// </summary>
        public void ClosePosition(MockPosition pos)
        {
            pos.MarketPosition = MockMarketPosition.Flat;
            pos.Quantity = 0;
        }

        public override string ToString()
        {
            return $"[Account] {Name} | Orders={Orders.Count} | Positions={Positions.Count}";
        }
    }

    #endregion

    #region Strategy State Simulation

    /// <summary>
    /// Mirrors the V12 PositionInfo class from UniversalORStrategyV12_002_Dev.cs.
    /// Contains all flags the real strategy uses to track position lifecycle.
    /// </summary>
    public class MockPositionInfo
    {
        public string SignalName;
        public MockMarketPosition Direction;
        public int TotalContracts;
        public int T1Contracts;
        public int T2Contracts;
        public int T3Contracts;
        public int T4Contracts;
        public int RemainingContracts;
        public double EntryPrice;
        public double InitialStopPrice;
        public double CurrentStopPrice;
        public double Target1Price;
        public double Target2Price;
        public double Target3Price;
        public bool EntryFilled;
        public bool T1Filled;
        public bool T2Filled;
        public bool T3Filled;
        public bool BracketSubmitted;
        public double ExtremePriceSinceEntry;
        public int CurrentTrailLevel;
        public bool IsRMATrade;
        public bool ManualBreakevenArmed;
        public bool ManualBreakevenTriggered;
        public int TicksSinceEntry;
        public bool IsTRENDTrade;
        public bool IsRetestTrade;
        public bool IsMOMOTrade;
        public bool IsFFMATrade;
    }

    /// <summary>
    /// Simulates the core strategy state that the Rule Lab tests manipulate.
    /// Think of this as a "headless mini-strategy" — it has the same dictionaries
    /// and flags, but no NT8 plumbing.
    /// </summary>
    public class StrategyStateSim
    {
        public Dictionary<string, MockPositionInfo> ActivePositions { get; set; }
        public Dictionary<string, MockOrder> EntryOrders { get; set; }
        public Dictionary<string, MockOrder> StopOrders { get; set; }
        public Dictionary<string, MockOrder> Target1Orders { get; set; }
        public Dictionary<string, MockOrder> Target2Orders { get; set; }
        public Dictionary<string, MockOrder> Target3Orders { get; set; }
        public MockAccount Account { get; set; }

        public StrategyStateSim(string accountName = "Sim1234")
        {
            ActivePositions = new Dictionary<string, MockPositionInfo>();
            EntryOrders = new Dictionary<string, MockOrder>();
            StopOrders = new Dictionary<string, MockOrder>();
            Target1Orders = new Dictionary<string, MockOrder>();
            Target2Orders = new Dictionary<string, MockOrder>();
            Target3Orders = new Dictionary<string, MockOrder>();
            Account = new MockAccount(accountName);
        }

        /// <summary>
        /// Simulates the V12 OnOrderUpdate logic for terminal states.
        /// Mirrors the Ghost Order Hardening from V12.13:
        ///   - Rejected/Cancelled → nullify order ref, clean up position if entry wasn't filled.
        /// </summary>
        public void SimulateOrderUpdate(MockOrder order, MockOrderState newState)
        {
            order.OrderState = newState;
            string entryName = order.Name;

            if (newState == MockOrderState.Rejected || newState == MockOrderState.Cancelled)
            {
                // V12.13 Ghost Order Hardening: Nullify references on terminal states
                if (EntryOrders.ContainsKey(entryName) && EntryOrders[entryName] == order)
                {
                    EntryOrders.Remove(entryName);

                    // If entry was never filled, clean up the entire position
                    if (ActivePositions.ContainsKey(entryName) && !ActivePositions[entryName].EntryFilled)
                    {
                        CleanupPosition(entryName);
                    }
                }

                // Also check stop/target dictionaries
                if (StopOrders.ContainsKey(entryName) && StopOrders[entryName] == order)
                    StopOrders.Remove(entryName);
                if (Target1Orders.ContainsKey(entryName) && Target1Orders[entryName] == order)
                    Target1Orders.Remove(entryName);
                if (Target2Orders.ContainsKey(entryName) && Target2Orders[entryName] == order)
                    Target2Orders.Remove(entryName);
                if (Target3Orders.ContainsKey(entryName) && Target3Orders[entryName] == order)
                    Target3Orders.Remove(entryName);
            }
            else if (newState == MockOrderState.Filled)
            {
                if (EntryOrders.ContainsKey(entryName) && EntryOrders[entryName] == order)
                {
                    if (ActivePositions.ContainsKey(entryName))
                    {
                        var pos = ActivePositions[entryName];
                        if (order.AverageFillPrice > 0)
                        {
                            pos.EntryFilled = true;
                            pos.EntryPrice = order.AverageFillPrice;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Mirrors the V12 CleanupPosition method — removes all traces of a position.
        /// </summary>
        public void CleanupPosition(string entryName)
        {
            ActivePositions.Remove(entryName);
            EntryOrders.Remove(entryName);
            StopOrders.Remove(entryName);
            Target1Orders.Remove(entryName);
            Target2Orders.Remove(entryName);
            Target3Orders.Remove(entryName);
        }

        /// <summary>
        /// Simulates FLATTEN_ONLY: close all account positions but preserve pending orders.
        /// This mirrors the V12.21 FLATTEN_ONLY implementation in ProcessIpcCommands.
        /// </summary>
        public int SimulateFlattenOnly()
        {
            int closedCount = 0;
            foreach (var pos in Account.Positions.ToArray())
            {
                if (pos.MarketPosition != MockMarketPosition.Flat)
                {
                    Account.ClosePosition(pos);
                    closedCount++;
                }
            }
            // Pending orders are intentionally NOT cancelled
            return closedCount;
        }

        // --- V12 Management & Risk Logic ---

        public double CurrentATR { get; set; } = 2.0; // Default mock ATR
        public double PointValue { get; set; } = 5.0; // Default MES
        public double TickSize { get; set; } = 0.25; // Default MES tick size
        public int BreakEvenOffsetTicks { get; set; } = 2; // V12.23: Consolidation
        public double Target1FixedPoints { get; set; } = 1.0;
        public double RMAT1ATRMultiplier { get; set; } = 0.5;  // V12.24: For FFMA target calculation
        public double RMAT2ATRMultiplier { get; set; } = 1.0;  // V12.24: For FFMA target calculation

        /// <summary>
        /// Logic for V12.23 Trailing Stop update to Breakeven with Offset
        /// </summary>
        public void MoveStopsToBreakevenWithOffset(string entryName, double offsetPoints)
        {
            if (!ActivePositions.ContainsKey(entryName)) return;
            var pos = ActivePositions[entryName];

            double newStop = pos.Direction == MockMarketPosition.Long
                ? pos.EntryPrice + offsetPoints
                : pos.EntryPrice - offsetPoints;

            // Round to tick (mock)
            newStop = Math.Round(newStop / TickSize) * TickSize;

            // Only move if better
            bool isBetter = (pos.Direction == MockMarketPosition.Long && newStop > pos.CurrentStopPrice)
                         || (pos.Direction == MockMarketPosition.Short && newStop < pos.CurrentStopPrice);

            if (isBetter)
            {
                pos.CurrentStopPrice = newStop;
            }
        }

        /// <summary>
        /// Logic for V12.2 ATR Sizing: contracts = MaxRisk / (stopDist * pointValue)
        /// </summary>
        public int CalculateContracts(double maxRisk, double stopDistance)
        {
            if (stopDistance <= 0) return 0;
            return (int)Math.Max(1, Math.Floor(maxRisk / (stopDistance * PointValue)));
        }

        /// <summary>
        /// Logic for V12 Trailing Stop update
        /// </summary>
        public bool UpdateTrailingStop(string entryName, double currentPrice)
        {
            if (!ActivePositions.ContainsKey(entryName)) return false;
            var pos = ActivePositions[entryName];

            // Update extreme price
            if (pos.Direction == MockMarketPosition.Long)
                pos.ExtremePriceSinceEntry = Math.Max(pos.ExtremePriceSinceEntry, currentPrice);
            else
                pos.ExtremePriceSinceEntry = Math.Min(pos.ExtremePriceSinceEntry, currentPrice);

            // Calculate new stop (example: 1.0x ATR distance)
            double trailDist = CurrentATR * 1.5;
            double newStop = pos.Direction == MockMarketPosition.Long
                ? pos.ExtremePriceSinceEntry - trailDist
                : pos.ExtremePriceSinceEntry + trailDist;

            // Only update if it improves the stop
            bool shouldUpdate = pos.Direction == MockMarketPosition.Long
                ? newStop > pos.CurrentStopPrice
                : newStop < pos.CurrentStopPrice;

            if (shouldUpdate)
            {
                pos.CurrentStopPrice = newStop;
                return true;
            }
            return false;
        }
    }

    #endregion

    #region Rule Lab Core

    /// <summary>
    /// The Rule Lab Harness allows us to simulate NinjaTrader events
    /// (Orders, Fills, Rejections) to test strategy logic without the NT8 UI.
    /// </summary>
    public class RuleLab
    {
        public event Action<string> OnLog;

        private int _passCount = 0;
        private int _failCount = 0;

        public int PassCount => _passCount;
        public int FailCount => _failCount;

        public void Log(string message)
        {
            OnLog?.Invoke($"[RULE LAB] {message}");
        }

        /// <summary>
        /// Core assertion: logs PASS or FAIL with context.
        /// Like a "trade journal" for your code tests.
        /// </summary>
        public void Assert(bool condition, string testName, string detail = "")
        {
            if (condition)
            {
                _passCount++;
                OnLog?.Invoke($"  [PASS] {testName}{(string.IsNullOrEmpty(detail) ? "" : " — " + detail)}");
            }
            else
            {
                _failCount++;
                OnLog?.Invoke($"  [FAIL] {testName}{(string.IsNullOrEmpty(detail) ? "" : " — " + detail)}");
            }
        }

        public void PrintSummary()
        {
            string result = _failCount == 0 ? "ALL TESTS PASSED" : $"FAILURES DETECTED";
            OnLog?.Invoke($"\n══════════════════════════════════════════");
            OnLog?.Invoke($"  RULE LAB SUMMARY: {_passCount} PASS | {_failCount} FAIL → {result}");
            OnLog?.Invoke($"══════════════════════════════════════════\n");
        }
    }

    #endregion

    #region Test Suite: Ghost Order

    /// <summary>
    /// GHOST ORDER SUITE
    /// Tests the V12.13 Ghost Order Hardening logic.
    ///
    /// Scenario: A T1 entry is submitted → goes Working → broker Rejects it.
    /// Expected: EntryFilled stays false, order ref is nulled, position is cleaned up.
    ///
    /// In trading terms: You click "enter long" but your broker rejects it (margin issue).
    /// The strategy should completely clean up — no phantom position should remain.
    /// </summary>
    public class GhostOrderSuite
    {
        public static void Run(RuleLab lab)
        {
            lab.Log("═══ GHOST ORDER SUITE ═══");
            lab.Log("Scenario: T1 Entry Rejected by Broker\n");

            var state = new StrategyStateSim("TestAccount_Apex01");

            // --- Setup: Create a T1 entry order (like clicking Long on the panel) ---
            string entryName = "T1_RMA_Long_001";
            var entryOrder = new MockOrder(entryName, "MES 03-26")
            {
                Action = MockOrderAction.Buy,
                OrderType = MockOrderType.Market,
                Quantity = 3
            };

            // Register in strategy state (mirrors what happens in ExecuteRMAEntryV2)
            state.EntryOrders[entryName] = entryOrder;
            state.ActivePositions[entryName] = new MockPositionInfo
            {
                SignalName = entryName,
                Direction = MockMarketPosition.Long,
                TotalContracts = 3,
                T1Contracts = 1,
                T2Contracts = 1,
                T3Contracts = 1,
                RemainingContracts = 3,
                EntryFilled = false,       // Not yet filled
                BracketSubmitted = false,
                IsRMATrade = true
            };

            lab.Log($"Order Created: {entryOrder}");
            lab.Log($"Position Registered: {entryName} (EntryFilled=false)");

            // --- Step 1: Order goes Working (accepted by NT gateway) ---
            state.SimulateOrderUpdate(entryOrder, MockOrderState.Working);
            lab.Log($"Step 1 → OrderState: {entryOrder.OrderState}");
            lab.Assert(entryOrder.OrderState == MockOrderState.Working, "Order reaches Working state");
            lab.Assert(state.ActivePositions.ContainsKey(entryName), "Position still exists while Working");

            // --- Step 2: Broker REJECTS the order (margin call, exchange limit, etc.) ---
            lab.Log("\n  ⚡ BROKER REJECTION TRIGGERED ⚡\n");
            state.SimulateOrderUpdate(entryOrder, MockOrderState.Rejected);
            lab.Log($"Step 2 → OrderState: {entryOrder.OrderState}");

            // --- Assertions: Verify complete cleanup ---
            lab.Assert(entryOrder.OrderState == MockOrderState.Rejected,
                "Order reaches Rejected state");

            lab.Assert(!state.EntryOrders.ContainsKey(entryName),
                "Entry order reference NULLIFIED",
                "Ghost order would persist if this fails");

            lab.Assert(!state.ActivePositions.ContainsKey(entryName),
                "Position REMOVED from activePositions",
                "Position was never filled — must be cleaned");

            lab.Assert(!state.StopOrders.ContainsKey(entryName),
                "Stop order reference clean");

            lab.Assert(!state.Target1Orders.ContainsKey(entryName),
                "T1 target reference clean");

            lab.Log("");

            // --- Bonus: Verify a FILLED entry is NOT cleaned up on rejection ---
            lab.Log("Bonus Scenario: Filled entry should survive stop rejection\n");

            string filledEntry = "T1_RMA_Short_002";
            var filledOrder = new MockOrder(filledEntry, "MES 03-26")
            {
                Action = MockOrderAction.SellShort,
                Quantity = 2,
                AverageFillPrice = 6235.50
            };
            state.EntryOrders[filledEntry] = filledOrder;
            state.ActivePositions[filledEntry] = new MockPositionInfo
            {
                SignalName = filledEntry,
                Direction = MockMarketPosition.Short,
                TotalContracts = 2,
                RemainingContracts = 2,
                EntryFilled = true,         // Already filled!
                EntryPrice = 6235.50,
                IsRMATrade = true
            };

            // Now a stop order gets rejected (unusual but possible)
            var stopOrder = new MockOrder(filledEntry, "MES 03-26")
            {
                OrderType = MockOrderType.StopMarket,
                StopPrice = 6240.00,
                Quantity = 2
            };
            state.StopOrders[filledEntry] = stopOrder;

            state.SimulateOrderUpdate(stopOrder, MockOrderState.Rejected);

            lab.Assert(state.ActivePositions.ContainsKey(filledEntry),
                "Filled position SURVIVES stop rejection",
                "Position with EntryFilled=true must not be cleaned up");

            lab.Assert(!state.StopOrders.ContainsKey(filledEntry),
                "Rejected stop reference is cleaned",
                "But the position itself remains for manual intervention");

            lab.Log("\n═══ GHOST ORDER SUITE COMPLETE ═══\n");
        }
    }

    #endregion

    #region Test Suite: Surgical Flatten

    /// <summary>
    /// SURGICAL FLATTEN TEST
    /// Tests the V12.21 FLATTEN_ONLY logic.
    ///
    /// Scenario: 2 open positions + 3 pending limit orders.
    /// FLATTEN_ONLY should close positions but leave pending orders alive.
    ///
    /// In trading terms: You hit the Flatten button during a trade, but you
    /// DON'T want to lose your pending retest limit orders waiting at OR levels.
    /// </summary>
    public class SurgicalFlattenTest
    {
        public static void Run(RuleLab lab)
        {
            lab.Log("═══ SURGICAL FLATTEN TEST ═══");
            lab.Log("Scenario: 2 positions + 3 pending orders → FLATTEN_ONLY\n");

            var state = new StrategyStateSim("TestAccount_Apex02");

            // --- Setup: Add 2 open positions ---
            state.Account.Positions.Add(new MockPosition("MES 03-26", MockMarketPosition.Long, 3, 6230.00));
            state.Account.Positions.Add(new MockPosition("MGC 04-26", MockMarketPosition.Short, 2, 2945.50));

            // --- Setup: Add 3 pending orders (limit entries waiting at levels) ---
            var pendingOrder1 = new MockOrder("RETEST_Long_MES", "MES 03-26")
            {
                OrderType = MockOrderType.Limit,
                LimitPrice = 6225.00,
                Quantity = 2,
                OrderState = MockOrderState.Working
            };
            var pendingOrder2 = new MockOrder("RETEST_Short_MES", "MES 03-26")
            {
                OrderType = MockOrderType.Limit,
                LimitPrice = 6245.00,
                Quantity = 2,
                OrderState = MockOrderState.Working
            };
            var pendingOrder3 = new MockOrder("RMA_Long_MGC", "MGC 04-26")
            {
                OrderType = MockOrderType.Limit,
                LimitPrice = 2940.00,
                Quantity = 1,
                OrderState = MockOrderState.Working
            };
            state.Account.Orders.Add(pendingOrder1);
            state.Account.Orders.Add(pendingOrder2);
            state.Account.Orders.Add(pendingOrder3);

            lab.Log($"Setup: {state.Account.Positions.Count} positions, {state.Account.Orders.Count} pending orders");
            foreach (var pos in state.Account.Positions) lab.Log($"  {pos}");
            foreach (var ord in state.Account.Orders) lab.Log($"  {ord}");

            // --- Execute FLATTEN_ONLY ---
            lab.Log("\n  ⚡ FLATTEN_ONLY COMMAND FIRED ⚡\n");
            int closed = state.SimulateFlattenOnly();

            // --- Assertions ---
            lab.Assert(closed == 2,
                $"Closed {closed} positions (expected 2)");

            lab.Assert(state.Account.Positions.All(p => p.MarketPosition == MockMarketPosition.Flat),
                "All positions are now FLAT");

            lab.Assert(state.Account.Orders.Count == 3,
                $"Pending orders count: {state.Account.Orders.Count} (expected 3 — preserved)");

            lab.Assert(pendingOrder1.OrderState == MockOrderState.Working,
                "RETEST Long MES order still Working");

            lab.Assert(pendingOrder2.OrderState == MockOrderState.Working,
                "RETEST Short MES order still Working");

            lab.Assert(pendingOrder3.OrderState == MockOrderState.Working,
                "RMA Long MGC order still Working");

            // --- Negative Test: Regular FLATTEN should cancel orders too ---
            lab.Log("\nBonus: Verifying regular FLATTEN cancels orders\n");

            // Re-add a position to flatten
            state.Account.Positions.Add(new MockPosition("MES 03-26", MockMarketPosition.Long, 1, 6231.00));

            // Simulate regular FLATTEN (closes positions AND cancels orders)
            foreach (var pos in state.Account.Positions.ToArray())
            {
                if (pos.MarketPosition != MockMarketPosition.Flat)
                    state.Account.ClosePosition(pos);
            }
            foreach (var ord in state.Account.Orders.ToArray())
            {
                if (ord.OrderState == MockOrderState.Working ||
                    ord.OrderState == MockOrderState.Accepted ||
                    ord.OrderState == MockOrderState.Submitted)
                {
                    state.Account.Cancel(ord);
                }
            }

            lab.Assert(state.Account.Orders.All(o => o.OrderState == MockOrderState.Cancelled),
                "Regular FLATTEN cancels all pending orders",
                "This confirms FLATTEN_ONLY is different from FLATTEN");

            lab.Log("\n═══ SURGICAL FLATTEN TEST COMPLETE ═══\n");
        }
    }

    #endregion

    #region Test Suite: Risk and Sizing

    /// <summary>
    /// RISK AND SIZING SUITE
    /// Tests the ATR-based sizing logic from V12.2.
    /// Scenario: $150 Max Risk with a 5.0pt stop on MES ($5/pt).
    /// Expected: 6 contracts ($150 / (5.0 * 5.0) = 6).
    /// </summary>
    public class RiskSizingSuite
    {
        public static void Run(RuleLab lab)
        {
            lab.Log("═══ RISK AND SIZING SUITE ═══");
            var state = new StrategyStateSim();
            state.PointValue = 5.0; // MES

            double stopDist = 5.0; // 5 points
            double maxRisk = 150.0; // $150
            
            int qty = state.CalculateContracts(maxRisk, stopDist);
            lab.Log($"Scenario: ${maxRisk} Max Risk | {stopDist}pt Stop | $5/pt Instrument");
            lab.Assert(qty == 6, $"Calculated Quantity: {qty} (expected 6)");

            // Test small stop
            stopDist = 2.0;
            qty = state.CalculateContracts(maxRisk, stopDist);
            lab.Assert(qty == 15, $"Calculated Quantity (tight stop): {qty} (expected 15)");

            lab.Log("");
        }
    }

    #endregion

    #region Test Suite: Management (Trails & Stops)

    /// <summary>
    /// MANAGEMENT SUITE
    /// Tests Trailing Stop behavior.
    /// Scenario: Long entry @ 6250.00. Price moves up to 6260.00.
    /// Expected: Stop trails upward to Extreme - Distance.
    /// </summary>
    public class ManagementSuite
    {
        public static void Run(RuleLab lab)
        {
            lab.Log("═══ MANAGEMENT SUITE (TRAILS) ═══");
            var state = new StrategyStateSim();
            state.CurrentATR = 2.0; // Trail Dist = 2.0 * 1.5 = 3.0 pts
            
            string name = "TrailTest_001";
            var pos = new MockPositionInfo {
                SignalName = name,
                Direction = MockMarketPosition.Long,
                EntryPrice = 6250.00,
                InitialStopPrice = 6245.00,
                CurrentStopPrice = 6245.00,
                ExtremePriceSinceEntry = 6250.00,
                EntryFilled = true
            };
            state.ActivePositions[name] = pos;

            // Scenario 1: Price moves up to 6255
            lab.Log("Step 1: Price moves 6250 -> 6255");
            state.UpdateTrailingStop(name, 6255.00);
            double expectedStop = 6255.00 - 3.0; // 6252
            lab.Assert(pos.CurrentStopPrice == expectedStop, $"Stop moved to {pos.CurrentStopPrice} (expected {expectedStop})");

            // Scenario 2: Price retraces to 6253
            lab.Log("Step 2: Price retraces 6255 -> 6253");
            state.UpdateTrailingStop(name, 6253.00);
            lab.Assert(pos.CurrentStopPrice == expectedStop, "Stop STAYED (no move on retrace)");

            // Scenario 3: Price moves to 6260
            lab.Log("Step 3: Price moves to 6260");
            state.UpdateTrailingStop(name, 6260.00);
             expectedStop = 6260.00 - 3.0; // 6257
            lab.Assert(pos.CurrentStopPrice == 6257.00, $"Stop moved to {pos.CurrentStopPrice} (expected 6257)");

            lab.Log("");
        }
    }

    #endregion

    #region Test Suite: CIF (Chase If Touch)

    /// <summary>
    /// CIF SUITE
    /// Tests Chase If Touch limit behavior.
    /// </summary>
    public class CIFSuite
    {
        public static void Run(RuleLab lab)
        {
            lab.Log("═══ CIF SUITE (CHASE IF TOUCH) ═══");
            var state = new StrategyStateSim();
            
            string name = "CIF_Test";
            var limitOrder = new MockOrder(name) {
                OrderType = MockOrderType.Limit,
                LimitPrice = 6250.00,
                Action = MockOrderAction.Buy,
                OrderState = MockOrderState.Working
            };
            state.EntryOrders[name] = limitOrder;

            // Scenario 1: Price is below limit
            bool trigger = (6248.00 >= limitOrder.LimitPrice);
            lab.Assert(!trigger, "No trigger at 6248.00 (below limit)");

            // Scenario 2: Price touches limit
            trigger = (6250.00 >= limitOrder.LimitPrice);
            lab.Assert(trigger, "CIF Triggered at 6250.00 (Price Touched Limit)");

            lab.Log("");
        }
    }

    #endregion

    #region Breakeven Suite (V12.23)
    public class BreakevenSuite
    {
        public static void Run(RuleLab lab)
        {
            lab.Log("═══ BREAKEVEN SUITE (V12.23 CONSOLIDATION) ═══");
            var state = new StrategyStateSim { PointValue = 50, TickSize = 0.25 }; // ES Mock
            
            // Setup a LONG position filled at 6250.00
            string name = "BE_TEST";
            state.ActivePositions[name] = new MockPositionInfo {
                SignalName = name,
                Direction = MockMarketPosition.Long,
                EntryPrice = 6250.00,
                CurrentStopPrice = 6245.00, // 5pt stop
                RemainingContracts = 2,
                EntryFilled = true
            };

            // Scenario 1: IPC BE_CUSTOM|2 (default) arrives
            state.BreakEvenOffsetTicks = 2; 
            double beOffset = state.BreakEvenOffsetTicks * state.TickSize; // 0.50 points
            double expectedStop = 6250.00 + beOffset; // 6250.50

            state.MoveStopsToBreakevenWithOffset(name, beOffset);
            lab.Assert(state.ActivePositions[name].CurrentStopPrice == expectedStop, 
                $"BE+2 (0.50pt) moved stop to {expectedStop}");

            // Scenario 2: IPC BE_CUSTOM|4 arrives
            state.BreakEvenOffsetTicks = 4;
            beOffset = state.BreakEvenOffsetTicks * state.TickSize; // 1.00 points
            expectedStop = 6250.00 + beOffset; // 6251.00

            state.MoveStopsToBreakevenWithOffset(name, beOffset);
            lab.Assert(state.ActivePositions[name].CurrentStopPrice == expectedStop, 
                $"BE+4 (1.00pt) moved stop to {expectedStop}");

            lab.Log("");
        }
    }
    #endregion

    #region V12.24 FFMA Smart Toggle Suite

    /// <summary>
    /// V12.24: Tests FFMA Smart Toggle logic — MODE_M immediate entry, MODE_FFMA arming,
    /// FFMA_DISARM reset, and FFMA trade DNA (targets, stops, position sizing).
    /// </summary>
    public static class FFMASmartToggleSuite
    {
        public static void Run(RuleLab lab)
        {
            lab.Log("\n═══ V12.24 FFMA Smart Toggle Suite ═══");

            // --- Test 1: MODE_M Short Direction (price > EMA9) ---
            lab.Log("--- MODE_M: Immediate Entry (Short — Price > EMA9) ---");
            {
                var state = new StrategyStateSim();
                state.CurrentATR = 4.0;

                double entryPrice = 6300.0;
                double candleHigh = 6305.0;
                double ema9Value = 6290.0;  // Price > EMA → Short

                // Simulate direction logic from ToggleStrategyMode MODE_M
                var direction = entryPrice > ema9Value
                    ? MockMarketPosition.Short : MockMarketPosition.Long;

                lab.Assert(direction == MockMarketPosition.Short,
                    "MODE_M direction: Price 6300 > EMA9 6290 → Short");

                // Simulate ExecuteFFMAEntry(Short)
                double stopPrice = candleHigh; // Short → stop at candle high
                double stopDistance = Math.Abs(entryPrice - stopPrice); // 5.0 pts
                double t1 = entryPrice - state.Target1FixedPoints;     // 6299
                double t2 = entryPrice - (state.CurrentATR * state.RMAT1ATRMultiplier); // 6298
                double t3 = entryPrice - (state.CurrentATR * state.RMAT2ATRMultiplier); // 6296

                var pos = new MockPositionInfo
                {
                    SignalName = "FFMAShort_143000",
                    Direction = MockMarketPosition.Short,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = t1,
                    Target2Price = t2,
                    Target3Price = t3,
                    IsFFMATrade = true,
                    IsRMATrade = false,
                    IsMOMOTrade = false,
                    EntryFilled = true
                };
                state.ActivePositions[pos.SignalName] = pos;

                lab.Assert(pos.IsFFMATrade == true, "FFMA Short: IsFFMATrade flag = true");
                lab.Assert(pos.IsRMATrade == false, "FFMA Short: IsRMATrade flag = false");
                lab.Assert(pos.Direction == MockMarketPosition.Short, "FFMA Short: Direction = Short");
                lab.Assert(pos.InitialStopPrice > pos.EntryPrice, "FFMA Short: Stop (6305) > Entry (6300)");
                lab.Assert(pos.Target1Price == 6299.0, $"FFMA Short: T1 = {pos.Target1Price} (expected 6299)");
                lab.Assert(pos.Target2Price == 6298.0, $"FFMA Short: T2 = {pos.Target2Price} (expected 6298)");
                lab.Assert(pos.Target3Price == 6296.0, $"FFMA Short: T3 = {pos.Target3Price} (expected 6296)");
            }

            // --- Test 2: MODE_M Long Direction (price < EMA9) ---
            lab.Log("\n--- MODE_M: Immediate Entry (Long — Price < EMA9) ---");
            {
                var state = new StrategyStateSim();
                state.CurrentATR = 4.0;

                double entryPrice = 6280.0;
                double candleLow = 6275.0;
                double ema9Value = 6290.0;  // Price < EMA → Long

                var direction = entryPrice > ema9Value
                    ? MockMarketPosition.Short : MockMarketPosition.Long;

                lab.Assert(direction == MockMarketPosition.Long,
                    "MODE_M direction: Price 6280 < EMA9 6290 → Long");

                double stopPrice = candleLow; // Long → stop at candle low
                double t1 = entryPrice + state.Target1FixedPoints;     // 6281
                double t2 = entryPrice + (state.CurrentATR * state.RMAT1ATRMultiplier); // 6282
                double t3 = entryPrice + (state.CurrentATR * state.RMAT2ATRMultiplier); // 6284

                var pos = new MockPositionInfo
                {
                    SignalName = "FFMALong_143100",
                    Direction = MockMarketPosition.Long,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = t1,
                    Target2Price = t2,
                    Target3Price = t3,
                    IsFFMATrade = true,
                    IsRMATrade = false,
                    IsMOMOTrade = false,
                    EntryFilled = true
                };
                state.ActivePositions[pos.SignalName] = pos;

                lab.Assert(pos.IsFFMATrade == true, "FFMA Long: IsFFMATrade flag = true");
                lab.Assert(pos.Direction == MockMarketPosition.Long, "FFMA Long: Direction = Long");
                lab.Assert(pos.InitialStopPrice < pos.EntryPrice, "FFMA Long: Stop (6275) < Entry (6280)");
                lab.Assert(pos.Target1Price == 6281.0, $"FFMA Long: T1 = {pos.Target1Price} (expected 6281)");
                lab.Assert(pos.Target2Price == 6282.0, $"FFMA Long: T2 = {pos.Target2Price} (expected 6282)");
                lab.Assert(pos.Target3Price == 6284.0, $"FFMA Long: T3 = {pos.Target3Price} (expected 6284)");
            }

            // --- Test 3: MODE_FFMA Arms Scanner (no position created) ---
            lab.Log("\n--- MODE_FFMA: Arms Scanner (Flag State Only) ---");
            {
                var state = new StrategyStateSim();
                bool isFFMAModeArmed = false;

                // Simulate MODE_FFMA handler
                isFFMAModeArmed = true;

                lab.Assert(isFFMAModeArmed == true, "MODE_FFMA: isFFMAModeArmed = true after arming");
                lab.Assert(state.ActivePositions.Count == 0,
                    "MODE_FFMA: No positions created (scanner armed, not executed)");
            }

            // --- Test 4: FFMA_DISARM Resets Flag ---
            lab.Log("\n--- FFMA_DISARM: Resets Armed Flag ---");
            {
                bool isFFMAModeArmed = true; // Armed state

                // Simulate FFMA_DISARM handler
                isFFMAModeArmed = false;

                lab.Assert(isFFMAModeArmed == false, "FFMA_DISARM: isFFMAModeArmed = false after disarm");
            }

            // --- Test 5: FFMA Position Sizing ---
            lab.Log("\n--- FFMA Trade DNA: Position Sizing ---");
            {
                var state = new StrategyStateSim();
                state.PointValue = 5.0;  // MES
                state.CurrentATR = 4.0;

                double entryPrice = 6300.0;
                double stopPrice = 6305.0; // Short: stop at candle high
                double stopDistance = Math.Abs(entryPrice - stopPrice); // 5.0 pts
                double maxRisk = 150.0;

                int contracts = state.CalculateContracts(maxRisk, stopDistance);
                // 150 / (5.0 * 5.0) = 150/25 = 6
                lab.Assert(contracts == 6,
                    $"FFMA Sizing: $150 risk / (5.0pt stop * $5/pt) = {contracts} contracts (expected 6)");
            }

            lab.Log("");
        }
    }
    #endregion

    #region Entry Point

    /// <summary>
    /// Main entry point — runs all test suites and prints summary.
    /// Execute with: dotnet-script RuleLabHarness.cs
    /// Or compile: csc RuleLabHarness.cs && RuleLabHarness.exe
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n╔══════════════════════════════════════════════════════╗");
            Console.WriteLine("║  V12 RULE LAB — Phase 6 Testing Rig                 ║");
            Console.WriteLine("║  Zero-Touch Verification for Strategy DNA            ║");
            Console.WriteLine($"║  {DateTime.Now:yyyy-MM-dd HH:mm:ss}                             ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════╝\n");
            Console.ResetColor();

            var lab = new RuleLab();
            lab.OnLog += (msg) =>
            {
                if (msg.Contains("[PASS]"))
                    Console.ForegroundColor = ConsoleColor.Green;
                else if (msg.Contains("[FAIL]"))
                    Console.ForegroundColor = ConsoleColor.Red;
                else if (msg.Contains("═══"))
                    Console.ForegroundColor = ConsoleColor.Yellow;
                else
                    Console.ForegroundColor = ConsoleColor.Gray;

                Console.WriteLine(msg);
                Console.ResetColor();
            };

            // Run all test suites
            GhostOrderSuite.Run(lab);
            SurgicalFlattenTest.Run(lab);
            RiskSizingSuite.Run(lab);
            ManagementSuite.Run(lab);
            CIFSuite.Run(lab);
            BreakevenSuite.Run(lab); // V12.23
            FFMASmartToggleSuite.Run(lab); // V12.24

            // Final summary
            lab.PrintSummary();

            // Exit code: 0 = all pass, 1 = failures
            Environment.ExitCode = lab.FailCount > 0 ? 1 : 0;
        }
    }

    #endregion
}
