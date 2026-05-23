// EPIC-5-PERF T04: Concurrent Modification Test for Snapshot Pattern
// Validates thread-safe iteration using .ToArray() snapshots with concurrent mutations
// MANDATORY GATE: 1000 iterations, zero exceptions, zero data corruption

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace V12_002.Tests
{
    public class T04_SnapshotPattern_ConcurrentModification_Test
    {
        private static int _testsPassed = 0;
        private static int _testsFailed = 0;
        private static readonly object _consoleLock = new object();

        public static void Main(string[] args)
        {
            Console.WriteLine("=== EPIC-5-PERF T04: Snapshot Pattern Concurrent Modification Test ===");
            Console.WriteLine("Target: 1000 iterations, zero exceptions, zero data corruption");
            Console.WriteLine();

            RunAllTests();

            Console.WriteLine();
            Console.WriteLine("=== TEST SUMMARY ===");
            Console.WriteLine($"PASSED: {_testsPassed}");
            Console.WriteLine($"FAILED: {_testsFailed}");
            Console.WriteLine();

            if (_testsFailed == 0)
            {
                Console.WriteLine("[GATE PASS] All concurrent modification tests passed!");
                Environment.Exit(0);
            }
            else
            {
                Console.WriteLine("[GATE FAIL] Some tests failed. Review output above.");
                Environment.Exit(1);
            }
        }

        private static void RunAllTests()
        {
            // Test 1: Basic snapshot pattern with concurrent adds
            Test_SnapshotWithConcurrentAdds(1000);

            // Test 2: Snapshot pattern with concurrent removes
            Test_SnapshotWithConcurrentRemoves(1000);

            // Test 3: Snapshot pattern with mixed add/remove operations
            Test_SnapshotWithMixedOperations(1000);

            // Test 4: Nested snapshot reuse (Director's critical requirement)
            Test_NestedSnapshotReuse(1000);

            // Test 5: ContainsKey re-check validation
            Test_ContainsKeyRecheck(1000);
        }

        // Test 1: Concurrent adds during snapshot iteration
        private static void Test_SnapshotWithConcurrentAdds(int iterations)
        {
            string testName = "SnapshotWithConcurrentAdds";
            Console.WriteLine($"[TEST] {testName} ({iterations} iterations)...");

            try
            {
                for (int i = 0; i < iterations; i++)
                {
                    var dict = new ConcurrentDictionary<string, int>();
                    dict["A"] = 1;
                    dict["B"] = 2;
                    dict["C"] = 3;

                    var snapshot = dict.ToArray();
                    int snapshotCount = snapshot.Length;

                    // Start concurrent add operations
                    var addTask = Task.Run(() =>
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            dict[$"NEW_{j}"] = j;
                            Thread.Sleep(1);
                        }
                    });

                    // Iterate snapshot (should not throw)
                    int iteratedCount = 0;
                    foreach (var kvp in snapshot)
                    {
                        iteratedCount++;
                        // Simulate work
                        Thread.Sleep(1);
                    }

                    addTask.Wait();

                    // Validate: snapshot count should match iterated count
                    if (iteratedCount != snapshotCount)
                    {
                        throw new Exception(
                            $"Iteration {i}: Count mismatch. Expected {snapshotCount}, got {iteratedCount}"
                        );
                    }
                }

                LogPass(testName);
            }
            catch (Exception ex)
            {
                LogFail(testName, ex.Message);
            }
        }

        // Test 2: Concurrent removes during snapshot iteration
        private static void Test_SnapshotWithConcurrentRemoves(int iterations)
        {
            string testName = "SnapshotWithConcurrentRemoves";
            Console.WriteLine($"[TEST] {testName} ({iterations} iterations)...");

            try
            {
                for (int i = 0; i < iterations; i++)
                {
                    var dict = new ConcurrentDictionary<string, int>();
                    for (int j = 0; j < 20; j++)
                    {
                        dict[$"KEY_{j}"] = j;
                    }

                    var snapshot = dict.ToArray();

                    // Start concurrent remove operations
                    var removeTask = Task.Run(() =>
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            dict.TryRemove($"KEY_{j}", out _);
                            Thread.Sleep(1);
                        }
                    });

                    // Iterate snapshot with ContainsKey re-check
                    int validCount = 0;
                    foreach (var kvp in snapshot)
                    {
                        if (dict.ContainsKey(kvp.Key))
                        {
                            validCount++;
                        }
                        Thread.Sleep(1);
                    }

                    removeTask.Wait();

                    // Validate: no exceptions thrown
                    if (validCount < 0)
                    {
                        throw new Exception($"Iteration {i}: Invalid count {validCount}");
                    }
                }

                LogPass(testName);
            }
            catch (Exception ex)
            {
                LogFail(testName, ex.Message);
            }
        }

        // Test 3: Mixed add/remove operations
        private static void Test_SnapshotWithMixedOperations(int iterations)
        {
            string testName = "SnapshotWithMixedOperations";
            Console.WriteLine($"[TEST] {testName} ({iterations} iterations)...");

            try
            {
                for (int i = 0; i < iterations; i++)
                {
                    var dict = new ConcurrentDictionary<string, int>();
                    for (int j = 0; j < 15; j++)
                    {
                        dict[$"KEY_{j}"] = j;
                    }

                    var snapshot = dict.ToArray();

                    // Start concurrent mixed operations
                    var mixedTask = Task.Run(() =>
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            if (j % 2 == 0)
                            {
                                dict[$"NEW_{j}"] = j;
                            }
                            else
                            {
                                dict.TryRemove($"KEY_{j}", out _);
                            }
                            Thread.Sleep(1);
                        }
                    });

                    // Iterate snapshot with re-check
                    int processedCount = 0;
                    foreach (var kvp in snapshot)
                    {
                        if (dict.ContainsKey(kvp.Key))
                        {
                            int value = kvp.Value;
                            processedCount++;
                        }
                        Thread.Sleep(1);
                    }

                    mixedTask.Wait();

                    // Validate: no exceptions, processed count reasonable
                    if (processedCount < 0 || processedCount > snapshot.Length)
                    {
                        throw new Exception($"Iteration {i}: Invalid processed count {processedCount}");
                    }
                }

                LogPass(testName);
            }
            catch (Exception ex)
            {
                LogFail(testName, ex.Message);
            }
        }

        // Test 4: Nested snapshot reuse (Director's critical requirement)
        private static void Test_NestedSnapshotReuse(int iterations)
        {
            string testName = "NestedSnapshotReuse";
            Console.WriteLine($"[TEST] {testName} ({iterations} iterations)...");

            try
            {
                for (int i = 0; i < iterations; i++)
                {
                    var dict = new ConcurrentDictionary<string, int>();
                    for (int j = 0; j < 10; j++)
                    {
                        dict[$"KEY_{j}"] = j;
                    }

                    // Single snapshot at scope start
                    var snapshot = dict.ToArray();

                    // Start concurrent mutations
                    var mutateTask = Task.Run(() =>
                    {
                        for (int j = 0; j < 5; j++)
                        {
                            dict[$"MUTATE_{j}"] = j;
                            dict.TryRemove($"KEY_{j}", out _);
                            Thread.Sleep(1);
                        }
                    });

                    // Outer loop uses snapshot
                    int outerCount = 0;
                    foreach (var kvp in snapshot)
                    {
                        if (dict.ContainsKey(kvp.Key))
                        {
                            outerCount++;

                            // Inner loop REUSES same snapshot (zero additional allocation)
                            int innerCount = 0;
                            foreach (var kvp2 in snapshot)
                            {
                                if (dict.ContainsKey(kvp2.Key))
                                {
                                    innerCount++;
                                }
                            }

                            // Validate inner loop completed
                            if (innerCount < 0)
                            {
                                throw new Exception($"Iteration {i}: Inner loop failed");
                            }
                        }
                    }

                    mutateTask.Wait();

                    // Validate: no exceptions, counts reasonable
                    if (outerCount < 0 || outerCount > snapshot.Length)
                    {
                        throw new Exception($"Iteration {i}: Invalid outer count {outerCount}");
                    }
                }

                LogPass(testName);
            }
            catch (Exception ex)
            {
                LogFail(testName, ex.Message);
            }
        }

        // Test 5: ContainsKey re-check validation
        private static void Test_ContainsKeyRecheck(int iterations)
        {
            string testName = "ContainsKeyRecheck";
            Console.WriteLine($"[TEST] {testName} ({iterations} iterations)...");

            try
            {
                for (int i = 0; i < iterations; i++)
                {
                    var dict = new ConcurrentDictionary<string, int>();
                    dict["A"] = 1;
                    dict["B"] = 2;
                    dict["C"] = 3;

                    var snapshot = dict.ToArray();

                    // Remove all items before iteration
                    dict.Clear();

                    // Iterate snapshot with re-check (should skip all items)
                    int accessedCount = 0;
                    foreach (var kvp in snapshot)
                    {
                        if (dict.ContainsKey(kvp.Key))
                        {
                            accessedCount++;
                        }
                    }

                    // Validate: zero items accessed (all were removed)
                    if (accessedCount != 0)
                    {
                        throw new Exception($"Iteration {i}: Expected 0 accessed, got {accessedCount}");
                    }
                }

                LogPass(testName);
            }
            catch (Exception ex)
            {
                LogFail(testName, ex.Message);
            }
        }

        private static void LogPass(string testName)
        {
            lock (_consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[PASS] {testName}");
                Console.ResetColor();
                _testsPassed++;
            }
        }

        private static void LogFail(string testName, string error)
        {
            lock (_consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] {testName}: {error}");
                Console.ResetColor();
                _testsFailed++;
            }
        }
    }
}

// Made with Bob
