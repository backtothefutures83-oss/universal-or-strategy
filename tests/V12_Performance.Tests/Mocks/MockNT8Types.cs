// Mock NinjaTrader 8 types for unit testing
// Uses composition pattern since NT8 types are sealed
using System;

namespace V12_Performance.Tests.Mocks
{
    /// <summary>
    /// Mock Order for testing. Since NT8 Order is sealed, we create a simple POCO.
    /// Tests will use this instead of the real Order class.
    /// </summary>
    public class MockOrder
    {
        public string Name { get; set; }
        public MockOrderState OrderState { get; set; }
        public MockAccount Account { get; set; }
        public string OrderId { get; set; }
        public int Quantity { get; set; }
        public int Filled { get; set; }
        public double LimitPrice { get; set; }
        public double StopPrice { get; set; }
        public double AverageFillPrice { get; set; }
        public DateTime Time { get; set; }

        public MockOrder(string name, MockOrderState orderState, MockAccount account)
        {
            Name = name;
            OrderState = orderState;
            Account = account;
            OrderId = Guid.NewGuid().ToString();
            Time = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Mock OrderState enum. Mirrors NT8 OrderState for testing.
    /// </summary>
    public enum MockOrderState
    {
        Unknown,
        Submitted,
        Accepted,
        Working,
        ChangePending,
        ChangeSubmitted,
        PartFilled,
        Filled,
        Cancelled,
        Rejected,
    }

    /// <summary>
    /// Mock Account for testing.
    /// </summary>
    public class MockAccount
    {
        public string Name { get; set; }

        public MockAccount(string name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// Mock Position for testing.
    /// </summary>
    public class MockPosition
    {
        public MockMarketPosition MarketPosition { get; set; }
        public int Quantity { get; set; }
        public double AveragePrice { get; set; }
    }

    /// <summary>
    /// Mock MarketPosition enum.
    /// </summary>
    public enum MockMarketPosition
    {
        Flat,
        Long,
        Short,
    }
}

// Made with Bob (EPIC-CCN-10 TDD Infrastructure)
