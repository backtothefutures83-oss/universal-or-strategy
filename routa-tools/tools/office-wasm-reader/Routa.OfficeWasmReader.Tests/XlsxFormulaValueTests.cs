using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using Google.Protobuf;
using Xunit;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace Routa.OfficeWasmReader;

public sealed class XlsxFormulaValueTests
{
    [Fact]
    public void XlsxReaderCalculatesFormulaCellsWithoutCachedValues()
    {
        var workbookBytes = CreateFormulaWorkbookWithoutCachedValues();

        var protoBytes = XlsxWorkbookProtoReader.Read(workbookBytes);
        var values = DecodeCellValues(protoBytes);

        Assert.Equal("15", values["Calc!B1"]);
        Assert.Equal("ok", values["Calc!B2"]);
        Assert.Equal("40", values["Calc!B3"]);
        Assert.Equal("TRUE", values["Calc!B4"]);
        Assert.Equal("#DIV/0!", values["Calc!B5"]);
        Assert.Equal("46143", values["Calc!B6"]);
        Assert.Equal("open", values["Calc!B7"]);
        Assert.Equal("fallback", values["Calc!B8"]);
        Assert.Equal("40", values["Calc!B9"]);
        Assert.Equal("2", values["Calc!B10"]);
        Assert.Equal("4", values["Calc!B11"]);
        Assert.Equal("6", values["Calc!B12"]);
    }

    [Fact]
    public void XlsxReaderPreservesCachedFormulaValuesWhenBackfillIsActive()
    {
        var workbookBytes = CreateMixedCachedFormulaWorkbook();

        var protoBytes = XlsxWorkbookProtoReader.Read(workbookBytes);
        var values = DecodeCellValues(protoBytes);

        Assert.Equal("999", values["Calc!B1"]);
        Assert.Equal("12", values["Calc!C1"]);
    }

    private static byte[] CreateFormulaWorkbookWithoutCachedValues()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Calc");
        sheet.Cell("A1").Value = 10;
        sheet.Cell("A2").Value = 5;
        sheet.Cell("B1").FormulaA1 = "SUM(A1:A2)";
        sheet.Cell("B2").FormulaA1 = "IF(B1>10,\"ok\",\"bad\")";
        sheet.Cell("B3").FormulaA1 = "VLOOKUP(\"b\",Lookup!A1:B2,2,FALSE)";
        sheet.Cell("B4").FormulaA1 = "B1=15";
        sheet.Cell("B5").FormulaA1 = "1/0";
        sheet.Cell("B6").FormulaA1 = "DATE(2026,5,1)";
        sheet.Cell("B7").FormulaA1 = "HYPERLINK(\"https://example.com\",\"open\")";
        sheet.Cell("B8").FormulaA1 = "IFERROR(B5,\"fallback\")";
        sheet.Cell("B9").FormulaA1 = "XLOOKUP(\"b\",Lookup!A1:A2,Lookup!B1:B2,\"missing\")";
        sheet.Cell("A10").Value = 1;
        sheet.Cell("A11").Value = 2;
        sheet.Cell("A12").Value = 3;
        sheet.Cell("B10").FormulaA1 = "A10*2";
        sheet.Cell("B11").FormulaA1 = "A11*2";
        sheet.Cell("B12").FormulaA1 = "A12*2";

        var lookup = workbook.Worksheets.Add("Lookup");
        lookup.Cell("A1").Value = "a";
        lookup.Cell("B1").Value = 20;
        lookup.Cell("A2").Value = "b";
        lookup.Cell("B2").Value = 40;

        using var saved = new MemoryStream();
        workbook.SaveAs(saved);
        saved.Position = 0;

        using var stripped = new MemoryStream();
        stripped.Write(saved.ToArray());
        stripped.Position = 0;
        using (var document = SpreadsheetDocument.Open(stripped, true))
        {
            var worksheetParts = document.WorkbookPart?.WorksheetParts ?? [];
            foreach (var cell in worksheetParts
                         .SelectMany(part => part.Worksheet?.Descendants<S.Cell>() ?? []))
            {
                if (cell.CellFormula is null)
                {
                    continue;
                }

                cell.CellValue?.Remove();
                cell.DataType = null;
            }

            var calcSheet = document.WorkbookPart?.WorksheetParts
                .FirstOrDefault(part => part.Worksheet.Descendants<S.SheetData>()
                    .SelectMany(sheetData => sheetData.Elements<S.Row>())
                    .SelectMany(row => row.Elements<S.Cell>())
                    .Any(cell => cell.CellReference?.Value == "B10"));
            if (calcSheet is not null)
            {
                MakeSharedFormula(calcSheet, "B10", "B10:B12", 0, "A10*2");
                MakeSharedFormula(calcSheet, "B11", "B10:B12", 0, "");
                MakeSharedFormula(calcSheet, "B12", "B10:B12", 0, "");
            }
        }

        return stripped.ToArray();
    }

    private static byte[] CreateMixedCachedFormulaWorkbook()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Calc");
        sheet.Cell("A1").Value = 4;
        sheet.Cell("B1").FormulaA1 = "A1*2";
        sheet.Cell("C1").FormulaA1 = "A1*3";

        using var saved = new MemoryStream();
        workbook.SaveAs(saved);
        saved.Position = 0;

        using var stripped = new MemoryStream();
        stripped.Write(saved.ToArray());
        stripped.Position = 0;
        using (var document = SpreadsheetDocument.Open(stripped, true))
        {
            var cells = document.WorkbookPart?.WorksheetParts
                .SelectMany(part => part.Worksheet?.Descendants<S.Cell>() ?? [])
                .ToDictionary(cell => cell.CellReference?.Value ?? "", StringComparer.OrdinalIgnoreCase);
            Assert.NotNull(cells);

            cells["B1"].CellValue = new S.CellValue("999");
            cells["B1"].DataType = null;
            cells["C1"].CellValue?.Remove();
            cells["C1"].DataType = null;
        }

        return stripped.ToArray();
    }

    private static void MakeSharedFormula(WorksheetPart worksheetPart, string address, string reference, uint sharedIndex, string formulaText)
    {
        var cell = worksheetPart.Worksheet.Descendants<S.Cell>().First(cell => cell.CellReference?.Value == address);
        cell.CellFormula = new S.CellFormula(formulaText)
        {
            FormulaType = S.CellFormulaValues.Shared,
            SharedIndex = sharedIndex,
            Reference = formulaText.Length > 0 ? reference : null,
        };
    }

    private static Dictionary<string, string> DecodeCellValues(byte[] protoBytes)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var input = new CodedInputStream(protoBytes);
        while (input.ReadTag() is var tag && tag != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == 1)
            {
                DecodeSheet(input.ReadBytes().ToByteArray(), values);
            }
            else
            {
                input.SkipLastField();
            }
        }

        return values;
    }

    private static void DecodeSheet(byte[] sheetBytes, Dictionary<string, string> values)
    {
        var input = new CodedInputStream(sheetBytes);
        var sheetName = "";
        while (input.ReadTag() is var tag && tag != 0)
        {
            var fieldNumber = WireFormat.GetTagFieldNumber(tag);
            if (fieldNumber == 2)
            {
                sheetName = input.ReadString();
            }
            else if (fieldNumber == 3)
            {
                DecodeRow(input.ReadBytes().ToByteArray(), sheetName, values);
            }
            else
            {
                input.SkipLastField();
            }
        }
    }

    private static void DecodeRow(byte[] rowBytes, string sheetName, Dictionary<string, string> values)
    {
        var input = new CodedInputStream(rowBytes);
        while (input.ReadTag() is var tag && tag != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == 2)
            {
                var cell = DecodeCell(input.ReadBytes().ToByteArray());
                if (cell.Address.Length > 0)
                {
                    values[$"{sheetName}!{cell.Address}"] = cell.Value;
                }
            }
            else
            {
                input.SkipLastField();
            }
        }
    }

    private static (string Address, string Value) DecodeCell(byte[] cellBytes)
    {
        var input = new CodedInputStream(cellBytes);
        var address = "";
        var value = "";
        while (input.ReadTag() is var tag && tag != 0)
        {
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1:
                    address = input.ReadString();
                    break;
                case 2:
                    value = input.ReadString();
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return (address, value);
    }
}
