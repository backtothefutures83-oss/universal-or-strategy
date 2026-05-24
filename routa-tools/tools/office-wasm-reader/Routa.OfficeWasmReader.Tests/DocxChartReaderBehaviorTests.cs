using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Google.Protobuf;
using System.Text;
using Xunit;

namespace Routa.OfficeWasmReader.Tests;

public class DocxChartReaderBehaviorTests
{
    [Fact]
    public void Read_DocumentWithInlineChart_EmitsChartRecordAndReference()
    {
        var docx = ChartDocx();
        var protoBytes = DocxDocumentProtoReader.Read(docx);
        var charts = MessagesForField(protoBytes, 1);
        var elements = MessagesForField(protoBytes, 5);

        var chart = Assert.Single(charts);
        Assert.Equal("Trend", StringField(chart, 1));
        Assert.Equal(13, Int32Field(chart, 5)); // line chart
        Assert.Contains("Feb", StringsForField(chart, 2));
        Assert.Single(MessagesForField(chart, 3));
        Assert.Contains(elements, element => CountFields(element).ContainsKey(18));
    }

    private static byte[] ChartDocx()
    {
        using var ms = new MemoryStream();
        using (var document = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());

            var chartPart = mainPart.AddNewPart<ChartPart>("rIdChart1");
            using (var chartStream = new MemoryStream(Encoding.UTF8.GetBytes(ChartXml())))
            {
                chartPart.FeedData(chartStream);
            }

            mainPart.Document.Body!.AppendChild(new Paragraph(
                new Run(new Text("Before chart")),
                new Run(new Drawing(ChartDrawingXml())),
                new Run(new Text("After chart"))));
            mainPart.Document.Save();
            document.Save();
        }

        return ms.ToArray();
    }

    private static string ChartDrawingXml()
    {
        return """
<w:drawing xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <wp:inline distB="0" distT="0" distL="0" distR="0">
    <wp:extent cx="4572000" cy="2743200"/>
    <wp:effectExtent b="0" l="0" r="0" t="0"/>
    <wp:docPr id="8" name="Chart 1"/>
    <a:graphic>
      <a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/chart">
        <c:chart r:id="rIdChart1"/>
      </a:graphicData>
    </a:graphic>
  </wp:inline>
</w:drawing>
""";
    }

    private static string ChartXml()
    {
        return """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<c:chartSpace xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
  <c:chart>
    <c:title><c:tx><c:rich><a:bodyPr/><a:lstStyle/><a:p><a:r><a:t>Trend</a:t></a:r></a:p></c:rich></c:tx></c:title>
    <c:plotArea>
      <c:lineChart>
        <c:ser>
          <c:idx val="0"/>
          <c:order val="0"/>
          <c:tx><c:v>North</c:v></c:tx>
          <c:spPr><a:solidFill><a:srgbClr val="5B9BD5"/></a:solidFill></c:spPr>
          <c:cat><c:strRef><c:strCache><c:ptCount val="2"/><c:pt idx="0"><c:v>Jan</c:v></c:pt><c:pt idx="1"><c:v>Feb</c:v></c:pt></c:strCache></c:strRef></c:cat>
          <c:val><c:numRef><c:numCache><c:formatCode>General</c:formatCode><c:ptCount val="2"/><c:pt idx="0"><c:v>7</c:v></c:pt><c:pt idx="1"><c:v>9</c:v></c:pt></c:numCache></c:numRef></c:val>
        </c:ser>
      </c:lineChart>
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
}
