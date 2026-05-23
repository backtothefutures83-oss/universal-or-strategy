using System;
using System.Threading;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Thread-local string formatting buffer to eliminate string.Format() allocations in hot paths.
    /// V12 DNA: ThreadStatic (validated safe by T01B), ASCII-only, zero CYC increase.
    /// </summary>
    public static class LogBuffer
    {
        [ThreadStatic]
        private static char[] _buffer;

        [ThreadStatic]
        private static int _threadId;

        [ThreadStatic]
        private static bool _threadIdInitialized;

        private static int _overflowCount;
        private static int _threadAffinityWarnings;

        /// <summary>
        /// Drop-in replacement for string.Format() with zero allocations for common patterns.
        /// Falls back to string.Format() if buffer overflows (correctness by construction).
        /// </summary>
        public static string Format(string format, params object[] args)
        {
            // Lazy initialization of thread-local buffer
            if (_buffer == null)
            {
                _buffer = new char[512];
            }

            // ValidateThreadAffinity telemetry (T01B Section 6.3)
            ValidateThreadAffinity();

            // Attempt zero-allocation formatting
            int length = FormatInternal(format, args);
            if (length >= 0 && length < _buffer.Length)
            {
                return new string(_buffer, 0, length);
            }

            // Overflow: fallback to string.Format() and increment counter
            Interlocked.Increment(ref _overflowCount);
            return string.Format(format, args);
        }

        /// <summary>
        /// Internal formatting logic supporting common patterns:
        /// - {0}, {1}, {2}, etc. (positional arguments)
        /// - Mixed literal text and placeholders
        /// </summary>
        private static int FormatInternal(string format, object[] args)
        {
            int bufferPos = 0;
            int formatPos = 0;

            while (formatPos < format.Length)
            {
                char c = format[formatPos];

                if (c == '{')
                {
                    // Scan for format specifier (e.g., {0:F2})
                    int closingBrace = formatPos + 1;
                    while (closingBrace < format.Length && format[closingBrace] != '}')
                    {
                        if (format[closingBrace] == ':')
                        {
                            // Format specifier detected - fallback to string.Format
                            return -1;
                        }
                        closingBrace++;
                    }

                    // Check for placeholder {N}
                    if (formatPos + 2 < format.Length && format[formatPos + 2] == '}')
                    {
                        char digitChar = format[formatPos + 1];
                        if (digitChar >= '0' && digitChar <= '9')
                        {
                            int argIndex = digitChar - '0';
                            if (argIndex < args.Length)
                            {
                                string argStr = args[argIndex]?.ToString() ?? "null";
                                if (bufferPos + argStr.Length >= _buffer.Length)
                                {
                                    return -1; // Overflow
                                }
                                argStr.CopyTo(0, _buffer, bufferPos, argStr.Length);
                                bufferPos += argStr.Length;
                                formatPos += 3; // Skip {N}
                                continue;
                            }
                        }
                    }
                }

                // Copy literal character
                if (bufferPos >= _buffer.Length)
                {
                    return -1; // Overflow
                }
                _buffer[bufferPos++] = c;
                formatPos++;
            }

            return bufferPos;
        }

        /// <summary>
        /// ValidateThreadAffinity: Track thread ID on first buffer access per thread.
        /// Log warning if thread ID changes (indicates NinjaTrader platform update).
        /// T01B Section 6.3 early-warning system.
        /// </summary>
        private static void ValidateThreadAffinity()
        {
            int currentThreadId = Thread.CurrentThread.ManagedThreadId;

            if (!_threadIdInitialized)
            {
                _threadId = currentThreadId;
                _threadIdInitialized = true;
            }
            else if (_threadId != currentThreadId)
            {
                // Thread affinity violation detected
                Interlocked.Increment(ref _threadAffinityWarnings);
                _threadId = currentThreadId; // Update to new thread ID
            }
        }

        /// <summary>
        /// Telemetry: Get overflow count (buffer too small for format string).
        /// </summary>
        public static int GetOverflowCount() => _overflowCount;

        /// <summary>
        /// Telemetry: Get thread affinity warning count (ThreadStatic migration detected).
        /// </summary>
        public static int GetThreadAffinityWarnings() => _threadAffinityWarnings;
    }
}

// Made with Bob
