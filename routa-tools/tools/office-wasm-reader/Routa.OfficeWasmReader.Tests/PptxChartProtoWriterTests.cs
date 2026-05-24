using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Google.Protobuf;
using System.Text;
using Xunit;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using P = DocumentFormat.OpenXml.Presentation;

namespace Routa.OfficeWasmReader;

public class PptxChartProtoWriterTests
{
    [Fact]
    public void WriteReference_EmitsChartId()
    {
        var bytes = PptxChartProtoWriter.WriteReference("/ppt/charts/chart1.xml");

        Assert.Equal("/ppt/charts/chart1.xml", StringField(bytes, 1));
    }

    [Fact]
    public void WriteChart_EmitsTitleCategoriesSeriesAndBarDirection()
    {
        using var document = CreatePresentation();
        var chartPart = AddChartPart(document, BarChartXml());

        var bytes = PptxChartProtoWriter.WriteChart(chartPart, WriteFakeFill);
        var fields = CountFields(bytes);
        var series = MessagesForField(bytes, 3);
        var firstSeries = series[0];

        Assert.Equal("Quarterly Revenue", StringField(bytes, 1));
        Assert.Equal(["Q1", "Q2", "Q3"], StringsForField(bytes, 2));
        Assert.Equal(2, series.Count);
        Assert.Equal("North", StringField(firstSeries, 1));
        Assert.Equal([12D, 18.5D], DoublesForField(firstSeries, 2));
        Assert.Equal(["Q1", "Q2"], StringsForField(firstSeries, 5));
        Assert.Equal(1, CountFields(firstSeries)[7]); // fill from delegate
        Assert.Equal("series-00000000", StringField(firstSeries, 8));
        Assert.Equal(4, Int32Field(bytes, 5)); // bar chart
        Assert.EndsWith("charts/chart1.xml", StringField(bytes, 7), StringComparison.Ordinal);
        Assert.Equal(0, Int32Field(bytes, 10)); // current reader only writes explicit bar direction
        Assert.Equal(1, fields[11]); // legend flag
    }

    [Theory]
    [InlineData("<c:areaChart/>", 2)]
    [InlineData("<c:bubbleChart/>", 5)]
    [InlineData("<c:doughnutChart/>", 8)]
    [InlineData("<c:lineChart/>", 13)]
    [InlineData("<c:pieChart/>", 16)]
    [InlineData("<c:radarChart/>", 17)]
    [InlineData("<c:scatterChart/>", 18)]
    [InlineData("<c:surfaceChart/>", 22)]
    [InlineData("", 0)]
    public void WriteChart_MapsChartTypes(string chartXml, int expectedType)
    {
        using var document = CreatePresentation();
        var chartPart = AddChartPart(document, EmptyChartXml(chartXml));

        var bytes = PptxChartProtoWriter.WriteChart(chartPart, WriteFakeFill);

        Assert.Equal(expectedType, Int32Field(bytes, 5));
    }

    [Fact]
    public void WriteChart_ExtractsScatterAndBubbleSeriesCoordinates()
    {
        using var document = CreatePresentation();
        var chartPart = AddChartPart(document, ScatterBubbleChartXml());

        var bytes = PptxChartProtoWriter.WriteChart(chartPart, WriteFakeFill);
        var series = MessagesForField(bytes, 3);

        Assert.Equal(2, series.Count);
        Assert.Equal(["1", "2"], StringsForField(series[0], 5));
        Assert.Equal([10D, 20D], DoublesForField(series[0], 2));
        Assert.Equal([4D, 5D], DoublesForField(series[1], 2));
    }

    private static PresentationDocument CreatePresentation()
    {
        var stream = new MemoryStream();
        var document = PresentationDocument.Create(stream, PresentationDocumentType.Presentation);
        var presentationPart = document.AddPresentationPart();
        var slidePart = presentationPart.AddNewPart<SlidePart>("rIdSlide1");
        slidePart.Slide = new P.Slide();
        return document;
    }

    private static ChartPart AddChartPart(PresentationDocument document, string xml)
    {
        var slidePart = document.PresentationPart!.SlideParts.Single();
        var chartPart = slidePart.AddNewPart<ChartPart>("rIdChart1");
        using var chartStream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        chartPart.FeedData(chartStream);
        return chartPart;
    }

    private static byte[] WriteFakeFill(A.SolidFill fill)
    {
        return Message(output => WriteString(output, 1, fill.InnerText.Length > 0 ? fill.InnerText : "fill"));
    }

    private static string BarChartXml()
    {
        return """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<c:chartSpace xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
  <c:chart>
    <c:title><c:tx><c:rich><a:bodyPr/><a:lstStyle/><a:p><a:r><a:t>Quarterly Revenue</a:t></a:r></a:p></c:rich></c:tx></c:title>
    <c:plotArea>
      <c:barChart>
        <c:barDir val="col"/>
        <c:ser>
          <c:idx val="0"/>
          <c:order val="0"/>
          <c:tx><c:v>North</c:v></c:tx>
          <c:spPr><a:solidFill><a:srgbClr val="70AD47"/></a:solidFill></c:spPr>
          <c:cat><c:strRef><c:strCache><c:ptCount val="2"/><c:pt idx="0"><c:v>Q1</c:v></c:pt><c:pt idx="1"><c:v>Q2</c:v></c:pt></c:strCache></c:strRef></c:cat>
          <c:val><c:numRef><c:numCache><c:ptCount val="2"/><c:pt idx="0"><c:v>12</c:v></c:pt><c:pt idx="1"><c:v>18.5</c:v></c:pt></c:numCache></c:numRef></c:val>
        </c:ser>
        <c:ser>
          <c:idx val="1"/>
          <c:order val="1"/>
          <c:tx><c:v>South</c:v></c:tx>
          <c:cat><c:strRef><c:strCache><c:ptCount val="2"/><c:pt idx="0"><c:v>Q2</c:v></c:pt><c:pt idx="1"><c:v>Q3</c:v></c:pt></c:strCache></c:strRef></c:cat>
          <c:val><c:numRef><c:numCache><c:ptCount val="2"/><c:pt idx="0"><c:v>9</c:v></c:pt><c:pt idx="1"><c:v>11</c:v></c:pt></c:numCache></c:numRef></c:val>
        </c:ser>
      </c:barChart>
    </c:plotArea>
    <c:legend><c:legendPos val="r"/></c:legend>
  </c:chart>
</c:chartSpace>
""";
    }

    private static string EmptyChartXml(string chartXml)
    {
        return $"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<c:chartSpace xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart">
  <c:chart><c:plotArea>{chartXml}</c:plotArea></c:chart>
</c:chartSpace>
""";
    }

    private static string ScatterBubbleChartXml()
    {
        return """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<c:chartSpace xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart">
  <c:chart>
    <c:plotArea>
      <c:scatterChart>
        <c:ser><c:tx><c:v>Scatter</c:v></c:tx><c:xVal><c:numLit><c:pt idx="0"><c:v>1</c:v></c:pt><c:pt idx="1"><c:v>2</c:v></c:pt></c:numLit></c:xVal><c:yVal><c:numLit><c:pt idx="0"><c:v>10</c:v></c:pt><c:pt idx="1"><c:v>20</c:v></c:pt></c:numLit></c:yVal></c:ser>
      </c:scatterChart>
      <c:bubbleChart>
        <c:ser><c:tx><c:v>Bubble</c:v></c:tx><c:bubbleSize><c:numLit><c:pt idx="0"><c:v>4</c:v></c:pt><c:pt idx="1"><c:v>5</c:v></c:pt></c:numLit></c:bubbleSize></c:ser>
      </c:bubbleChart>
    </c:plotArea>
  </c:chart>
</c:chartSpace>
""";
    }

    private static Dictionary<int, int> CountFields(byte[] bytes)
    {
        var counts = new Dictionary<int, int>();
        var input = new CodedInputStream(bytes);
        while (input.ReadTag() is var tag && tag != 0)
        {
            var fieldNumber = WireFormat.GetTagFieldNumber(tag);
            counts[fieldNumber] = counts.GetValueOrDefault(fieldNumber) + 1;
            input.SkipLastField();
        }

        return counts;
    }

    private static List<byte[]> MessagesForField(byte[] bytes, int targetField)
    {
        var messages = new List<byte[]>();
        var input = new CodedInputStream(bytes);
        while (input.ReadTag() is var tag && tag != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == targetField && WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited)
            {
                messages.Add(input.ReadBytes().ToByteArray());
                continue;
            }

            input.SkipLastField();
        }

        return messages;
    }

    private static List<string> StringsForField(byte[] bytes, int targetField)
    {
        var values = new List<string>();
        var input = new CodedInputStream(bytes);
        while (input.ReadTag() is var tag && tag != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == targetField && WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited)
            {
                values.Add(input.ReadString());
                continue;
            }

            input.SkipLastField();
        }

        return values;
    }

    private static string StringField(byte[] bytes, int targetField) =>
        StringsForField(bytes, targetField).FirstOrDefault() ?? "";

    private static List<double> DoublesForField(byte[] bytes, int targetField)
    {
        var values = new List<double>();
        var input = new CodedInputStream(bytes);
        while (input.ReadTag() is var tag && tag != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == targetField && WireFormat.GetTagWireType(tag) == WireFormat.WireType.Fixed64)
            {
                values.Add(input.ReadDouble());
                continue;
            }

            input.SkipLastField();
        }

        return values;
    }

    private static int Int32Field(byte[] bytes, int targetField)
    {
        var input = new CodedInputStream(bytes);
        while (input.ReadTag() is var tag && tag != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == targetField && WireFormat.GetTagWireType(tag) == WireFormat.WireType.Varint)
            {
                return input.ReadInt32();
            }

            input.SkipLastField();
        }

        return 0;
    }

    private static byte[] Message(Action<CodedOutputStream> write)
    {
        using var stream = new MemoryStream();
        using var output = new CodedOutputStream(stream);
        write(output);
        output.Flush();
        return stream.ToArray();
    }

    private static void WriteString(CodedOutputStream output, int fieldNumber, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.LengthDelimited);
        output.WriteString(value);
    }
}
