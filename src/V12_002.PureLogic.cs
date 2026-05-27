// V12.1102Z: Pure Logic Engine (Zero-Allocation / No-NinjaTrader Dependencies)
// Contains: Mathematical kernels extracted for Unit Testing
using System;
using System.Collections.Generic;
using System.Linq;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// V12_002.PureLogic contains the deterministic mathematical nuclei of the strategy.
    /// Extracted here to allow NUnit testing without the NinjaTrader Strategy runtime.
    /// </summary>
    public static class V12_PureLogic
    {
        /// <summary>
        /// IS-01: Iron Shield Target Distribution [V12.BEYOND-BUG]
        /// Deterministically divides contracts into a bucketed distribution.
        /// </summary>
        public static int[] GetTargetDistribution(int contracts, int targetCount)
        {
            if (contracts <= 0)
                return new int[5];

            // Clamp count to [1, 5]
            int count = Math.Max(1, Math.Min(5, targetCount));

            int[] buckets = new int[5];
            int baseQty = contracts / count;
            int remainder = contracts % count;

            // Distribute base and remainder (scalp preference: extras go to T1 first)
            for (int i = 0; i < count; i++)
                buckets[i] = baseQty + (i < remainder ? 1 : 0);

            // Audit: Ensure sum matches input
            int sum = buckets.Sum();
            if (sum != contracts)
            {
                // Panic adjustment (should not happen with integer division logic above)
                buckets[count - 1] += (contracts - sum);
            }

            return buckets;
        }

        /// <summary>
        /// V12.30: Core Sizing Logic Kernel
        /// Deterministically calculates quantity based on risk budget and stop points.
        /// </summary>
        public static int CalculatePositionSize(
            double stopPoints,
            double maxRiskAmount,
            double slippageCushionPoints,
            double pointValue,
            int minContracts,
            int maxContracts
        )
        {
            if (double.IsNaN(stopPoints) || stopPoints <= 0 || pointValue <= 0)
                return Math.Max(1, minContracts);

            double stopDollars = stopPoints * pointValue;
            double slippageCushionDollars = slippageCushionPoints * pointValue;
            double effectiveRisk = maxRiskAmount - slippageCushionDollars;

            if (effectiveRisk <= 0)
                return Math.Max(1, minContracts);

            int contracts;
            try
            {
                contracts = checked((int)Math.Floor(effectiveRisk / stopDollars));
            }
            catch (OverflowException)
            {
                contracts = maxContracts;
            }

            return Math.Max(minContracts, Math.Min(contracts, maxContracts));
        }

        /// <summary>
        /// V12.30: ATR Stop Distance Logic Kernel
        /// </summary>
        public static double CalculateATRStopDistance(double atr, double atrMultiplier, double minStop, double maxStop)
        {
            if (atr <= 0)
                return minStop;

            double rawStop = atr * atrMultiplier;
            double ceilingStop = Math.Ceiling(rawStop);
            return Math.Max(minStop, Math.Min(ceilingStop, maxStop));
        }
    }
}
