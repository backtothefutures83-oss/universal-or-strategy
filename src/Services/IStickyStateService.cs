// V12 Services: IStickyStateService -- Pure C# state persistence interface
// Zero NinjaTrader dependencies - enables dotnet test without NT runtime
using System;
using System.Collections.Generic;

namespace NinjaTrader.NinjaScript.Strategies.Services
{
    /// <summary>
    /// Pure C# service for StickyState serialization and deserialization.
    /// Accepts all state via method parameters (no global statics).
    /// </summary>
    public interface IStickyStateService
    {
        /// <summary>
        /// Serializes a state snapshot to file path.
        /// Thread-safe: accepts immutable snapshot created on strategy thread.
        /// </summary>
        void Serialize(StickyStateSnapshot snapshot, string filePath);

        /// <summary>
        /// Deserializes INI file into structured data.
        /// Returns null if file doesn't exist or parsing fails.
        /// </summary>
        StickyStateData Deserialize(string filePath);
    }

    /// <summary>
    /// Logging abstraction for service (injected by Strategy).
    /// </summary>
    public interface IStickyStateLogger
    {
        void Log(string message);
    }
}

// Made with Bob
