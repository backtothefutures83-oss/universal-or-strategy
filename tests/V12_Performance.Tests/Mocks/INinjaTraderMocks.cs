using System;

namespace V12_Performance.Tests.Mocks
{
    /// <summary>
    /// Mock interfaces and struct implementations for NinjaTrader API isolation.
    /// Enables testing V12 logic without NinjaTrader assembly dependencies.
    /// All structs are value types (zero heap allocation).
    /// </summary>
    // ============================================================================
    // BAR DATA MOCKS
    // ============================================================================

    /// <summary>
    /// Mock interface for NinjaTrader bar data.
    /// </summary>
    public interface IBar
    {
        double Open { get; }
        double High { get; }
        double Low { get; }
        double Close { get; }
        DateTime Time { get; }
        long Volume { get; }
    }

    /// <summary>
    /// Struct implementation of IBar for zero-allocation testing.
    /// </summary>
    public struct MockBar : IBar
    {
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public DateTime Time { get; set; }
        public long Volume { get; set; }

        public MockBar(double open, double high, double low, double close, DateTime time, long volume)
        {
            Open = open;
            High = high;
            Low = low;
            Close = close;
            Time = time;
            Volume = volume;
        }
    }

    // ============================================================================
    // ORDER MOCKS
    // ============================================================================

    /// <summary>
    /// Mock interface for NinjaTrader order.
    /// </summary>
    public interface IOrder
    {
        string Name { get; }
        int Quantity { get; }
        double LimitPrice { get; }
        double StopPrice { get; }
        OrderState OrderState { get; }
    }

    /// <summary>
    /// Struct implementation of IOrder for zero-allocation testing.
    /// </summary>
    public struct MockOrder : IOrder
    {
        public string Name { get; set; }
        public int Quantity { get; set; }
        public double LimitPrice { get; set; }
        public double StopPrice { get; set; }
        public OrderState OrderState { get; set; }

        public MockOrder(string name, int quantity, double limitPrice, double stopPrice, OrderState state)
        {
            Name = name;
            Quantity = quantity;
            LimitPrice = limitPrice;
            StopPrice = stopPrice;
            OrderState = state;
        }
    }

    /// <summary>
    /// Order state enum matching NinjaTrader 8 OrderState values.
    /// </summary>
    public enum OrderState
    {
        Initialized = 0,
        Submitted = 1,
        Accepted = 2,
        Working = 3,
        Filled = 4,
        Cancelled = 5,
        Rejected = 6,
    }

    // ============================================================================
    // EXECUTION MOCKS
    // ============================================================================

    /// <summary>
    /// Mock interface for NinjaTrader execution.
    /// </summary>
    public interface IExecution
    {
        double Price { get; }
        int Quantity { get; }
        DateTime Time { get; }
    }

    /// <summary>
    /// Struct implementation of IExecution for zero-allocation testing.
    /// </summary>
    public struct MockExecution : IExecution
    {
        public double Price { get; set; }
        public int Quantity { get; set; }
        public DateTime Time { get; set; }

        public MockExecution(double price, int quantity, DateTime time)
        {
            Price = price;
            Quantity = quantity;
            Time = time;
        }
    }

    // ============================================================================
    // ACCOUNT MOCKS
    // ============================================================================

    /// <summary>
    /// Mock interface for NinjaTrader account.
    /// </summary>
    public interface IAccount
    {
        double CashValue { get; }
        double RealizedPnL { get; }
    }

    /// <summary>
    /// Struct implementation of IAccount for zero-allocation testing.
    /// </summary>
    public struct MockAccount : IAccount
    {
        public double CashValue { get; set; }
        public double RealizedPnL { get; set; }

        public MockAccount(double cashValue, double realizedPnL)
        {
            CashValue = cashValue;
            RealizedPnL = realizedPnL;
        }
    }
}

// Made with Bob
