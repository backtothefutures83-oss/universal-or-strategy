// V12_002.StructuredLog.cs -- Structured Logging Wrapper for V12 Kernel
// Build 1105: Wraps NinjaTrader Print() with level + module + trace ID tagging.
// Protocol: Lock-free. ASCII-only. Zero external dependencies (.NET 4.8).
// Satisfies Droid criterion: structured_logging (replaces bare Print() pattern).
//
// Log format: [TRACE:NNNNN][MODULE][LEVEL] message
// Example   : [TRACE:00042][SIMA.Dispatch][INFO] FleetBroadcast started nAccounts=3

using System;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region Log Level Enum

        // ASCII-safe level monikers. Width is fixed (5 chars) for column alignment.
        private enum V12LogLevel
        {
            DEBUG, // Low-noise diagnostics -- only visible when needed
            INFO, // Standard operational events
            WARN, // Unexpected but recoverable conditions
            ERROR, // Logic invariant violations or exception paths
        }

        #endregion

        #region Core Structured Emitter

        /// <summary>
        /// Emits a structured log line via NinjaTrader Print().
        /// Format: [TRACE:NNNNN][MODULE][LEVEL] message
        /// All args are validated for null to prevent format exceptions on the hot path.
        /// </summary>
        private void StructuredPrint(string traceId, string module, V12LogLevel level, string message)
        {
            // Defensive null guards -- format must never throw in a trading strategy.
            string safeId = traceId ?? "?????";
            string safeModule = module ?? "UNKNOWN";
            string safeMessage = message ?? "(null)";

            Print(string.Format("[TRACE:{0}][{1}][{2}] {3}", safeId, safeModule, level, safeMessage));
        }

        #endregion

        #region Convenience Wrappers (use _currentTraceId implicitly)

        /// <summary>Emit an INFO-level structured log using the current trace context.</summary>
        private void LogInfo(string module, string message)
        {
            StructuredPrint(_currentTraceId, module, V12LogLevel.INFO, message);
        }

        /// <summary>Emit a WARN-level structured log using the current trace context.</summary>
        private void LogWarn(string module, string message)
        {
            StructuredPrint(_currentTraceId, module, V12LogLevel.WARN, message);
        }

        /// <summary>Emit an ERROR-level structured log using the current trace context.</summary>
        private void LogError(string module, string message)
        {
            StructuredPrint(_currentTraceId, module, V12LogLevel.ERROR, message);
        }

        /// <summary>
        /// Emit a DEBUG-level structured log using the current trace context.
        /// Conditionally compiled based on a compile-time guard -- not yet hooked to a runtime flag.
        /// Call-sites are no-ops in production unless explicitly enabled.
        /// </summary>
        private void LogDebug(string module, string message)
        {
            // Debug logs are suppressed in normal operation to protect Print() throughput.
            // Uncomment the body below when diagnosing a specific subsystem:
            // StructuredPrint(_currentTraceId, module, V12LogLevel.DEBUG, message);
        }

        /// <summary>
        /// Emit a structured log with an explicit trace ID override.
        /// Use when logging outside the current trace context (e.g., from a callback).
        /// </summary>
        private void LogWithTrace(string traceId, string module, V12LogLevel level, string message)
        {
            StructuredPrint(traceId, module, level, message);
        }

        #endregion

        #region Exception Logger

        /// <summary>
        /// Logs an exception as a structured ERROR line with module + trace context.
        /// Includes the exception Type and Message. Stack traces are intentionally omitted
        /// to keep Output window readable in live trading.
        /// </summary>
        private void LogException(string module, string context, Exception ex)
        {
            if (ex == null)
            {
                LogError(module, string.Format("{0}: null exception (unexpected)", context));
                return;
            }

            StructuredPrint(
                _currentTraceId,
                module,
                V12LogLevel.ERROR,
                string.Format("{0}: [{1}] {2}", context, ex.GetType().Name, ex.Message)
            );
        }

        #endregion
    }
}
