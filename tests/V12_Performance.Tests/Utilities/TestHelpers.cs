using System;
using System.Threading;
using System.Threading.Tasks;

namespace NinjaTrader.Custom.AddOns.V12_Performance.Tests.Utilities
{
    /// <summary>
    /// Reusable test utilities for V12 testing.
    /// </summary>
    public static class TestHelpers
    {
        /// <summary>
        /// Executes action with timeout to prevent hanging tests.
        /// </summary>
        public static void ExecuteWithTimeout(Action action, int timeoutMs = 5000)
        {
            var cts = new CancellationTokenSource(timeoutMs);
            var task = Task.Run(action, cts.Token);

            if (!task.Wait(timeoutMs))
            {
                throw new TimeoutException($"Test exceeded timeout of {timeoutMs}ms");
            }
        }

        /// <summary>
        /// Retries action until condition is met or timeout.
        /// Useful for testing async/eventual consistency scenarios.
        /// </summary>
        public static void RetryUntil(Func<bool> condition, int maxRetries = 10, int delayMs = 100)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                if (condition())
                    return;

                Thread.Sleep(delayMs);
            }

            throw new TimeoutException($"Condition not met after {maxRetries} retries");
        }

        /// <summary>
        /// Generates random test data within specified range.
        /// </summary>
        public static double GenerateRandomPrice(double min = 100.0, double max = 200.0)
        {
            var random = new Random();
            return min + (random.NextDouble() * (max - min));
        }

        /// <summary>
        /// Validates V12 DNA compliance: ASCII-only strings.
        /// </summary>
        public static bool IsAsciiOnly(string input)
        {
            foreach (char c in input)
            {
                if (c > 127)
                    return false;
            }
            return true;
        }
    }
}

// Made with Bob
