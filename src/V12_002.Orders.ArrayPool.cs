// Build 1111.009: Orders.ArrayPool -- Lock-free Order[] pooling for SIMA propagation hot path
// EPIC-5-PERF T04: Eliminates `new[] { order }` allocations in PropagateMasterTargetMove and PropagateFollowerEntryReplace
using System;
using System.Collections.Concurrent;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region Orders ArrayPool

        /// <summary>
        /// Lock-free pool for Order[1] arrays used in Submit/Cancel operations.
        /// Pre-warms 20 instances to eliminate allocations in propagation hot path.
        /// </summary>
        private class OrderArrayPool
        {
            private readonly ConcurrentBag<Order[]> _pool = new ConcurrentBag<Order[]>();

            public OrderArrayPool()
            {
                // Pre-warm 20 instances for typical fleet size (12 accounts + headroom)
                for (int i = 0; i < 20; i++)
                {
                    _pool.Add(new Order[1]);
                }
            }

            /// <summary>
            /// Rent an Order[1] array from the pool. Never returns null.
            /// </summary>
            public Order[] Rent()
            {
                Order[] array;
                if (_pool.TryTake(out array))
                {
                    return array;
                }
                // Pool exhausted - allocate new (rare, only if >20 concurrent propagations)
                return new Order[1];
            }

            /// <summary>
            /// Return an Order[1] array to the pool. Clears the reference to prevent memory leaks.
            /// </summary>
            public void Return(Order[] array)
            {
                if (array != null && array.Length == 1)
                {
                    array[0] = null; // Clear reference to prevent holding stale Order objects
                    _pool.Add(array);
                }
            }
        }

        #endregion
    }
}

// Made with Bob
