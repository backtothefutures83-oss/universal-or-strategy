// <copyright file="NinjaTrader.Mocks.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
// V12.Sentinel: NinjaTrader Mocks for GitHub Actions
// This file allows core logic to compile on GitHub without NinjaTrader DLLs.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;

namespace NinjaTrader.Cbi
{
    public enum OrderState { Initialized, PendingSubmit, Accepted, Working, PartFilled, Filled, Cancelled, Rejected, Unknown }
    public enum OrderAction { Buy, Sell }
    public enum OrderType { Limit, Market, StopMarket, StopLimit }
    public enum MarketPosition { Flat, Long, Short }
    public class Account { public string Name; public static List<Account> All = new List<Account>(); }
    public class Instrument { public string FullName; public double MasterInstrument; }
    public class Order { public string Name; public OrderState OrderState; public double LimitPrice; public double StopPrice; public int Quantity; public int Filled; }
    public class Execution { public string Name; public int Quantity; public double Price; }
}

namespace NinjaTrader.Gui { }
namespace NinjaTrader.Gui.Chart { }
namespace NinjaTrader.Gui.Tools { }
namespace NinjaTrader.Data { }
namespace NinjaTrader.NinjaScript { }
namespace NinjaTrader.NinjaScript.DrawingTools { }
namespace NinjaTrader.NinjaScript.Indicators { }

namespace NinjaTrader.NinjaScript.Strategies
{
    public class Strategy
    {
        public NinjaTrader.Cbi.Instrument Instrument { get; set; } = new NinjaTrader.Cbi.Instrument { FullName = "MOCK" };
        public void Print(string message) { Console.WriteLine(message); }

        // Common Strategy Properties
        public string BUILD_TAG = "MOCK";
        public int activeTargetCount;
        public double Target1Value, Target2Value, Target3Value, Target4Value, Target5Value;
        public TargetMode T1Type, T2Type, T3Type, T4Type, T5Type;
        public double MaxRiskAmount, RiskPerTrade;
        public string ChaseIfTouchPoints;
        public bool isTrendRmaMode, isRetestRmaMode, isRMAModeActive, isRMAButtonClicked;
        public bool isRetestModeActive, isTRENDModeActive, isMOMOModeActive, isFFMAModeArmed;
        public double RMAStopATRMultiplier, StopMultiplier, cachedMnlPrice, currentATR, MinimumStop;
        public double tickSize = 0.25;
        public string _stickyLeaderAccount;
        public RmaAnchorType currentRmaAnchor;
        public Dictionary<string, bool> _pendingStickyFleetToggles;
        public ConcurrentDictionary<string, bool> activeFleetAccounts = new ConcurrentDictionary<string, bool>();

        public enum TargetMode { ATR, Ticks, Points, Runner }
        public enum RmaAnchorType { Ema30, Ema65, Ema200, OrHigh, OrLow, Manual }

        public void SetRmaAnchorFromIpc(string val) { }
        public void MarkDispatchSyncPending(string key) { }
        public void AddExpectedPositionDeltaLocked(string key, int delta) { }
        public void Enqueue(Action a) { a(); }

        public class ModeConfigProfile
        {
            public int TargetCount;
            public double T1, T2, T3, T4, T5, StopMult, MaxRisk;
            public TargetMode T1Type, T2Type, T3Type, T4Type, T5Type;
        }

        public class TargetSnapshot { public string Name; public int Quantity; public double Price; }
    }

    // This partial merges with the real strategy parts to provide missing fields in mock builds
    public partial class V12_002 : Strategy
    {
        // activePositions and _modeProfiles are defined here to use the correct types from this class
        private ConcurrentDictionary<string, PositionInfo> activePositions = new ConcurrentDictionary<string, PositionInfo>();
        private ConcurrentDictionary<string, ModeConfigProfile> _modeProfiles = new ConcurrentDictionary<string, ModeConfigProfile>();
    }

    public static class NinjaTraderExtensions
    {
        public static double RoundToTickSize(this double val, double tickSize)
        {
            return Math.Round(val / tickSize) * tickSize;
        }
    }
}
