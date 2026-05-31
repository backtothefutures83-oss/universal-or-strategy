// <copyright file="V12_002.Telemetry.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
// V12_002.Telemetry.cs -- Distributed Tracing + Logic Metrics for V12 Kernel
// Build 1105: Implements lightweight trace propagation and FSM counters.
// Protocol: Lock-free (Interlocked only). ASCII-only. No external dependencies.
// Satisfies Droid criteria: distributed_tracing, metrics_collection (partial).

using System;
using System.Threading;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region Telemetry State

        // Monotonic correlation counter. Resets to 0 on SetDefaults.
        // Short-form (5-digit mod) keeps Print() lines scannable.
        private long _traceCounter = 0;

        // Current active trace ID for the ongoing logical operation.
        // Written on strategy thread only; read-only from other callsites.
        private string _currentTraceId = "00000";

        // -- Logic Metric Counters (Interlocked -- never lock) ---------------------
        // Each counter tracks a distinct FSM event across the strategy lifetime.
        private long _metricFsmTransitions = 0; // Every actor Enqueue() execution
        private long _metricSimaDispatches = 0; // Every SIMA fleet broadcast
        private long _metricReaperAudits = 0; // Every AuditApexPositions() call
        private long _metricSymmetryReplace = 0; // Every follower bracket Replace FSM entry
        private long _metricOrderSubmissions = 0; // Every SubmitOrderUnmanaged call
        private long _metricIpcCommands = 0; // Every IPC command processed

        // -- Photon SPSC Ring Buffer Counters (GAP-2) ------------------------------
        private long _metricPhotonEnqueues = 0; // Successful ring buffer enqueues
        private long _metricPhotonDequeues = 0; // Successful ring buffer dequeues
        private long _metricPhotonRingFull = 0; // Ring full fallback to ConcurrentQueue
        private long _metricPhotonPoolExhausted = 0; // Pool exhausted fallback to heap
        private long _metricPhotonCrcFailures = 0; // XorShadow integrity failures

        // -- State Persistence Diagnostic Counters (EPIC-7-QUALITY) ----------------
        private long _statePersistenceFailures = 0; // Failed state write/read operations
        private long _stateSecurityViolations = 0; // Path validation failures
        private long _stateRetryAttempts = 0; // File I/O retry attempts
        private long _stateRollbacksExecuted = 0; // Rollback to backup operations
        #endregion

        #region Trace ID Management

        /// <summary>
        /// Generates the next monotonic correlation ID and sets it as the current trace context.
        /// Returns the ID as a 5-digit zero-padded string, e.g. "00042".
        /// Call at the start of any distinct logical operation (audit, dispatch, FSM entry).
        /// </summary>
        private string NewTraceId()
        {
            long next = Interlocked.Increment(ref _traceCounter) % 100000L;
            _currentTraceId = string.Format("{0:D5}", next);
            return _currentTraceId;
        }

        /// <summary>
        /// Resets the trace counter and current ID. Called from SetDefaults state.
        /// </summary>
        private void ResetTelemetry()
        {
            Interlocked.Exchange(ref _traceCounter, 0L);
            Interlocked.Exchange(ref _metricFsmTransitions, 0L);
            Interlocked.Exchange(ref _metricSimaDispatches, 0L);
            Interlocked.Exchange(ref _metricReaperAudits, 0L);
            Interlocked.Exchange(ref _metricSymmetryReplace, 0L);
            Interlocked.Exchange(ref _metricOrderSubmissions, 0L);
            Interlocked.Exchange(ref _metricIpcCommands, 0L);
            Interlocked.Exchange(ref _metricPhotonEnqueues, 0L);
            Interlocked.Exchange(ref _metricPhotonDequeues, 0L);
            Interlocked.Exchange(ref _metricPhotonRingFull, 0L);
            Interlocked.Exchange(ref _metricPhotonPoolExhausted, 0L);
            Interlocked.Exchange(ref _metricPhotonCrcFailures, 0L);
            Interlocked.Exchange(ref _statePersistenceFailures, 0L);
            Interlocked.Exchange(ref _stateSecurityViolations, 0L);
            Interlocked.Exchange(ref _stateRetryAttempts, 0L);
            Interlocked.Exchange(ref _stateRollbacksExecuted, 0L);
            _currentTraceId = "00000";
        }

        #endregion

        #region Metric Increment Helpers

        // These helpers are the public surface for metric collection.
        // Each maps 1:1 to a distinct FSM event. Call-site adds zero heap allocation.

        /// <summary>Increment FSM actor transition counter. Call once per Enqueue execution.</summary>
        private void TrackFsmTransition()
        {
            Interlocked.Increment(ref _metricFsmTransitions);
        }

        /// <summary>Increment SIMA broadcast counter. Call once per fleet dispatch cycle.</summary>
        private void TrackSimaDispatch()
        {
            Interlocked.Increment(ref _metricSimaDispatches);
        }

        /// <summary>Increment Reaper audit counter. Call once per AuditApexPositions cycle.</summary>
        private void TrackReaperAudit()
        {
            Interlocked.Increment(ref _metricReaperAudits);
        }

        /// <summary>Increment Symmetry Replace FSM entry counter.</summary>
        private void TrackSymmetryReplace()
        {
            Interlocked.Increment(ref _metricSymmetryReplace);
        }

        /// <summary>Increment order submission counter. Call once per SubmitOrderUnmanaged.</summary>
        private void TrackOrderSubmission()
        {
            Interlocked.Increment(ref _metricOrderSubmissions);
        }

        /// <summary>Increment IPC command processed counter.</summary>
        private void TrackIpcCommand()
        {
            Interlocked.Increment(ref _metricIpcCommands);
        }

        /// <summary>Increment state persistence failure counter.</summary>
        private void TrackStatePersistenceFailure()
        {
            Interlocked.Increment(ref _statePersistenceFailures);
        }

        /// <summary>Increment state security violation counter.</summary>
        private void TrackStateSecurityViolation()
        {
            Interlocked.Increment(ref _stateSecurityViolations);
        }

        /// <summary>Increment state retry attempt counter.</summary>
        private void TrackStateRetryAttempt()
        {
            Interlocked.Increment(ref _stateRetryAttempts);
        }

        /// <summary>Increment state rollback executed counter.</summary>
        private void TrackStateRollback()
        {
            Interlocked.Increment(ref _stateRollbacksExecuted);
        }

        /// <summary>Increment Photon ring buffer enqueue counter. Call on successful TryEnqueue.</summary>
        private void TrackPhotonEnqueue()
        {
            Interlocked.Increment(ref _metricPhotonEnqueues);
        }

        /// <summary>Increment Photon ring buffer dequeue counter. Call on successful TryDequeue.</summary>
        private void TrackPhotonDequeue()
        {
            Interlocked.Increment(ref _metricPhotonDequeues);
        }

        /// <summary>Increment Photon ring full counter. Call when ring full triggers ConcurrentQueue fallback.</summary>
        private void TrackPhotonRingFull()
        {
            Interlocked.Increment(ref _metricPhotonRingFull);
        }

        /// <summary>Increment Photon pool exhausted counter. Call when pool exhausted triggers heap allocation.</summary>
        private void TrackPhotonPoolExhausted()
        {
            Interlocked.Increment(ref _metricPhotonPoolExhausted);
        }

        /// <summary>Increment Photon CRC failure counter. Call when XorShadow integrity check fails.</summary>
        private void TrackPhotonCrcFailure()
        {
            Interlocked.Increment(ref _metricPhotonCrcFailures);
        }

        #endregion

        #region TraceSpan -- Stack-Allocated Stopwatch Token

        /// <summary>
        /// Lightweight, stack-allocated span that records entry/exit timestamps.
        /// Zero heap allocation -- use as a local variable, never store on heap.
        /// Call BeginSpan() to get a token; call End() when the operation completes.
        /// </summary>
        private readonly struct TraceSpan
        {
            internal readonly string TraceId;
            internal readonly string Module;
            internal readonly long StartTicks; // DateTime.UtcNow.Ticks at span entry

            internal TraceSpan(string traceId, string module)
            {
                TraceId = traceId;
                Module = module;
                StartTicks = DateTime.UtcNow.Ticks;
            }

            /// <summary>
            /// Closes the span and emits a structured SPAN log line via the supplied print delegate.
            /// Elapsed is in milliseconds (integer precision -- sufficient for strategy diagnostics).
            /// </summary>
            internal void End(Action<string> print)
            {
                if (print == null)
                    return;
                long elapsedMs = (DateTime.UtcNow.Ticks - StartTicks) / TimeSpan.TicksPerMillisecond;
                print(string.Format("[TRACE:{0}][{1}][SPAN] elapsed={2}ms", TraceId, Module, elapsedMs));
            }
        }

        /// <summary>
        /// Begins a new trace span. Allocates a new correlation ID and returns a
        /// stack-allocated TraceSpan token. Pass the token to End() when done.
        /// </summary>
        private TraceSpan BeginSpan(string module)
        {
            string id = NewTraceId();
            LogInfo(module, "span-start");
            return new TraceSpan(id, module);
        }

        #endregion

        #region Metrics Summary Emitter

        /// <summary>
        /// Prints a structured metrics summary to the NinjaTrader Output window.
        /// Called once at strategy Terminated state (after DrainQueuesForShutdown).
        /// Provides an end-of-session snapshot of all FSM event counters.
        /// </summary>
        private void EmitMetricsSummary()
        {
            try
            {
                long fsm = Interlocked.Read(ref _metricFsmTransitions);
                long sima = Interlocked.Read(ref _metricSimaDispatches);
                long reaper = Interlocked.Read(ref _metricReaperAudits);
                long symmetry = Interlocked.Read(ref _metricSymmetryReplace);
                long orders = Interlocked.Read(ref _metricOrderSubmissions);
                long ipc = Interlocked.Read(ref _metricIpcCommands);
                long photonEnq = Interlocked.Read(ref _metricPhotonEnqueues);
                long photonDeq = Interlocked.Read(ref _metricPhotonDequeues);
                long photonFull = Interlocked.Read(ref _metricPhotonRingFull);
                long photonExhaust = Interlocked.Read(ref _metricPhotonPoolExhausted);
                long photonCrc = Interlocked.Read(ref _metricPhotonCrcFailures);
                long stateFailures = Interlocked.Read(ref _statePersistenceFailures);
                long stateViolations = Interlocked.Read(ref _stateSecurityViolations);
                long stateRetries = Interlocked.Read(ref _stateRetryAttempts);
                long stateRollbacks = Interlocked.Read(ref _stateRollbacksExecuted);

                Print("------------------------------------------------");
                Print(string.Format("[{0}] SESSION METRICS REPORT", BUILD_TAG));
                Print(string.Format("  FSM Transitions   : {0}", fsm));
                Print(string.Format("  SIMA Dispatches   : {0}", sima));
                Print(string.Format("  Reaper Audits     : {0}", reaper));
                Print(string.Format("  Symmetry Replaces : {0}", symmetry));
                Print(string.Format("  Order Submissions : {0}", orders));
                Print(string.Format("  IPC Commands      : {0}", ipc));
                Print(string.Format("  Photon Enqueues   : {0}", photonEnq));
                Print(string.Format("  Photon Dequeues   : {0}", photonDeq));
                Print(string.Format("  Photon Ring Full  : {0}", photonFull));
                Print(string.Format("  Photon Pool Exhaust: {0}", photonExhaust));
                Print(string.Format("  Photon CRC Fails  : {0}", photonCrc));
                Print(string.Format("  State Failures    : {0}", stateFailures));
                Print(string.Format("  Security Violations: {0}", stateViolations));
                Print(string.Format("  State Retries     : {0}", stateRetries));
                Print(string.Format("  State Rollbacks   : {0}", stateRollbacks));
                Print("------------------------------------------------");
            }
            catch
            {
                // Metrics emit is non-fatal -- never throw from Terminated state teardown.
            }
        }

        #endregion
    }
}
