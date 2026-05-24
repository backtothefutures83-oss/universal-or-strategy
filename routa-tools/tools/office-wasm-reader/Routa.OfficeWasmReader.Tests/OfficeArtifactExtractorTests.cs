using Google.Protobuf;
using Xunit;

namespace Routa.OfficeWasmReader.Tests;

public class OfficeArtifactExtractorTests
{
    private static readonly string FixtureDir =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "fixtures"));

    [Fact]
    public void ExtractDocxProto_WithValidDocument_WritesArtifactEnvelope()
    {
        var protoBytes = OfficeArtifactExtractor.ExtractDocxProto(ReadFixture("dll_viewer_solution_test_document.docx"), ignoreErrors: false);
        var fields = CountFields(protoBytes);

        Assert.Equal("docx", StringField(protoBytes, 1));
        Assert.True(fields.ContainsKey(3), "DOCX extractor should emit text blocks.");
        Assert.True(fields.ContainsKey(8), "DOCX extractor should emit images.");
    }

    [Fact]
    public void ExtractPptxProto_WhenIgnoringErrors_WritesDiagnosticEnvelope()
    {
        var protoBytes = OfficeArtifactExtractor.ExtractPptxProto([1, 2, 3], ignoreErrors: true);

        Assert.Equal("pptx", StringField(protoBytes, 1));
        Assert.Equal("PPTX parse failed", StringField(protoBytes, 2));
        Assert.NotEmpty(MessagesForField(protoBytes, 6));
        Assert.NotEmpty(MessagesForField(protoBytes, 7));
    }

    [Fact]
    public void ExtractXlsxProto_WhenIgnoringErrors_ReturnsEmptyPayload()
    {
        var protoBytes = OfficeArtifactExtractor.ExtractXlsxProto([1, 2, 3], ignoreErrors: true);

        Assert.Empty(protoBytes);
    }

    [Fact]
    public void ExtractPptxProto_WhenNotIgnoringErrors_PropagatesParseFailure()
    {
        Assert.ThrowsAny<Exception>(() => OfficeArtifactExtractor.ExtractPptxProto([1, 2, 3], ignoreErrors: false));
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
}
