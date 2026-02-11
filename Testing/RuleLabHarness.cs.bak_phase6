using System;
using System.Collections.Generic;

namespace V12.Testing
{
    /// <summary>
    /// The Rule Lab Harness allows us to simulate NinjaTrader events 
    /// (Orders, Fills, Rejections) to test strategy logic without the NT8 UI.
    /// </summary>
    public class RuleLab
    {
        public bool IsSimulatedRejectionEnabled { get; set; } = false;
        
        public event Action<string> OnLog;

        public void Log(string message)
        {
            OnLog?.Invoke($"[RULE LAB] {message}");
        }

        // Simulated Order Update logic for testing "Ghost Order" type bugs
        public void SimulateOrderUpdate(string orderId, string state, string instrument)
        {
            Log($"Simulating Order Update: {orderId} | State: {state} | Instrument: {instrument}");
            
            if (state == "Rejected" || state == "Cancelled")
            {
                Log($"CRITICAL: Handling {state} state for {orderId}. Verifying state cleanup...");
                // Here we would call the strategy's Cleanup method in a real test
            }
        }
    }

    /// <summary>
    /// A sample test case to prove the "Rule Lab" concept.
    /// </summary>
    public class GhostOrderTest
    {
        public static void Run()
        {
            var lab = new RuleLab();
            lab.OnLog += Console.WriteLine;

            lab.Log("Starting Ghost Order Logic Test...");
            
            // Scenario: T1 Order is sent, but immediately rejected by broker (e.g. margin issue)
            lab.SimulateOrderUpdate("T1_ORDER_001", "Rejected", "MNQ 03-24");
            
            lab.Log("Test Complete. Verification required: Did Strategy state reset?");
        }
    }
}
