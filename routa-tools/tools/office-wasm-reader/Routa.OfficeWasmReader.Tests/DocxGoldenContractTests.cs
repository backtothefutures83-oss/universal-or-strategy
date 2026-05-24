using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Routa.OfficeWasmReader.Tests;

/// <summary>
/// Characterization tests that lock the binary proto output of DocxDocumentProtoReader
/// against pre-recorded golden snapshots. If the SHA-256 changes after a refactoring,
/// the behavioral contract has been broken.
/// </summary>
public class DocxGoldenContractTests
{
    private static readonly string FixtureDir =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "fixtures"));

    private static string FixturePath(string name) =>
        Path.Combine(FixtureDir, name);

    private static string GoldenPath(string name) =>
        Path.Combine(FixtureDir, "golden", name);

    // ── helpers ─────────────────────────────────────────────────────────────

    private static byte[] ReadFixture(string docxName) =>
        File.ReadAllBytes(FixturePath(docxName));

    private static GoldenSnapshot ReadGolden(string jsonName)
    {
        var json = File.ReadAllText(GoldenPath(jsonName));
        return JsonSerializer.Deserialize<GoldenSnapshot>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void AssertMatchesGolden(string docxFile, string goldenFile)
    {
        var fixture = ReadFixture(docxFile);
        var golden = ReadGolden(goldenFile);

        var protoBytes = DocxDocumentProtoReader.Read(fixture);

        Assert.Equal(golden.WasmProtoByteLength, protoBytes.Length);
        Assert.Equal(golden.WasmProtoSha256, Sha256Hex(protoBytes));
    }

    // ── contract tests ───────────────────────────────────────────────────────

    [Fact]
    public void Read_AdvancedContract_MatchesGolden() =>
        AssertMatchesGolden(
            "docx_advanced_contract.docx",
            "docx_advanced_contract.json");

    [Fact]
    public void Read_AnchorLayoutContract_MatchesGolden() =>
        AssertMatchesGolden(
            "docx_anchor_layout_contract.docx",
            "docx_anchor_layout_contract.json");

    [Fact]
    public void Read_StyleSectionContract_MatchesGolden() =>
        AssertMatchesGolden(
            "docx_style_section_contract.docx",
            "docx_style_section_contract.json");

    [Fact]
    public void Read_TableStyleContract_MatchesGolden() =>
        AssertMatchesGolden(
            "docx_table_style_contract.docx",
            "docx_table_style_contract.json");

    [Fact]
    public void Read_DllViewerSolutionTestDocument_MatchesGolden() =>
        AssertMatchesGolden(
            "dll_viewer_solution_test_document.docx",
            "dll_viewer_solution_test_document.json");

    // ── snapshot record ──────────────────────────────────────────────────────

    private sealed record GoldenSnapshot(
        int WasmProtoByteLength,
        string WasmProtoSha256);
}
