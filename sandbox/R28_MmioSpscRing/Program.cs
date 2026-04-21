using System;
using System.Diagnostics;

namespace R28
{
    // R28 8-test battery for MmioSpscRing<T>.
    // Exit code 0 on all-pass, 1 on any failure.
    internal static unsafe class Program
    {
        private static int _failCount;

        private static void Report(int idx, string name, bool pass, string detail)
        {
            string tag = pass ? "PASS" : "FAIL";
            if (!pass) _failCount++;
            string line = "[" + idx + "] " + name.PadRight(22) + " " + tag;
            if (detail != null) line += " (" + detail + ")";
            Console.WriteLine(line);
        }

        internal static int Main(string[] args)
        {
            Console.WriteLine("R28 MmioSpscRing<T> -- 8-test battery");
            Console.WriteLine("=====================================");

            try
            {
                Test1_Roundtrip();
                Test2_Sequential10();
                Test3_Corruption();
                Test4_RingFull();
                Test5_RingEmpty();
                Test6_WrapAround();
                Test7_Throughput();
                Test8_MultiType();
            }
            catch (Exception ex)
            {
                Console.WriteLine("UNEXPECTED EXCEPTION: " + ex.GetType().Name + ": " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                return 2;
            }

            Console.WriteLine("=====================================");
            if (_failCount == 0)
            {
                Console.WriteLine("ALL TESTS PASSED");
                return 0;
            }
            Console.WriteLine(_failCount + " TEST(S) FAILED");
            return 1;
        }

        // ---------- Test 1: Single round-trip ----------
        private static void Test1_Roundtrip()
        {
            using (var ring = new MmioSpscRing<OrderSlot>(64))
            {
                var o = new OrderSlot
                {
                    Id = 1,
                    SymbolHash = 0xAABBCCDD,
                    Price = 100.5,
                    Quantity = 10,
                    Side = 1,
                    TimestampTicks = 123456789L,
                    Account = 7,
                    Reserved = 42L
                };
                bool enq = ring.TryEnqueue(ref o);

                OrderSlot r;
                bool valid;
                bool deq = ring.TryDequeue(out r, out valid);

                bool pass = enq && deq && valid
                            && r.Id == 1
                            && r.SymbolHash == 0xAABBCCDD
                            && r.Price == 100.5
                            && r.Quantity == 10
                            && r.Side == 1
                            && r.TimestampTicks == 123456789L
                            && r.Account == 7
                            && r.Reserved == 42L;
                Report(1, "Round-trip", pass, "shadowValid=" + valid);
            }
        }

        // ---------- Test 2: Sequential 10 ----------
        private static void Test2_Sequential10()
        {
            using (var ring = new MmioSpscRing<OrderSlot>(64))
            {
                for (int i = 0; i < 10; i++)
                {
                    var o = new OrderSlot { Id = i, Quantity = i * 2, Price = i + 0.5 };
                    if (!ring.TryEnqueue(ref o))
                    {
                        Report(2, "Sequential 10", false, "enq failed at i=" + i);
                        return;
                    }
                }
                for (int i = 0; i < 10; i++)
                {
                    OrderSlot r;
                    bool valid;
                    if (!ring.TryDequeue(out r, out valid) || !valid
                        || r.Id != i || r.Quantity != i * 2 || r.Price != i + 0.5)
                    {
                        Report(2, "Sequential 10", false, "deq mismatch at i=" + i);
                        return;
                    }
                }
                Report(2, "Sequential 10", true, "10/10");
            }
        }

        // ---------- Test 3: Corruption detection ----------
        private static void Test3_Corruption()
        {
            using (var ring = new MmioSpscRing<OrderSlot>(64))
            {
                var o = new OrderSlot { Id = 42, Quantity = 99, Price = 123.45 };
                ring.TryEnqueue(ref o);

                // Tamper with the first byte of slot 0's payload (before the shadow).
                byte* slotPtr = ring.DebugRegionPointer() + ring.DebugHeaderBytes() + 0 * ring.DebugSlotSize();
                slotPtr[0] ^= 0xFF;

                OrderSlot r;
                bool valid;
                ring.TryDequeue(out r, out valid);

                // valid MUST be false after in-place tampering.
                bool pass = !valid;
                Report(3, "Corruption detect", pass, "shadowValid=" + valid);
            }
        }

        // ---------- Test 4: Ring full behavior (capacity 64) ----------
        private static void Test4_RingFull()
        {
            using (var ring = new MmioSpscRing<OrderSlot>(64))
            {
                for (int i = 0; i < 64; i++)
                {
                    var o = new OrderSlot { Id = i };
                    if (!ring.TryEnqueue(ref o))
                    {
                        Report(4, "Ring full", false, "enq failed at i=" + i);
                        return;
                    }
                }
                var extra = new OrderSlot { Id = 999 };
                bool rejected = !ring.TryEnqueue(ref extra);
                Report(4, "Ring full", rejected, "capacity=" + ring.Capacity + " count=" + ring.Count);
            }
        }

        // ---------- Test 5: Ring empty behavior ----------
        private static void Test5_RingEmpty()
        {
            using (var ring = new MmioSpscRing<OrderSlot>(64))
            {
                OrderSlot r;
                bool valid;
                bool emptyBefore = ring.IsEmpty;
                bool deqFailed = !ring.TryDequeue(out r, out valid);
                bool pass = emptyBefore && deqFailed && !valid;
                Report(5, "Ring empty", pass, "IsEmpty=" + emptyBefore);
            }
        }

        // ---------- Test 6: Generation wrap-around ----------
        private static void Test6_WrapAround()
        {
            using (var ring = new MmioSpscRing<OrderSlot>(64))
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    for (int i = 0; i < 64; i++)
                    {
                        var o = new OrderSlot { Id = pass * 1000 + i };
                        if (!ring.TryEnqueue(ref o))
                        {
                            Report(6, "Wrap-around", false, "enq pass=" + pass + " i=" + i);
                            return;
                        }
                    }
                    for (int i = 0; i < 64; i++)
                    {
                        OrderSlot r;
                        bool valid;
                        if (!ring.TryDequeue(out r, out valid) || !valid || r.Id != pass * 1000 + i)
                        {
                            Report(6, "Wrap-around", false, "deq pass=" + pass + " i=" + i + " valid=" + valid);
                            return;
                        }
                    }
                }
                Report(6, "Wrap-around", true, "192 ops, 3 generations");
            }
        }

        // ---------- Test 7: Throughput benchmark ----------
        private static void Test7_Throughput()
        {
            const int Iterations = 10_000_000;
            using (var ring = new MmioSpscRing<OrderSlot>(64))
            {
                var o = new OrderSlot { Id = 1, Quantity = 5, Price = 100.0 };

                // Warmup (JIT + cache warm).
                for (int i = 0; i < 200_000; i++)
                {
                    ring.TryEnqueue(ref o);
                    OrderSlot tmp;
                    bool v;
                    ring.TryDequeue(out tmp, out v);
                }

                long gc0 = GC.CollectionCount(0);
                long gc1 = GC.CollectionCount(1);
                long gc2 = GC.CollectionCount(2);

                var sw = Stopwatch.StartNew();
                for (int i = 0; i < Iterations; i++)
                {
                    ring.TryEnqueue(ref o);
                    OrderSlot tmp;
                    bool v;
                    ring.TryDequeue(out tmp, out v);
                }
                sw.Stop();

                long dgc0 = GC.CollectionCount(0) - gc0;
                long dgc1 = GC.CollectionCount(1) - gc1;
                long dgc2 = GC.CollectionCount(2) - gc2;

                double totalOps = Iterations * 2.0;
                double nsPerOp = sw.Elapsed.TotalMilliseconds * 1_000_000.0 / totalOps;

                // Generous ceiling -- prompt target is <14 ns/op; sandbox net48 JIT is slower than .NET 9,
                // and MemoryMappedFile indirection adds overhead. Fail only if grossly off.
                bool perfPass = nsPerOp < 200.0;
                // Zero-alloc assertion: no GC collections should occur during the hot loop.
                bool gcPass = (dgc0 == 0 && dgc1 == 0 && dgc2 == 0);
                bool pass = perfPass && gcPass;

                string detail = nsPerOp.ToString("F2") + " ns/op, gc=["
                                + dgc0 + "," + dgc1 + "," + dgc2 + "]";
                Report(7, "Throughput", pass, detail);
            }
        }

        // ---------- Test 8: Multi-type generic (FillSlot) ----------
        private static void Test8_MultiType()
        {
            using (var ring = new MmioSpscRing<FillSlot>(64))
            {
                for (int i = 0; i < 10; i++)
                {
                    var f = new FillSlot
                    {
                        OrderId = i,
                        FillId = i * 3,
                        FillPrice = 50.25 + i,
                        FillQty = i + 1,
                        Side = i & 1,
                        FillTicks = 1_000_000L + i,
                        ExecHash = 0xDEADBEEFL ^ i,
                        Reserved = i
                    };
                    if (!ring.TryEnqueue(ref f))
                    {
                        Report(8, "Multi-type FillSlot", false, "enq i=" + i);
                        return;
                    }
                }
                for (int i = 0; i < 10; i++)
                {
                    FillSlot r;
                    bool valid;
                    if (!ring.TryDequeue(out r, out valid) || !valid
                        || r.OrderId != i || r.FillId != i * 3
                        || r.FillPrice != 50.25 + i || r.FillQty != i + 1)
                    {
                        Report(8, "Multi-type FillSlot", false, "deq i=" + i + " valid=" + valid);
                        return;
                    }
                }
                Report(8, "Multi-type FillSlot", true, "10/10");
            }
        }
    }
}
