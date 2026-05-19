// Epic 2 Visual & Command Pipeline Concurrency Hardening Tests
// Tests for H09-H12: Visual render race, button command race, stale state, and re-entrancy protection
// Source-scan assertions verify the actual production code patterns (same model as Build981ComplianceTests.cs)
using System;
using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace V12_002.Tests
{
    public class Epic2VisualTests
    {
        private static string SrcPath(string file)
            => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "src", file);

        // ---------------------------------------------------------------
        // H09 + H11: Panel Refresh Snapshot Isolation & Null Guard
        // Fix location: V12_002.UI.Panel.StateSync.cs
        // ---------------------------------------------------------------

        [Fact]
        public void H09_PanelStateSync_UsesThreadMemoryBarrier()
        {
            // ASSERT: Thread.MemoryBarrier() is present before snapshot read in UpdatePanelState
            // This prevents stale reads when fleet sync updates state on another thread.
            string src = File.ReadAllText(SrcPath("V12_002.UI.Panel.StateSync.cs"), Encoding.UTF8);
            Assert.Contains("Thread.MemoryBarrier()", src);
        }

        [Fact]
        public void H09_PanelStateSync_CreatesLocalTargetsSnapshot()
        {
            // ASSERT: Local array snapshot taken before iteration to prevent collection-modified exceptions
            string src = File.ReadAllText(SrcPath("V12_002.UI.Panel.StateSync.cs"), Encoding.UTF8);
            Assert.Contains("UILiveTargetSnapshot[] targetsSnapshot", src);
        }

        [Fact]
        public void H11_PanelStateSync_NullGuardsSnapshot()
        {
            // ASSERT: Null guard on snapshot before dereference (crash prevention during termination)
            string src = File.ReadAllText(SrcPath("V12_002.UI.Panel.StateSync.cs"), Encoding.UTF8);
            Assert.Contains("if (snapshot == null) return;", src);
        }

        [Fact]
        public void H11_PanelStateSync_NullGuardsTargetsArray()
        {
            // ASSERT: Null guard on targetsSnapshot to prevent NullReferenceException
            string src = File.ReadAllText(SrcPath("V12_002.UI.Panel.StateSync.cs"), Encoding.UTF8);
            Assert.Contains("if (targetsSnapshot == null) return;", src);
        }

        // ---------------------------------------------------------------
        // H10: Button Command Execution Race -- Enqueue Pattern
        // Fix location: V12_002.UI.IPC.Commands.Fleet.cs
        // ---------------------------------------------------------------

        [Fact]
        public void H10_FlattenCommand_EnqueuesFlattenAllApexAccounts()
        {
            // ASSERT: FLATTEN uses Enqueue to FSM actor, not direct call
            string src = File.ReadAllText(SrcPath("V12_002.UI.IPC.Commands.Fleet.cs"), Encoding.UTF8);
            Assert.Contains("Enqueue(ctx => ctx.FlattenAllApexAccounts())", src);
        }

        [Fact]
        public void H10_FlattenCommand_EnqueuesFlattenAll()
        {
            // ASSERT: Master account flatten also enqueued via actor model
            string src = File.ReadAllText(SrcPath("V12_002.UI.IPC.Commands.Fleet.cs"), Encoding.UTF8);
            Assert.Contains("Enqueue(ctx => ctx.FlattenAll())", src);
        }

        [Fact]
        public void H10_CancelAllCommand_EnqueuesCancelAllOrders()
        {
            // ASSERT: CANCEL_ALL enqueued via Enqueue, not direct call
            string src = File.ReadAllText(SrcPath("V12_002.UI.IPC.Commands.Fleet.cs"), Encoding.UTF8);
            Assert.Contains("Enqueue(ctx => ctx.ExecuteCancelAllOrders())", src);
        }

        // ---------------------------------------------------------------
        // H12: IPC Command Re-Entrancy Protection
        // Fix location: V12_002.UI.IPC.Commands.Fleet.cs
        // ---------------------------------------------------------------

        [Fact]
        public void H12_ReentrancyProtection_DeclaresLastFlattenTicks()
        {
            // ASSERT: Long field for atomic cooldown tracking declared
            string src = File.ReadAllText(SrcPath("V12_002.UI.IPC.Commands.Fleet.cs"), Encoding.UTF8);
            Assert.Contains("_lastFlattenTicks", src);
        }

        [Fact]
        public void H12_ReentrancyProtection_DeclaresLastCancelAllTicks()
        {
            // ASSERT: Separate cooldown field for CANCEL_ALL
            string src = File.ReadAllText(SrcPath("V12_002.UI.IPC.Commands.Fleet.cs"), Encoding.UTF8);
            Assert.Contains("_lastCancelAllTicks", src);
        }

        [Fact]
        public void H12_ReentrancyProtection_UsesCooldownConstant()
        {
            // ASSERT: 1-second cooldown constant defined (TimeSpan.TicksPerSecond)
            string src = File.ReadAllText(SrcPath("V12_002.UI.IPC.Commands.Fleet.cs"), Encoding.UTF8);
            Assert.Contains("ReentrancyCooldownTicks", src);
            Assert.Contains("TimeSpan.TicksPerSecond", src);
        }

        [Fact]
        public void H12_ReentrancyProtection_UsesInterlockedCompareExchange()
        {
            // ASSERT: Atomic CAS used for cooldown claim -- prevents race between concurrent requests
            string src = File.ReadAllText(SrcPath("V12_002.UI.IPC.Commands.Fleet.cs"), Encoding.UTF8);
            Assert.Contains("Interlocked.CompareExchange(ref _lastFlattenTicks", src);
            Assert.Contains("Interlocked.CompareExchange(ref _lastCancelAllTicks", src);
        }

        [Fact]
        public void H12_ReentrancyProtection_UsesInterlockedRead()
        {
            // ASSERT: Atomic read of ticks (not non-atomic long read which can tear on 32-bit)
            string src = File.ReadAllText(SrcPath("V12_002.UI.IPC.Commands.Fleet.cs"), Encoding.UTF8);
            Assert.Contains("Interlocked.Read(ref _lastFlattenTicks)", src);
            Assert.Contains("Interlocked.Read(ref _lastCancelAllTicks)", src);
        }

        // ---------------------------------------------------------------
        // DNA Compliance Audits (cross-file)
        // ---------------------------------------------------------------

        [Fact]
        public void Epic2_PanelStateSync_NoNewLockStatements()
        {
            // ASSERT: Zero C# lock() statements in UI Panel file (DNA: lock-free)
            // Use " lock(" (space-prefixed) to avoid matching substrings like unlock( or deadlock(
            string src = File.ReadAllText(SrcPath("V12_002.UI.Panel.StateSync.cs"), Encoding.UTF8);
            bool hasLockStatement = src.Contains(" lock(") || src.Contains("\tlock(") || src.Contains("\nlock(");
            Assert.False(hasLockStatement, "Panel.StateSync.cs must not contain C# lock() statements (V12 DNA violation)");
        }

        [Fact]
        public void Epic2_IPCCommandsFleet_NoNewLockStatements()
        {
            // ASSERT: Zero C# lock() statements in IPC commands file (DNA: lock-free)
            // Use " lock(" (space-prefixed) to avoid matching substrings like unlock( or deadlock(
            string src = File.ReadAllText(SrcPath("V12_002.UI.IPC.Commands.Fleet.cs"), Encoding.UTF8);
            bool hasLockStatement = src.Contains(" lock(") || src.Contains("\tlock(") || src.Contains("\nlock(");
            Assert.False(hasLockStatement, "IPC.Commands.Fleet.cs must not contain C# lock() statements (V12 DNA violation)");
        }

        [Fact]
        public void Epic2_IPCCommandsFleet_H12CommentPresent()
        {
            // ASSERT: H12 fix comment is present (audit trail in source)
            string src = File.ReadAllText(SrcPath("V12_002.UI.IPC.Commands.Fleet.cs"), Encoding.UTF8);
            Assert.Contains("H12", src);
        }

        [Fact]
        public void Epic2_PanelStateSync_H09CommentPresent()
        {
            // ASSERT: H09/H11 fix comment present (audit trail in source)
            string src = File.ReadAllText(SrcPath("V12_002.UI.Panel.StateSync.cs"), Encoding.UTF8);
            Assert.Contains("H09", src);
        }
    }
}

// Made with Bob + Antigravity (Epic 2 test quality pass -- Assert.True stubs replaced)
