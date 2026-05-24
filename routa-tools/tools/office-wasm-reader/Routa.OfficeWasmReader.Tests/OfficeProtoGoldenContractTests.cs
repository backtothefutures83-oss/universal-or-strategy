using Google.Protobuf;
using Xunit;

namespace Routa.OfficeWasmReader.Tests;

/// <summary>
/// Structural characterization tests for the Walnut-like protobuf readers used
/// by the browser WASM export surface. These intentionally assert stable
/// protocol shape instead of byte-for-byte payloads, because generated IDs and
/// binary packing can differ between local .NET test execution and the
/// published browser-WASM bundle.
/// </summary>
public class OfficeProtoGoldenContractTests
{
    private static readonly string FixtureDir =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "fixtures"));

    private static string FixturePath(string name) =>
        Path.Combine(FixtureDir, name);

    [Fact]
    public void Read_ComplexExcelRendererWorkbook_EmitsWorkbookShape()
    {
        var protoBytes = XlsxWorkbookProtoReader.Read(ReadFixture("complex_excel_renderer_test.xlsx"));
        var workbookFields = CountFields(protoBytes);
        var sheets = MessagesForField(protoBytes, 1);

        Assert.Equal(9, sheets.Count);
        Assert.Equal(1, workbookFields[2]); // styles
        Assert.Equal(1, workbookFields[3]); // theme

        var firstSheet = sheets[0];
        var firstSheetFields = CountFields(firstSheet);
        Assert.Equal("00_README", StringField(firstSheet, 2));
        Assert.Equal(80, firstSheetFields[3]); // rows
        Assert.Equal(9, firstSheetFields[6]); // columns
        Assert.Equal(2, firstSheetFields[8]); // drawings
        Assert.Equal(5, firstSheetFields[12]); // merged ranges
    }

    [Fact]
    public void Read_AgenticTechnicalBlueprintPresentation_EmitsPresentationShape()
    {
        var protoBytes = PptxPresentationProtoReader.Read(ReadFixture("agentic_ui_proactive_agent_technical_blueprint.pptx"));
        var presentationFields = CountFields(protoBytes);
        var slides = MessagesForField(protoBytes, 1);

        Assert.Equal(20, slides.Count);
        Assert.Equal(1, presentationFields[2]); // theme
        Assert.Equal(2, presentationFields[3]); // master/layout records

        var firstSlide = slides[0];
        var firstSlideFields = CountFields(firstSlide);
        Assert.Equal(1, Int32Field(firstSlide, 1));
        Assert.Equal("/ppt/slides/slide1.xml", StringField(firstSlide, 11));
        Assert.Equal(23, firstSlideFields[3]); // elements
        Assert.Equal(1, firstSlideFields[10]); // background
    }

    [Theory]
    [InlineData("xlsx_comments_contract.xlsx")]
    [InlineData("xlsx_defined_names_contract.xlsx")]
    [InlineData("xlsx_external_link_contract.xlsx")]
    [InlineData("xlsx_image_drawing_contract.xlsx")]
    [InlineData("xlsx_multi_chart_contract.xlsx")]
    [InlineData("xlsx_pivot_contract.xlsx")]
    [InlineData("xlsx_slicer_contract.xlsx")]
    [InlineData("xlsx_sparkline_contract.xlsx")]
    [InlineData("xlsx_surface_chart_contract.xlsx")]
    [InlineData("xlsx_threaded_comments_contract.xlsx")]
    [InlineData("xlsx_timeline_contract.xlsx")]
    public void Read_XlsxContractFixture_EmitsWorkbookShell(string fixtureName)
    {
        var protoBytes = XlsxWorkbookProtoReader.Read(ReadFixture(fixtureName));
        var sheets = MessagesForField(protoBytes, 1);

        Assert.NotEmpty(protoBytes);
        Assert.NotEmpty(sheets);
        Assert.All(sheets, sheet => Assert.False(string.IsNullOrWhiteSpace(StringField(sheet, 2))));
    }

    [Theory]
    [InlineData("pptx_group_connector_contract.pptx")]
    [InlineData("pptx_table_contract.pptx")]
    public void Read_PptxContractFixture_EmitsSlideShell(string fixtureName)
    {
        var protoBytes = PptxPresentationProtoReader.Read(ReadFixture(fixtureName));
        var slides = MessagesForField(protoBytes, 1);

        Assert.NotEmpty(protoBytes);
        Assert.NotEmpty(slides);
        Assert.All(slides, slide => Assert.True(CountFields(slide).ContainsKey(3), "Slide should emit elements."));
    }

    [Theory]
    [InlineData("docx-renderer-gap-checklist.docx")]
    [InlineData("docx_advanced_contract.docx")]
    [InlineData("docx_anchor_layout_contract.docx")]
    [InlineData("docx_style_section_contract.docx")]
    [InlineData("docx_table_style_contract.docx")]
    public void Read_DocxContractFixture_EmitsDocumentShell(string fixtureName)
    {
        var protoBytes = DocxDocumentProtoReader.Read(ReadFixture(fixtureName));
        var documentFields = CountFields(protoBytes);

        Assert.NotEmpty(protoBytes);
        Assert.True(documentFields.ContainsKey(5), "Document should emit elements.");
        Assert.True(documentFields.ContainsKey(13), "Document should emit section summaries.");
    }

    private static byte[] ReadFixture(string name) =>
        File.ReadAllBytes(FixturePath(name));

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

    private static string StringField(byte[] bytes, int targetField)
    {
        var input = new CodedInputStream(bytes);
        while (input.ReadTag() is var tag && tag != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == targetField && WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited)
            {
                return input.ReadString();
            }

            input.SkipLastField();
        }

        return "";
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
}
