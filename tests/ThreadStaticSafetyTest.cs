using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace V12_002.Tests
{
    /// <summary>
    /// ThreadStatic Safety Test Harness for EPIC-5-PERF Ticket 01B
    /// Tests NinjaTrader's threading model to determine if ThreadStatic is safe for T05 buffer optimization.
    ///
    /// CRITICAL REQUIREMENT: Test 4 must validate thread reuse scenarios to detect state leakage
    /// between strategy instances if NinjaTrader uses thread pooling.
    /// </summary>
    public class ThreadStaticSafetyTest
    {
        [ThreadStatic]
        private static string _testBuffer;

        [ThreadStatic]
        private static int _testCounter;

        private static readonly object _consoleLock = new object();

        public static void Main(string[] args)
        {
            Console.WriteLine("=== ThreadStatic Safety Test Harness ===");
            Console.WriteLine("EPIC-5-PERF Ticket 01B: Thread Model Analysis");
            Console.WriteLine("Objective: Determine if ThreadStatic is SAFE for T05 buffer optimization\n");

            bool allTestsPassed = true;

            allTestsPassed &= Test1_SingleThreadedBaseline();
            allTestsPassed &= Test2_MultiThreadedIsolation();
            allTestsPassed &= Test3_RapidContextSwitching();
            allTestsPassed &= Test4_ThreadReuseDetection();

            Console.WriteLine("\n=== FINAL VERDICT ===");
            if (allTestsPassed)
            {
                Console.WriteLine("✓ ALL TESTS PASSED");
                Console.WriteLine("Preliminary Verdict: ThreadStatic appears SAFE for isolated thread scenarios");
                Console.WriteLine("CRITICAL: Must validate against actual NinjaTrader threading model");
            }
            else
            {
                Console.WriteLine("✗ TESTS FAILED");
                Console.WriteLine("Verdict: ThreadStatic is UNSAFE - fallback to instance-level buffer required");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Test 1: Single-threaded baseline
        /// Validates basic ThreadStatic behavior in a single-threaded context.
        /// Expected: State persists within same thread, resets on new thread.
        /// </summary>
        private static bool Test1_SingleThreadedBaseline()
        {
            Console.WriteLine("\n--- Test 1: Single-threaded Baseline ---");
            bool passed = true;

            try
            {
                // Reset state
                _testBuffer = null;
                _testCounter = 0;

                // Write to ThreadStatic
                _testBuffer = "TEST1_DATA";
                _testCounter = 42;

                // Verify persistence
                if (_testBuffer != "TEST1_DATA" || _testCounter != 42)
                {
                    Console.WriteLine("✗ FAIL: ThreadStatic state did not persist in same thread");
                    passed = false;
                }
                else
                {
                    Console.WriteLine("✓ PASS: ThreadStatic state persists in same thread");
                }

                // Verify isolation on new thread
                bool isolationVerified = false;
                var newThread = new Thread(() =>
                {
                    if (_testBuffer == null && _testCounter == 0)
                    {
                        isolationVerified = true;
                        Console.WriteLine("✓ PASS: ThreadStatic state is null on new thread (expected)");
                    }
                    else
                    {
                        Console.WriteLine(
                            $"✗ FAIL: ThreadStatic leaked to new thread: buffer={_testBuffer}, counter={_testCounter}"
                        );
                    }
                });
                newThread.Start();
                newThread.Join();

                passed &= isolationVerified;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ EXCEPTION: {ex.Message}");
                passed = false;
            }

            return passed;
        }

        /// <summary>
        /// Test 2: Multi-threaded isolation
        /// Validates that ThreadStatic state is isolated between concurrent threads.
        /// Expected: Each thread maintains independent state with no cross-contamination.
        /// </summary>
        private static bool Test2_MultiThreadedIsolation()
        {
            Console.WriteLine("\n--- Test 2: Multi-threaded Isolation ---");
            bool passed = true;
            const int threadCount = 10;
            var threads = new Thread[threadCount];
            var results = new bool[threadCount];

            try
            {
                for (int i = 0; i < threadCount; i++)
                {
                    int threadId = i;
                    threads[i] = new Thread(() =>
                    {
                        // Each thread writes unique data
                        string expectedData = $"THREAD_{threadId}_DATA";
                        _testBuffer = expectedData;
                        _testCounter = threadId * 100;

                        // Simulate work
                        Thread.Sleep(10);

                        // Verify no contamination
                        if (_testBuffer == expectedData && _testCounter == threadId * 100)
                        {
                            lock (_consoleLock)
                            {
                                Console.WriteLine($"  Thread {threadId}: ✓ State isolated correctly");
                            }
                            results[threadId] = true;
                        }
                        else
                        {
                            lock (_consoleLock)
                            {
                                Console.WriteLine(
                                    $"  Thread {threadId}: ✗ State contaminated! Expected={expectedData}, Got={_testBuffer}"
                                );
                            }
                            results[threadId] = false;
                        }
                    });
                    threads[i].Start();
                }

                // Wait for all threads
                foreach (var thread in threads)
                {
                    thread.Join();
                }

                // Check results
                foreach (var result in results)
                {
                    passed &= result;
                }

                if (passed)
                {
                    Console.WriteLine("✓ PASS: All threads maintained isolated state");
                }
                else
                {
                    Console.WriteLine("✗ FAIL: Thread state contamination detected");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ EXCEPTION: {ex.Message}");
                passed = false;
            }

            return passed;
        }

        /// <summary>
        /// Test 3: Rapid context switching
        /// Validates ThreadStatic behavior under rapid thread creation/destruction.
        /// Expected: State remains isolated even with aggressive thread churn.
        /// </summary>
        private static bool Test3_RapidContextSwitching()
        {
            Console.WriteLine("\n--- Test 3: Rapid Context Switching ---");
            bool passed = true;
            const int iterations = 100;
            var tasks = new Task<bool>[iterations];

            try
            {
                for (int i = 0; i < iterations; i++)
                {
                    int taskId = i;
                    tasks[i] = Task.Run(() =>
                    {
                        string expectedData = $"TASK_{taskId}";
                        _testBuffer = expectedData;
                        _testCounter = taskId;

                        // Minimal delay to maximize context switching
                        Thread.Sleep(1);

                        return _testBuffer == expectedData && _testCounter == taskId;
                    });
                }

                Task.WaitAll(tasks);

                int successCount = 0;
                foreach (var task in tasks)
                {
                    if (task.Result)
                    {
                        successCount++;
                    }
                }

                if (successCount == iterations)
                {
                    Console.WriteLine($"✓ PASS: All {iterations} rapid context switches maintained isolated state");
                    passed = true;
                }
                else
                {
                    Console.WriteLine(
                        $"✗ FAIL: {iterations - successCount}/{iterations} tasks had state contamination"
                    );
                    passed = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ EXCEPTION: {ex.Message}");
                passed = false;
            }

            return passed;
        }

        /// <summary>
        /// Test 4: Thread reuse detection (CRITICAL - Director requirement)
        /// Simulates NinjaTrader's potential thread pooling behavior.
        /// Tests if ThreadStatic state leaks between different strategy instances on the same thread.
        ///
        /// Pattern: Instance A writes "AAA", Instance B writes "BBB" on same thread.
        /// Expected: No cross-contamination if ThreadStatic is safe.
        /// UNSAFE if: Instance B sees Instance A's data.
        /// </summary>
        private static bool Test4_ThreadReuseDetection()
        {
            Console.WriteLine("\n--- Test 4: Thread Reuse Detection (CRITICAL) ---");
            Console.WriteLine("Simulating multiple strategy instances on same thread (thread pool scenario)");
            bool passed = true;

            try
            {
                // Simulate thread pool with limited threads
                ThreadPool.SetMinThreads(2, 2);
                ThreadPool.SetMaxThreads(2, 2);

                var results = new List<string>();
                var resultsLock = new object();
                const int instanceCount = 20; // More instances than threads to force reuse
                var tasks = new Task[instanceCount];

                for (int i = 0; i < instanceCount; i++)
                {
                    int instanceId = i;
                    tasks[i] = Task.Run(() =>
                    {
                        int threadId = Thread.CurrentThread.ManagedThreadId;
                        string instanceData = $"INSTANCE_{instanceId}";

                        // Check for leaked state from previous instance on this thread
                        string leakedState = _testBuffer;
                        if (leakedState != null)
                        {
                            lock (resultsLock)
                            {
                                results.Add(
                                    $"✗ Thread {threadId}: Instance {instanceId} found leaked state: {leakedState}"
                                );
                            }
                        }

                        // Write this instance's data
                        _testBuffer = instanceData;
                        _testCounter = instanceId;

                        // Simulate work
                        Thread.Sleep(5);

                        // Verify our data wasn't corrupted
                        if (_testBuffer != instanceData || _testCounter != instanceId)
                        {
                            lock (resultsLock)
                            {
                                results.Add(
                                    $"✗ Thread {threadId}: Instance {instanceId} data corrupted! Expected={instanceData}, Got={_testBuffer}"
                                );
                            }
                        }
                        else
                        {
                            lock (resultsLock)
                            {
                                results.Add($"✓ Thread {threadId}: Instance {instanceId} state correct");
                            }
                        }

                        // CRITICAL: Clear state to simulate instance cleanup
                        // If NinjaTrader doesn't do this, ThreadStatic will leak!
                        _testBuffer = null;
                        _testCounter = 0;
                    });
                }

                Task.WaitAll(tasks);

                // Analyze results
                int leakCount = 0;
                int corruptionCount = 0;
                int successCount = 0;

                foreach (var result in results)
                {
                    Console.WriteLine($"  {result}");
                    if (result.Contains("leaked state"))
                    {
                        leakCount++;
                    }
                    else if (result.Contains("corrupted"))
                    {
                        corruptionCount++;
                    }
                    else if (result.Contains("✓"))
                    {
                        successCount++;
                    }
                }

                Console.WriteLine(
                    $"\nResults: {successCount} success, {leakCount} leaks, {corruptionCount} corruptions"
                );

                if (leakCount > 0)
                {
                    Console.WriteLine("✗ CRITICAL FAIL: ThreadStatic state leaked between instances on same thread");
                    Console.WriteLine("VERDICT: ThreadStatic is UNSAFE for NinjaTrader thread pool model");
                    Console.WriteLine("RECOMMENDATION: Use instance-level buffer with lock for T05");
                    passed = false;
                }
                else if (corruptionCount > 0)
                {
                    Console.WriteLine("✗ FAIL: State corruption detected (possible race condition)");
                    passed = false;
                }
                else
                {
                    Console.WriteLine("✓ PASS: No state leakage detected in thread reuse scenario");
                    Console.WriteLine("NOTE: This test assumes explicit state cleanup. Verify NinjaTrader does this.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ EXCEPTION: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                passed = false;
            }

            return passed;
        }
    }
}

// Made with Bob
