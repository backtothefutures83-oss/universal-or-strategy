using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml;
using System.Xml.Linq;
using A = DocumentFormat.OpenXml.Drawing;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace Routa.OfficeWasmReader;

internal static class XlsxArtifactReader
{
    public static OfficeArtifactModel Read(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var document = SpreadsheetDocument.Open(stream, false);

        var artifact = new OfficeArtifactModel
        {
            SourceKind = "xlsx",
            Title = TextNormalization.Clean(document.PackageProperties.Title),
        };
        artifact.Metadata["reader"] = "routa-office-wasm-reader";

        var workbookPart = document.WorkbookPart;
        if (workbookPart?.Workbook.Sheets is null)
        {
            artifact.Diagnostics.Add(new DiagnosticModel("warning", "XLSX has no workbook sheets."));
            return artifact;
        }

        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
        ExtractStyles(workbookPart.WorkbookStylesPart?.Stylesheet, artifact.Styles);
        foreach (var sheetElement in workbookPart.Workbook.Sheets.Elements<S.Sheet>())
        {
            if (artifact.Sheets.Count >= OpenXmlReaderLimits.MaxSheets)
            {
                artifact.Diagnostics.Add(new DiagnosticModel("warning", "XLSX sheet limit reached."));
                break;
            }

            var relationshipId = sheetElement.Id?.Value;
            if (string.IsNullOrEmpty(relationshipId))
            {
                continue;
            }

            if (workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
            {
                continue;
            }

            var sheet = new SheetModel
            {
                Name = TextNormalization.Clean(sheetElement.Name?.Value) is { Length: > 0 } name ? name : $"Sheet {artifact.Sheets.Count + 1}",
            };
            artifact.Title = FirstNonEmpty(artifact.Title, sheet.Name);

            ExtractSheetLayout(worksheetPart, sheet);
            ExtractShapes(worksheetPart, sheet.Name, artifact);
            foreach (var rowElement in worksheetPart.Worksheet.Descendants<S.Row>().Take(OpenXmlReaderLimits.MaxRowsPerSheet))
            {
                var row = new RowModel
                {
                    Index = (uint)(rowElement.RowIndex?.Value ?? 0),
                    Height = rowElement.Height?.Value ?? 0,
                };
                foreach (var cellElement in rowElement.Elements<S.Cell>().Take(OpenXmlReaderLimits.MaxCellsPerRow))
                {
                    row.Cells.Add(new CellModel(
                        cellElement.CellReference?.Value ?? "",
                        ReadCellText(cellElement, sharedStrings),
                        TextNormalization.Clean(cellElement.CellFormula?.Text),
                        EnumText(cellElement.DataType),
                        (uint)(cellElement.StyleIndex?.Value ?? 0),
                        CellHasValue(cellElement)));
                }

                if (row.Cells.Count > 0)
                {
                    sheet.Rows.Add(row);
                }
            }

            ExtractSheetFeatures(worksheetPart, sheet, workbookPart.WorkbookStylesPart?.Stylesheet);
            ExtractCharts(worksheetPart, sheet.Name, artifact);
            artifact.Sheets.Add(sheet);
        }

        ExtractImages(workbookPart, artifact);

        artifact.Metadata["sheetCount"] = artifact.Sheets.Count.ToString();
        artifact.Metadata["imageCount"] = artifact.Images.Count.ToString();
        artifact.Metadata["chartCount"] = artifact.Charts.Count.ToString();
        return artifact;
    }

    private static string ReadCellText(S.Cell cell, S.SharedStringTable? sharedStrings)
    {
        var raw = cell.CellValue?.Text ?? cell.InnerText;
        if (string.IsNullOrEmpty(raw))
        {
            return "";
        }

        if (cell.DataType?.Value == S.CellValues.SharedString && int.TryParse(raw, out var sharedStringIndex))
        {
            return TextNormalization.Clean(sharedStrings?.Elements<S.SharedStringItem>().ElementAtOrDefault(sharedStringIndex)?.InnerText);
        }

        if (cell.DataType?.Value == S.CellValues.Boolean)
        {
            return raw == "1" ? "TRUE" : "FALSE";
        }

        return TextNormalization.Clean(raw);
    }

    private static bool CellHasValue(S.Cell cell)
    {
        return !string.IsNullOrEmpty(cell.CellValue?.Text) ||
               !string.IsNullOrEmpty(cell.InlineString?.InnerText) ||
               cell.CellFormula is not null;
    }

    private static void ExtractImages(WorkbookPart workbookPart, OfficeArtifactModel artifact)
    {
        foreach (var worksheetPart in workbookPart.WorksheetParts)
        {
            var drawingPart = worksheetPart.DrawingsPart;
            if (drawingPart is null)
            {
                continue;
            }

            foreach (var imagePart in drawingPart.ImageParts)
            {
                if (artifact.Images.Count >= OpenXmlReaderLimits.MaxImages)
                {
                    artifact.Diagnostics.Add(new DiagnosticModel("warning", "XLSX image limit reached."));
                    return;
                }

                var image = OpenXmlImageReader.Read(drawingPart, imagePart, $"worksheets.image[{artifact.Images.Count}]");
                if (image is not null)
                {
                    artifact.Images.Add(image);
                }
            }
        }
    }

    private static void ExtractCharts(WorksheetPart worksheetPart, string sheetName, OfficeArtifactModel artifact)
    {
        var drawingPart = worksheetPart.DrawingsPart;
        if (drawingPart is null)
        {
            return;
        }

        foreach (var chartPart in drawingPart.ChartParts)
        {
            var chart = OpenXmlChartReader.Read(drawingPart, chartPart, $"worksheets.chart[{artifact.Charts.Count}]", sheetName);
            if (chart is not null)
            {
                artifact.Charts.Add(chart);
            }
        }
    }

    private static void ExtractShapes(WorksheetPart worksheetPart, string sheetName, OfficeArtifactModel artifact)
    {
        var drawingPart = worksheetPart.DrawingsPart;
        if (drawingPart is null)
        {
            return;
        }

        XDocument drawingDocument;
        using (var stream = drawingPart.GetStream())
        {
            drawingDocument = XDocument.Load(stream);
        }

        XNamespace xdr = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
        XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        foreach (var anchor in drawingDocument.Root?.Elements(xdr + "oneCellAnchor") ?? [])
        {
            var shape = anchor.Element(xdr + "sp");
            var from = anchor.Element(xdr + "from");
            var ext = anchor.Element(xdr + "ext");
            if (shape is null || from is null || ext is null)
            {
                continue;
            }

            var properties = shape.Element(xdr + "spPr");
            var nonVisual = shape.Element(xdr + "nvSpPr")?.Element(xdr + "cNvPr");
            artifact.Shapes.Add(new SpreadsheetShapeModel(
                nonVisual?.Attribute("id")?.Value ?? $"shape-{artifact.Shapes.Count + 1}",
                sheetName,
                ParseUInt(from.Element(xdr + "col")?.Value),
                ParseUInt(from.Element(xdr + "row")?.Value),
                ParseDouble(from.Element(xdr + "colOff")?.Value),
                ParseDouble(from.Element(xdr + "rowOff")?.Value),
                ParseDouble(ext.Attribute("cx")?.Value),
                ParseDouble(ext.Attribute("cy")?.Value),
                ReadDrawingColor(properties?.Element(a + "solidFill")),
                ReadDrawingColor(properties?.Element(a + "ln")?.Element(a + "solidFill")),
                TextNormalization.Clean(string.Concat(shape.Descendants(a + "t").Select(text => text.Value))),
                properties?.Element(a + "prstGeom")?.Attribute("prst")?.Value ?? ""));
        }
    }

    private static void ExtractSheetLayout(WorksheetPart worksheetPart, SheetModel sheet)
    {
        var format = worksheetPart.Worksheet.SheetFormatProperties;
        sheet.DefaultColWidth = format?.DefaultColumnWidth?.Value ?? 0;
        sheet.DefaultRowHeight = format?.DefaultRowHeight?.Value ?? 0;

        foreach (var column in worksheetPart.Worksheet.Elements<S.Columns>().SelectMany(columns => columns.Elements<S.Column>()))
        {
            sheet.Columns.Add(new ColumnModel(
                (uint)(column.Min?.Value ?? 0),
                (uint)(column.Max?.Value ?? 0),
                column.Width?.Value ?? 0,
                column.Hidden?.Value ?? false));
        }
    }

    private static void ExtractSheetFeatures(WorksheetPart worksheetPart, SheetModel sheet, S.Stylesheet? stylesheet)
    {
        foreach (var mergeCell in worksheetPart.Worksheet.Descendants<S.MergeCell>())
        {
            var reference = mergeCell.Reference?.Value;
            if (!string.IsNullOrWhiteSpace(reference))
            {
                sheet.MergedRanges.Add(new MergedRangeModel(reference));
            }
        }

        foreach (var tablePart in worksheetPart.TableDefinitionParts)
        {
            var table = tablePart.Table;
            var style = table?.TableStyleInfo;
            sheet.Tables.Add(new SheetTableModel(
                table?.Name?.Value ?? table?.DisplayName?.Value ?? "",
                table?.Reference?.Value ?? "",
                style?.Name?.Value ?? "",
                table?.AutoFilter is not null));
        }

        foreach (var formatting in worksheetPart.Worksheet.Descendants<S.ConditionalFormatting>())
        {
            var ranges = SplitReferences(formatting.GetAttribute("sqref", "").Value ?? "");
            foreach (var rule in formatting.Elements<S.ConditionalFormattingRule>())
            {
                var differentialStyle = stylesheet?.DifferentialFormats is not null && rule.FormatId?.Value is { } formatId
                    ? stylesheet.DifferentialFormats.Elements<S.DifferentialFormat>().ElementAtOrDefault((int)formatId)
                    : null;

                sheet.ConditionalFormats.Add(new ConditionalFormatModel(
                    EnumText(rule.Type),
                    (uint)(rule.Priority?.Value ?? 0),
                    ranges,
                    EnumText(rule.Operator),
                    rule.Elements<S.Formula>().Select(formula => TextNormalization.Clean(formula.Text)).ToArray(),
                    rule.Text?.Value ?? "",
                    ExtractFillColor(differentialStyle?.Fill),
                    ExtractFontColor(differentialStyle?.Font),
                    differentialStyle?.Font?.Bold is not null,
                    ReadColorScale(rule.Elements<S.ColorScale>().FirstOrDefault()),
                    ReadDataBar(rule.Elements<S.DataBar>().FirstOrDefault()),
                    ReadIconSet(rule.Elements<S.IconSet>().FirstOrDefault())));
            }
        }
    }

    private static void ExtractStyles(S.Stylesheet? stylesheet, SpreadsheetStylesModel styles)
    {
        if (stylesheet is null)
        {
            return;
        }

        foreach (var format in stylesheet.NumberingFormats?.Elements<S.NumberingFormat>() ?? [])
        {
            styles.NumberFormats.Add(new NumberFormatModel(
                (uint)(format.NumberFormatId?.Value ?? 0),
                format.FormatCode?.Value ?? ""));
        }

        foreach (var format in stylesheet.CellFormats?.Elements<S.CellFormat>() ?? [])
        {
            styles.CellFormats.Add(new CellFormatModel(
                (uint)(format.NumberFormatId?.Value ?? 0),
                (uint)(format.FontId?.Value ?? 0),
                (uint)(format.FillId?.Value ?? 0),
                (uint)(format.BorderId?.Value ?? 0),
                EnumText(format.Alignment?.Horizontal),
                EnumText(format.Alignment?.Vertical)));
        }

        foreach (var font in stylesheet.Fonts?.Elements<S.Font>() ?? [])
        {
            styles.Fonts.Add(new FontStyleModel(
                font.Bold is not null,
                font.Italic is not null,
                font.FontSize?.Val?.Value ?? 0,
                font.FontName?.Val?.Value ?? "",
                ExtractFontColor(font)));
        }

        foreach (var fill in stylesheet.Fills?.Elements<S.Fill>() ?? [])
        {
            styles.Fills.Add(new FillStyleModel(ExtractFillColor(fill)));
        }

        foreach (var border in stylesheet.Borders?.Elements<S.Border>() ?? [])
        {
            styles.Borders.Add(new BorderStyleModel(ReadColor(border.BottomBorder?.Color)));
        }
    }

    private static ColorScaleModel? ReadColorScale(S.ColorScale? colorScale)
    {
        var colors = colorScale?.Elements<S.Color>().Select(ReadColor).Where(color => color.Length > 0).ToArray() ?? [];
        return colors.Length > 0 ? new ColorScaleModel(colors) : null;
    }

    private static DataBarModel? ReadDataBar(S.DataBar? dataBar)
    {
        var color = ReadColor(dataBar?.Elements<S.Color>().FirstOrDefault());
        return color.Length > 0 ? new DataBarModel(color) : null;
    }

    private static IconSetModel? ReadIconSet(S.IconSet? iconSet)
    {
        if (iconSet is null)
        {
            return null;
        }

        return new IconSetModel(
            EnumText(iconSet.IconSetValue),
            iconSet.ShowValue?.Value ?? true,
            iconSet.Reverse?.Value ?? false);
    }

    private static string ExtractFillColor(S.Fill? fill)
    {
        return FirstNonEmpty(
            ReadColor(fill?.PatternFill?.ForegroundColor),
            ReadColor(fill?.PatternFill?.BackgroundColor));
    }

    private static string ExtractFontColor(S.Font? font)
    {
        return ReadColor(font?.Color);
    }

    private static string ReadColor(OpenXmlElement? color)
    {
        return color?.GetAttribute("rgb", "").Value ?? "";
    }

    private static string ReadDrawingColor(XElement? fill)
    {
        if (fill is null)
        {
            return "";
        }

        XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        return fill.Element(a + "srgbClr")?.Attribute("val")?.Value ??
            fill.Element(a + "schemeClr")?.Attribute("val")?.Value ??
            "";
    }

    private static uint ParseUInt(string? value)
    {
        return uint.TryParse(value, out var parsed) ? parsed : 0;
    }

    private static double ParseDouble(string? value)
    {
        return double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static string EnumText(OpenXmlSimpleType? value)
    {
        return value?.InnerText ?? "";
    }

    private static IReadOnlyList<string> SplitReferences(string value)
    {
        return value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    private static string FirstNonEmpty(string current, string candidate)
    {
        return string.IsNullOrWhiteSpace(current) ? candidate : current;
    }
}
