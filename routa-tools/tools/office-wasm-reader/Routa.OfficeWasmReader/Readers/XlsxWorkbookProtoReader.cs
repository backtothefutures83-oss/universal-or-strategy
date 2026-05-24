using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Google.Protobuf;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using S = DocumentFormat.OpenXml.Spreadsheet;
using T = DocumentFormat.OpenXml.Office2019.Excel.ThreadedComments;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace Routa.OfficeWasmReader;

internal static class XlsxWorkbookProtoReader
{
    private const int ColorTypeRgb = 1;
    private const int ColorTypeScheme = 2;
    private const int ColorTypeSystem = 3;
    private const int EffectTypeShadow = 1;
    private const int EffectTypeGlow = 3;
    private const int EffectTypeReflection = 4;
    private const int EffectTypeSoftEdges = 5;
    private const int FillTypeSolid = 1;
    private const int FillTypeGradient = 2;
    private const int GradientKindLinear = 1;

    public static byte[] Read(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var document = SpreadsheetDocument.Open(stream, false);
        var workbookPart = document.WorkbookPart;
        if (workbookPart?.Workbook?.Sheets is null)
        {
            return Message(_ => { });
        }

        using var formulaValues = WorkbookNeedsFormulaBackfill(workbookPart)
            ? ClosedXmlFormulaValueProvider.TryCreate(bytes)
            : null;
        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
        var stylesheet = workbookPart.WorkbookStylesPart?.Stylesheet;
        var sheetIndex = 0;

        return Message(output =>
        {
            foreach (var sheetElement in workbookPart.Workbook.Sheets.Elements<S.Sheet>())
            {
                var relationshipId = sheetElement.Id?.Value;
                if (string.IsNullOrEmpty(relationshipId))
                {
                    continue;
                }

                if (workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
                {
                    continue;
                }

                WriteMessage(output, 1, WriteSheet(worksheetPart, sheetElement, sharedStrings, stylesheet, formulaValues, sheetIndex));
                sheetIndex += 1;
                if (sheetIndex >= OpenXmlReaderLimits.MaxSheets)
                {
                    break;
                }
            }

            WriteMessage(output, 2, WriteStyles(stylesheet));
            var theme = WriteTheme(workbookPart.ThemePart);
            if (theme is not null)
            {
                WriteMessage(output, 3, theme);
            }

            foreach (var image in WorkbookImages(workbookPart))
            {
                WriteMessage(output, 5, WriteWorkbookImage(image));
            }

            var commentSheets = WorksheetCommentSheets(workbookPart).ToArray();
            var threadedCommentSheets = WorksheetThreadedCommentSheets(workbookPart).ToArray();
            foreach (var person in commentSheets.SelectMany(WorkbookCommentAuthors))
            {
                WriteMessage(output, 20, WritePerson(person.Id, person.DisplayName));
            }

            foreach (var person in WorkbookThreadedPeople(workbookPart))
            {
                WriteMessage(output, 20, WritePerson(person.Id, person.DisplayName));
            }

            foreach (var thread in threadedCommentSheets.SelectMany(WorkbookThreads))
            {
                WriteMessage(output, 21, thread);
            }

            foreach (var note in commentSheets.SelectMany(WorkbookNotes))
            {
                WriteMessage(output, 22, note);
            }

            foreach (var slicerCache in WorkbookSlicerCaches(workbookPart))
            {
                WriteMessage(output, 23, slicerCache);
            }

            foreach (var pivotCache in WorkbookPivotCaches(workbookPart))
            {
                WriteMessage(output, 24, pivotCache);
            }

            foreach (var timelineCache in WorkbookTimelineCaches(workbookPart))
            {
                WriteMessage(output, 25, timelineCache);
            }

            var definedNames = WriteDefinedNames(workbookPart.Workbook.DefinedNames);
            if (definedNames is not null)
            {
                WriteMessage(output, 26, definedNames);
            }
        });
    }

    private static bool WorkbookNeedsFormulaBackfill(WorkbookPart workbookPart)
    {
        return workbookPart.WorksheetParts.Any(part =>
            (part.Worksheet?.Descendants<S.Cell>() ?? [])
                .Any(cell => cell.CellFormula is not null && string.IsNullOrEmpty(cell.CellValue?.Text)));
    }

    private static byte[] WriteSheet(
        WorksheetPart worksheetPart,
        S.Sheet sheetElement,
        S.SharedStringTable? sharedStrings,
        S.Stylesheet? stylesheet,
        ClosedXmlFormulaValueProvider? formulaValues,
        int sheetIndex)
    {
        var worksheet = worksheetPart.Worksheet;
        if (worksheet is null)
        {
            return Message(_ => { });
        }

        var name = PreserveText(sheetElement.Name?.Value);
        return Message(output =>
        {
            WriteInt32(output, 1, sheetIndex);
            WriteString(output, 2, name.Length > 0 ? name : $"Sheet {sheetIndex + 1}");
            WriteString(output, 20, sheetElement.SheetId?.Value.ToString() ?? "");

            foreach (var row in worksheet.Descendants<S.Row>().Take(OpenXmlReaderLimits.MaxRowsPerSheet))
            {
                WriteMessage(output, 3, WriteRow(row, name, sharedStrings, stylesheet, formulaValues));
            }

            foreach (var column in worksheet.Elements<S.Columns>().SelectMany(columns => columns.Elements<S.Column>()))
            {
                WriteMessage(output, 6, WriteColumn(column));
            }

            var format = worksheet.SheetFormatProperties;
            WriteFloat(output, 7, (float)(format?.DefaultRowHeight?.Value ?? 0));
            WriteFloatIncludingZero(output, 21, (float)(format?.BaseColumnWidth?.Value ?? 0));

            foreach (var drawing in WorksheetDrawings(worksheetPart))
            {
                WriteMessage(output, 8, drawing);
            }

            WriteFloat(output, 9, (float)(format?.DefaultColumnWidth?.Value ?? 0));
            var sheetView = worksheet.SheetViews?.Elements<S.SheetView>().FirstOrDefault();
            if (sheetView?.ShowGridLines?.Value is { } showGridLines)
            {
                WriteBool(output, 10, showGridLines);
            }

            WriteMessage(output, 18, WriteColor(worksheet.SheetProperties?.TabColor));

            foreach (var mergeCell in worksheet.Descendants<S.MergeCell>())
            {
                var range = WriteRangeTarget(name, mergeCell.Reference?.Value ?? "");
                if (range.Length > 0)
                {
                    WriteMessage(output, 12, range);
                }
            }

            foreach (var formatting in worksheet.Descendants<S.ConditionalFormatting>())
            {
                WriteMessage(output, 13, WriteConditionalFormatting(name, formatting));
            }

            foreach (var sharedFormula in SharedFormulas(worksheet))
            {
                WriteMessage(output, 14, sharedFormula);
            }

            foreach (var tablePart in worksheetPart.TableDefinitionParts)
            {
                WriteMessage(output, 15, WriteTable(tablePart.Table));
            }

            foreach (var pivotTablePart in worksheetPart.PivotTableParts)
            {
                WriteMessage(output, 16, WritePivotTable(pivotTablePart.PivotTableDefinition));
            }

            foreach (var slicer in WorksheetSlicers(worksheetPart))
            {
                WriteMessage(output, 17, slicer);
            }

            var sparklineGroups = WriteSparklineGroups(worksheet);
            if (sparklineGroups is not null)
            {
                WriteMessage(output, 27, sparklineGroups);
            }

            var dataValidations = worksheet.Elements<S.DataValidations>().FirstOrDefault();
            if (dataValidations is not null)
            {
                WriteMessage(output, 28, WriteDataValidations(dataValidations));
            }
        });
    }

    private static byte[] WriteRow(
        S.Row row,
        string sheetName,
        S.SharedStringTable? sharedStrings,
        S.Stylesheet? stylesheet,
        ClosedXmlFormulaValueProvider? formulaValues)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, (int)(row.RowIndex?.Value ?? 0));
            foreach (var cell in row.Elements<S.Cell>().Take(OpenXmlReaderLimits.MaxCellsPerRow))
            {
                WriteMessage(output, 2, WriteCell(cell, sheetName, sharedStrings, stylesheet, formulaValues));
            }

            WriteFloat(output, 3, (float)(row.Height?.Value ?? 0));
            WriteBool(output, 4, row.CustomHeight?.Value ?? false);
            if (row.StyleIndex?.Value is { } styleIndex)
            {
                WriteInt32(output, 5, (int)styleIndex);
            }

            if (row.Hidden?.Value == true)
            {
                WriteBool(output, 6, true);
            }
        });
    }

    private static byte[] WriteCell(
        S.Cell cell,
        string sheetName,
        S.SharedStringTable? sharedStrings,
        S.Stylesheet? stylesheet,
        ClosedXmlFormulaValueProvider? formulaValues)
    {
        return Message(output =>
        {
            var address = cell.CellReference?.Value ?? "";
            var sharedStringItem = SharedStringItem(cell, sharedStrings);
            var text = ReadCellText(cell, sharedStringItem, formulaValues, sheetName, address);
            var formula = cell.CellFormula;
            var formulaText = PreserveText(formula?.Text);
            WriteString(output, 1, address);
            WriteString(output, 2, text);
            WriteString(output, 3, formulaText);
            if (ShouldWriteCellDataType(cell, formulaText))
            {
                WriteInt32(output, 4, CellDataType(cell));
            }

            WriteInt32IncludingZero(output, 5, (int)(cell.StyleIndex?.Value ?? 0));

            foreach (var paragraph in ExtractCellParagraphs(cell, sharedStringItem, stylesheet))
            {
                WriteMessage(output, 6, paragraph);
            }

            if (formula is not null && ShouldWriteCellFormulaMetadata(formula, formulaText))
            {
                if (formula.SharedIndex?.Value is { } sharedIndex)
                {
                    WriteInt32(output, 8, (int)sharedIndex);
                }

                WriteInt32(output, 9, CellFormulaType(formula));
                WriteString(output, 10, formula.Reference?.Value ?? "");
                if (formula.AlwaysCalculateArray?.Value is { } alwaysCalculateArray)
                {
                    WriteBool(output, 11, alwaysCalculateArray);
                }
            }
        });
    }

    private static byte[] WriteColumn(S.Column column)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, (int)(column.Min?.Value ?? 0));
            WriteInt32(output, 2, (int)(column.Max?.Value ?? 0));
            WriteFloat(output, 3, (float)(column.Width?.Value ?? 0));
            WriteBool(output, 4, column.CustomWidth?.Value ?? false);
            if (column.Style?.Value is { } styleIndex)
            {
                WriteInt32(output, 5, (int)styleIndex);
            }

            if (column.Hidden?.Value is { } hidden)
            {
                WriteBool(output, 6, hidden);
            }
        });
    }

    private static IEnumerable<byte[]> SharedFormulas(S.Worksheet worksheet)
    {
        var rows = worksheet
            .Descendants<S.Row>()
            .Take(OpenXmlReaderLimits.MaxRowsPerSheet);
        var cells = rows.SelectMany(row => row.Elements<S.Cell>().Take(OpenXmlReaderLimits.MaxCellsPerRow));

        foreach (var cell in cells)
        {
            var formula = cell.CellFormula;
            if (formula?.FormulaType?.Value != S.CellFormulaValues.Shared ||
                formula.SharedIndex?.Value is not { } sharedIndex)
            {
                continue;
            }

            var formulaText = PreserveText(formula.Text);
            if (formulaText.Length == 0)
            {
                continue;
            }

            yield return WriteSharedFormula((int)sharedIndex, formulaText, cell.CellReference?.Value ?? "");
        }
    }

    private static byte[] WriteSharedFormula(int sharedIndex, string formulaText, string anchor)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, sharedIndex);
            WriteString(output, 2, formulaText);
            WriteString(output, 3, anchor);
        });
    }

    private static byte[] WriteTable(S.Table? table)
    {
        return Message(output =>
        {
            if (table is null)
            {
                return;
            }

            WriteInt32(output, 1, (int)(table.Id?.Value ?? 0));
            WriteString(output, 2, table.Name?.Value ?? table.DisplayName?.Value ?? "");
            WriteString(output, 3, table.DisplayName?.Value ?? table.Name?.Value ?? "");
            WriteString(output, 4, table.Reference?.Value ?? "");
            foreach (var column in table.TableColumns?.Elements<S.TableColumn>() ?? [])
            {
                WriteMessage(output, 5, WriteTableColumn(column));
            }

            WriteMessage(output, 6, WriteTableStyle(table.TableStyleInfo));
            if (table.TotalsRowShown?.Value is { } totalsRowShown)
            {
                WriteBool(output, 7, totalsRowShown);
            }

            if (table.HeaderRowCount?.Value is { } headerRowCount)
            {
                WriteInt32(output, 8, (int)headerRowCount);
            }

            if (table.TotalsRowCount?.Value is { } totalsRowCount)
            {
                WriteInt32(output, 9, (int)totalsRowCount);
            }

            if (table.AutoFilter is not null)
            {
                WriteMessage(output, 10, WriteAutoFilter(table.AutoFilter));
            }
        });
    }

    private static byte[] WriteTableColumn(S.TableColumn column)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, (int)(column.Id?.Value ?? 0));
            WriteString(output, 2, column.Name?.Value ?? "");
        });
    }

    private static byte[]? WriteDefinedNames(S.DefinedNames? definedNames)
    {
        var names = definedNames?.Elements<S.DefinedName>().ToArray() ?? [];
        if (names.Length == 0)
        {
            return null;
        }

        return Message(output =>
        {
            foreach (var name in names)
            {
                WriteMessage(output, 1, WriteDefinedName(name));
            }
        });
    }

    private static byte[] WriteDefinedName(S.DefinedName definedName)
    {
        return Message(output =>
        {
            WriteString(output, 1, definedName.Name?.Value ?? "");
            WriteString(output, 2, PreserveText(definedName.Text));
            WriteInt32Value(output, 3, Int32Attribute(definedName, "localSheetId"));
            WriteBoolValue(output, 4, BoolAttribute(definedName, "hidden"));
            WriteString(output, 5, AttributeValue(definedName, "comment"));
            WriteString(output, 6, AttributeValue(definedName, "description"));
            WriteString(output, 7, AttributeValue(definedName, "customMenu"));
            WriteString(output, 8, AttributeValue(definedName, "help"));
            WriteString(output, 9, AttributeValue(definedName, "statusBar"));
            WriteString(output, 10, AttributeValue(definedName, "shortcutKey"));
            WriteBoolValue(output, 11, BoolAttribute(definedName, "function"));
            WriteBoolValue(output, 12, BoolAttribute(definedName, "vbProcedure"));
            WriteInt32Value(output, 13, Int32Attribute(definedName, "functionGroupId"));
            WriteBoolValue(output, 14, BoolAttribute(definedName, "publishToServer"));
            WriteBoolValue(output, 15, BoolAttribute(definedName, "workbookParameter"));
            WriteBoolValue(output, 16, BoolAttribute(definedName, "xlm"));
        });
    }

    private static byte[] WriteTableStyle(S.TableStyleInfo? style)
    {
        return Message(output =>
        {
            if (style is null)
            {
                return;
            }

            WriteString(output, 1, style.Name?.Value ?? "");
            if (style.ShowFirstColumn?.Value is { } showFirstColumn)
            {
                WriteBool(output, 2, showFirstColumn);
            }

            if (style.ShowLastColumn?.Value is { } showLastColumn)
            {
                WriteBool(output, 3, showLastColumn);
            }

            if (style.ShowRowStripes?.Value is { } showRowStripes)
            {
                WriteBool(output, 4, showRowStripes);
            }

            if (style.ShowColumnStripes?.Value is { } showColumnStripes)
            {
                WriteBool(output, 5, showColumnStripes);
            }
        });
    }

    private static byte[] WriteAutoFilter(S.AutoFilter autoFilter)
    {
        return Message(output =>
        {
            WriteString(output, 1, autoFilter.Reference?.Value ?? "");
            foreach (var column in autoFilter.Elements<S.FilterColumn>())
            {
                WriteMessage(output, 2, WriteFilterColumn(column));
            }
        });
    }

    private static byte[] WritePivotTable(S.PivotTableDefinition? pivotTable)
    {
        return Message(output =>
        {
            if (pivotTable is null)
            {
                return;
            }

            WriteString(output, 1, pivotTable.Name?.Value ?? "");
            WriteInt32(output, 2, (int)(pivotTable.CacheId?.Value ?? 0));
            if (pivotTable.Location is not null)
            {
                WriteMessage(output, 3, WritePivotLocation(pivotTable.Location));
            }

            WriteBoolValue(output, 4, BoolAttribute(pivotTable, "dataOnRows"));
            WriteBoolValue(output, 5, BoolAttribute(pivotTable, "rowGrandTotals"));
            WriteBoolValue(output, 6, BoolAttribute(pivotTable, "colGrandTotals"));

            var fieldIndex = 0;
            foreach (var field in pivotTable.PivotFields?.Elements<S.PivotField>() ?? [])
            {
                WriteMessage(output, 7, WritePivotField(field, fieldIndex));
                fieldIndex += 1;
            }

            WritePackedInt32s(output, 8, PivotFieldIndexes(ChildByLocalName(pivotTable, "rowFields")));
            WritePackedInt32s(output, 9, PivotFieldIndexes(ChildByLocalName(pivotTable, "colFields")));

            foreach (var pageField in ChildByLocalName(pivotTable, "pageFields")?.Elements() ?? [])
            {
                if (pageField.LocalName == "pageField")
                {
                    WriteMessage(output, 10, WritePivotPageField(pageField));
                }
            }

            foreach (var dataField in pivotTable.DataFields?.Elements<S.DataField>() ?? [])
            {
                WriteMessage(output, 11, WritePivotDataField(dataField));
            }

            foreach (var filter in ChildByLocalName(pivotTable, "filters")?.Elements() ?? [])
            {
                if (filter.LocalName == "filter")
                {
                    WriteMessage(output, 12, WritePivotFilter(filter));
                }
            }

            WriteBoolValue(output, 13, BoolAttribute(pivotTable, "compact"));
            WriteBoolValue(output, 14, BoolAttribute(pivotTable, "outline"));
            WriteBoolValue(output, 15, BoolAttribute(pivotTable, "showDrill"));

            var style = ChildByLocalName(pivotTable, "pivotTableStyleInfo");
            WriteString(output, 16, AttributeValue(style, "name"));

            foreach (var item in ChildByLocalName(pivotTable, "rowItems")?.Elements() ?? [])
            {
                if (item.LocalName == "i")
                {
                    WriteMessage(output, 17, WritePivotItem(item));
                }
            }

            foreach (var item in ChildByLocalName(pivotTable, "colItems")?.Elements() ?? [])
            {
                if (item.LocalName == "i")
                {
                    WriteMessage(output, 18, WritePivotItem(item));
                }
            }

            WriteBoolValue(output, 19, BoolAttribute(style, "showRowHeaders"));
            WriteBoolValue(output, 20, BoolAttribute(style, "showColHeaders"));
            WriteBoolValue(output, 21, BoolAttribute(style, "showRowStripes"));
            WriteBoolValue(output, 22, BoolAttribute(style, "showColStripes"));
            WriteBoolValue(output, 23, BoolAttribute(style, "showLastColumn"));
            WriteBoolValue(output, 24, BoolAttribute(pivotTable, "applyNumberFormats"));
            WriteBoolValue(output, 25, BoolAttribute(pivotTable, "applyBorderFormats"));
            WriteBoolValue(output, 26, BoolAttribute(pivotTable, "applyFontFormats"));
            WriteBoolValue(output, 27, BoolAttribute(pivotTable, "applyPatternFormats"));
            WriteBoolValue(output, 28, BoolAttribute(pivotTable, "applyAlignmentFormats"));
            WriteBoolValue(output, 29, BoolAttribute(pivotTable, "applyWidthHeightFormats"));
            WriteString(output, 30, AttributeValue(pivotTable, "dataCaption"));
            WriteInt32Value(output, 31, Int32Attribute(pivotTable, "updatedVersion"));
            WriteInt32Value(output, 32, Int32Attribute(pivotTable, "minRefreshableVersion"));
            WriteBoolValue(output, 33, BoolAttribute(pivotTable, "useAutoFormatting"));
            WriteBoolValue(output, 34, BoolAttribute(pivotTable, "itemPrintTitles"));
            WriteInt32Value(output, 35, Int32Attribute(pivotTable, "createdVersion"));
            WriteDoubleValue(output, 36, DoubleAttribute(pivotTable, "indent"));
            WriteBoolValue(output, 37, BoolAttribute(pivotTable, "outlineData"));
            WriteBoolValue(output, 38, BoolAttribute(pivotTable, "multipleFieldFilters"));
            WriteInt32Value(output, 39, Int32Attribute(pivotTable, "chartFormat"));
            WriteString(output, 40, TextNormalization.Clean(pivotTable.PivotTableDefinitionExtensionList?.OuterXml));
        });
    }

    private static byte[] WritePivotLocation(S.Location location)
    {
        return Message(output =>
        {
            WriteString(output, 1, location.Reference?.Value ?? "");
            WriteInt32Value(output, 2, Int32Attribute(location, "firstHeaderRow"));
            WriteInt32Value(output, 3, Int32Attribute(location, "firstDataRow"));
            WriteInt32Value(output, 4, Int32Attribute(location, "firstHeaderCol"));
            WriteInt32Value(output, 5, Int32Attribute(location, "firstDataCol"));
            WriteInt32Value(output, 6, Int32Attribute(location, "rowPageCount"));
            WriteInt32Value(output, 7, Int32Attribute(location, "colPageCount"));
        });
    }

    private static byte[] WritePivotField(S.PivotField field, int index)
    {
        var axis = AttributeValue(field, "axis");
        var sortType = AttributeValue(field, "sortType");
        return Message(output =>
        {
            WriteInt32(output, 1, index);
            WriteString(output, 2, field.Name?.Value ?? "");
            WriteString(output, 3, axis);
            WriteBoolValue(output, 4, BoolAttribute(field, "dataField"));
            WriteBoolValue(output, 5, BoolAttribute(field, "showAll"));
            WriteBoolValue(output, 6, BoolAttribute(field, "subtotalTop"));

            foreach (var item in field.Items?.Elements<S.Item>() ?? [])
            {
                WriteMessage(output, 7, WritePivotItem(item));
            }

            WriteInt32Value(output, 8, Int32Attribute(field, "numFmtId"));
            WriteString(output, 9, sortType);
            WriteBoolValue(output, 10, BoolAttribute(field, "multipleItemSelectionAllowed"));
            WriteInt32(output, 30, PivotAxis(axis));
            WriteInt32(output, 31, FieldSort(sortType));
        });
    }

    private static byte[] WritePivotItem(OpenXmlElement item)
    {
        var index = Int32Attribute(item, "x");
        if (index is null)
        {
            index = item.Elements().FirstOrDefault(child => child.LocalName == "x") is { } child
                ? Int32Attribute(child, "v")
                : null;
        }

        return Message(output =>
        {
            WriteString(output, 1, AttributeValue(item, "t"));
            WriteInt32Value(output, 2, index);
            WriteBoolValue(output, 3, BoolAttribute(item, "h"));
            WriteBoolValue(output, 4, BoolAttribute(item, "c"));
            WriteBoolValue(output, 5, BoolAttribute(item, "m"));
            WriteInt32Value(output, 6, Int32Attribute(item, "r"));
            WriteInt32Value(output, 7, Int32Attribute(item, "i"));
        });
    }

    private static byte[] WritePivotPageField(OpenXmlElement pageField)
    {
        return Message(output =>
        {
            WriteInt32Value(output, 1, Int32Attribute(pageField, "fld"));
            WriteInt32Value(output, 2, Int32Attribute(pageField, "item"));
            WriteString(output, 3, AttributeValue(pageField, "name"));
            WriteInt32Value(output, 4, Int32Attribute(pageField, "hier"));
        });
    }

    private static byte[] WritePivotDataField(S.DataField dataField)
    {
        var subtotal = AttributeValue(dataField, "subtotal");
        return Message(output =>
        {
            WriteInt32(output, 1, (int)(dataField.Field?.Value ?? 0));
            WriteString(output, 2, dataField.Name?.Value ?? "");
            WriteString(output, 3, subtotal);
            WriteInt32Value(output, 4, Int32Attribute(dataField, "numFmtId"));
            WriteString(output, 5, AttributeValue(dataField, "showDataAs"));
            WriteInt32Value(output, 6, Int32Attribute(dataField, "baseField"));
            WriteInt32Value(output, 7, Int32Attribute(dataField, "baseItem"));
            WriteInt32(output, 30, DataConsolidateFunction(subtotal));
        });
    }

    private static byte[] WritePivotFilter(OpenXmlElement filter)
    {
        var type = AttributeValue(filter, "type");
        return Message(output =>
        {
            WriteInt32Value(output, 1, Int32Attribute(filter, "fld"));
            WriteString(output, 2, type);
            WriteString(output, 3, AttributeValue(filter, "name"));
            WriteString(output, 4, AttributeValue(filter, "description"));
            WriteInt32(output, 30, PivotFilterType(type));
        });
    }

    private static IEnumerable<byte[]> WorksheetSlicers(WorksheetPart worksheetPart)
    {
        var anchors = WorksheetSlicerAnchors(worksheetPart)
            .GroupBy(anchor => anchor.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var slicersPart in worksheetPart.SlicersParts)
        {
            foreach (var slicer in slicersPart.Slicers?.Elements() ?? [])
            {
                if (!string.Equals(slicer.LocalName, "slicer", StringComparison.Ordinal))
                {
                    continue;
                }

                var name = AttributeValue(slicer, "name");
                anchors.TryGetValue(name, out var anchor);
                yield return WriteSlicer(slicer, anchor);
            }
        }
    }

    private static IEnumerable<WorksheetSlicerAnchor> WorksheetSlicerAnchors(WorksheetPart worksheetPart)
    {
        var worksheetDrawing = worksheetPart.DrawingsPart?.WorksheetDrawing;
        if (worksheetDrawing is null)
        {
            yield break;
        }

        foreach (var anchor in worksheetDrawing.ChildElements)
        {
            var name = AnchorSlicerName(anchor);
            if (name.Length == 0)
            {
                continue;
            }

            yield return new WorksheetSlicerAnchor(
                name,
                anchor.GetFirstChild<Xdr.FromMarker>(),
                anchor.GetFirstChild<Xdr.ToMarker>());
        }
    }

    private static byte[] WriteSlicer(OpenXmlElement slicer, WorksheetSlicerAnchor? anchor)
    {
        return Message(output =>
        {
            WriteString(output, 1, AttributeValue(slicer, "name"));
            WriteString(output, 2, AttributeValue(slicer, "caption"));
            WriteString(output, 3, AttributeValue(slicer, "cache"));
            WriteBoolValue(output, 4, BoolAttribute(slicer, "lockedPosition"));
            WriteBoolValue(output, 5, BoolAttribute(slicer, "showCaption"));
            WriteBoolValue(output, 6, BoolAttribute(slicer, "showNoDataItems"));

            if (anchor?.From is not null)
            {
                WriteMessage(output, 9, WriteAnchorMarker(anchor.From));
            }

            if (anchor?.To is not null)
            {
                WriteMessage(output, 10, WriteAnchorMarker(anchor.To));
            }

            WriteInt32Value(output, 11, Int32Attribute(slicer, "cacheId"));
            WriteDoubleValue(output, 12, DoubleAttribute(slicer, "width"));
            WriteDoubleValue(output, 13, DoubleAttribute(slicer, "height"));
            WriteBoolValue(output, 14, BoolAttribute(slicer, "isMultiSelect"));
        });
    }

    private static byte[] WriteDataValidations(S.DataValidations dataValidations)
    {
        return Message(output =>
        {
            foreach (var validation in dataValidations.Elements<S.DataValidation>())
            {
                WriteMessage(output, 1, WriteDataValidation(validation));
            }
        });
    }

    private static byte[] WriteDataValidation(S.DataValidation validation)
    {
        return Message(output =>
        {
            WriteString(output, 1, validation.SequenceOfReferences?.InnerText ?? "");
            if (validation.Type?.Value is { })
            {
                WriteInt32(output, 2, DataValidationType(EnumText(validation.Type)));
            }

            if (validation.ErrorStyle?.Value is { })
            {
                WriteInt32(output, 3, DataValidationErrorStyle(EnumText(validation.ErrorStyle)));
            }

            if (validation.ImeMode?.Value is { })
            {
                WriteInt32(output, 4, DataValidationImeMode(EnumText(validation.ImeMode)));
            }

            if (validation.Operator?.Value is { })
            {
                WriteInt32(output, 5, DataValidationOperator(EnumText(validation.Operator)));
            }

            if (validation.AllowBlank?.Value is { } allowBlank)
            {
                WriteBool(output, 6, allowBlank);
            }

            if (validation.ShowDropDown?.Value is { } showDropDown)
            {
                WriteBool(output, 7, showDropDown);
            }

            if (validation.ShowInputMessage?.Value is { } showInputMessage)
            {
                WriteBool(output, 8, showInputMessage);
            }

            if (validation.ShowErrorMessage?.Value is { } showErrorMessage)
            {
                WriteBool(output, 9, showErrorMessage);
            }

            WriteString(output, 10, validation.ErrorTitle?.Value ?? "");
            WriteString(output, 11, validation.Error?.Value ?? "");
            WriteString(output, 12, validation.PromptTitle?.Value ?? "");
            WriteString(output, 13, validation.Prompt?.Value ?? "");
            WriteString(output, 14, PreserveText(validation.Elements<S.Formula1>().FirstOrDefault()?.Text));
            WriteString(output, 15, PreserveText(validation.Elements<S.Formula2>().FirstOrDefault()?.Text));
            WriteString(output, 16, ExtendedAttributeValue(validation, "uid"));
        });
    }

    private static byte[]? WriteSparklineGroups(S.Worksheet worksheet)
    {
        var groups = worksheet
            .Descendants()
            .Where(element => element.LocalName == "sparklineGroup")
            .ToArray();
        if (groups.Length == 0)
        {
            return null;
        }

        return Message(output =>
        {
            foreach (var group in groups)
            {
                WriteMessage(output, 1, WriteSparklineGroup(group));
            }
        });
    }

    private static byte[] WriteSparklineGroup(OpenXmlElement group)
    {
        return Message(output =>
        {
            WriteString(output, 1, AttributeValue(group, "uid"));
            WriteDoubleValue(output, 2, DoubleAttribute(group, "manualMax"));
            WriteDoubleValue(output, 3, DoubleAttribute(group, "manualMin"));
            WriteDoubleValue(output, 4, DoubleAttribute(group, "lineWeight"));
            WriteInt32(output, 5, SparklineType(AttributeValue(group, "type")));
            WriteBoolValue(output, 6, BoolAttribute(group, "dateAxis"));
            WriteInt32(output, 7, SparklineDisplayEmptyCellsAs(AttributeValue(group, "displayEmptyCellsAs")));
            WriteBoolValue(output, 8, BoolAttribute(group, "markers"));
            WriteBoolValue(output, 9, BoolAttribute(group, "high"));
            WriteBoolValue(output, 10, BoolAttribute(group, "low"));
            WriteBoolValue(output, 11, BoolAttribute(group, "first"));
            WriteBoolValue(output, 12, BoolAttribute(group, "last"));
            WriteBoolValue(output, 13, BoolAttribute(group, "negative"));
            WriteBoolValue(output, 14, BoolAttribute(group, "displayXAxis"));
            WriteBoolValue(output, 15, BoolAttribute(group, "displayHidden"));
            WriteInt32(output, 16, SparklineAxisType(AttributeValue(group, "minAxisType")));
            WriteInt32(output, 17, SparklineAxisType(AttributeValue(group, "maxAxisType")));
            WriteBoolValue(output, 18, BoolAttribute(group, "rightToLeft"));
            WriteMessage(output, 19, WriteSparklineColor(ChildByLocalName(group, "colorSeries")));
            WriteMessage(output, 20, WriteSparklineColor(ChildByLocalName(group, "colorNegative")));
            WriteMessage(output, 21, WriteSparklineColor(ChildByLocalName(group, "colorAxis")));
            WriteMessage(output, 22, WriteSparklineColor(ChildByLocalName(group, "colorMarkers")));
            WriteMessage(output, 23, WriteSparklineColor(ChildByLocalName(group, "colorFirst")));
            WriteMessage(output, 24, WriteSparklineColor(ChildByLocalName(group, "colorLast")));
            WriteMessage(output, 25, WriteSparklineColor(ChildByLocalName(group, "colorHigh")));
            WriteMessage(output, 26, WriteSparklineColor(ChildByLocalName(group, "colorLow")));
            WriteString(output, 27, PreserveText(group.Elements().FirstOrDefault(element => element.LocalName == "f")?.InnerText));

            foreach (var sparkline in group.Descendants().Where(element => element.LocalName == "sparkline"))
            {
                WriteMessage(output, 28, WriteSparkline(sparkline));
            }
        });
    }

    private static byte[] WriteSparkline(OpenXmlElement sparkline)
    {
        return Message(output =>
        {
            WriteString(output, 1, PreserveText(sparkline.Elements().FirstOrDefault(element => element.LocalName == "f")?.InnerText));
            WriteString(output, 2, PreserveText(sparkline.Elements().FirstOrDefault(element => element.LocalName == "sqref")?.InnerText));
        });
    }

    private static byte[] WriteFilterColumn(S.FilterColumn column)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, (int)(column.ColumnId?.Value ?? 0));
            WriteString(output, 2, column.LocalName);
        });
    }

    private static byte[] WriteConditionalFormatting(string sheetName, S.ConditionalFormatting formatting)
    {
        var ranges = SplitReferences(formatting.GetAttribute("sqref", "").Value ?? "");
        return Message(output =>
        {
            foreach (var range in ranges)
            {
                WriteMessage(output, 1, WriteRangeTarget(sheetName, range));
            }

            foreach (var rule in formatting.Elements<S.ConditionalFormattingRule>())
            {
                WriteMessage(output, 2, WriteConditionalRule(rule));
            }
        });
    }

    private static byte[] WriteConditionalRule(S.ConditionalFormattingRule rule)
    {
        return Message(output =>
        {
            WriteString(output, 1, EnumText(rule.Type));
            WriteInt32(output, 2, (int)(rule.Priority?.Value ?? 0));
            if (rule.FormatId?.Value is { } formatId)
            {
                WriteInt32IncludingZero(output, 3, (int)formatId);
            }

            var operatorText = EnumText(rule.Operator);
            if (operatorText.Length > 0)
            {
                WriteString(output, 4, operatorText);
            }
            else
            {
                WriteStringIncludingEmpty(output, 4, "");
            }

            foreach (var formula in rule.Elements<S.Formula>())
            {
                WriteString(output, 5, PreserveText(formula.Text));
            }

            if (rule.StopIfTrue?.Value is { } stopIfTrue)
            {
                WriteBool(output, 6, stopIfTrue);
            }

            WriteBoolValue(output, 7, BoolAttribute(rule, "aboveAverage"));
            if (rule.Percent?.Value is { } percent)
            {
                WriteBool(output, 8, percent);
            }

            WriteBoolValue(output, 9, BoolAttribute(rule, "bottom"));
            var colorScale = rule.Elements<S.ColorScale>().FirstOrDefault();
            if (colorScale is not null)
            {
                WriteMessage(output, 10, WriteColorScale(colorScale));
            }

            var dataBar = rule.Elements<S.DataBar>().FirstOrDefault();
            if (dataBar is not null)
            {
                WriteMessage(output, 11, WriteDataBar(dataBar));
            }

            var iconSet = rule.Elements<S.IconSet>().FirstOrDefault();
            if (iconSet is not null)
            {
                WriteMessage(output, 12, WriteIconSet(iconSet));
            }

            WriteString(output, 13, rule.Text?.Value ?? "");
            WriteString(output, 14, AttributeValue(rule, "timePeriod"));
            WriteInt32Value(output, 15, Int32Attribute(rule, "rank"));
            WriteInt32Value(output, 16, Int32Attribute(rule, "stdDev"));
            WriteBoolValue(output, 17, BoolAttribute(rule, "equalAverage"));
            WriteString(output, 18, ConditionalRuleId(rule));
        });
    }

    private static IEnumerable<byte[]> WorksheetDrawings(WorksheetPart worksheetPart)
    {
        var drawingPart = worksheetPart.DrawingsPart;
        var worksheetDrawing = drawingPart?.WorksheetDrawing;
        if (drawingPart is null || worksheetDrawing is null)
        {
            yield break;
        }

        foreach (var anchor in worksheetDrawing.ChildElements)
        {
            byte[]? drawing = anchor switch
            {
                Xdr.OneCellAnchor oneCellAnchor => WriteOneCellDrawing(drawingPart, oneCellAnchor),
                Xdr.TwoCellAnchor twoCellAnchor => WriteTwoCellDrawing(drawingPart, twoCellAnchor),
                _ => null,
            };

            if (drawing is { Length: > 0 })
            {
                yield return drawing;
            }
        }
    }

    private static byte[]? WriteOneCellDrawing(DrawingsPart drawingPart, Xdr.OneCellAnchor anchor)
    {
        var chart = ChartFromAnchor(drawingPart, anchor);
        var image = ImageFromAnchor(drawingPart, anchor);
        var shape = anchor.GetFirstChild<Xdr.Shape>();
        var isSlicerShape = IsSlicerShape(shape);
        if (chart is null && image is null && shape is null)
        {
            return null;
        }

        return Message(output =>
        {
            WriteMessage(output, 1, WriteAnchorMarker(anchor.FromMarker));
            if (chart is not null)
            {
                WriteMessage(output, 3, WriteChart(chart));
            }

            if (image is not null)
            {
                WriteMessage(output, 4, WriteImageReference(image.Id));
            }

            var extentCx = anchor.Extent?.Cx?.Value ?? 0;
            var extentCy = anchor.Extent?.Cy?.Value ?? 0;
            if (!isSlicerShape)
            {
                WriteString(output, 5, extentCx.ToString());
                WriteString(output, 6, extentCy.ToString());
            }

            if (shape is not null)
            {
                WriteMessage(output, 7, WriteShapeElement(shape, extentCx, extentCy, isSlicerShape));
            }
        });
    }

    private static byte[]? WriteTwoCellDrawing(DrawingsPart drawingPart, Xdr.TwoCellAnchor anchor)
    {
        var chart = ChartFromAnchor(drawingPart, anchor);
        var image = ImageFromAnchor(drawingPart, anchor);
        var shape = anchor.GetFirstChild<Xdr.Shape>();
        var isSlicerShape = IsSlicerShape(shape);
        if (chart is null && image is null && shape is null)
        {
            return null;
        }

        return Message(output =>
        {
            WriteMessage(output, 1, WriteAnchorMarker(anchor.FromMarker));
            WriteMessage(output, 2, WriteAnchorMarker(anchor.ToMarker));
            if (chart is not null)
            {
                WriteMessage(output, 3, WriteChart(chart));
            }

            if (image is not null)
            {
                WriteMessage(output, 4, WriteImageReference(image.Id));
            }

            var extent = anchor.Descendants<A.Extents>().FirstOrDefault();
            var extentCx = extent?.Cx?.Value ?? 0;
            var extentCy = extent?.Cy?.Value ?? 0;
            if (shape is not null && !isSlicerShape)
            {
                WriteString(output, 5, extentCx.ToString());
                WriteString(output, 6, extentCy.ToString());
            }

            if (shape is not null)
            {
                WriteMessage(output, 7, WriteShapeElement(shape, extentCx, extentCy, isSlicerShape));
            }
        });
    }

    private static ChartReadModel? ChartFromAnchor(DrawingsPart drawingPart, OpenXmlElement anchor)
    {
        var relationshipId = anchor.Descendants<C.ChartReference>().FirstOrDefault()?.Id?.Value;
        if (string.IsNullOrEmpty(relationshipId) || drawingPart.GetPartById(relationshipId) is not ChartPart chartPart)
        {
            return null;
        }

        return ReadChart(chartPart);
    }

    private static WorksheetImageReference? ImageFromAnchor(DrawingsPart drawingPart, OpenXmlElement anchor)
    {
        var picture = anchor.GetFirstChild<Xdr.Picture>();
        var blip = picture?.Descendants<A.Blip>().FirstOrDefault();
        var relationshipId = blip?.Embed?.Value ?? blip?.Link?.Value;
        if (string.IsNullOrEmpty(relationshipId) || drawingPart.GetPartById(relationshipId) is not ImagePart imagePart)
        {
            return null;
        }

        var imageId = imagePart.Uri.OriginalString;
        return string.IsNullOrEmpty(imageId) ? null : new WorksheetImageReference(imageId, imagePart);
    }

    private static string AnchorSlicerName(OpenXmlElement anchor)
    {
        return TextNormalization.Clean(anchor
            .GetFirstChild<Xdr.Shape>()
            ?.Descendants()
            .FirstOrDefault(IsDrawingSlicerElement)
            ?.GetAttribute("name", "")
            .Value);
    }

    private static bool IsSlicerShape(Xdr.Shape? shape)
    {
        return shape?.Descendants().Any(IsDrawingSlicerLikeElement) == true;
    }

    private static bool IsDrawingSlicerLikeElement(OpenXmlElement element)
    {
        return IsDrawingSlicerElement(element) || IsDrawingTimeSlicerElement(element);
    }

    private static bool IsDrawingSlicerElement(OpenXmlElement element)
    {
        return IsElement(element, "slicer", "http://schemas.microsoft.com/office/drawing/2010/slicer");
    }

    private static bool IsDrawingTimeSlicerElement(OpenXmlElement element)
    {
        return IsElement(element, "timeslicer", "http://schemas.microsoft.com/office/drawing/2012/timeslicer");
    }

    private static IEnumerable<WorksheetImageReference> WorkbookImages(WorkbookPart workbookPart)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var imageCount = 0;
        foreach (var worksheetPart in WorksheetPartsInWorkbookOrder(workbookPart))
        {
            var drawingPart = worksheetPart.DrawingsPart;
            if (drawingPart is null)
            {
                continue;
            }

            foreach (var anchor in drawingPart.WorksheetDrawing?.ChildElements ?? [])
            {
                var image = ImageFromAnchor(drawingPart, anchor);
                if (image is null || !seen.Add(image.Id))
                {
                    continue;
                }

                yield return image;
                imageCount += 1;
                if (imageCount >= OpenXmlReaderLimits.MaxImages)
                {
                    yield break;
                }
            }
        }
    }

    private static IEnumerable<byte[]> WorkbookSlicerCaches(WorkbookPart workbookPart)
    {
        foreach (var slicerCachePart in workbookPart.SlicerCacheParts)
        {
            if (slicerCachePart.SlicerCacheDefinition is { } definition)
            {
                yield return WriteSlicerCache(definition);
            }
        }
    }

    private static byte[] WriteSlicerCache(OpenXmlElement definition)
    {
        var data = ChildByLocalName(definition, "data");
        var tabular = ChildByLocalName(data, "tabular");
        var olap = ChildByLocalName(data, "olap");
        var cacheData = tabular ?? olap;

        return Message(output =>
        {
            WriteString(output, 1, AttributeValue(definition, "name"));
            WriteString(output, 2, AttributeValue(definition, "caption"));
            WriteString(output, 3, AttributeValue(definition, "sourceName"));
            if (cacheData is not null)
            {
                WriteString(output, 4, "pivot");
            }

            WriteInt32Value(output, 5, Int32Attribute(cacheData, "pivotCacheId"));
            WritePackedInt32s(output, 6, SlicerPivotTableIds(definition));
            WriteString(output, 10, SlicerCrossFilterText(cacheData));
            WriteString(output, 11, SlicerSortOrderText(cacheData));

            foreach (var item in ChildByLocalName(tabular, "items")?.Elements() ?? [])
            {
                if (string.Equals(item.LocalName, "i", StringComparison.Ordinal))
                {
                    WriteMessage(output, 12, WriteSlicerCacheItem(item));
                }
            }
        });
    }

    private static byte[] WriteSlicerCacheItem(OpenXmlElement item)
    {
        return Message(output =>
        {
            WriteInt32Value(output, 1, Int32Attribute(item, "x"));
            WriteString(output, 2, AttributeValue(item, "v"));
            WriteBoolValue(output, 3, BoolAttribute(item, "s"));
            WriteBoolValue(output, 4, BoolAttribute(item, "d"));
            WriteBoolValue(output, 5, BoolAttribute(item, "nd"));
        });
    }

    private static IEnumerable<byte[]> WorkbookTimelineCaches(WorkbookPart workbookPart)
    {
        foreach (var timelineCachePart in workbookPart.TimeLineCacheParts)
        {
            if (timelineCachePart.TimelineCacheDefinition is { } definition)
            {
                yield return WriteTimelineCache(definition);
            }
        }
    }

    private static byte[] WriteTimelineCache(OpenXmlElement definition)
    {
        return Message(output =>
        {
            WriteString(output, 1, AttributeValue(definition, "name"));
        });
    }

    private static IEnumerable<byte[]> WorkbookPivotCaches(WorkbookPart workbookPart)
    {
        foreach (var pivotCache in workbookPart.Workbook.Descendants<S.PivotCache>())
        {
            var relationshipId = pivotCache.Id?.Value;
            if (string.IsNullOrEmpty(relationshipId) ||
                workbookPart.GetPartById(relationshipId) is not PivotTableCacheDefinitionPart cachePart)
            {
                continue;
            }

            yield return WritePivotCache((int)(pivotCache.CacheId?.Value ?? 0), cachePart.PivotCacheDefinition);
        }
    }

    private static byte[] WritePivotCache(int cacheId, S.PivotCacheDefinition? definition)
    {
        return Message(output =>
        {
            if (definition is null)
            {
                return;
            }

            WriteInt32(output, 1, cacheId);

            foreach (var field in definition.CacheFields?.Elements<S.CacheField>() ?? [])
            {
                WriteMessage(output, 3, WritePivotCacheField(field));
            }

            var worksheetSource = definition.CacheSource?.WorksheetSource;
            WriteString(output, 6, worksheetSource?.Reference?.Value ?? "");
            WriteString(output, 7, worksheetSource?.Sheet?.Value ?? "");
            WriteString(output, 8, definition.RefreshedBy?.Value ?? "");
            WriteString(output, 9, PivotRefreshedDate(definition));
            WriteInt32Value(output, 10, Int32Attribute(definition, "createdVersion"));
            WriteInt32Value(output, 11, Int32Attribute(definition, "refreshedVersion"));
            WriteInt32Value(output, 12, Int32Attribute(definition, "minRefreshableVersion"));
            WriteInt32Value(output, 13, Int32Attribute(definition, "recordCount"));
            WriteString(output, 14, TextNormalization.Clean(definition.PivotCacheDefinitionExtensionList?.OuterXml));
        });
    }

    private static byte[] WritePivotCacheField(S.CacheField field)
    {
        return Message(output =>
        {
            WriteString(output, 1, field.Name?.Value ?? "");
            WriteInt32Value(output, 2, Int32Attribute(field, "numFmtId"));
            if (field.SharedItems is not null)
            {
                WriteMessage(output, 3, WritePivotSharedItems(field.SharedItems));
            }

            if (field.FieldGroup is not null)
            {
                WriteMessage(output, 4, WritePivotFieldGroup(field.FieldGroup));
            }
        });
    }

    private static byte[] WritePivotSharedItems(S.SharedItems sharedItems)
    {
        return Message(output =>
        {
            foreach (var item in sharedItems.Elements())
            {
                var value = AttributeValue(item, "v");
                if (value.Length > 0)
                {
                    WriteString(output, 1, value);
                }
            }

            WriteBoolValue(output, 2, BoolAttribute(sharedItems, "containsBlank"));
            WriteBoolValue(output, 3, BoolAttribute(sharedItems, "containsDate"));
            WriteBoolValue(output, 4, BoolAttribute(sharedItems, "containsNumber"));
            WriteBoolValue(output, 5, BoolAttribute(sharedItems, "containsString"));
            WriteBoolValue(output, 6, BoolAttribute(sharedItems, "containsSemiMixedTypes"));
            WriteBoolValue(output, 7, BoolAttribute(sharedItems, "containsNonDate"));
            WriteBoolValue(output, 8, BoolAttribute(sharedItems, "containsInteger"));
            WriteDoubleValue(output, 9, DoubleAttribute(sharedItems, "minValue"));
            WriteDoubleValue(output, 10, DoubleAttribute(sharedItems, "maxValue"));
            WriteString(output, 11, AttributeValue(sharedItems, "minDate"));
            WriteString(output, 12, AttributeValue(sharedItems, "maxDate"));
            WriteInt32Value(output, 13, Int32Attribute(sharedItems, "count"));
            WriteBoolValue(output, 14, BoolAttribute(sharedItems, "containsMixedTypes"));
        });
    }

    private static byte[] WritePivotFieldGroup(S.FieldGroup fieldGroup)
    {
        return Message(output =>
        {
            WriteInt32Value(output, 1, Int32Attribute(fieldGroup, "par"));
            WriteInt32Value(output, 2, Int32Attribute(fieldGroup, "base"));
            var range = ChildByLocalName(fieldGroup, "rangePr");
            if (range is not null)
            {
                WriteMessage(output, 3, Message(rangeOutput =>
                {
                    WriteString(rangeOutput, 1, AttributeValue(range, "groupBy"));
                    WriteString(rangeOutput, 2, AttributeValue(range, "startDate"));
                    WriteString(rangeOutput, 3, AttributeValue(range, "endDate"));
                }));
            }

            foreach (var item in ChildByLocalName(fieldGroup, "groupItems")?.Elements() ?? [])
            {
                var value = AttributeValue(item, "v");
                if (value.Length > 0)
                {
                    WriteString(output, 4, value);
                }
            }
        });
    }

    private static IEnumerable<WorksheetCommentSheet> WorksheetCommentSheets(WorkbookPart workbookPart)
    {
        foreach (var sheetElement in workbookPart.Workbook.Sheets?.Elements<S.Sheet>() ?? [])
        {
            var relationshipId = sheetElement.Id?.Value;
            if (string.IsNullOrEmpty(relationshipId) || workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
            {
                continue;
            }

            if (worksheetPart.WorksheetCommentsPart?.Comments is null)
            {
                continue;
            }

            var sheetName = TextNormalization.Clean(sheetElement.Name?.Value);
            yield return new WorksheetCommentSheet(
                worksheetPart,
                sheetName.Length > 0 ? sheetName : $"Sheet {sheetElement.SheetId?.Value ?? 0}");
        }
    }

    private static IEnumerable<WorksheetCommentSheet> WorksheetThreadedCommentSheets(WorkbookPart workbookPart)
    {
        foreach (var sheetElement in workbookPart.Workbook.Sheets?.Elements<S.Sheet>() ?? [])
        {
            var relationshipId = sheetElement.Id?.Value;
            if (string.IsNullOrEmpty(relationshipId) || workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
            {
                continue;
            }

            if (!worksheetPart.WorksheetThreadedCommentsParts.Any(part => part.ThreadedComments is not null))
            {
                continue;
            }

            var sheetName = TextNormalization.Clean(sheetElement.Name?.Value);
            yield return new WorksheetCommentSheet(
                worksheetPart,
                sheetName.Length > 0 ? sheetName : $"Sheet {sheetElement.SheetId?.Value ?? 0}");
        }
    }

    private static IEnumerable<WorkbookPerson> WorkbookThreadedPeople(WorkbookPart workbookPart)
    {
        foreach (var person in workbookPart.WorkbookPersonParts
                     .SelectMany(part => part.PersonList?.Elements<T.Person>() ?? []))
        {
            var id = AttributeValue(person, "id");
            var displayName = TextNormalization.Clean(AttributeValue(person, "displayName"));
            if (id.Length == 0 && displayName.Length == 0)
            {
                continue;
            }

            yield return new WorkbookPerson(id, displayName);
        }
    }

    private static IEnumerable<WorkbookPerson> WorkbookCommentAuthors(WorksheetCommentSheet commentSheet)
    {
        var authorIndex = 0;
        foreach (var author in commentSheet.Part.WorksheetCommentsPart?.Comments?.Authors?.Elements<S.Author>() ?? [])
        {
            yield return new WorkbookPerson(
                CommentAuthorId(commentSheet.SheetName, authorIndex),
                TextNormalization.Clean(author.Text));
            authorIndex += 1;
        }
    }

    private static IEnumerable<byte[]> WorkbookNotes(WorksheetCommentSheet commentSheet)
    {
        foreach (var comment in commentSheet.Part.WorksheetCommentsPart?.Comments?.CommentList?.Elements<S.Comment>() ?? [])
        {
            var reference = TextNormalization.Clean(comment.Reference?.Value);
            var body = PreserveText(comment.Elements<S.CommentText>().FirstOrDefault()?.InnerText);
            if (reference.Length == 0 && body.Length == 0)
            {
                continue;
            }

            var authorId = (int)(comment.AuthorId?.Value ?? 0);
            yield return WriteNote(
                $"{commentSheet.SheetName}:{reference}",
                commentSheet.SheetName,
                reference,
                CommentAuthorId(commentSheet.SheetName, authorId),
                body);
        }
    }

    private static IEnumerable<byte[]> WorkbookThreads(WorksheetCommentSheet commentSheet)
    {
        foreach (var part in commentSheet.Part.WorksheetThreadedCommentsParts)
        {
            var comments = part.ThreadedComments?
                .Elements<T.ThreadedComment>()
                .Select(ReadThreadedComment)
                .Where(comment => comment.Id.Length > 0 || comment.Address.Length > 0 || comment.Body.Length > 0)
                .ToArray() ?? [];

            foreach (var thread in GroupThreadedComments(commentSheet.SheetName, comments))
            {
                yield return WriteThread(thread);
            }
        }
    }

    private static ThreadedCommentReadModel ReadThreadedComment(T.ThreadedComment comment)
    {
        return new ThreadedCommentReadModel(
            AttributeValue(comment, "id"),
            AttributeValue(comment, "parentId"),
            AttributeValue(comment, "personId"),
            FormatThreadedDate(AttributeValue(comment, "dT")),
            TextNormalization.Clean(ChildByLocalName(comment, "text")?.InnerText),
            TextNormalization.Clean(AttributeValue(comment, "ref")),
            BoolAttribute(comment, "done") == true);
    }

    private static IEnumerable<ThreadReadModel> GroupThreadedComments(
        string sheetName,
        IReadOnlyList<ThreadedCommentReadModel> comments)
    {
        var commentsById = comments
            .Where(comment => comment.Id.Length > 0)
            .ToDictionary(comment => comment.Id, StringComparer.Ordinal);
        var groups = new Dictionary<string, List<ThreadedCommentReadModel>>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var comment in comments)
        {
            var rootId = ThreadRootId(comment, commentsById);
            if (!groups.TryGetValue(rootId, out var group))
            {
                group = [];
                groups[rootId] = group;
                order.Add(rootId);
            }

            group.Add(comment);
        }

        foreach (var rootId in order)
        {
            var group = groups[rootId];
            var root = group.FirstOrDefault(comment => comment.ParentId.Length == 0)
                ?? group.First();
            var threadId = root.Id.Length > 0 ? root.Id : rootId;
            var address = root.Address.Length > 0
                ? root.Address
                : group.Select(comment => comment.Address).FirstOrDefault(address => address.Length > 0) ?? "";

            yield return new ThreadReadModel(
                threadId,
                sheetName,
                address,
                root.Done ? 2 : 1,
                group);
        }
    }

    private static string ThreadRootId(
        ThreadedCommentReadModel comment,
        IReadOnlyDictionary<string, ThreadedCommentReadModel> commentsById)
    {
        var current = comment;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (current.ParentId.Length > 0 && seen.Add(current.ParentId) && commentsById.TryGetValue(current.ParentId, out var parent))
        {
            current = parent;
        }

        if (current.Id.Length > 0)
        {
            return current.Id;
        }

        if (comment.ParentId.Length > 0)
        {
            return comment.ParentId;
        }

        return comment.Address;
    }

    private static byte[] WriteThread(ThreadReadModel thread)
    {
        return Message(output =>
        {
            WriteString(output, 1, thread.Id);
            WriteMessage(output, 2, Message(targetOutput =>
            {
                WriteMessage(targetOutput, 1, WriteCellTarget(thread.SheetName, thread.Address));
            }));
            foreach (var comment in thread.Comments)
            {
                WriteMessage(output, 3, WriteThreadComment(comment));
            }

            WriteInt32(output, 4, thread.Status);
        });
    }

    private static byte[] WriteThreadComment(ThreadedCommentReadModel comment)
    {
        return Message(output =>
        {
            WriteString(output, 1, comment.Id);
            WriteString(output, 2, comment.ParentId);
            WriteString(output, 3, comment.AuthorId);
            WriteString(output, 4, comment.CreatedAt);
            WriteStringIncludingEmpty(output, 5, "");
            WriteMessage(output, 6, Message(bodyOutput =>
            {
                WriteString(bodyOutput, 1, comment.Body);
            }));
        });
    }

    private static byte[] WritePerson(string id, string displayName)
    {
        return Message(output =>
        {
            WriteString(output, 1, id);
            WriteString(output, 2, displayName);
        });
    }

    private static byte[] WriteNote(string id, string sheetName, string address, string authorId, string body)
    {
        return Message(output =>
        {
            WriteString(output, 1, id);
            WriteMessage(output, 2, Message(targetOutput =>
            {
                WriteMessage(targetOutput, 1, WriteCellTarget(sheetName, address));
            }));
            WriteString(output, 3, authorId);
            WriteMessage(output, 5, Message(bodyOutput =>
            {
                WriteString(bodyOutput, 1, body);
            }));
        });
    }

    private static byte[] WriteCellTarget(string sheetName, string address)
    {
        return Message(output =>
        {
            WriteString(output, 1, sheetName);
            WriteStringIncludingEmpty(output, 2, "");
            WriteString(output, 3, address);
        });
    }

    private static string CommentAuthorId(string sheetName, int authorIndex)
    {
        return $"authors/{sheetName}/{authorIndex}";
    }

    private static string FormatThreadedDate(string value)
    {
        if (value.Length == 0)
        {
            return "";
        }

        return DateTimeOffset.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture)
            : value;
    }

    private static IEnumerable<WorksheetPart> WorksheetPartsInWorkbookOrder(WorkbookPart workbookPart)
    {
        foreach (var sheetElement in workbookPart.Workbook.Sheets?.Elements<S.Sheet>() ?? [])
        {
            var relationshipId = sheetElement.Id?.Value;
            if (!string.IsNullOrEmpty(relationshipId) && workbookPart.GetPartById(relationshipId) is WorksheetPart worksheetPart)
            {
                yield return worksheetPart;
            }
        }
    }

    private static byte[] WriteWorkbookImage(WorksheetImageReference image)
    {
        return Message(output =>
        {
            WriteString(output, 1, NormalizeImageContentType(image.Part.ContentType));
            using var stream = image.Part.GetStream();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var bytes = memory.ToArray();
            if (bytes.Length <= OpenXmlReaderLimits.MaxImageBytes)
            {
                WriteBytes(output, 2, bytes);
            }

            WriteString(output, 3, image.Id);
        });
    }

    private static byte[] WriteImageReference(string imageId)
    {
        return Message(output =>
        {
            WriteString(output, 1, imageId);
        });
    }

    private static byte[] WriteAnchorMarker(Xdr.MarkerType? marker)
    {
        return Message(output =>
        {
            WriteString(output, 1, marker?.RowId?.Text ?? "");
            WriteString(output, 2, marker?.ColumnId?.Text ?? "");
            WriteString(output, 3, marker?.ColumnOffset?.Text ?? "");
            WriteString(output, 4, marker?.RowOffset?.Text ?? "");
        });
    }

    private static byte[] WriteChart(ChartReadModel chart)
    {
        return Message(output =>
        {
            WriteString(output, 1, chart.Title);
            for (var index = 0; index < chart.Series.Count; index++)
            {
                WriteMessage(output, 3, WriteChartSeries(chart.Series[index], index, chart.Type == 13));
            }

            WriteInt32(output, 5, chart.Type);
            if (chart.CategoryAxis is not null)
            {
                WriteMessage(output, 8, WriteChartAxis(chart.CategoryAxis, false));
            }

            if (chart.ValueAxis is not null)
            {
                WriteMessage(output, 9, WriteChartAxis(chart.ValueAxis, true));
            }

            if (chart.HasLegend)
            {
                WriteBool(output, 11, true);
                WriteMessage(output, 12, WriteChartLegend(chart.LegendPosition));
            }

            if (chart.HasTitleTextStyle)
            {
                WriteMessageIncludingEmpty(output, 13, Message(_ => { }));
            }

            if (chart.HasDataLabels)
            {
                WriteMessage(output, 14, WriteChartDataLabels());
            }

            WriteMessage(output, 25, chart.ChartSpaceLine ?? []);
            WriteBoolValue(output, 26, chart.RoundedCorners);
            if (chart.HasView3D)
            {
                WriteMessageIncludingEmpty(output, 41, Message(_ => { }));
            }

            if (chart.Type == 4)
            {
                WriteMessage(output, 50, WriteBarOptions(chart));
            }

            WriteChartFamilyOptions(output, chart);
        });
    }

    private static byte[] WriteChartSeries(ChartSeriesReadModel series, int index, bool includeMarker)
    {
        return Message(output =>
        {
            WriteString(output, 8, index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            WriteString(output, 1, series.Name);
            WritePackedDoubles(output, 2, series.Values);
            foreach (var category in series.Categories)
            {
                WriteString(output, 5, category);
            }

            if (includeMarker && series.HasMarker)
            {
                WriteMessageIncludingEmpty(output, 16, Message(_ => { }));
            }
        });
    }

    private static byte[] WriteChartAxis(ChartAxisReadModel axis, bool isValueAxis)
    {
        return Message(output =>
        {
            WriteMessage(output, 5, axis.MajorGridline ?? []);
            if (axis.NumberFormatCode is not null)
            {
                WriteStringIncludingEmpty(output, 7, axis.NumberFormatCode);
            }
            WriteBoolValue(output, 21, axis.NumberFormatSourceLinked);
            WriteInt32(output, 10, axis.Position);
            WriteInt32(output, 11, 1);
            WriteInt32Value(output, 12, axis.MajorTickMark);
            WriteInt32Value(output, 13, axis.MinorTickMark);
            WriteInt32Value(output, 14, axis.TickLabelPosition);
            if (isValueAxis)
            {
                WriteInt32(output, 15, 1);
                WriteInt32(output, 16, 1);
            }

            WriteBoolValue(output, 18, axis.Deleted);
            if (!string.IsNullOrEmpty(axis.Title))
            {
                WriteString(output, 19, axis.Title);
                WriteMessageIncludingEmpty(output, 20, Message(_ => { }));
            }
        });
    }

    private static byte[] WriteChartLegend(int position)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, position);
            WriteBool(output, 2, false);
        });
    }

    private static byte[] WriteChartDataLabels()
    {
        return Message(output =>
        {
            WriteInt32(output, 15, 127);
        });
    }

    private static byte[] WriteBarOptions(ChartReadModel chart)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, chart.BarDirection);
            WriteInt32IncludingZero(output, 2, chart.BarGrouping ?? 0);
            if (chart.BarVaryColors is { } varyColors)
            {
                WriteBool(output, 3, varyColors);
            }

            WriteInt32Value(output, 4, chart.BarGapWidth);
            WriteInt32Value(output, 6, chart.BarOverlap);
        });
    }

    private static void WriteChartFamilyOptions(CodedOutputStream output, ChartReadModel chart)
    {
        if (chart.AreaGrouping is not null || chart.AreaVaryColors is not null)
        {
            WriteMessage(output, 52, Message(options =>
            {
                WriteInt32Value(options, 1, chart.AreaGrouping);
                WriteBoolValue(options, 2, chart.AreaVaryColors);
            }));
        }

        if (chart.PieFirstSliceAngle is not null || chart.PieVaryColors is not null)
        {
            WriteMessage(output, 53, Message(options =>
            {
                WriteInt32Value(options, 1, chart.PieFirstSliceAngle);
                WriteBoolValue(options, 2, chart.PieVaryColors);
            }));
        }

        if (chart.DoughnutHoleSize is not null || chart.DoughnutFirstSliceAngle is not null || chart.DoughnutVaryColors is not null)
        {
            WriteMessage(output, 54, Message(options =>
            {
                WriteInt32Value(options, 1, chart.DoughnutHoleSize);
                WriteInt32Value(options, 2, chart.DoughnutFirstSliceAngle);
                WriteBoolValue(options, 3, chart.DoughnutVaryColors);
            }));
        }

        if (chart.ScatterStyle is not null || chart.ScatterVaryColors is not null)
        {
            WriteMessage(output, 55, Message(options =>
            {
                WriteInt32Value(options, 1, chart.ScatterStyle);
                WriteBoolValue(options, 2, chart.ScatterVaryColors);
            }));
        }

        if (chart.BubbleIs3D is not null || chart.BubbleScale is not null || chart.BubbleShowNegative is not null || chart.BubbleVaryColors is not null)
        {
            WriteMessage(output, 56, Message(options =>
            {
                WriteBoolValue(options, 1, chart.BubbleIs3D);
                WriteInt32Value(options, 2, chart.BubbleScale);
                WriteBoolValue(options, 3, chart.BubbleShowNegative);
                WriteBoolValue(options, 4, chart.BubbleVaryColors);
            }));
        }

        if (chart.RadarStyle is not null || chart.RadarVaryColors is not null)
        {
            WriteMessage(output, 57, Message(options =>
            {
                WriteInt32Value(options, 1, chart.RadarStyle);
                WriteBoolValue(options, 2, chart.RadarVaryColors);
            }));
        }

        if (chart.SurfaceWireframe is not null)
        {
            WriteMessage(output, 58, Message(options =>
            {
                WriteBoolValue(options, 1, chart.SurfaceWireframe);
            }));
        }
    }

    private static ChartReadModel ReadChart(ChartPart chartPart)
    {
        var chartSpace = chartPart.ChartSpace;
        if (chartSpace is null)
        {
            return new ChartReadModel(
                Title: "",
                Type: 0,
                HasLegend: false,
                LegendPosition: 0,
                CategoryAxis: null,
                ValueAxis: null,
                ChartSpaceLine: null,
                HasTitleTextStyle: false,
                HasDataLabels: false,
                RoundedCorners: null,
                HasView3D: false,
                BarDirection: 0,
                BarGrouping: null,
                BarVaryColors: null,
                BarGapWidth: null,
                BarOverlap: null,
                AreaGrouping: null,
                AreaVaryColors: null,
                PieFirstSliceAngle: null,
                PieVaryColors: null,
                DoughnutHoleSize: null,
                DoughnutFirstSliceAngle: null,
                DoughnutVaryColors: null,
                ScatterStyle: null,
                ScatterVaryColors: null,
                BubbleIs3D: null,
                BubbleScale: null,
                BubbleShowNegative: null,
                BubbleVaryColors: null,
                RadarStyle: null,
                RadarVaryColors: null,
                SurfaceWireframe: null,
                Series: []);
        }

        var categoryAxis = chartSpace.Descendants<C.CategoryAxis>().FirstOrDefault();
        var valueAxis = chartSpace.Descendants<C.ValueAxis>().FirstOrDefault();
        var areaChart = chartSpace.Descendants<C.AreaChart>().FirstOrDefault();
        var barChart = chartSpace.Descendants<C.BarChart>().FirstOrDefault();
        var pieChart = chartSpace.Descendants<C.PieChart>().FirstOrDefault();
        var doughnutChart = chartSpace.Descendants<C.DoughnutChart>().FirstOrDefault();
        var scatterChart = chartSpace.Descendants<C.ScatterChart>().FirstOrDefault();
        var bubbleChart = chartSpace.Descendants<C.BubbleChart>().FirstOrDefault();
        var radarChart = chartSpace.Descendants<C.RadarChart>().FirstOrDefault();
        var surfaceChart = chartSpace.Descendants<C.SurfaceChart>().FirstOrDefault();
        return new ChartReadModel(
            ChartTitle(chartSpace),
            ChartType(chartSpace),
            chartSpace.Descendants<C.Legend>().Any(),
            LegendPosition(chartSpace.Descendants<C.LegendPosition>().FirstOrDefault()),
            ReadChartAxis(categoryAxis),
            ReadChartAxis(valueAxis),
            ChartSpaceLine(chartSpace),
            ChartTitleHasTextStyle(chartSpace),
            chartSpace.Descendants<C.DataLabels>().Any(),
            RoundedCorners(chartSpace),
            chartSpace.Descendants<C.View3D>().Any(),
            BarDirection(chartSpace.Descendants<C.BarDirection>().FirstOrDefault()),
            barChart is null ? null : BarGrouping(ChildByLocalName(barChart, "grouping")),
            barChart is null ? null : BoolValueFromChild(barChart, "varyColors"),
            barChart is null ? null : IntValueFromChild(barChart, "gapWidth"),
            barChart is null ? null : IntValueFromChild(barChart, "overlap"),
            areaChart is null ? null : AreaGrouping(ChildByLocalName(areaChart, "grouping")),
            areaChart is null ? null : BoolValueFromChild(areaChart, "varyColors"),
            pieChart is null ? null : IntValueFromChild(pieChart, "firstSliceAng"),
            pieChart is null ? null : BoolValueFromChild(pieChart, "varyColors") ?? false,
            doughnutChart is null ? null : IntValueFromChild(doughnutChart, "holeSize"),
            doughnutChart is null ? null : IntValueFromChild(doughnutChart, "firstSliceAng"),
            doughnutChart is null ? null : BoolValueFromChild(doughnutChart, "varyColors") ?? false,
            scatterChart is null ? null : ScatterStyle(ChildByLocalName(scatterChart, "scatterStyle")),
            scatterChart is null ? null : BoolValueFromChild(scatterChart, "varyColors"),
            bubbleChart is null ? null : BoolValueFromChild(bubbleChart, "bubble3D"),
            bubbleChart is null ? null : IntValueFromChild(bubbleChart, "bubbleScale"),
            bubbleChart is null ? null : BoolValueFromChild(bubbleChart, "showNegBubbles"),
            bubbleChart is null ? null : BoolValueFromChild(bubbleChart, "varyColors") ?? false,
            radarChart is null ? null : RadarStyle(ChildByLocalName(radarChart, "radarStyle")),
            radarChart is null ? null : BoolValueFromChild(radarChart, "varyColors"),
            surfaceChart is null ? null : BoolValueFromChild(surfaceChart, "wireframe"),
            ExtractChartSeries(chartSpace).ToArray());
    }

    private static string ChartTitle(C.ChartSpace chartSpace)
    {
        return TextNormalization.Clean(string.Concat(
            chartSpace.Descendants<C.Title>().FirstOrDefault()?.Descendants<A.Text>().Select(item => item.Text) ??
            Enumerable.Empty<string>()));
    }

    private static int ChartType(C.ChartSpace chartSpace)
    {
        if (chartSpace.Descendants<C.AreaChart>().Any()) return 2;
        if (chartSpace.Descendants<C.BarChart>().Any()) return 4;
        if (chartSpace.Descendants<C.BubbleChart>().Any()) return 5;
        if (chartSpace.Descendants<C.DoughnutChart>().Any()) return 8;
        if (chartSpace.Descendants<C.LineChart>().Any()) return 13;
        if (chartSpace.Descendants<C.PieChart>().Any()) return 16;
        if (chartSpace.Descendants<C.RadarChart>().Any()) return 17;
        if (chartSpace.Descendants<C.ScatterChart>().Any()) return 18;
        if (chartSpace.Descendants<C.SurfaceChart>().Any()) return 22;
        return 0;
    }

    private static int LegendPosition(C.LegendPosition? position)
    {
        return position?.Val?.InnerText switch
        {
            "l" => 1,
            "t" => 2,
            "r" => 3,
            "b" => 4,
            _ => 0,
        };
    }

    private static int BarDirection(C.BarDirection? direction)
    {
        return direction?.Val?.InnerText switch
        {
            "col" => 1,
            "bar" => 2,
            _ => 0,
        };
    }

    private static int? BarGrouping(OpenXmlElement? grouping)
    {
        if (grouping is null)
        {
            return null;
        }

        return AttributeValue(grouping, "val") switch
        {
            "clustered" => 1,
            "stacked" => 2,
            "percentStacked" => 3,
            _ => 0,
        };
    }

    private static ChartAxisReadModel? ReadChartAxis(OpenXmlElement? axis)
    {
        if (axis is null)
        {
            return null;
        }

        var numberingFormat = ChildByLocalName(axis, "numFmt");
        return new ChartAxisReadModel(
            AxisPosition(ChildByLocalName(axis, "axPos")),
            AxisTitle(axis),
            AxisMajorGridline(axis),
            numberingFormat is null ? null : AttributeValue(numberingFormat, "formatCode"),
            BoolAttribute(numberingFormat, "sourceLinked"),
            TickMark(ChildByLocalName(axis, "majorTickMark")) ?? 0,
            TickMark(ChildByLocalName(axis, "minorTickMark")) ?? 0,
            TickLabelPosition(ChildByLocalName(axis, "tickLblPos")) ?? 0,
            BoolValueFromChild(axis, "delete"));
    }

    private static bool ChartTitleHasTextStyle(C.ChartSpace chartSpace)
    {
        var title = chartSpace.Descendants<C.Title>().FirstOrDefault();
        return ChildByLocalName(title, "txPr") is not null ||
               title?.Descendants<A.RunProperties>().Any() == true;
    }

    private static string AxisTitle(OpenXmlElement? axis)
    {
        return TextNormalization.Clean(string.Concat(
            axis?.Elements<C.Title>().FirstOrDefault()?.Descendants<A.Text>().Select(item => item.Text) ??
            Enumerable.Empty<string>()));
    }

    private static int AxisPosition(OpenXmlElement? position)
    {
        return AttributeValue(position, "val") switch
        {
            "l" => 1,
            "r" => 2,
            "t" => 3,
            "b" => 4,
            _ => 0,
        };
    }

    private static int? TickMark(OpenXmlElement? tickMark)
    {
        if (tickMark is null)
        {
            return null;
        }

        return AttributeValue(tickMark, "val") switch
        {
            "none" => 1,
            "in" => 2,
            "out" => 3,
            "cross" => 4,
            _ => 0,
        };
    }

    private static int? TickLabelPosition(OpenXmlElement? tickLabelPosition)
    {
        if (tickLabelPosition is null)
        {
            return null;
        }

        return AttributeValue(tickLabelPosition, "val") switch
        {
            "high" => 1,
            "low" => 2,
            "nextTo" => 3,
            _ => 0,
        };
    }

    private static int? AreaGrouping(OpenXmlElement? grouping)
    {
        if (grouping is null)
        {
            return null;
        }

        return AttributeValue(grouping, "val") switch
        {
            "standard" => 1,
            "stacked" => 2,
            "percentStacked" => 3,
            _ => 0,
        };
    }

    private static int? ScatterStyle(OpenXmlElement? style)
    {
        if (style is null)
        {
            return null;
        }

        return AttributeValue(style, "val") switch
        {
            "lineMarker" => 1,
            "line" => 2,
            "marker" => 3,
            "smooth" => 4,
            "smoothMarker" => 5,
            _ => 0,
        };
    }

    private static int? RadarStyle(OpenXmlElement? style)
    {
        if (style is null)
        {
            return null;
        }

        return AttributeValue(style, "val") switch
        {
            "standard" => 1,
            "marker" => 2,
            "filled" => 3,
            _ => 0,
        };
    }

    private static int? IntValueFromChild(OpenXmlElement? element, string localName)
    {
        return Int32Attribute(ChildByLocalName(element, localName), "val");
    }

    private static bool? BoolValueFromChild(OpenXmlElement? element, string localName)
    {
        var child = ChildByLocalName(element, localName);
        if (child is null)
        {
            return null;
        }

        return BoolAttribute(child, "val") ?? true;
    }

    private static bool? RoundedCorners(C.ChartSpace chartSpace)
    {
        return BoolValueFromChild(chartSpace, "roundedCorners");
    }

    private static byte[]? ChartSpaceLine(C.ChartSpace chartSpace)
    {
        var line = chartSpace.ChildElements
            .LastOrDefault(element => element.LocalName == "spPr")
            ?.Descendants<A.Outline>()
            .FirstOrDefault()
            ?? chartSpace.Elements<C.Chart>()
                .FirstOrDefault()
                ?.ChildElements
                .LastOrDefault(element => element.LocalName == "spPr")
                ?.Descendants<A.Outline>()
                .FirstOrDefault();
        return line is null ? null : WriteLine(line);
    }

    private static byte[]? AxisMajorGridline(OpenXmlElement? axis)
    {
        var line = axis?.Elements<C.MajorGridlines>()
            .FirstOrDefault()
            ?.Elements<C.ChartShapeProperties>()
            .FirstOrDefault()
            ?.GetFirstChild<A.Outline>();
        return line is null ? null : WriteLine(line);
    }

    private static IEnumerable<ChartSeriesReadModel> ExtractChartSeries(C.ChartSpace chartSpace)
    {
        var seriesElements = chartSpace.Descendants<C.BarChartSeries>().Cast<OpenXmlElement>()
            .Concat(chartSpace.Descendants<C.LineChartSeries>())
            .Concat(chartSpace.Descendants<C.PieChartSeries>())
            .Concat(chartSpace.Descendants<C.AreaChartSeries>())
            .Concat(chartSpace.Descendants<C.ScatterChartSeries>())
            .Concat(chartSpace.Descendants<C.BubbleChartSeries>())
            .Concat(chartSpace.Descendants<C.RadarChartSeries>())
            .Concat(chartSpace.Descendants<C.SurfaceChartSeries>());

        var index = 0;
        foreach (var series in seriesElements)
        {
            var name = TextNormalization.Clean(series.Elements<C.SeriesText>().FirstOrDefault()?.InnerText);
            yield return new ChartSeriesReadModel(
                name.Length > 0 ? name : $"Series {index + 1}",
                ExtractChartCategories(series).ToArray(),
                ExtractChartValues(series).ToArray(),
                series.Elements<C.Marker>().Any());
            index += 1;
        }
    }

    private static IEnumerable<string> ExtractChartCategories(OpenXmlElement series)
    {
        return series.Elements<C.CategoryAxisData>().FirstOrDefault()
            ?.Descendants<C.NumericValue>()
            .Select(value => TextNormalization.Clean(value.Text))
            .Where(value => value.Length > 0)
            ?? [];
    }

    private static IEnumerable<double> ExtractChartValues(OpenXmlElement series)
    {
        OpenXmlElement? values = series.Elements<C.YValues>().FirstOrDefault();
        values ??= series.Elements<C.Values>().FirstOrDefault();
        return values
            ?.Descendants<C.NumericValue>()
            .Select(value => double.TryParse(value.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : double.NaN)
            .Where(double.IsFinite)
            ?? [];
    }

    private static byte[] WriteShapeElement(Xdr.Shape shape, long extentCx, long extentCy, bool isSlicerShape)
    {
        var nonVisual = shape.NonVisualShapeProperties?.NonVisualDrawingProperties;
        var shapeProperties = shape.ShapeProperties;
        var transform = shapeProperties?.GetFirstChild<A.Transform2D>();
        var paragraphs = isSlicerShape ? [] : ExtractShapeParagraphs(shape.TextBody).ToList();
        return Message(output =>
        {
            WriteMessage(output, 1, WriteBoundingBox(transform, extentCx, extentCy));
            WriteMessage(output, 4, WriteShape(shapeProperties, isSlicerShape));

            var wroteParagraph = false;
            foreach (var paragraph in paragraphs)
            {
                WriteMessageIncludingEmpty(output, 6, paragraph);
                wroteParagraph = true;
            }

            if (isSlicerShape && !wroteParagraph)
            {
                WriteMessageIncludingEmpty(output, 6, WriteEmptyParagraphWithTextStyle());
            }

            foreach (var effect in ExtractEffects(shapeProperties))
            {
                WriteMessage(output, 15, effect);
            }

            WriteString(output, 10, nonVisual?.Name?.Value ?? "");
            WriteInt32(output, 11, !isSlicerShape && shape.TextBody is null ? 5 : 1);

            var bodyTextStyle = isSlicerShape ? null : WriteShapeBodyTextStyle(shape.TextBody?.BodyProperties);
            if (bodyTextStyle is not null)
            {
                WriteMessageIncludingEmpty(output, 14, bodyTextStyle);
            }
            else if (isSlicerShape)
            {
                WriteMessageIncludingEmpty(output, 14, Message(_ => { }));
            }

            var connector = WriteShapeConnector(shapeProperties);
            if (connector is not null)
            {
                WriteMessage(output, 28, connector);
            }

            WriteString(output, 27, nonVisual?.Id?.Value.ToString() ?? "");
        });
    }

    private static IEnumerable<byte[]> ExtractShapeParagraphs(OpenXmlElement? textBody)
    {
        if (textBody is null)
        {
            yield break;
        }

        foreach (var paragraph in textBody.Elements<A.Paragraph>())
        {
            var paragraphProperties = paragraph.ParagraphProperties;
            yield return Message(output =>
            {
                foreach (var run in ExtractShapeRuns(paragraph))
                {
                    WriteMessageIncludingEmpty(output, 1, run);
                }

                var paragraphTextStyle = WriteShapeParagraphTextStyle(paragraphProperties);
                if (paragraphTextStyle is not null)
                {
                    WriteMessageIncludingEmpty(output, 2, paragraphTextStyle);
                }

                var bulletCharacter = paragraphProperties?.GetFirstChild<A.CharacterBullet>()?.Char?.Value;
                if (bulletCharacter is not null)
                {
                    WriteStringIncludingEmpty(output, 3, bulletCharacter);
                }

                if (paragraphProperties?.LeftMargin?.Value is { } marginLeft)
                {
                    WriteInt32IncludingZero(output, 4, marginLeft);
                }

                if (paragraphProperties?.Indent?.Value is { } indent)
                {
                    WriteInt32IncludingZero(output, 5, indent);
                }

                if (SpacingAfter(paragraphProperties) is { } spaceAfter)
                {
                    WriteInt32IncludingZero(output, 6, spaceAfter);
                }

                if (SpacingBefore(paragraphProperties) is { } spaceBefore)
                {
                    WriteInt32IncludingZero(output, 7, spaceBefore);
                }

                var paragraphStyle = WriteShapeParagraphStyle(paragraphProperties);
                if (paragraphStyle is not null)
                {
                    WriteMessageIncludingEmpty(output, 10, paragraphStyle);
                }
            });
        }
    }

    private static IEnumerable<byte[]> ExtractShapeRuns(A.Paragraph paragraph)
    {
        foreach (var child in paragraph.ChildElements)
        {
            if (child is A.Run run)
            {
                yield return WriteShapeTextRun(PreserveText(run.Text?.Text), run.RunProperties);
                continue;
            }

            if (string.Equals(child.LocalName, "br", StringComparison.Ordinal))
            {
                yield return WriteShapeTextRun("\n", null);
            }
        }
    }

    private static byte[] WriteShapeTextRun(string text, A.RunProperties? properties)
    {
        return Message(output =>
        {
            WriteStringIncludingEmpty(output, 1, text);
            if (properties is not null)
            {
                var textStyle = WriteShapeRunTextStyle(properties);
                if (textStyle.Length > 0)
                {
                    WriteMessage(output, 2, textStyle);
                }
            }
        });
    }

    private static byte[]? WriteShapeParagraphTextStyle(A.ParagraphProperties? paragraphProperties)
    {
        if (paragraphProperties is null)
        {
            return null;
        }

        return Message(output =>
        {
            WriteInt32(output, 8, AlignmentCode(AttributeValue(paragraphProperties, "algn")));
        });
    }

    private static byte[]? WriteShapeParagraphStyle(OpenXmlElement? paragraphProperties)
    {
        if (paragraphProperties is null)
        {
            return null;
        }

        return Message(output =>
        {
            var bulletCharacter = paragraphProperties.GetFirstChild<A.CharacterBullet>()?.Char?.Value;
            if (bulletCharacter is not null || paragraphProperties.GetFirstChild<A.NoBullet>() is not null)
            {
                WriteStringIncludingEmpty(output, 1, bulletCharacter ?? "");
            }

            if (Int32Attribute(paragraphProperties, "marL") is { } marginLeft)
            {
                WriteInt32IncludingZero(output, 2, marginLeft);
            }

            if (Int32Attribute(paragraphProperties, "indent") is { } indent)
            {
                WriteInt32IncludingZero(output, 3, indent);
            }

            if (paragraphProperties.GetFirstChild<A.LineSpacing>()?.GetFirstChild<A.SpacingPercent>()?.Val?.Value is { } lineSpacing)
            {
                WriteInt32IncludingZero(output, 4, lineSpacing);
            }
        });
    }

    private static byte[] WriteShapeRunTextStyle(A.RunProperties runProperties)
    {
        return Message(output =>
        {
            WriteTextStyleProperties(output, runProperties, null);
        });
    }

    private static byte[]? WriteShapeBodyTextStyle(A.BodyProperties? bodyProperties)
    {
        if (bodyProperties is null)
        {
            return null;
        }

        return Message(output =>
        {
            WriteInt32(output, 1, BodyAnchorCode(bodyProperties.Anchor));
            WriteInt32(output, 2, VerticalTextCode(bodyProperties.Vertical));
            WriteInt32IncludingZero(output, 10, bodyProperties.BottomInset?.Value ?? 0);
            WriteInt32IncludingZero(output, 11, bodyProperties.LeftInset?.Value ?? 0);
            WriteInt32IncludingZero(output, 12, bodyProperties.RightInset?.Value ?? 0);
            WriteInt32IncludingZero(output, 13, bodyProperties.TopInset?.Value ?? 0);
            WriteBoolValue(output, 14, BoolAttribute(bodyProperties, "spcFirstLastPara"));
            WriteInt32(output, 20, TextWrappingCode(bodyProperties.Wrap));
            var autoFit = WriteShapeAutoFit(bodyProperties);
            if (autoFit is not null)
            {
                WriteMessageIncludingEmpty(output, 21, autoFit);
            }
        });
    }

    private static byte[]? WriteShapeAutoFit(OpenXmlElement bodyProperties)
    {
        if (bodyProperties.ChildElements.Any(element => string.Equals(element.LocalName, "normAutofit", StringComparison.Ordinal)))
        {
            return Message(output =>
            {
                WriteMessageIncludingEmpty(output, 2, Message(_ => { }));
            });
        }

        if (bodyProperties.ChildElements.Any(element => string.Equals(element.LocalName, "spAutoFit", StringComparison.Ordinal)))
        {
            return Message(output =>
            {
                WriteMessageIncludingEmpty(output, 3, Message(_ => { }));
            });
        }

        if (bodyProperties.ChildElements.Any(element => string.Equals(element.LocalName, "noAutofit", StringComparison.Ordinal)))
        {
            return Message(output =>
            {
                WriteMessageIncludingEmpty(output, 1, Message(_ => { }));
            });
        }

        return null;
    }

    private static byte[]? WriteShapeConnector(OpenXmlElement? shapeProperties)
    {
        var line = shapeProperties?.GetFirstChild<A.Outline>();
        var head = line?.GetFirstChild<A.HeadEnd>() ?? ChildByLocalName(line, "headEnd");
        var tail = line?.GetFirstChild<A.TailEnd>() ?? ChildByLocalName(line, "tailEnd");
        if (head is null && tail is null)
        {
            return null;
        }

        return Message(output =>
        {
            WriteMessage(output, 5, Message(lineStyleOutput =>
            {
                WriteInt32(lineStyleOutput, 5, 1);
                WriteInt32(lineStyleOutput, 6, ConnectorJoin(shapeProperties));
                WriteMessage(lineStyleOutput, 7, WriteLineEnd(head));
                WriteMessage(lineStyleOutput, 8, WriteLineEnd(tail));
            }));
        });
    }

    private static int ConnectorJoin(OpenXmlElement? shapeProperties)
    {
        return ShapeGeometry(shapeProperties?.GetFirstChild<A.PresetGeometry>()?.GetAttribute("prst", "").Value) == 5 ? 3 : 1;
    }

    private static byte[] WriteLineEnd(OpenXmlElement? lineEnd)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, LineEndType(AttributeValue(lineEnd, "type")));
            WriteInt32(output, 2, LineEndWidth(AttributeValue(lineEnd, "w")));
            WriteInt32(output, 3, LineEndLength(AttributeValue(lineEnd, "len")));
        });
    }

    private static int LineEndType(string? value)
    {
        return value switch
        {
            "triangle" => 2,
            "stealth" => 3,
            "diamond" => 4,
            "oval" => 5,
            "arrow" => 6,
            _ => 1,
        };
    }

    private static int LineEndWidth(string? value)
    {
        return value switch
        {
            "med" => 2,
            "lg" => 3,
            _ => 1,
        };
    }

    private static int LineEndLength(string? value)
    {
        return value switch
        {
            "med" => 2,
            "lg" => 3,
            _ => 1,
        };
    }

    private static int? SpacingBefore(OpenXmlElement? paragraphProperties)
    {
        return SpacingPoints(paragraphProperties, "spcBef");
    }

    private static int? SpacingAfter(OpenXmlElement? paragraphProperties)
    {
        return SpacingPoints(paragraphProperties, "spcAft");
    }

    private static int? SpacingPoints(OpenXmlElement? paragraphProperties, string localName)
    {
        var spacing = paragraphProperties?.ChildElements.FirstOrDefault(child => string.Equals(child.LocalName, localName, StringComparison.Ordinal));
        if (spacing is null)
        {
            return null;
        }

        var points = spacing.ChildElements.FirstOrDefault(child => string.Equals(child.LocalName, "spcPts", StringComparison.Ordinal));
        if (Int32Attribute(points, "val") is { } pointValue)
        {
            return pointValue;
        }

        return 0;
    }

    private static int? BodyAnchorCode(OpenXmlSimpleType? value)
    {
        return EnumText(value) switch
        {
            "t" => 1,
            "ctr" => 2,
            "b" => 3,
            "just" => 4,
            "dist" => 5,
            _ => null,
        };
    }

    private static int? TextWrappingCode(OpenXmlSimpleType? value)
    {
        return EnumText(value) switch
        {
            "none" => 1,
            "square" => 2,
            _ => null,
        };
    }

    private static int? VerticalTextCode(OpenXmlSimpleType? value)
    {
        return EnumText(value) switch
        {
            "horz" => 1,
            "vert" => 2,
            "vert270" => 3,
            "wordArtVert" => 4,
            "eaVert" => 5,
            _ => null,
        };
    }

    private static int? AlignmentCode(string? value)
    {
        return value switch
        {
            "l" => 1,
            "ctr" => 2,
            "r" => 3,
            "just" => 4,
            "justLow" => 5,
            "dist" => 6,
            "thaiDist" => 7,
            _ => null,
        };
    }

    private static void WriteTextStyleProperties(CodedOutputStream output, OpenXmlElement? textProperties, OpenXmlElement? paragraphProperties)
    {
        WriteBoolValue(output, 4, BoolAttribute(textProperties, "b"));
        WriteBoolValue(output, 5, BoolAttribute(textProperties, "i"));
        WriteInt32(output, 6, Int32Attribute(textProperties, "sz"));
        var fill = textProperties?.GetFirstChild<A.SolidFill>();
        if (fill is not null)
        {
            WriteMessage(output, 7, WriteSolidFill(fill));
        }

        WriteInt32(output, 8, AlignmentCode(AttributeValue(paragraphProperties, "algn")));

        var underline = AttributeValue(textProperties, "u");
        if (!string.IsNullOrEmpty(underline))
        {
            WriteString(output, 9, underline);
        }

        var typeface =
            textProperties?.GetFirstChild<A.LatinFont>()?.Typeface?.Value ??
            textProperties?.GetFirstChild<A.EastAsianFont>()?.Typeface?.Value ??
            textProperties?.GetFirstChild<A.ComplexScriptFont>()?.Typeface?.Value;
        WriteString(output, 18, typeface);
    }

    private static byte[] WriteEmptyParagraphWithTextStyle()
    {
        return Message(output =>
        {
            WriteMessageIncludingEmpty(output, 2, Message(_ => { }));
        });
    }

    private static byte[] WriteBoundingBox(A.Transform2D? transform, long fallbackExtentCx, long fallbackExtentCy)
    {
        var offset = transform?.Offset;
        var extents = transform?.Extents;
        return Message(output =>
        {
            WriteInt64IncludingZero(output, 1, offset?.X?.Value ?? 0);
            WriteInt64IncludingZero(output, 2, offset?.Y?.Value ?? 0);
            WriteInt64(output, 3, extents?.Cx?.Value ?? fallbackExtentCx);
            WriteInt64(output, 4, extents?.Cy?.Value ?? fallbackExtentCy);
        });
    }

    private static byte[] WriteShape(OpenXmlElement? shapeProperties, bool suppressLineStyle = false)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, ShapeGeometry(shapeProperties?.GetFirstChild<A.PresetGeometry>()?.GetAttribute("prst", "").Value));
            WriteMessageIncludingEmpty(output, 5, WriteSolidFill(shapeProperties?.GetFirstChild<A.SolidFill>()));
            WriteMessage(output, 6, WriteLine(shapeProperties?.GetFirstChild<A.Outline>(), suppressLineStyle));
        });
    }

    private static int ShapeGeometry(string? value)
    {
        return value switch
        {
            "rtTriangle" => 4,
            "rect" => 5,
            "roundRect" => 26,
            "ellipse" => 35,
            _ => 0,
        };
    }

    private static byte[] WriteSolidFill(A.SolidFill? fill)
    {
        return Message(output =>
        {
            if (fill is null)
            {
                return;
            }

            WriteInt32(output, 1, FillTypeSolid);
            var color = ColorFromDrawingElement(fill);
            if (color is not null)
            {
                WriteMessage(output, 2, WriteColor(color));
            }
        });
    }

    private static byte[] WriteLine(A.Outline? line, bool suppressLineStyle = false)
    {
        return Message(output =>
        {
            var fill = line?.GetFirstChild<A.SolidFill>();
            var noFill = line?.GetFirstChild<A.NoFill>() is not null;
            if (fill is null && !noFill && line?.Width is null)
            {
                return;
            }

            var lineStyle = LineStyle(line);
            if (lineStyle != 0 && !suppressLineStyle)
            {
                WriteInt32(output, 1, lineStyle);
            }

            WriteInt32(output, 2, line?.Width?.Value ?? 0);
            if (fill is not null)
            {
                WriteMessage(output, 3, WriteSolidFill(fill));
            }
            else if (noFill)
            {
                WriteMessageIncludingEmpty(output, 3, Message(_ => { }));
            }
        });
    }

    private static int LineStyle(A.Outline? line)
    {
        return line?.GetFirstChild<A.PresetDash>()?.Val?.InnerText switch
        {
            "solid" => 1,
            "dash" => 2,
            "dot" => 3,
            "dashDot" => 4,
            "lgDash" => 6,
            "sysDash" => 7,
            "sysDot" => 8,
            "lgDashDot" => 9,
            "sysDashDot" => 10,
            "lgDashDotDot" => 11,
            "sysDashDotDot" => 12,
            _ => line?.GetFirstChild<A.SolidFill>() is not null ? 1 : 0,
        };
    }

    private static byte[]? WriteTheme(ThemePart? themePart)
    {
        var themeElements = themePart?.Theme?.ThemeElements;
        if (themeElements is null)
        {
            return null;
        }

        return Message(output =>
        {
            var colorScheme = themeElements.ColorScheme;
            if (colorScheme is not null)
            {
                WriteMessage(output, 1, WriteColorScheme(colorScheme));
            }

            var formatScheme = themeElements.FormatScheme;
            foreach (var fill in formatScheme?.BackgroundFillStyleList?.ChildElements ?? Enumerable.Empty<OpenXmlElement>())
            {
                var fillProto = WriteFillFromElement(fill);
                if (fillProto is not null)
                {
                    WriteMessage(output, 2, fillProto);
                }
            }

            foreach (var line in formatScheme?.LineStyleList?.Elements<A.Outline>() ?? Enumerable.Empty<A.Outline>())
            {
                WriteMessage(output, 3, WriteLine(line));
            }

            foreach (var effectStyle in formatScheme?.EffectStyleList?.Elements<A.EffectStyle>() ?? Enumerable.Empty<A.EffectStyle>())
            {
                WriteMessageIncludingEmpty(output, 4, WriteEffectStyle(effectStyle));
            }
        });
    }

    private static byte[] WriteColorScheme(A.ColorScheme colorScheme)
    {
        return Message(output =>
        {
            WriteString(output, 1, colorScheme.Name?.Value);
            foreach (var child in OrderedColorSchemeElements(colorScheme))
            {
                var color = ColorFromDrawingElement(child);
                if (color is not null)
                {
                    WriteMessage(output, 2, WriteThemeColor(child.LocalName, color));
                }
            }
        });
    }

    private static byte[] WriteThemeColor(string name, DrawingColorValue color)
    {
        return Message(output =>
        {
            WriteString(output, 1, name);
            WriteMessage(output, 2, WriteColor(color));
        });
    }

    private static IEnumerable<OpenXmlElement> OrderedColorSchemeElements(A.ColorScheme colorScheme)
    {
        var byName = colorScheme.ChildElements
            .GroupBy(element => element.LocalName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var name in new[] { "accent1", "accent2", "accent3", "accent4", "accent5", "accent6", "dk1", "lt1", "dk2", "lt2", "hlink", "folHlink" })
        {
            if (byName.TryGetValue(name, out var element))
            {
                yield return element;
            }
        }
    }

    private static byte[]? WriteFillFromElement(OpenXmlElement fill)
    {
        return fill switch
        {
            A.SolidFill solidFill => WriteSolidFill(solidFill),
            A.GradientFill gradientFill => WriteGradientFill(gradientFill),
            _ => null,
        };
    }

    private static byte[] WriteGradientFill(A.GradientFill fill)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, FillTypeGradient);
            foreach (var stop in fill.GradientStopList?.Elements<A.GradientStop>() ?? Enumerable.Empty<A.GradientStop>())
            {
                var color = ColorFromDrawingElement(stop);
                if (color is null)
                {
                    continue;
                }

                WriteMessage(output, 3, Message(stopOutput =>
                {
                    if (stop.Position?.Value is { } position)
                    {
                        WriteInt32IncludingZero(stopOutput, 1, position);
                    }

                    WriteMessage(stopOutput, 2, WriteColor(color));
                }));
            }

            var linear = fill.GetFirstChild<A.LinearGradientFill>();
            if (linear is not null)
            {
                WriteInt32(output, 5, GradientKindLinear);
                if (linear.Angle?.Value is { } angle)
                {
                    WriteDouble(output, 6, angle / 60000d);
                }

                WriteBoolValue(output, 7, linear.Scaled?.Value);
            }
        });
    }

    private static byte[] WriteEffectStyle(A.EffectStyle effectStyle)
    {
        return Message(output =>
        {
            foreach (var effect in ExtractEffects(effectStyle))
            {
                WriteMessage(output, 1, effect);
            }
        });
    }

    private static IEnumerable<byte[]> ExtractEffects(OpenXmlElement? element)
    {
        if (element is null)
        {
            yield break;
        }

        foreach (var shadow in element.Descendants<A.OuterShadow>())
        {
            yield return WriteShadowEffect(shadow);
        }

        foreach (var glow in element.Descendants<A.Glow>())
        {
            yield return WriteGlowEffect(glow);
        }

        foreach (var reflection in element.Descendants<A.Reflection>())
        {
            yield return WriteReflectionEffect(reflection);
        }

        foreach (var softEdges in element.Descendants<A.SoftEdge>())
        {
            yield return WriteSoftEdgesEffect(softEdges);
        }
    }

    private static byte[] WriteShadowEffect(A.OuterShadow shadow)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, EffectTypeShadow);
            WriteMessage(output, 2, Message(shadowOutput =>
            {
                var color = ColorFromDrawingElement(shadow);
                if (color is not null)
                {
                    WriteMessage(shadowOutput, 2, WriteColor(color));
                }

                WriteInt32(shadowOutput, 3, ToInt32(shadow.BlurRadius));
                WriteInt32(shadowOutput, 4, ToInt32(shadow.Distance));
                WriteInt32(shadowOutput, 5, shadow.Direction?.Value);
                WriteString(shadowOutput, 6, EnumText(shadow.Alignment));
                WriteBoolValue(shadowOutput, 7, shadow.RotateWithShape?.Value);
            }));
        });
    }

    private static byte[] WriteGlowEffect(A.Glow glow)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, EffectTypeGlow);
            WriteMessage(output, 3, Message(glowOutput =>
            {
                var color = ColorFromDrawingElement(glow);
                if (color is not null)
                {
                    WriteMessage(glowOutput, 1, WriteColor(color));
                }

                WriteInt64(glowOutput, 2, ToLong(glow.Radius));
            }));
        });
    }

    private static byte[] WriteReflectionEffect(A.Reflection reflection)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, EffectTypeReflection);
            WriteMessage(output, 4, Message(reflectionOutput =>
            {
                WriteInt64(reflectionOutput, 1, ToLong(reflection.BlurRadius));
                WriteInt32(reflectionOutput, 2, reflection.StartOpacity?.Value);
                WriteInt32(reflectionOutput, 3, reflection.StartPosition?.Value);
                WriteInt32(reflectionOutput, 4, reflection.EndAlpha?.Value);
                WriteInt32(reflectionOutput, 5, reflection.EndPosition?.Value);
                WriteInt64(reflectionOutput, 6, ToLong(reflection.Distance));
                WriteInt32(reflectionOutput, 7, reflection.Direction?.Value);
                WriteInt32(reflectionOutput, 8, reflection.FadeDirection?.Value);
                WriteInt32(reflectionOutput, 9, reflection.HorizontalRatio?.Value);
                WriteInt32(reflectionOutput, 10, reflection.VerticalRatio?.Value);
                WriteInt32(reflectionOutput, 11, reflection.HorizontalSkew?.Value);
                WriteInt32(reflectionOutput, 12, reflection.VerticalSkew?.Value);
                WriteString(reflectionOutput, 13, reflection.Alignment?.Value.ToString());
                WriteBoolValue(reflectionOutput, 14, reflection.RotateWithShape?.Value);
            }));
        });
    }

    private static byte[] WriteSoftEdgesEffect(A.SoftEdge softEdges)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, EffectTypeSoftEdges);
            WriteMessage(output, 5, Message(softEdgesOutput =>
            {
                WriteInt64(softEdgesOutput, 1, ToLong(softEdges.Radius));
            }));
        });
    }

    private static byte[] WriteColorScale(S.ColorScale colorScale)
    {
        return Message(output =>
        {
            foreach (var threshold in colorScale.Elements<S.ConditionalFormatValueObject>())
            {
                WriteMessage(output, 1, WriteCfvo(threshold));
            }

            foreach (var color in colorScale.Elements<S.Color>())
            {
                WriteMessage(output, 2, WriteColor(color));
            }
        });
    }

    private static byte[] WriteDataBar(S.DataBar dataBar)
    {
        return Message(output =>
        {
            foreach (var threshold in dataBar.Elements<S.ConditionalFormatValueObject>())
            {
                WriteMessage(output, 1, WriteCfvo(threshold));
            }

            WriteMessage(output, 2, WriteColor(dataBar.Elements<S.Color>().FirstOrDefault()));
            WriteBool(output, 3, true);
            if (dataBar.ShowValue?.Value is { } showValue)
            {
                WriteBool(output, 6, showValue);
            }
        });
    }

    private static byte[] WriteIconSet(S.IconSet iconSet)
    {
        return Message(output =>
        {
            WriteString(output, 1, EnumText(iconSet.IconSetValue));
            if (iconSet.ShowValue?.Value is { } showValue)
            {
                WriteBool(output, 2, showValue);
            }

            if (iconSet.Reverse?.Value is { } reverse)
            {
                WriteBool(output, 3, reverse);
            }

            foreach (var threshold in iconSet.Elements<S.ConditionalFormatValueObject>())
            {
                WriteMessage(output, 5, WriteCfvo(threshold));
            }

            if (iconSet.Percent?.Value is { } percent)
            {
                WriteBool(output, 6, percent);
            }
        });
    }

    private static byte[] WriteCfvo(S.ConditionalFormatValueObject threshold)
    {
        return Message(output =>
        {
            WriteString(output, 1, EnumText(threshold.Type));
            WriteString(output, 2, threshold.Val?.Value ?? "");
            if (threshold.GreaterThanOrEqual?.Value is { } gte)
            {
                WriteBool(output, 3, gte);
            }
        });
    }

    private static byte[] WriteRangeTarget(string sheetName, string reference)
    {
        var (startAddress, endAddress) = SplitCellRange(reference);
        if (startAddress.Length == 0)
        {
            return [];
        }

        return Message(output =>
        {
            WriteString(output, 1, sheetName);
            WriteStringIncludingEmpty(output, 2, "");
            WriteString(output, 3, startAddress);
            WriteString(output, 4, endAddress);
        });
    }

    private static byte[] WriteStyles(S.Stylesheet? stylesheet)
    {
        return Message(output =>
        {
            if (stylesheet is null)
            {
                return;
            }

            foreach (var font in StylesheetFontEntries(stylesheet.Fonts))
            {
                WriteMessageIncludingEmpty(output, 1, font is null ? Message(_ => { }) : WriteFont(font));
            }

            foreach (var fill in stylesheet.Fills?.Elements<S.Fill>() ?? [])
            {
                WriteMessage(output, 2, WriteFill(fill));
            }

            foreach (var format in stylesheet.CellFormats?.Elements<S.CellFormat>() ?? [])
            {
                WriteMessage(output, 3, WriteCellFormat(format));
            }

            foreach (var border in stylesheet.Borders?.Elements<S.Border>() ?? [])
            {
                WriteMessageIncludingEmpty(output, 4, WriteBorder(border));
            }

            var cellStyleIndex = 0;
            foreach (var style in stylesheet.CellStyles?.Elements<S.CellStyle>() ?? [])
            {
                WriteMessage(output, 5, WriteCellStyle(style, cellStyleIndex));
                cellStyleIndex++;
            }

            var cellStyleFormatIndex = 0;
            foreach (var format in stylesheet.CellStyleFormats?.Elements<S.CellFormat>() ?? [])
            {
                WriteMessage(output, 6, WriteCellStyleFormat(format, cellStyleFormatIndex));
                cellStyleFormatIndex++;
            }

            foreach (var format in stylesheet.NumberingFormats?.Elements<S.NumberingFormat>() ?? [])
            {
                WriteMessage(output, 7, WriteNumberFormat(format));
            }

            foreach (var format in stylesheet.DifferentialFormats?.Elements<S.DifferentialFormat>() ?? [])
            {
                WriteMessage(output, 8, WriteDifferentialFormat(format));
            }
        });
    }

    private static byte[] WriteCellStyle(S.CellStyle style, int index)
    {
        return Message(output =>
        {
            WriteInt32IncludingZero(output, 1, 0);
            WriteString(output, 2, style.Name?.Value ?? "");
            WriteString(output, 3, "");
            if (style.FormatId?.Value is { } formatId)
            {
                WriteInt32IncludingZero(output, 4, (int)formatId);
            }
        });
    }

    private static byte[] WriteCellStyleFormat(S.CellFormat format, int index)
    {
        return Message(output =>
        {
            WriteInt32IncludingZero(output, 1, index);
            WriteMessage(output, 2, WriteCellFormat(format));
        });
    }

    private static byte[] WriteFont(S.Font font)
    {
        return Message(output =>
        {
            if (font.Bold is not null)
            {
                WriteBool(output, 4, true);
            }

            if (font.Italic is not null)
            {
                WriteBool(output, 5, true);
            }

            if (font.FontSize?.Val?.Value is { } fontSize)
            {
                WriteInt32(output, 6, (int)Math.Round(fontSize));
            }

            var color = WriteColor(font.Color);
            if (color.Length > 0)
            {
                WriteMessage(output, 7, WriteColorFill(font.Color));
            }

            var underlineElement = font.Underline;
            var underline = underlineElement?.Val?.InnerText;
            if (underlineElement is not null && underline != "none")
            {
                WriteString(output, 9, string.IsNullOrEmpty(underline) ? "single" : underline);
            }

            var family = AttributeValue(font.FontFamilyNumbering, "val");
            if (int.TryParse(family, out var familyValue))
            {
                WriteInt32(output, 16, familyValue);
            }

            WriteString(output, 17, EnumText(font.FontScheme?.Val));
            WriteString(output, 18, font.FontName?.Val?.Value ?? "");
        });
    }

    private static IEnumerable<S.Font?> StylesheetFontEntries(S.Fonts? fonts)
    {
        if (fonts is null)
        {
            yield break;
        }

        foreach (var child in fonts.ChildElements)
        {
            if (child is S.Font font)
            {
                yield return font;
                continue;
            }

            if (string.Equals(child.LocalName, "font", StringComparison.Ordinal))
            {
                yield return null;
            }
        }
    }

    private static byte[] WriteCellFormat(S.CellFormat format)
    {
        return Message(output =>
        {
            if (format.NumberFormatId?.Value is { } numberFormatId)
            {
                WriteInt32IncludingZero(output, 1, (int)numberFormatId);
            }

            if (format.FontId?.Value is { } fontId)
            {
                WriteInt32IncludingZero(output, 2, (int)fontId);
            }

            if (format.FillId?.Value is { } fillId)
            {
                WriteInt32IncludingZero(output, 3, (int)fillId);
            }

            if (format.BorderId?.Value is { } borderId)
            {
                WriteInt32IncludingZero(output, 4, (int)borderId);
            }

            if (format.FormatId?.Value is { } formatId)
            {
                WriteInt32IncludingZero(output, 5, (int)formatId);
            }

            if (format.ApplyFill?.Value is { } applyFill)
            {
                WriteBool(output, 6, applyFill);
            }

            if (format.ApplyFont?.Value is { } applyFont)
            {
                WriteBool(output, 7, applyFont);
            }

            if (format.ApplyBorder?.Value is { } applyBorder)
            {
                WriteBool(output, 8, applyBorder);
            }

            if (format.ApplyAlignment?.Value is { } applyAlignment)
            {
                WriteBool(output, 9, applyAlignment);
            }

            WriteString(output, 10, EnumText(format.Alignment?.Horizontal));
            WriteString(output, 11, EnumText(format.Alignment?.Vertical));
            if (format.ApplyNumberFormat?.Value is { } applyNumberFormat)
            {
                WriteBool(output, 12, applyNumberFormat);
            }

            if (format.ApplyProtection?.Value is { } applyProtection)
            {
                WriteBool(output, 13, applyProtection);
            }

            if (format.Alignment?.WrapText?.Value is { } wrapText)
            {
                WriteBool(output, 14, wrapText);
            }

            if (format.Alignment?.ShrinkToFit?.Value is { } shrinkToFit)
            {
                WriteBool(output, 15, shrinkToFit);
            }
        });
    }

    private static byte[] WriteNumberFormat(S.NumberingFormat format)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, (int)(format.NumberFormatId?.Value ?? 0));
            WriteString(output, 2, format.FormatCode?.Value ?? "");
        });
    }

    private static byte[] WriteDifferentialFormat(S.DifferentialFormat format)
    {
        return Message(output =>
        {
            if (format.Font is not null)
            {
                WriteMessage(output, 1, WriteFont(format.Font));
            }

            if (format.Fill is not null)
            {
                WriteMessage(output, 2, WriteFill(format.Fill));
            }

            if (format.Border is not null)
            {
                WriteMessageIncludingEmpty(output, 3, WriteBorder(format.Border));
            }

            if (format.NumberingFormat is not null)
            {
                WriteMessage(output, 4, WriteNumberFormat(format.NumberingFormat));
            }
        });
    }

    private static byte[] WriteFill(S.Fill fill)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, 3);
            var fillColor =
                (OpenXmlElement?)fill.PatternFill?.ForegroundColor ??
                fill.PatternFill?.BackgroundColor;
            WriteMessage(output, 2, WriteColor(fillColor));
            WriteMessage(output, 17, WritePattern(fill.PatternFill));
        });
    }

    private static byte[] WriteColorFill(S.ColorType? color)
    {
        return Message(output =>
        {
            WriteMessage(output, 2, WriteColor(color));
        });
    }

    private static byte[] WriteCellTextColorFill(OpenXmlElement color)
    {
        return Message(output =>
        {
            WriteMessage(output, 2, WriteColor(color, preserveAlpha: true));
        });
    }

    private static byte[] WritePattern(S.PatternFill? patternFill)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, PatternType(patternFill?.GetAttribute("patternType", "").Value));
        });
    }

    private static int PatternType(string? value)
    {
        return value switch
        {
            "none" => 1,
            "solid" => 2,
            "mediumGray" => 3,
            "darkGray" => 4,
            "lightGray" => 5,
            "darkHorizontal" => 6,
            "darkVertical" => 7,
            "darkDown" => 8,
            "darkUp" => 9,
            "darkGrid" => 10,
            "darkTrellis" => 11,
            "lightHorizontal" => 12,
            "lightVertical" => 13,
            "lightDown" => 14,
            "lightUp" => 15,
            "lightGrid" => 16,
            "lightTrellis" => 17,
            "gray125" => 18,
            "gray0625" => 19,
            _ => 0,
        };
    }

    private static byte[] WriteBorder(S.Border border)
    {
        return Message(output =>
        {
            if (border.LeftBorder is not null)
            {
                WriteMessageIncludingEmpty(output, 1, WriteBorderLine(border.LeftBorder));
            }

            if (border.RightBorder is not null)
            {
                WriteMessageIncludingEmpty(output, 2, WriteBorderLine(border.RightBorder));
            }

            if (border.TopBorder is not null)
            {
                WriteMessageIncludingEmpty(output, 3, WriteBorderLine(border.TopBorder));
            }

            if (border.BottomBorder is not null)
            {
                WriteMessageIncludingEmpty(output, 4, WriteBorderLine(border.BottomBorder));
            }

            if (border.DiagonalBorder is not null)
            {
                WriteMessageIncludingEmpty(output, 5, WriteBorderLine(border.DiagonalBorder));
            }
            if (border.DiagonalUp?.Value is { } diagonalUp)
            {
                WriteBool(output, 6, diagonalUp);
            }

            if (border.DiagonalDown?.Value is { } diagonalDown)
            {
                WriteBool(output, 7, diagonalDown);
            }
        });
    }

    private static byte[] WriteBorderLine(S.BorderPropertiesType? border)
    {
        return Message(output =>
        {
            WriteString(output, 1, EnumText(border?.Style));
            WriteMessage(output, 2, WriteColor(border?.Color));
        });
    }

    private static byte[] WriteColor(OpenXmlElement? color)
    {
        return WriteColor(color, preserveAlpha: true);
    }

    private static byte[] WriteColor(OpenXmlElement? color, bool preserveAlpha)
    {
        var rgb = AttributeValue(color, "rgb");
        if (rgb.Length > 0)
        {
            return WriteColorValue(rgb, preserveAlpha);
        }

        var theme = AttributeValue(color, "theme");
        if (theme.Length > 0)
        {
            return Message(output =>
            {
                WriteInt32(output, 1, ColorTypeScheme);
                WriteString(output, 2, $"theme:{theme}");
                WriteColorTransform(output, color);
            });
        }

        var indexed = AttributeValue(color, "indexed");
        if (indexed.Length > 0)
        {
            return Message(output =>
            {
                WriteInt32(output, 1, 3);
                WriteString(output, 2, $"indexed:{indexed}");
                WriteColorTransform(output, color);
            });
        }

        return WriteColorValue("", preserveAlpha);
    }

    private static void WriteColorTransform(CodedOutputStream output, OpenXmlElement? color)
    {
        var tint = AttributeValue(color, "tint");
        if (!double.TryParse(tint, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var tintValue))
        {
            return;
        }

        WriteMessage(output, 3, Message(transformOutput =>
        {
            WriteInt32(transformOutput, 1, (int)Math.Round(tintValue * 100000));
        }));
    }

    private static byte[] WriteSparklineColor(OpenXmlElement? color)
    {
        return WriteColorValue(color is null ? "" : AttributeValue(color, "rgb"), preserveAlpha: true);
    }

    private static byte[] WriteColor(string? value)
    {
        return WriteColorValue(value, preserveAlpha: false);
    }

    private static byte[] WriteColorValue(string? value, bool preserveAlpha)
    {
        return Message(output =>
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var normalized = !preserveAlpha && value.Length == 8 ? value[2..] : value;
            WriteInt32(output, 1, 1);
            WriteString(output, 2, normalized);
        });
    }

    private static byte[] WriteColor(DrawingColorValue color)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, color.Type);
            WriteString(output, 2, color.Value);
            if (color.HasTransform)
            {
                WriteMessage(output, 3, Message(transformOutput =>
                {
                    WriteInt32(transformOutput, 1, color.Tint);
                    WriteInt32(transformOutput, 2, color.Shade);
                    WriteInt32(transformOutput, 3, color.LuminanceModulation);
                    WriteInt32(transformOutput, 4, color.LuminanceOffset);
                    WriteInt32(transformOutput, 5, color.SaturationModulation);
                    if (color.Alpha is not null)
                    {
                        WriteInt32IncludingZero(transformOutput, 6, color.Alpha.Value);
                    }
                }));
            }

            WriteString(output, 4, color.LastColor);
        });
    }

    private static DrawingColorValue? ColorFromDrawingElement(OpenXmlElement element)
    {
        var rgb = element.GetFirstChild<A.RgbColorModelHex>() ?? element.Descendants<A.RgbColorModelHex>().FirstOrDefault();
        if (rgb?.Val?.Value is { Length: > 0 } value)
        {
            return ColorValueFromDrawingElement(ColorTypeRgb, value, rgb, LastColor: null);
        }

        var scheme = element.GetFirstChild<A.SchemeColor>() ?? element.Descendants<A.SchemeColor>().FirstOrDefault();
        if (scheme?.Val?.Value is not null)
        {
            return ColorValueFromDrawingElement(ColorTypeScheme, EnumText(scheme.Val), scheme, LastColor: null);
        }

        var system = element.GetFirstChild<A.SystemColor>() ?? element.Descendants<A.SystemColor>().FirstOrDefault();
        if (system?.Val?.Value is not null)
        {
            return ColorValueFromDrawingElement(
                ColorTypeSystem,
                EnumText(system.Val),
                system,
                system.LastColor?.Value);
        }

        return null;
    }

    private static DrawingColorValue ColorValueFromDrawingElement(
        int type,
        string value,
        OpenXmlElement element,
        string? LastColor)
    {
        return new DrawingColorValue(
            type,
            value,
            LastColor,
            TintFrom(element),
            ShadeFrom(element),
            LuminanceModulationFrom(element),
            LuminanceOffsetFrom(element),
            SaturationModulationFrom(element),
            AlphaFrom(element));
    }

    private static int? AlphaFrom(OpenXmlElement element)
    {
        return element.GetFirstChild<A.Alpha>()?.Val?.Value;
    }

    private static int? TintFrom(OpenXmlElement element)
    {
        return element.GetFirstChild<A.Tint>()?.Val?.Value;
    }

    private static int? ShadeFrom(OpenXmlElement element)
    {
        return element.GetFirstChild<A.Shade>()?.Val?.Value;
    }

    private static int? LuminanceModulationFrom(OpenXmlElement element)
    {
        return element.GetFirstChild<A.LuminanceModulation>()?.Val?.Value;
    }

    private static int? LuminanceOffsetFrom(OpenXmlElement element)
    {
        return element.GetFirstChild<A.LuminanceOffset>()?.Val?.Value;
    }

    private static int? SaturationModulationFrom(OpenXmlElement element)
    {
        return element.GetFirstChild<A.SaturationModulation>()?.Val?.Value;
    }

    private static string ReadCellText(
        S.Cell cell,
        S.SharedStringItem? sharedStringItem,
        ClosedXmlFormulaValueProvider? formulaValues,
        string sheetName,
        string address)
    {
        var raw = cell.CellValue?.Text;
        if (string.IsNullOrEmpty(raw) && cell.CellFormula is null)
        {
            raw = cell.InnerText;
        }

        if (string.IsNullOrEmpty(raw))
        {
            if (cell.CellFormula is not null &&
                formulaValues?.TryGetFormulaValue(sheetName, address, out var formulaValue) == true)
            {
                return PreserveText(formulaValue);
            }

            return "";
        }

        if (cell.DataType?.Value == S.CellValues.SharedString)
        {
            var sharedText = PreserveText(sharedStringItem?.InnerText);
            return sharedText.Length > 0 ? sharedText : PreserveText(raw);
        }

        if (cell.DataType?.Value == S.CellValues.Boolean)
        {
            return raw == "1" ? "TRUE" : "FALSE";
        }

        return PreserveText(raw);
    }

    private static S.SharedStringItem? SharedStringItem(S.Cell cell, S.SharedStringTable? sharedStrings)
    {
        var raw = cell.CellValue?.Text;
        if (cell.DataType?.Value != S.CellValues.SharedString || !int.TryParse(raw, out var sharedStringIndex))
        {
            return null;
        }

        return sharedStrings?.Elements<S.SharedStringItem>().ElementAtOrDefault(sharedStringIndex);
    }

    private static IEnumerable<byte[]> ExtractCellParagraphs(
        S.Cell cell,
        S.SharedStringItem? sharedStringItem,
        S.Stylesheet? stylesheet)
    {
        var runs = sharedStringItem?.Elements<S.Run>().ToArray();
        if (runs is { Length: > 0 })
        {
            yield return WriteCellParagraph(runs, ResolveCellFont(cell, stylesheet));
            yield break;
        }

        var inlineRuns = cell.InlineString?.Elements<S.Run>().ToArray();
        if (inlineRuns is { Length: > 0 })
        {
            yield return WriteCellParagraph(inlineRuns, ResolveCellFont(cell, stylesheet));
        }
    }

    private static byte[] WriteCellParagraph(IReadOnlyList<S.Run> runs, S.Font? inheritedFont)
    {
        return Message(output =>
        {
            foreach (var run in runs)
            {
                var runText = PreserveText(run.Text?.Text);
                if (runText.Length == 0 && run.RunProperties is null)
                {
                    continue;
                }

                WriteMessage(output, 1, WriteCellTextRun(run, runText, inheritedFont));
            }
        });
    }

    private static byte[] WriteCellTextRun(S.Run run, string text, S.Font? inheritedFont)
    {
        return Message(output =>
        {
            WriteString(output, 1, text);
            if (ShouldWriteCellTextStyle(run.RunProperties))
            {
                WriteMessage(output, 2, WriteCellTextStyle(run.RunProperties!, inheritedFont));
            }
        });
    }

    private static bool ShouldWriteCellTextStyle(S.RunProperties? properties)
    {
        return properties is not null && (
            properties.GetFirstChild<S.Bold>() is not null ||
            properties.GetFirstChild<S.Italic>() is not null ||
            properties.GetFirstChild<S.FontSize>() is not null ||
            properties.GetFirstChild<S.Color>() is not null ||
            properties.GetFirstChild<S.Underline>() is not null ||
            properties.GetFirstChild<S.FontFamily>() is not null ||
            properties.GetFirstChild<S.RunFont>() is not null);
    }

    private static byte[] WriteCellTextStyle(S.RunProperties properties, S.Font? inheritedFont)
    {
        return Message(output =>
        {
            WriteBool(output, 4, IsOnOffElementPresentOrInherited(properties.GetFirstChild<S.Bold>(), inheritedFont?.Bold));
            WriteBool(output, 5, IsOnOffElementPresentOrInherited(properties.GetFirstChild<S.Italic>(), inheritedFont?.Italic));

            if (properties.GetFirstChild<S.FontSize>()?.Val?.Value is { } fontSize)
            {
                WriteInt32(output, 6, (int)Math.Round(fontSize));
            }

            var color = properties.GetFirstChild<S.Color>();
            if (color is not null)
            {
                WriteMessage(output, 7, WriteCellTextColorFill(color));
            }

            var underlineElement = properties.GetFirstChild<S.Underline>();
            var underline = underlineElement?.Val?.InnerText;
            if (underlineElement is not null && underline != "none")
            {
                WriteString(output, 9, string.IsNullOrEmpty(underline) ? "single" : underline);
            }

            var family = properties.GetFirstChild<S.FontFamily>()?.Val?.Value;
            if (family is not null)
            {
                WriteInt32(output, 16, family);
            }

            WriteString(output, 18, properties.GetFirstChild<S.RunFont>()?.Val?.Value ?? "");
        });
    }

    private static S.Font? ResolveCellFont(S.Cell cell, S.Stylesheet? stylesheet)
    {
        if (stylesheet?.CellFormats is null || cell.StyleIndex?.Value is not { } styleIndex)
        {
            return null;
        }

        var cellFormat = stylesheet.CellFormats.Elements<S.CellFormat>().ElementAtOrDefault((int)styleIndex);
        if (cellFormat?.FontId?.Value is not { } fontId)
        {
            return null;
        }

        return StylesheetFontEntries(stylesheet.Fonts).ElementAtOrDefault((int)fontId);
    }

    private static int CellFormulaType(S.CellFormula formula)
    {
        if (formula.FormulaType?.Value == S.CellFormulaValues.Array)
        {
            return 2;
        }

        if (formula.FormulaType?.Value == S.CellFormulaValues.DataTable)
        {
            return 3;
        }

        if (formula.FormulaType?.Value == S.CellFormulaValues.Shared)
        {
            return 4;
        }

        return 1;
    }

    private static bool ShouldWriteCellFormulaMetadata(S.CellFormula formula, string formulaText)
    {
        return formulaText.Length > 0 ||
            formula.SharedIndex?.Value is not null ||
            formula.FormulaType?.Value is not null ||
            !string.IsNullOrEmpty(formula.Reference?.Value);
    }

    private static int DataValidationType(string value)
    {
        return value switch
        {
            "none" => 1,
            "whole" => 2,
            "decimal" => 3,
            "list" => 4,
            "date" => 5,
            "time" => 6,
            "textLength" => 7,
            "custom" => 8,
            _ => 0,
        };
    }

    private static int DataValidationErrorStyle(string value)
    {
        return value switch
        {
            "stop" => 1,
            "warning" => 2,
            "information" => 3,
            _ => 0,
        };
    }

    private static int DataValidationImeMode(string value)
    {
        return value switch
        {
            "noControl" => 1,
            "off" => 2,
            "on" => 3,
            "disabled" => 4,
            "hiragana" => 5,
            "fullKatakana" => 6,
            "halfKatakana" => 7,
            "fullAlpha" => 8,
            "halfAlpha" => 9,
            "fullHangul" => 10,
            "halfHangul" => 11,
            _ => 0,
        };
    }

    private static int DataValidationOperator(string value)
    {
        return value switch
        {
            "between" => 1,
            "notBetween" => 2,
            "equal" => 3,
            "notEqual" => 4,
            "lessThan" => 5,
            "lessThanOrEqual" => 6,
            "greaterThan" => 7,
            "greaterThanOrEqual" => 8,
            _ => 0,
        };
    }

    private static int PivotAxis(string value)
    {
        return value switch
        {
            "axisRow" => 1,
            "axisCol" => 2,
            "axisPage" => 3,
            "axisValues" => 4,
            _ => 0,
        };
    }

    private static int FieldSort(string value)
    {
        return value switch
        {
            "manual" => 1,
            "ascending" => 2,
            "descending" => 3,
            _ => 0,
        };
    }

    private static int DataConsolidateFunction(string value)
    {
        return value switch
        {
            "sum" => 1,
            "average" => 2,
            "count" => 3,
            "countNums" => 4,
            "max" => 5,
            "min" => 6,
            "product" => 7,
            "stdDev" => 8,
            "stdDevP" => 9,
            "var" => 10,
            "varP" => 11,
            _ => 0,
        };
    }

    private static int PivotFilterType(string value)
    {
        return value switch
        {
            "unknown" => 1,
            "count" => 2,
            "percent" => 3,
            "sum" => 4,
            "captionEqual" => 5,
            "captionNotEqual" => 6,
            "captionBeginsWith" => 7,
            "captionEndsWith" => 8,
            "captionContains" => 9,
            "valueEqual" => 10,
            "valueNotEqual" => 11,
            "valueGreaterThan" => 12,
            "valueLessThan" => 13,
            "dateEqual" => 14,
            "today" => 15,
            "yesterday" => 16,
            "tomorrow" => 17,
            "thisMonth" => 18,
            "lastMonth" => 19,
            "nextMonth" => 20,
            "thisYear" => 21,
            "lastYear" => 22,
            "nextYear" => 23,
            "yearToDate" => 24,
            _ => 0,
        };
    }

    private static int CellDataType(S.Cell cell)
    {
        if (cell.DataType?.Value == S.CellValues.SharedString) return 1;
        if (cell.DataType?.Value == S.CellValues.InlineString) return 2;
        if (cell.DataType?.Value == S.CellValues.String) return 3;
        if (cell.DataType?.Value == S.CellValues.Boolean) return 4;
        if (cell.DataType?.Value == S.CellValues.Number) return 5;
        if (cell.DataType?.Value == S.CellValues.Error) return 6;
        return 0;
    }

    private static bool ShouldWriteCellDataType(S.Cell cell, string formulaText)
    {
        return cell.DataType is not null || !IsExternalWorkbookFormula(formulaText);
    }

    private static bool IsExternalWorkbookFormula(string formulaText)
    {
        var trimmed = formulaText.TrimStart();
        return trimmed.StartsWith("[", StringComparison.Ordinal) ||
            trimmed.StartsWith("'[", StringComparison.Ordinal);
    }

    private static bool IsOnOffElementEnabled(OpenXmlElement? element)
    {
        if (element is null)
        {
            return false;
        }

        var value = AttributeValue(element, "val");
        return value.Length == 0 || value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOnOffElementPresentOrInherited(OpenXmlElement? element, OpenXmlElement? inheritedElement)
    {
        return element is not null || inheritedElement is not null;
    }

    private static string ConditionalRuleId(S.ConditionalFormattingRule rule)
    {
        return TextNormalization.Clean(rule.Descendants()
            .FirstOrDefault(element => element.LocalName == "id")
            ?.InnerText);
    }

    private static (string StartAddress, string EndAddress) SplitCellRange(string reference)
    {
        var normalized = reference.Contains('!') ? reference[(reference.LastIndexOf('!') + 1)..] : reference;
        normalized = normalized.Replace("$", "", StringComparison.Ordinal).Trim();
        if (normalized.Length == 0)
        {
            return ("", "");
        }

        var parts = normalized.Split(':', 2, StringSplitOptions.TrimEntries);
        var startAddress = parts[0];
        var endAddress = parts.Length > 1 ? parts[1] : parts[0];
        return (startAddress, endAddress);
    }

    private static IReadOnlyList<string> SplitReferences(string value)
    {
        return value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    private static IReadOnlyList<int> PivotFieldIndexes(OpenXmlElement? fields)
    {
        return fields?.Elements()
            .Where(field => field.LocalName == "field")
            .Select(field => Int32Attribute(field, "x"))
            .Where(index => index is not null)
            .Select(index => index!.Value)
            .ToArray() ?? [];
    }

    private static IReadOnlyList<int> SlicerPivotTableIds(OpenXmlElement definition)
    {
        return ChildByLocalName(definition, "pivotTables")?.Elements()
            .Where(table => table.LocalName == "pivotTable")
            .Select(table => Int32Attribute(table, "tabId"))
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToArray() ?? [];
    }

    private static string PivotRefreshedDate(S.PivotCacheDefinition definition)
    {
        var refreshedDate = AttributeValue(definition, "refreshedDate");
        return refreshedDate.Length > 0 ? refreshedDate : AttributeValue(definition, "refreshedDateIso");
    }

    private static string SlicerCrossFilterText(OpenXmlElement? cacheData)
    {
        return AttributeValue(cacheData, "crossFilter").Length == 0 ? "" : "SlicerCacheCrossFilterValues { }";
    }

    private static string SlicerSortOrderText(OpenXmlElement? cacheData)
    {
        return AttributeValue(cacheData, "sortOrder").Length == 0 ? "" : "TabularSlicerCacheSortOrderValues { }";
    }

    private static int SparklineType(string? value)
    {
        return value switch
        {
            "line" => 1,
            "column" => 2,
            "stacked" => 3,
            _ => 0,
        };
    }

    private static int SparklineAxisType(string? value)
    {
        return value switch
        {
            "individual" => 1,
            "group" => 2,
            "custom" => 3,
            _ => 0,
        };
    }

    private static int SparklineDisplayEmptyCellsAs(string? value)
    {
        return value switch
        {
            "span" => 1,
            "gap" => 2,
            "zero" => 3,
            _ => 0,
        };
    }

    private static string EnumText(OpenXmlSimpleType? value)
    {
        return value?.InnerText ?? "";
    }

    private static OpenXmlElement? ChildByLocalName(OpenXmlElement? element, string localName)
    {
        return element?.Elements().FirstOrDefault(child => child.LocalName == localName);
    }

    private static bool IsElement(OpenXmlElement element, string localName, string namespaceUri)
    {
        return string.Equals(element.LocalName, localName, StringComparison.Ordinal) &&
            string.Equals(element.NamespaceUri, namespaceUri, StringComparison.Ordinal);
    }

    private static string AttributeValue(OpenXmlElement? element, string localName)
    {
        if (element is null)
        {
            return "";
        }

        return element.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == localName).Value ?? "";
    }

    private static string PreserveText(string? value)
    {
        return value ?? "";
    }

    private static double? DoubleAttribute(OpenXmlElement? element, string localName)
    {
        var value = AttributeValue(element, localName);
        if (value.Length == 0)
        {
            return null;
        }

        return double.TryParse(
            value,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }

    private static int? Int32Attribute(OpenXmlElement? element, string localName)
    {
        var value = AttributeValue(element, localName);
        if (value.Length == 0)
        {
            return null;
        }

        return int.TryParse(
            value,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }

    private static bool? BoolAttribute(OpenXmlElement? element, string localName)
    {
        var value = AttributeValue(element, localName);
        if (value.Length == 0)
        {
            return null;
        }

        return value switch
        {
            "1" => true,
            "0" => false,
            _ when bool.TryParse(value, out var parsed) => parsed,
            _ => null,
        };
    }

    private static string ExtendedAttributeValue(OpenXmlElement element, string localName)
    {
        return element.ExtendedAttributes.FirstOrDefault(attribute => attribute.LocalName == localName).Value ?? "";
    }

    private static int? ToInt32(Int64Value? value)
    {
        if (value is null)
        {
            return null;
        }

        return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, value.Value));
    }

    private static long? ToLong(Int64Value? value)
    {
        return value?.Value;
    }

    private static string NormalizeImageContentType(string contentType)
    {
        return string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase) ? "image/jpg" : contentType;
    }

    private static byte[] Message(Action<CodedOutputStream> write)
    {
        using var stream = new MemoryStream();
        var output = new CodedOutputStream(stream);
        write(output);
        output.Flush();
        return stream.ToArray();
    }

    private static void WriteMessage(CodedOutputStream output, int fieldNumber, byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.LengthDelimited);
        output.WriteBytes(ByteString.CopyFrom(bytes));
    }

    private static void WriteMessageIncludingEmpty(CodedOutputStream output, int fieldNumber, byte[] bytes)
    {
        output.WriteTag(fieldNumber, WireFormat.WireType.LengthDelimited);
        output.WriteBytes(ByteString.CopyFrom(bytes));
    }

    private static void WriteBytes(CodedOutputStream output, int fieldNumber, byte[] value)
    {
        if (value.Length == 0)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.LengthDelimited);
        output.WriteBytes(ByteString.CopyFrom(value));
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

    private static void WriteStringIncludingEmpty(CodedOutputStream output, int fieldNumber, string value)
    {
        output.WriteTag(fieldNumber, WireFormat.WireType.LengthDelimited);
        output.WriteString(value);
    }

    private static void WriteInt32(CodedOutputStream output, int fieldNumber, int value)
    {
        if (value == 0)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteInt32(value);
    }

    private static void WriteInt32(CodedOutputStream output, int fieldNumber, int? value)
    {
        if (value is null or 0)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteInt32(value.Value);
    }

    private static void WriteInt32IncludingZero(CodedOutputStream output, int fieldNumber, int value)
    {
        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteInt32(value);
    }

    private static void WriteInt32Value(CodedOutputStream output, int fieldNumber, int? value)
    {
        if (value is null)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteInt32(value.Value);
    }

    private static void WriteInt64(CodedOutputStream output, int fieldNumber, long value)
    {
        if (value == 0)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteInt64(value);
    }

    private static void WriteInt64IncludingZero(CodedOutputStream output, int fieldNumber, long value)
    {
        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteInt64(value);
    }

    private static void WriteInt64(CodedOutputStream output, int fieldNumber, long? value)
    {
        if (value is null or 0)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteInt64(value.Value);
    }

    private static void WritePackedDoubles(CodedOutputStream output, int fieldNumber, IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        var bytes = Message(inner =>
        {
            foreach (var value in values)
            {
                inner.WriteDouble(value);
            }
        });
        WriteMessage(output, fieldNumber, bytes);
    }

    private static void WritePackedInt32s(CodedOutputStream output, int fieldNumber, IReadOnlyList<int> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        var bytes = Message(inner =>
        {
            foreach (var value in values)
            {
                inner.WriteInt32(value);
            }
        });
        WriteMessage(output, fieldNumber, bytes);
    }

    private static void WriteFloat(CodedOutputStream output, int fieldNumber, float value)
    {
        if (value <= 0)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Fixed32);
        output.WriteFloat(value);
    }

    private static void WriteFloatIncludingZero(CodedOutputStream output, int fieldNumber, float value)
    {
        output.WriteTag(fieldNumber, WireFormat.WireType.Fixed32);
        output.WriteFloat(value);
    }

    private static void WriteDouble(CodedOutputStream output, int fieldNumber, double value)
    {
        if (value == 0)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Fixed64);
        output.WriteDouble(value);
    }

    private static void WriteDoubleValue(CodedOutputStream output, int fieldNumber, double? value)
    {
        if (value is null)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Fixed64);
        output.WriteDouble(value.Value);
    }

    private static void WriteBool(CodedOutputStream output, int fieldNumber, bool value)
    {
        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteBool(value);
    }

    private static void WriteBoolValue(CodedOutputStream output, int fieldNumber, bool? value)
    {
        if (value is null)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteBool(value.Value);
    }

    private sealed record WorksheetImageReference(string Id, ImagePart Part);

    private sealed record WorksheetSlicerAnchor(string Name, Xdr.MarkerType? From, Xdr.MarkerType? To);

    private sealed record WorksheetCommentSheet(WorksheetPart Part, string SheetName);

    private sealed record WorkbookPerson(string Id, string DisplayName);

    private sealed record ThreadReadModel(
        string Id,
        string SheetName,
        string Address,
        int Status,
        IReadOnlyList<ThreadedCommentReadModel> Comments);

    private sealed record ThreadedCommentReadModel(
        string Id,
        string ParentId,
        string AuthorId,
        string CreatedAt,
        string Body,
        string Address,
        bool Done);

    private sealed record DrawingColorValue(
        int Type,
        string Value,
        string? LastColor,
        int? Tint,
        int? Shade,
        int? LuminanceModulation,
        int? LuminanceOffset,
        int? SaturationModulation,
        int? Alpha)
    {
        public bool HasTransform =>
            Tint is not null ||
            Shade is not null ||
            LuminanceModulation is not null ||
            LuminanceOffset is not null ||
            SaturationModulation is not null ||
            Alpha is not null;
    }

    private sealed record ChartReadModel(
        string Title,
        int Type,
        bool HasLegend,
        int LegendPosition,
        ChartAxisReadModel? CategoryAxis,
        ChartAxisReadModel? ValueAxis,
        byte[]? ChartSpaceLine,
        bool HasTitleTextStyle,
        bool HasDataLabels,
        bool? RoundedCorners,
        bool HasView3D,
        int BarDirection,
        int? BarGrouping,
        bool? BarVaryColors,
        int? BarGapWidth,
        int? BarOverlap,
        int? AreaGrouping,
        bool? AreaVaryColors,
        int? PieFirstSliceAngle,
        bool? PieVaryColors,
        int? DoughnutHoleSize,
        int? DoughnutFirstSliceAngle,
        bool? DoughnutVaryColors,
        int? ScatterStyle,
        bool? ScatterVaryColors,
        bool? BubbleIs3D,
        int? BubbleScale,
        bool? BubbleShowNegative,
        bool? BubbleVaryColors,
        int? RadarStyle,
        bool? RadarVaryColors,
        bool? SurfaceWireframe,
        IReadOnlyList<ChartSeriesReadModel> Series);

    private sealed record ChartAxisReadModel(
        int Position,
        string Title,
        byte[]? MajorGridline,
        string? NumberFormatCode,
        bool? NumberFormatSourceLinked,
        int? MajorTickMark,
        int? MinorTickMark,
        int? TickLabelPosition,
        bool? Deleted);

    private sealed record ChartSeriesReadModel(
        string Name,
        IReadOnlyList<string> Categories,
        IReadOnlyList<double> Values,
        bool HasMarker);
}
