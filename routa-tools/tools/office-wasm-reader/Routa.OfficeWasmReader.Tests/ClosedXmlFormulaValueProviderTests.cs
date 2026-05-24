using ClosedXML.Excel;
using Xunit;

namespace Routa.OfficeWasmReader;

public class ClosedXmlFormulaValueProviderTests
{
    [Fact]
    public void TryCreate_WithInvalidWorkbook_ReturnsNull()
    {
        Assert.Null(ClosedXmlFormulaValueProvider.TryCreate([1, 2, 3]));
    }

    [Fact]
    public void TryGetFormulaValue_ReplaysXLookupFallbackModes()
    {
        using var provider = ClosedXmlFormulaValueProvider.TryCreate(CreateLookupWorkbook());
        Assert.NotNull(provider);

        AssertFormulaValue(provider, "Calc", "B1", "20"); // vertical exact
        AssertFormulaValue(provider, "Calc", "B2", "30"); // reverse search
        AssertFormulaValue(provider, "Calc", "B3", "200"); // horizontal range
        AssertFormulaValue(provider, "Calc", "B4", "10"); // wildcard match
        AssertFormulaValue(provider, "Calc", "B5", "quoted \"fallback\""); // fallback literal
        AssertFormulaValue(provider, "Calc", "B6", "#N/A"); // no match and no fallback
        AssertFormulaValue(provider, "Calc", "B7", "50"); // quoted sheet reference
        AssertFormulaValue(provider, "Calc", "B8", "TRUE"); // boolean lookup literal
        AssertFormulaValue(provider, "Calc", "B9", "99"); // numeric lookup literal
        AssertFormulaValue(provider, "Calc", "B13", "20"); // cell-reference lookup value
        AssertFormulaValue(provider, "Calc", "B14", "#N/A"); // horizontal miss with no fallback
        AssertFormulaValue(provider, "Calc", "B15", "missing"); // wildcard miss uses fallback
        AssertFormulaValue(provider, "Calc", "B16", ""); // blank return cell
        AssertFormulaValue(provider, "Calc", "B17", "75"); // quoted sheet name containing !
    }

    [Fact]
    public void TryGetFormulaValue_HandlesInvalidInputsAndUnsupportedXLookupModes()
    {
        using var provider = ClosedXmlFormulaValueProvider.TryCreate(CreateLookupWorkbook());
        Assert.NotNull(provider);

        Assert.False(provider.TryGetFormulaValue("", "B1", out _));
        Assert.False(provider.TryGetFormulaValue("Calc", "", out _));
        Assert.False(provider.TryGetFormulaValue("Missing", "B1", out _));
        Assert.False(provider.TryGetFormulaValue("Calc", "A1", out _));
        Assert.False(provider.TryGetFormulaValue("Calc", "not an address", out _));
        AssertFormulaValue(provider, "Calc", "B10", "#NAME?"); // unsupported match mode
        AssertFormulaValue(provider, "Calc", "B11", "#NAME?"); // unsupported search mode
        AssertFormulaValue(provider, "Calc", "B12", "#NAME?"); // missing sheet reference
        AssertFormulaValue(provider, "Calc", "B18", "#NAME?"); // non-XLOOKUP unknown function
        AssertFormulaValue(provider, "Calc", "B19", "#NAME?"); // too few XLOOKUP arguments
        AssertFormulaValue(provider, "Calc", "B20", "#NAME?"); // nested unsupported range expression
    }

    [Fact]
    public void FormulaValueProtocolText_MapsBlankTemporalAndErrorValues()
    {
        Assert.Equal("", FormulaValueToProtocolText(Blank.Value));
        Assert.Equal("46023", FormulaValueToProtocolText(new DateTime(2026, 1, 1)));
        Assert.Equal("0.5", FormulaValueToProtocolText(TimeSpan.FromHours(12)));
        Assert.Equal("#NULL!", FormulaErrorToProtocolText(XLError.NullValue));
        Assert.Equal("#VALUE!", FormulaErrorToProtocolText(XLError.IncompatibleValue));
        Assert.Equal("#REF!", FormulaErrorToProtocolText(XLError.CellReference));
        Assert.Equal("#NUM!", FormulaErrorToProtocolText(XLError.NumberInvalid));
        Assert.Equal("#N/A", FormulaErrorToProtocolText(XLError.NoValueAvailable));
        Assert.Equal("999", FormulaErrorToProtocolText((XLError)999));
        Assert.False(WildcardMatches("a*z", "abc"));
        Assert.False(WildcardMatches("abcd", "a"));
        Assert.Equal(10, LastSheetReferenceSeparator("'Odd!Name'!A1"));
        Assert.Equal(-1, LastSheetReferenceSeparator("'Odd!Name'"));
        Assert.False(TryFormulaLiteralOrCellValue("", out _));
        Assert.False(TryFormulaLiteralOrCellValue("Missing!A1", out _));
        Assert.False(TryResolveRange("", out _));
        Assert.False(TryResolveRange("A:A:A", out _));
    }

    private static void AssertFormulaValue(ClosedXmlFormulaValueProvider provider, string sheetName, string address, string expected)
    {
        Assert.True(provider.TryGetFormulaValue(sheetName, address, out var value));
        Assert.Equal(expected, value);
    }

    private static string FormulaValueToProtocolText(XLCellValue value)
    {
        var method = typeof(ClosedXmlFormulaValueProvider).GetMethod(
            "FormulaValueToProtocolText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(null, [value]));
    }

    private static string FormulaErrorToProtocolText(XLError error)
    {
        var method = typeof(ClosedXmlFormulaValueProvider).GetMethod(
            "FormulaErrorToProtocolText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(null, [error]));
    }

    private static bool WildcardMatches(string pattern, string value)
    {
        var method = typeof(ClosedXmlFormulaValueProvider).GetMethod(
            "WildcardMatches",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            [typeof(string), typeof(string)]);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method.Invoke(null, [pattern, value]));
    }

    private static int LastSheetReferenceSeparator(string reference)
    {
        var method = typeof(ClosedXmlFormulaValueProvider).GetMethod(
            "LastSheetReferenceSeparator",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<int>(method.Invoke(null, [reference]));
    }

    private static bool TryResolveRange(string reference, out IXLRange? range)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        using var provider = ClosedXmlFormulaValueProvider.TryCreate(CreateWorkbookBytes(workbook));
        Assert.NotNull(provider);
        var method = typeof(ClosedXmlFormulaValueProvider).GetMethod(
            "TryResolveRange",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        object?[] args = [sheet, reference, null];
        var result = Assert.IsType<bool>(method.Invoke(provider, args));
        range = args[2] as IXLRange;
        return result;
    }

    private static bool TryFormulaLiteralOrCellValue(string expression, out XLCellValue value)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        using var provider = ClosedXmlFormulaValueProvider.TryCreate(CreateWorkbookBytes(workbook));
        Assert.NotNull(provider);
        var method = typeof(ClosedXmlFormulaValueProvider).GetMethod(
            "TryFormulaLiteralOrCellValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        object?[] args = [sheet, expression, default(XLCellValue)];
        var result = Assert.IsType<bool>(method.Invoke(provider, args));
        value = Assert.IsType<XLCellValue>(args[2]);
        return result;
    }

    private static byte[] CreateWorkbookBytes(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] CreateLookupWorkbook()
    {
        using var workbook = new XLWorkbook();
        var calc = workbook.Worksheets.Add("Calc");
        calc.Cell("A1").Value = "plain";
        calc.Cell("B1").FormulaA1 = "XLOOKUP(\"b\",Lookup!A1:A3,Lookup!B1:B3,\"missing\")";
        calc.Cell("B2").FormulaA1 = "XLOOKUP(\"b\",Lookup!A1:A3,Lookup!B1:B3,\"missing\",0,-1)";
        calc.Cell("B3").FormulaA1 = "XLOOKUP(\"h2\",Lookup!A5:C5,Lookup!A6:C6,\"missing\")";
        calc.Cell("B4").FormulaA1 = "XLOOKUP(\"a*\",Lookup!A1:A3,Lookup!B1:B3,\"missing\",2)";
        calc.Cell("B5").FormulaA1 = "XLOOKUP(\"a,b\",Lookup!A1:A3,Lookup!B1:B3,\"quoted \"\"fallback\"\"\",2)";
        calc.Cell("B6").FormulaA1 = "XLOOKUP(\"missing\",Lookup!A1:A3,Lookup!B1:B3)";
        calc.Cell("B7").FormulaA1 = "XLOOKUP(\"remote\",'Other Data'!A1:A1,'Other Data'!B1:B1,\"missing\")";
        calc.Cell("B8").FormulaA1 = "XLOOKUP(TRUE,Lookup!A8:A9,Lookup!B8:B9,\"missing\")";
        calc.Cell("B9").FormulaA1 = "XLOOKUP(42,Lookup!A11:A12,Lookup!B11:B12,\"missing\")";
        calc.Cell("B10").FormulaA1 = "XLOOKUP(\"a\",Lookup!A1:A3,Lookup!B1:B3,\"missing\",1)";
        calc.Cell("B11").FormulaA1 = "XLOOKUP(\"a\",Lookup!A1:A3,Lookup!B1:B3,\"missing\",0,2)";
        calc.Cell("B12").FormulaA1 = "XLOOKUP(\"a\",Missing!A1:A3,Lookup!B1:B3,\"missing\")";
        calc.Cell("A13").Value = "b";
        calc.Cell("B13").FormulaA1 = "XLOOKUP(A13,Lookup!A1:A3,Lookup!B1:B3,\"missing\")";
        calc.Cell("B14").FormulaA1 = "XLOOKUP(\"missing\",Lookup!A5:C5,Lookup!A6:C6)";
        calc.Cell("B15").FormulaA1 = "XLOOKUP(\"z*\",Lookup!A1:A3,Lookup!B1:B3,\"missing\",2)";
        calc.Cell("B16").FormulaA1 = "XLOOKUP(\"blank\",Lookup!A13:A13,Lookup!B13:B13,\"missing\")";
        calc.Cell("B17").FormulaA1 = "XLOOKUP(\"bang\",'Odd!Name'!A1:A1,'Odd!Name'!B1:B1,\"missing\")";
        calc.Cell("B18").FormulaA1 = "UNKNOWN_FN(1)";
        calc.Cell("B19").FormulaA1 = "XLOOKUP(\"a\")";
        calc.Cell("B20").FormulaA1 = "XLOOKUP(\"a\",OFFSET(Lookup!A1,0,0,3,1),Lookup!B1:B3,\"missing\")";

        var lookup = workbook.Worksheets.Add("Lookup");
        lookup.Cell("A1").Value = "alpha";
        lookup.Cell("B1").Value = 10;
        lookup.Cell("A2").Value = "b";
        lookup.Cell("B2").Value = 20;
        lookup.Cell("A3").Value = "b";
        lookup.Cell("B3").Value = 30;
        lookup.Cell("A5").Value = "h1";
        lookup.Cell("B5").Value = "h2";
        lookup.Cell("C5").Value = "h3";
        lookup.Cell("A6").Value = 100;
        lookup.Cell("B6").Value = 200;
        lookup.Cell("C6").Value = 300;
        lookup.Cell("A8").Value = true;
        lookup.Cell("B8").Value = true;
        lookup.Cell("A9").Value = false;
        lookup.Cell("B9").Value = false;
        lookup.Cell("A11").Value = 41;
        lookup.Cell("B11").Value = 98;
        lookup.Cell("A12").Value = 42;
        lookup.Cell("B12").Value = 99;
        lookup.Cell("A13").Value = "blank";

        var other = workbook.Worksheets.Add("Other Data");
        other.Cell("A1").Value = "remote";
        other.Cell("B1").Value = 50;

        var odd = workbook.Worksheets.Add("Odd!Name");
        odd.Cell("A1").Value = "bang";
        odd.Cell("B1").Value = 75;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
