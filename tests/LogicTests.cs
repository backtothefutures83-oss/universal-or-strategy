using NUnit.Framework;
using NinjaTrader.NinjaScript.Strategies;
using System.Linq;

namespace UniversalOrStrategy.Tests
{
    [TestFixture]
    public class LogicTests
    {
        [Test]
        [TestCase(10, 1, new[] { 10, 0, 0, 0, 0 })]
        [TestCase(10, 2, new[] { 5, 5, 0, 0, 0 })]
        [TestCase(10, 3, new[] { 4, 3, 3, 0, 0 })] // Remainder 10%3=1 goes to T1
        [TestCase(10, 4, new[] { 3, 3, 2, 2, 0 })] // Remainder 10%4=2 goes to T1, T2
        [TestCase(7, 5, new[] { 2, 2, 1, 1, 1 })]  // Remainder 7%5=2 goes to T1, T2
        public void GetTargetDistribution_ValidInputs_ReturnsExpectedBuckets(int contracts, int count, int[] expected)
        {
            var result = V12_PureLogic.GetTargetDistribution(contracts, count);
            Assert.AreEqual(expected, result, $"Failed for {contracts} contracts with {count} targets");
        }

        [Test]
        public void CalculatePositionSize_BasicRisk_ReturnsCorrectQty()
        {
            // Stop = 10 points, PV = $50, Risk = $1000, No cushion
            // Contracts = 1000 / (10 * 50) = 2
            int qty = V12_PureLogic.CalculatePositionSize(10, 1000, 0, 50, 1, 100);
            Assert.AreEqual(2, qty);
        }

        [Test]
        public void CalculatePositionSize_WithCushion_ReturnsCorrectQty()
        {
            // Stop = 10 points, PV = $50, Risk = $1100, Cushion = 2 points ($100)
            // Effective Risk = 1100 - 100 = 1000
            // Contracts = 1000 / (10 * 50) = 2
            int qty = V12_PureLogic.CalculatePositionSize(10, 1100, 2, 50, 1, 100);
            Assert.AreEqual(2, qty);
        }

        [Test]
        public void CalculatePositionSize_MinMaxClamp_ClampsCorrectly()
        {
            // Math results in 10, but max is 5
            int qty = V12_PureLogic.CalculatePositionSize(2, 1000, 0, 50, 1, 5);
            Assert.AreEqual(5, qty);

            // Math results in 0, but min is 1
            qty = V12_PureLogic.CalculatePositionSize(100, 10, 0, 50, 1, 100);
            Assert.AreEqual(1, qty);
        }

        [Test]
        public void CalculateATRStopDistance_ValidATR_ReturnsCeilingStop()
        {
            // 2.3 ATR * 2.0 Mult = 4.6 -> Ceiling = 5.0
            double stop = V12_PureLogic.CalculateATRStopDistance(2.3, 2.0, 1.0, 100.0);
            Assert.AreEqual(5.0, stop);
        }
    }
}
