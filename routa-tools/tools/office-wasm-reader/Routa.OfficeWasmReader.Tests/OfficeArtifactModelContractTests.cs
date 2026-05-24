using Google.Protobuf;
using Xunit;

namespace Routa.OfficeWasmReader.Tests;

public class OfficeArtifactModelContractTests
{
    private static readonly string FixtureDir =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "fixtures"));

    [Fact]
    public void DocxArtifactReader_EmitsTextTablesImagesAndMetadata()
    {
        var artifact = DocxArtifactReader.Read(ReadFixture("dll_viewer_solution_test_document.docx"));
        var protoBytes = OfficeArtifactProtoWriter.Write(artifact);
        var fields = CountFields(protoBytes);

        Assert.Equal("docx", artifact.SourceKind);
        Assert.NotEmpty(artifact.TextBlocks);
        Assert.NotEmpty(artifact.Tables);
        Assert.NotEmpty(artifact.Images);
        Assert.Equal("routa-office-wasm-reader", artifact.Metadata["reader"]);
        Assert.Equal(artifact.TextBlocks.Count, fields[3]);
        Assert.Equal(artifact.Images.Count, fields[8]);
        Assert.Equal(artifact.Tables.Count, fields[9]);
    }

    [Fact]
    public void PptxArtifactReader_EmitsSlidesTablesAndMetadata()
    {
        var artifact = PptxArtifactReader.Read(ReadFixture("pptx_table_contract.pptx"));
        var protoBytes = OfficeArtifactProtoWriter.Write(artifact);
        var fields = CountFields(protoBytes);

        Assert.Equal("pptx", artifact.SourceKind);
        Assert.NotEmpty(artifact.Slides);
        Assert.NotEmpty(artifact.Tables);
        Assert.Equal("routa-office-wasm-reader", artifact.Metadata["reader"]);
        Assert.Equal(artifact.Slides.Count, fields[5]);
        Assert.Equal(artifact.Tables.Count, fields[9]);
    }

    [Fact]
    public void XlsxArtifactReader_EmitsSheetsChartsAndStyles()
    {
        var artifact = XlsxArtifactReader.Read(ReadFixture("complex_excel_renderer_test.xlsx"));
        var protoBytes = OfficeArtifactProtoWriter.Write(artifact);
        var fields = CountFields(protoBytes);

        Assert.Equal("xlsx", artifact.SourceKind);
        Assert.Equal(9, artifact.Sheets.Count);
        Assert.NotEmpty(artifact.Charts);
        Assert.NotEmpty(artifact.Styles.Fonts);
        Assert.Equal("routa-office-wasm-reader", artifact.Metadata["reader"]);
        Assert.Equal(artifact.Sheets.Count, fields[4]);
        Assert.Equal(artifact.Charts.Count, fields[10]);
        Assert.Equal(1, fields[11]);
    }

    [Fact]
    public void OfficeArtifactProtoWriter_WritesSheetValidationAndEmptyImageFields()
    {
        var artifact = new OfficeArtifactModel { SourceKind = "xlsx", Title = "contracts" };
        var sheet = new SheetModel { Name = "Rules", DefaultColWidth = 9.5, DefaultRowHeight = 15 };
        sheet.DataValidations.Add(new DataValidationModel("whole", "between", "1", "10", ["A1:A3"]));
        sheet.ConditionalFormats.Add(new ConditionalFormatModel(
            "cellIs",
            1,
            ["A1:A3"],
            Operator: "greaterThan",
            Formulas: ["5"],
            Text: "warn",
            FillColor: "FFCC00",
            FontColor: "FF0000",
            Bold: true,
            ColorScale: new ColorScaleModel(["FFFFFF", "FF0000"]),
            DataBar: new DataBarModel("63C384"),
            IconSet: new IconSetModel("3TrafficLights1", ShowValue: true, Reverse: false)));
        sheet.Columns.Add(new ColumnModel(1, 3, 12.25, Hidden: true));
        artifact.Sheets.Add(sheet);
        artifact.Images.Add(new ImageAssetModel("empty", "/xl/media/empty.png", "image/png", []));

        var protoBytes = OfficeArtifactProtoWriter.Write(artifact);
        var fields = CountFields(protoBytes);
        var sheetBytes = MessagesForField(protoBytes, 4).Single();
        var sheetFields = CountFields(sheetBytes);

        Assert.Equal(1, fields[4]);
        Assert.Equal(1, fields[8]);
        Assert.Equal(1, sheetFields[5]);
        Assert.Equal(1, sheetFields[6]);
        Assert.Equal(1, sheetFields[7]);
    }

    private static byte[] ReadFixture(string name) =>
        File.ReadAllBytes(Path.Combine(FixtureDir, name));

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
}
