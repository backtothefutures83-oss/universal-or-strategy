using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

namespace NinjaTrader.Cbi
{
    public enum MarketPosition { Flat, Long, Short }
    
    public class Instrument
    {
        public string FullName { get; set; } = "ES 06-26";
    }

    public class Position
    {
        public Instrument Instrument { get; set; } = new Instrument();
        public MarketPosition MarketPosition { get; set; } = MarketPosition.Flat;
    }

    public class Order
    {
        public string Name { get; set; }
        public double LimitPrice { get; set; }
        public double StopPrice { get; set; }
        public string Oco { get; set; }
        public string OrderId { get; set; } // Note: Empty immediately after Submit() per BUG-015
        public Account Account { get; set; }
    }

    public class Account
    {
        public string Name { get; set; }
        public List<Position> Positions { get; } = new List<Position>();
        public static List<Account> All = new List<Account>();

        public event EventHandler ExecutionUpdate;
        public event EventHandler OrderUpdate;

        public void Submit(Order[] orders)
        {
            // Mock submission: In real NT, OrderId is NOT assigned here (BUG-015)
            foreach(var o in orders) { if(o != null) o.Account = this; }
        }
    }

    public class ExecutionUpdateEventArgs : EventArgs { }
    public class OrderUpdateEventArgs : EventArgs { }
}

namespace NinjaTrader.NinjaScript.Strategies
{
    public class Strategy
    {
        public NinjaTrader.Cbi.Instrument Instrument { get; set; } = new NinjaTrader.Cbi.Instrument();
        public void Print(string msg) { Console.WriteLine(msg); }
        public void TriggerCustomEvent(Action<object> action, object arg) 
        {
            // Direct execution to simulate high-speed re-entrancy for BUG-002 testing
            action(arg); 
        }
    }
}

namespace MpmcBench
{
    // Placeholder for Photon Harness logic
    public class PhotonMockHarness
    {
        public static void Run()
        {
            Console.WriteLine("Photon Mock initialized. Ready for SIMA cluster injection.");
        }
    }
}
