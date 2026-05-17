using System;
using System.Threading;
using Xunit;

namespace V12.Sima.Tests
{
    /// <summary>
    /// V12 Phase 8: Circuit Breaker State Machine Tests
    /// Validates the SubmitCircuitBreaker FSM transitions and failure threshold behavior.
    /// Uses mockable time provider for instant, deterministic testing.
    /// </summary>
    public class CircuitBreakerBehaviorTests
    {
        /// <summary>
        /// Mock implementation of SubmitCircuitBreaker for testing.
        /// Mirrors the production implementation in V12_002.cs with mockable time.
        /// </summary>
        private class MockSubmitCircuitBreaker
        {
            private long _state;  // Packed: [State: 2 bits][FailureCount: 62 bits]
            private const int StateShift = 62;
            private const long FailureMask = (1L << 62) - 1;
            
            private const int STATE_CLOSED = 0;
            private const int STATE_HALF_OPEN = 1;
            private const int STATE_OPEN = 2;
            
            private long _openUntilTicks;  // Cooldown expiration timestamp
            private const int FailureThreshold = 5;
            private const long CooldownTicks = 30L * TimeSpan.TicksPerSecond;  // 30 seconds
            
            private readonly Func<long> _getTicksNow;
            
            public MockSubmitCircuitBreaker(Func<long> getTicksNow = null)
            {
                _getTicksNow = getTicksNow ?? (() => DateTime.UtcNow.Ticks);
                _openUntilTicks = 0;
            }
            
            public bool AllowSubmit()
            {
                long snapshot = Interlocked.Read(ref _state);
                int state = (int)((ulong)snapshot >> StateShift);
                
                if (state == STATE_OPEN)
                {
                    long nowTicks = _getTicksNow();
                    long openUntil = Volatile.Read(ref _openUntilTicks);
                    
                    if (nowTicks < openUntil)
                        return false;
                    
                    // Cooldown expired, try to transition to HALF_OPEN
                    if (TryHalfOpen(snapshot))
                        return true;
                    
                    // CAS failed - another thread may have changed state
                    // Re-read and fall through to check current state
                    snapshot = Interlocked.Read(ref _state);
                    state = (int)(snapshot >> StateShift);
                }
                
                // CLOSED or HALF_OPEN: allow submit
                return state == STATE_CLOSED || state == STATE_HALF_OPEN;
            }
            
            public void RecordSuccess()
            {
                long snapshot;
                do
                {
                    snapshot = Interlocked.Read(ref _state);
                    int state = (int)((ulong)snapshot >> StateShift);
                    
                    if (state == STATE_HALF_OPEN)
                    {
                        long next = ((long)STATE_CLOSED << StateShift) | 0L;
                        if (Interlocked.CompareExchange(ref _state, next, snapshot) == snapshot)
                            return;
                    }
                    else if (state == STATE_CLOSED)
                    {
                        long next = ((long)STATE_CLOSED << StateShift) | 0L;
                        if (Interlocked.CompareExchange(ref _state, next, snapshot) == snapshot)
                            return;
                    }
                    else
                    {
                        return;
                    }
                }
                while (true);
            }
            
            public void RecordFailure()
            {
                long snapshot;
                do
                {
                    snapshot = Interlocked.Read(ref _state);
                    int state = (int)((ulong)snapshot >> StateShift);
                    long failures = (snapshot & FailureMask) + 1;
                    
                    int nextState = state;
                    if (failures >= FailureThreshold)
                    {
                        nextState = STATE_OPEN;
                        Volatile.Write(ref _openUntilTicks,
                            _getTicksNow() + CooldownTicks);
                    }
                    else if (state == STATE_HALF_OPEN)
                    {
                        nextState = STATE_OPEN;
                        Volatile.Write(ref _openUntilTicks,
                            _getTicksNow() + CooldownTicks);
                    }
                    
                    long next = ((long)nextState << StateShift) | failures;
                    if (Interlocked.CompareExchange(ref _state, next, snapshot) == snapshot)
                        return;
                }
                while (true);
            }
            
            private bool TryHalfOpen(long snapshot)
            {
                long next = ((long)STATE_HALF_OPEN << StateShift) | 0L;
                long prev = Interlocked.CompareExchange(ref _state, next, snapshot);
                bool success = prev == snapshot;
                if (!success)
                {
                    // Debug: CAS failed
                    int prevState = (int)((ulong)prev >> StateShift);
                    long prevFailures = prev & FailureMask;
                    int snapState = (int)((ulong)snapshot >> StateShift);
                    long snapFailures = snapshot & FailureMask;
                    System.Diagnostics.Debug.WriteLine($"TryHalfOpen CAS FAILED: expected state={snapState} failures={snapFailures}, actual state={prevState} failures={prevFailures}");
                }
                return success;
            }
            
            public string GetDiagnostics()
            {
                long snapshot = Interlocked.Read(ref _state);
                int state = (int)((ulong)snapshot >> StateShift);
                long failures = snapshot & FailureMask;
                long openUntil = Volatile.Read(ref _openUntilTicks);
                long nowTicks = _getTicksNow();
                
                string stateName = state == STATE_CLOSED ? "Closed" :
                                  state == STATE_HALF_OPEN ? "HalfOpen" : "Open";
                
                return string.Format("CircuitBreaker: {0} (failures={1}, openUntil={2}, now={3}, diff={4})",
                    stateName, failures, openUntil, nowTicks, openUntil - nowTicks);
            }
        }
        
        [Fact]
        public void CircuitBreaker_Opens_After_Threshold_Failures()
        {
            var time = new MockTime(1000000L);
            var cb = new MockSubmitCircuitBreaker(time.GetTicks);
            
            // Record 5 failures (threshold)
            for (int i = 0; i < 5; i++)
                cb.RecordFailure();
            
            // Circuit should be open (cooldown not expired yet)
            bool allowed = cb.AllowSubmit();
            string diag = cb.GetDiagnostics();
            Assert.False(allowed, $"Expected circuit to be OPEN (AllowSubmit=false), but got AllowSubmit={allowed}. Diagnostics: {diag}");
        }
        
        [Fact]
        public void CircuitBreaker_Remains_Closed_Below_Threshold()
        {
            var cb = new MockSubmitCircuitBreaker();
            
            // Record 4 failures (below threshold)
            for (int i = 0; i < 4; i++)
                cb.RecordFailure();
            
            // Circuit should still be closed
            Assert.True(cb.AllowSubmit());
        }
        
        [Fact]
        public void CircuitBreaker_Transitions_To_HalfOpen_After_Cooldown()
        {
            var time = new MockTime(1000000L);
            var cb = new MockSubmitCircuitBreaker(time.GetTicks);
            
            // Open the circuit
            for (int i = 0; i < 5; i++)
                cb.RecordFailure();
            
            string diagBefore = cb.GetDiagnostics();
            
            // Advance time past cooldown (30 seconds + buffer)
            time.Advance(31L * TimeSpan.TicksPerSecond);
            
            string diagAfter = cb.GetDiagnostics();
            
            // Should allow one probe (transitions to HALF_OPEN)
            bool allowed = cb.AllowSubmit();
            string diagFinal = cb.GetDiagnostics();
            Assert.True(allowed, $"Before: {diagBefore}\nAfter time advance: {diagAfter}\nAfter AllowSubmit: {diagFinal}");
        }
        
        [Fact]
        public void CircuitBreaker_Resets_On_Successful_Probe()
        {
            var time = new MockTime(1000000L);
            var cb = new MockSubmitCircuitBreaker(time.GetTicks);
            
            // Open the circuit
            for (int i = 0; i < 5; i++)
                cb.RecordFailure();
            
            // Advance time past cooldown
            time.Advance(31L * TimeSpan.TicksPerSecond);
            
            // Successful probe (transitions to HALF_OPEN, then CLOSED)
            cb.AllowSubmit();
            cb.RecordSuccess();
            
            // Should be closed now
            Assert.True(cb.AllowSubmit());
        }
        
        [Fact]
        public void CircuitBreaker_Reopens_On_Failed_Probe()
        {
            var time = new MockTime(1000000L);
            var cb = new MockSubmitCircuitBreaker(time.GetTicks);
            
            // Open the circuit
            for (int i = 0; i < 5; i++)
                cb.RecordFailure();
            
            // Advance time past cooldown
            time.Advance(31L * TimeSpan.TicksPerSecond);
            
            // First AllowSubmit() transitions to HALF_OPEN and returns true (probe allowed)
            Assert.True(cb.AllowSubmit());
            
            // Record failure during probe - this reopens the circuit
            cb.RecordFailure();
            
            // Should be open again (cooldown restarted, but time hasn't advanced)
            Assert.False(cb.AllowSubmit());
        }
        
        [Fact]
        public void CircuitBreaker_Success_Resets_Failure_Count()
        {
            var cb = new MockSubmitCircuitBreaker();
            
            // Record 3 failures
            for (int i = 0; i < 3; i++)
                cb.RecordFailure();
            
            // Record success
            cb.RecordSuccess();
            
            // Record 4 more failures (would be 7 total without reset)
            for (int i = 0; i < 4; i++)
                cb.RecordFailure();
            
            // Should still be closed (4 < 5 threshold)
            Assert.True(cb.AllowSubmit());
        }
        
        /// <summary>
        /// Helper class for mockable time in tests.
        /// </summary>
        private class MockTime
        {
            private long _ticks;
            
            public MockTime(long initialTicks)
            {
                _ticks = initialTicks;
            }
            
            public long GetTicks() => _ticks;
            
            public void Advance(long deltaTicks) => _ticks += deltaTicks;
        }
    }
}

// Made with Bob
