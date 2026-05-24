using DocumentFormat.OpenXml.Drawing.Charts;
using Xunit;

namespace Routa.OfficeWasmReader;

public class OpenXmlChartReaderMappingTests
{
    [Theory]
    [InlineData("<c:pieChart/>", "pie")]
    [InlineData("<c:areaChart/>", "area")]
    [InlineData("<c:scatterChart/>", "scatter")]
    [InlineData("<c:bubbleChart/>", "bubble")]
    [InlineData("<c:doughnutChart/>", "doughnut")]
    [InlineData("<c:radarChart/>", "radar")]
    [InlineData("<c:surfaceChart/>", "surface")]
    [InlineData("", "unknown")]
    public void DetectChartType_MapsSupportedChartFamilies(string chartXml, string expected)
    {
        var chartSpace = new ChartSpace();
        chartSpace.InnerXml = $"""
<c:chart xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart">
  <c:plotArea>{chartXml}</c:plotArea>
</c:chart>
""";

        var method = typeof(OpenXmlChartReader).GetMethod(
            "DetectChartType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        Assert.Equal(expected, Assert.IsType<string>(method.Invoke(null, [chartSpace])));
    }
}
