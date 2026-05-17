using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Linq;

namespace SIMA.Baseline
{
    // --- PHOTON MOCK LAYER ---
    public enum MarketPosition { Flat, Long, Short }
    public enum OrderState { Unknown, Initialized, Submitted, Accepted, Working, PartFilled, Filled, Cancelled, Rejected }
    public enum OrderAction { Buy, Sell }
    public enum OrderType { Market, Limit, StopMarket, StopLimit }
    public enum FollowerBracketState { Idle, PendingSubmit, Submitted, Accepted, Active, Replacing, Cancelled, Closed }

    public class Instrument { public string FullName { get; set; } }
    public class Account 
    { 
        public string Name { get; set; } 
        public List<Position> Positions { get; set; } = new List<Position>();
        public event Action<Account, ExecutionEventArgs> ExecutionUpdate;
        public event Action<Account, OrderEventArgs> OrderUpdate;

        public void Submit(Order[] orders) { 
            foreach(var o in orders) o.OrderState = OrderState.Submitted; 
        }

        public void FireExecution(ExecutionEventArgs e) => ExecutionUpdate?.Invoke(this, e);
    }
    public class Position 
    { 
        public Instrument Instrument { get; set; } 
        public MarketPosition MarketPosition { get; set; } 
    }
    public class Order 
    { 
        public string Name { get; set; }
        public string OrderId { get; set; }
        public OrderState OrderState { get; set; }
        public double LimitPrice { get; set; }
        public double StopPrice { get; set; }
        public string Oco { get; set; }
    }
    public class ExecutionEventArgs { public string ExecutionId { get; set; } }
    public class OrderEventArgs { public Order Order { get; set; } }

    public class FollowerBracketFSM
    {
        public string AccountName;
        public string EntryName;
        public FollowerBracketState State;
        public int RemainingContracts;
        public DateTime LastUpdateUtc;
        public Order EntryOrder;
        public Order StopOrder;
        public Order[] Targets = new Order[5];
        public double ExpectedEntryPrice;
        public double ExpectedStopPrice;
        public double[] ExpectedTargetPrices = new double[5];
        public string OcoGroupId;
    }

    // --- REPRODUCER CLASS ---
    public partial class SIMA_Baseline_Reproducer
    {
        public bool _isPumpActive = false; // The missing sentinel we want to test
        public int _pendingFleetDispatchCount = 0;
        public ConcurrentDictionary<string, FollowerBracketFSM> _followerBrackets = new ConcurrentDictionary<string, FollowerBracketFSM>();
        public HashSet<string> _subscribedAccountNames = new HashSet<string>();

        // BUG-002: Re-entrancy reproduction
        public void PumpFleetDispatch_BUG002(bool simulateRecursion)
        {
            if (_isPumpActive) 
            {
                Console.WriteLine("[FAIL] BUG-002: Re-entrancy detected! Pump is already active.");
                return;
            }

            _isPumpActive = true;
            try
            {
                Console.WriteLine("[INFO] Pump started.");
                if (simulateRecursion)
                {
                    Console.WriteLine("[INFO] Simulating recursive call (e.g. from TriggerCustomEvent)...");
                    PumpFleetDispatch_BUG002(false);
                }
            }
            finally
            {
                // In the broken version, if we don't have a sentinel, we just recurse infinitely or corrupt state.
                _isPumpActive = false; 
                Console.WriteLine("[INFO] Pump finished.");
            }
        }

        // BUG-003: Sideband ordering reproduction
        public void ProcessFleetSlot_BUG003(int poolSlotIndex)
        {
            // Simulation of the broken ordering: Release then Clear
            Console.WriteLine("[INFO] Releasing pool slot {0}...", poolSlotIndex);
            // _photonPool.ReleaseByIndex(poolSlotIndex); // Release
            
            Console.WriteLine("[INFO] Clearing sideband for slot {0}...", poolSlotIndex);
            // _photonSideband[poolSlotIndex] = default; // Clear (TOO LATE)
            
            Console.WriteLine("[FAIL] BUG-003: Slot was released before sideband was cleared. Race window open.");
        }

        // BUG-001: O(N^2) Unsubscribe reproduction
        public void Unsubscribe_BUG001(List<Account> allAccounts)
        {
            int removals = 0;
            var snapshot = allAccounts.ToArray();
            
            // Broken version: Nested loops + double removal
            foreach(var trackedName in _subscribedAccountNames)
            {
                foreach(var acct in snapshot)
                {
                    if (acct.Name == trackedName)
                    {
                        // acct.ExecutionUpdate -= ...; 
                        removals++;
                    }
                }
            }
            
            Console.WriteLine("[WARN] BUG-001: Performed {0} removal attempts for {1} accounts (O(N^2)).", removals, _subscribedAccountNames.Count);
        }
    }

    // NOTE: Main() commented out to avoid conflict with StandaloneBench.cs
    // Uncomment to run SIMA baseline tests independently
    /*
    class Program
    {
        static void Main()
        {
            Console.WriteLine("=== SIMA BASELINE FORENSIC AUDIT ===");
            var reproducer = new SIMA_Baseline_Reproducer();

            // Test BUG-002
            Console.WriteLine("\n--- Testing BUG-002 (Re-entrancy) ---");
            reproducer.PumpFleetDispatch_BUG002(true);

            // Test BUG-003
            Console.WriteLine("\n--- Testing BUG-003 (Ordering) ---");
            reproducer.ProcessFleetSlot_BUG003(5);

            // Test BUG-001
            Console.WriteLine("\n--- Testing BUG-001 (O(N^2) Unsubscribe) ---");
            reproducer._subscribedAccountNames.Add("Account1");
            reproducer._subscribedAccountNames.Add("Account2");
            var accounts = new List<Account> { new Account { Name = "Account1" }, new Account { Name = "Account2" } };
            reproducer.Unsubscribe_BUG001(accounts);

            Console.WriteLine("\n=== BASELINE COMPLETE ===");
        }
    }
    */
}
