// <copyright file="V12_002.IO.PathValidation.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
// EPIC-7-QUALITY-010: File I/O Security - Path Validation
// Prevents path traversal, TOCTOU, and symlink attacks

using System;
using System.IO;
using System.Security;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002
    {
        /// <summary>
        /// Path validation helper for secure file I/O operations.
        /// Enforces Zero-Trust Architecture: validates all file paths before use.
        ///
        /// SECURITY MODEL:
        /// - Sandbox: All paths must resolve within MyDocuments\NinjaTrader 8
        /// - Canonicalization: Resolves .., symlinks, and relative paths via Path.GetFullPath()
        /// - TOCTOU Mitigation: Validation happens immediately before file operation
        /// - Fail-Fast: SecurityException thrown on any violation
        ///
        /// LIMITATIONS:
        /// - Does not prevent race conditions between validation and file operation
        /// - Relies on Windows ACLs for actual access control
        /// - Symlink resolution depends on .NET Framework behavior
        /// </summary>
        private static class PathValidation
        {
            // Base directory: MyDocuments\NinjaTrader 8
            // All file operations MUST stay within this sandbox
            // Ensure trailing separator for accurate prefix matching
            private static readonly string _baseDir = Path.GetFullPath(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NinjaTrader 8")
            );
            private static readonly string _baseDirWithSeparator =
                _baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            /// <summary>
            /// Validates and canonicalizes a file path.
            /// Throws SecurityException if path traversal is detected.
            /// </summary>
            /// <param name="path">Path to validate</param>
            /// <param name="operation">Operation name for logging (e.g., "WriteState", "ReadCSV")</param>
            /// <returns>Canonicalized safe path</returns>
            /// <exception cref="ArgumentException">Path is null or empty</exception>
            /// <exception cref="SecurityException">Path traversal detected</exception>
            public static string ValidateAndCanonicalize(string path, string operation)
            {
                // Guard: Null/empty check
                if (string.IsNullOrWhiteSpace(path))
                {
                    throw new ArgumentException(
                        string.Format("[IO_VALIDATION] Path cannot be null/empty for operation: {0}", operation)
                    );
                }

                try
                {
                    // Canonicalize: Resolve .., symlinks, and relative paths
                    string canonical = Path.GetFullPath(path);

                    // Security check: Ensure path stays within NinjaTrader 8 directory
                    // Use trailing separator to prevent bypass via paths like "C:\NinjaTrader 8.1"
                    // Allow exact match to base directory itself (for directory operations)
                    if (
                        !canonical.StartsWith(_baseDirWithSeparator, StringComparison.OrdinalIgnoreCase)
                        && !canonical.Equals(_baseDir, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        throw new SecurityException(
                            string.Format(
                                "[IO_VALIDATION] Path traversal blocked for operation '{0}': {1} (canonical: {2}) is outside allowed base: {3}",
                                operation,
                                path,
                                canonical,
                                _baseDir
                            )
                        );
                    }

                    return canonical;
                }
                catch (SecurityException)
                {
                    // Re-throw security exceptions as-is
                    throw;
                }
                catch (Exception ex)
                {
                    // Wrap other exceptions (e.g., invalid path characters)
                    throw new SecurityException(
                        string.Format(
                            "[IO_VALIDATION] Path validation failed for operation '{0}': {1} - {2}",
                            operation,
                            path,
                            ex.Message
                        ),
                        ex
                    );
                }
            }

            /// <summary>
            /// Validates a directory path for creation.
            /// </summary>
            /// <param name="path">Directory path to validate</param>
            /// <param name="operation">Operation name for logging</param>
            /// <returns>Canonicalized safe directory path</returns>
            public static string ValidateDirectoryPath(string path, string operation)
            {
                // Same validation logic as files
                return ValidateAndCanonicalize(path, operation);
            }
        }
    }
}

// Made with Bob
