using Xunit;
using NinjaTrader.NinjaScript.Strategies.Services;

namespace V12.Tests.Services
{
    public class StickyStateServiceTests
    {
        [Fact]
        public void CanInstantiateWithoutNinjaTrader()
        {
            // Proves dotnet test works without NinjaTrader runtime
            var logger = new TestLogger();
            var service = new StickyStateService(logger);
            Assert.NotNull(service);
        }

        private class TestLogger : IStickyStateLogger
        {
            public void Log(string message) { }
        }
    }
}

// Made with Bob
