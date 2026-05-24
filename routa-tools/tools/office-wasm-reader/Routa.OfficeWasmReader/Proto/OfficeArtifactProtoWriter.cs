using Google.Protobuf;

namespace Routa.OfficeWasmReader;

internal static class OfficeArtifactProtoWriter
{
    public static byte[] Write(OfficeArtifactModel artifact)
    {
        return Message(output =>
        {
            WriteString(output, 1, artifact.SourceKind);
            WriteString(output, 2, artifact.Title);

            foreach (var block in artifact.TextBlocks)
            {
                WriteMessage(output, 3, WriteTextBlock(block));
            }

            foreach (var sheet in artifact.Sheets)
            {
                WriteMessage(output, 4, WriteSheet(sheet));
            }

            foreach (var slide in artifact.Slides)
            {
                WriteMessage(output, 5, WriteSlide(slide));
            }

            foreach (var diagnostic in artifact.Diagnostics)
            {
                WriteMessage(output, 6, WriteDiagnostic(diagnostic));
            }

            foreach (var item in artifact.Metadata.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                WriteMessage(output, 7, WriteMetadata(item.Key, item.Value));
            }

            foreach (var image in artifact.Images)
            {
                WriteMessage(output, 8, WriteImage(image));
            }

            foreach (var table in artifact.Tables)
            {
                WriteMessage(output, 9, WriteTable(table));
            }

            foreach (var chart in artifact.Charts)
            {
                WriteMessage(output, 10, WriteChart(chart));
            }

            if (artifact.Styles.NumberFormats.Count > 0 ||
                artifact.Styles.CellFormats.Count > 0 ||
                artifact.Styles.Fonts.Count > 0 ||
                artifact.Styles.Fills.Count > 0 ||
                artifact.Styles.Borders.Count > 0)
            {
                WriteMessage(output, 11, WriteSpreadsheetStyles(artifact.Styles));
            }

            foreach (var shape in artifact.Shapes)
            {
                WriteMessage(output, 12, WriteSpreadsheetShape(shape));
            }
        });
    }

    private static byte[] WriteTextBlock(TextBlockModel block)
    {
        return Message(output =>
        {
            WriteString(output, 1, block.Path);
            WriteString(output, 2, block.Text);
        });
    }

    private static byte[] WriteSheet(SheetModel sheet)
    {
        return Message(output =>
        {
            WriteString(output, 1, sheet.Name);
            foreach (var row in sheet.Rows)
            {
                WriteMessage(output, 2, WriteRow(row));
            }

            foreach (var range in sheet.MergedRanges)
            {
                WriteMessage(output, 3, WriteMergedRange(range));
            }

            foreach (var table in sheet.Tables)
            {
                WriteMessage(output, 4, WriteSheetTable(table));
            }

            foreach (var validation in sheet.DataValidations)
            {
                WriteMessage(output, 5, WriteDataValidation(validation));
            }

            foreach (var format in sheet.ConditionalFormats)
            {
                WriteMessage(output, 6, WriteConditionalFormat(format));
            }

            foreach (var column in sheet.Columns)
            {
                WriteMessage(output, 7, WriteColumn(column));
            }

            WriteDouble(output, 8, sheet.DefaultColWidth);
            WriteDouble(output, 9, sheet.DefaultRowHeight);
        });
    }

    private static byte[] WriteTable(TableModel table)
    {
        return Message(output =>
        {
            WriteString(output, 1, table.Path);
            foreach (var row in table.Rows)
            {
                WriteMessage(output, 2, WriteRow(row));
            }
        });
    }

    private static byte[] WriteRow(RowModel row)
    {
        return Message(output =>
        {
            foreach (var cell in row.Cells)
            {
                WriteMessage(output, 1, WriteCell(cell));
            }

            WriteUInt32(output, 2, row.Index);
            WriteDouble(output, 3, row.Height);
        });
    }

    private static byte[] WriteCell(CellModel cell)
    {
        return Message(output =>
        {
            WriteString(output, 1, cell.Address);
            WriteString(output, 2, cell.Text);
            WriteString(output, 3, cell.Formula);
            WriteString(output, 4, cell.DataType);
            WriteUInt32(output, 5, cell.StyleIndex);
            WriteBoolAlways(output, 6, cell.HasValue);
        });
    }

    private static byte[] WriteColumn(ColumnModel column)
    {
        return Message(output =>
        {
            WriteUInt32(output, 1, column.Min);
            WriteUInt32(output, 2, column.Max);
            WriteDouble(output, 3, column.Width);
            WriteBool(output, 4, column.Hidden);
        });
    }

    private static byte[] WriteSlide(SlideModel slide)
    {
        return Message(output =>
        {
            WriteUInt32(output, 1, slide.Index);
            WriteString(output, 2, slide.Title);
            foreach (var block in slide.TextBlocks)
            {
                WriteMessage(output, 3, WriteTextBlock(block));
            }
        });
    }

    private static byte[] WriteDiagnostic(DiagnosticModel diagnostic)
    {
        return Message(output =>
        {
            WriteString(output, 1, diagnostic.Level);
            WriteString(output, 2, diagnostic.Message);
        });
    }

    private static byte[] WriteMetadata(string key, string value)
    {
        return Message(output =>
        {
            WriteString(output, 1, key);
            WriteString(output, 2, value);
        });
    }

    private static byte[] WriteImage(ImageAssetModel image)
    {
        return Message(output =>
        {
            WriteString(output, 1, image.Id);
            WriteString(output, 2, image.Path);
            WriteString(output, 3, image.ContentType);
            WriteBytes(output, 4, image.Bytes);
        });
    }

    private static byte[] WriteChart(ChartModel chart)
    {
        return Message(output =>
        {
            WriteString(output, 1, chart.Id);
            WriteString(output, 2, chart.Path);
            WriteString(output, 3, chart.Title);
            WriteString(output, 4, chart.ChartType);
            WriteString(output, 5, chart.SheetName);
            if (chart.Anchor is not null)
            {
                WriteMessage(output, 6, WriteChartAnchor(chart.Anchor));
            }

            foreach (var series in chart.Series ?? [])
            {
                WriteMessage(output, 7, WriteChartSeries(series));
            }
        });
    }

    private static byte[] WriteChartAnchor(ChartAnchorModel anchor)
    {
        return Message(output =>
        {
            WriteUInt32(output, 1, anchor.FromCol);
            WriteUInt32(output, 2, anchor.FromRow);
            WriteUInt32(output, 3, anchor.ToCol);
            WriteUInt32(output, 4, anchor.ToRow);
            WriteDouble(output, 5, anchor.FromColOffsetEmu);
            WriteDouble(output, 6, anchor.FromRowOffsetEmu);
            WriteDouble(output, 7, anchor.ToColOffsetEmu);
            WriteDouble(output, 8, anchor.ToRowOffsetEmu);
        });
    }

    private static byte[] WriteChartSeries(ChartSeriesModel series)
    {
        return Message(output =>
        {
            WriteString(output, 1, series.Label);
            foreach (var category in series.Categories)
            {
                WriteString(output, 2, category);
            }

            foreach (var value in series.Values)
            {
                WriteDoubleAlways(output, 3, value);
            }

            WriteString(output, 4, series.Color);
        });
    }

    private static byte[] WriteSpreadsheetShape(SpreadsheetShapeModel shape)
    {
        return Message(output =>
        {
            WriteString(output, 1, shape.Id);
            WriteString(output, 2, shape.SheetName);
            WriteUInt32(output, 3, shape.FromCol);
            WriteUInt32(output, 4, shape.FromRow);
            WriteDouble(output, 5, shape.FromColOffsetEmu);
            WriteDouble(output, 6, shape.FromRowOffsetEmu);
            WriteDouble(output, 7, shape.WidthEmu);
            WriteDouble(output, 8, shape.HeightEmu);
            WriteString(output, 9, shape.FillColor);
            WriteString(output, 10, shape.LineColor);
            WriteString(output, 11, shape.Text);
            WriteString(output, 12, shape.Geometry);
        });
    }

    private static byte[] WriteMergedRange(MergedRangeModel range)
    {
        return Message(output =>
        {
            WriteString(output, 1, range.Reference);
        });
    }

    private static byte[] WriteSheetTable(SheetTableModel table)
    {
        return Message(output =>
        {
            WriteString(output, 1, table.Name);
            WriteString(output, 2, table.Reference);
            WriteString(output, 3, table.Style);
            WriteBool(output, 4, table.ShowFilterButton);
        });
    }

    private static byte[] WriteDataValidation(DataValidationModel validation)
    {
        return Message(output =>
        {
            WriteString(output, 1, validation.Type);
            WriteString(output, 2, validation.Operator);
            WriteString(output, 3, validation.Formula1);
            WriteString(output, 4, validation.Formula2);
            foreach (var range in validation.Ranges)
            {
                WriteString(output, 5, range);
            }
        });
    }

    private static byte[] WriteConditionalFormat(ConditionalFormatModel format)
    {
        return Message(output =>
        {
            WriteString(output, 1, format.Type);
            WriteUInt32(output, 2, format.Priority);
            foreach (var range in format.Ranges)
            {
                WriteString(output, 3, range);
            }

            WriteString(output, 4, format.Operator);
            foreach (var formula in format.Formulas ?? [])
            {
                WriteString(output, 5, formula);
            }

            WriteString(output, 6, format.Text);
            WriteString(output, 7, format.FillColor);
            WriteString(output, 8, format.FontColor);
            WriteBool(output, 9, format.Bold);
            if (format.ColorScale is not null)
            {
                WriteMessage(output, 10, WriteColorScale(format.ColorScale));
            }

            if (format.DataBar is not null)
            {
                WriteMessage(output, 11, WriteDataBar(format.DataBar));
            }

            if (format.IconSet is not null)
            {
                WriteMessage(output, 12, WriteIconSet(format.IconSet));
            }
        });
    }

    private static byte[] WriteSpreadsheetStyles(SpreadsheetStylesModel styles)
    {
        return Message(output =>
        {
            foreach (var format in styles.NumberFormats)
            {
                WriteMessage(output, 1, WriteNumberFormat(format));
            }

            foreach (var format in styles.CellFormats)
            {
                WriteMessage(output, 2, WriteCellFormat(format));
            }

            foreach (var font in styles.Fonts)
            {
                WriteMessage(output, 3, WriteFontStyle(font));
            }

            foreach (var fill in styles.Fills)
            {
                WriteMessage(output, 4, WriteFillStyle(fill));
            }

            foreach (var border in styles.Borders)
            {
                WriteMessage(output, 5, WriteBorderStyle(border));
            }
        });
    }

    private static byte[] WriteNumberFormat(NumberFormatModel format)
    {
        return Message(output =>
        {
            WriteUInt32(output, 1, format.Id);
            WriteString(output, 2, format.FormatCode);
        });
    }

    private static byte[] WriteCellFormat(CellFormatModel format)
    {
        return Message(output =>
        {
            WriteUInt32(output, 1, format.NumFmtId);
            WriteUInt32(output, 2, format.FontId);
            WriteUInt32(output, 3, format.FillId);
            WriteUInt32(output, 4, format.BorderId);
            WriteString(output, 5, format.HorizontalAlignment);
            WriteString(output, 6, format.VerticalAlignment);
        });
    }

    private static byte[] WriteFontStyle(FontStyleModel font)
    {
        return Message(output =>
        {
            WriteBool(output, 1, font.Bold);
            WriteBool(output, 2, font.Italic);
            WriteDouble(output, 3, font.FontSize);
            WriteString(output, 4, font.Typeface);
            WriteString(output, 5, font.Color);
        });
    }

    private static byte[] WriteFillStyle(FillStyleModel fill)
    {
        return Message(output =>
        {
            WriteString(output, 1, fill.Color);
        });
    }

    private static byte[] WriteBorderStyle(BorderStyleModel border)
    {
        return Message(output =>
        {
            WriteString(output, 1, border.BottomColor);
        });
    }

    private static byte[] WriteColorScale(ColorScaleModel colorScale)
    {
        return Message(output =>
        {
            foreach (var color in colorScale.Colors)
            {
                WriteString(output, 1, color);
            }
        });
    }

    private static byte[] WriteDataBar(DataBarModel dataBar)
    {
        return Message(output =>
        {
            WriteString(output, 1, dataBar.Color);
        });
    }

    private static byte[] WriteIconSet(IconSetModel iconSet)
    {
        return Message(output =>
        {
            WriteString(output, 1, iconSet.Name);
            WriteBool(output, 2, iconSet.ShowValue);
            WriteBool(output, 3, iconSet.Reverse);
        });
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
        output.WriteTag(fieldNumber, WireFormat.WireType.LengthDelimited);
        output.WriteBytes(ByteString.CopyFrom(bytes));
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

    private static void WriteBytes(CodedOutputStream output, int fieldNumber, byte[]? value)
    {
        if (value is null || value.Length == 0)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.LengthDelimited);
        output.WriteBytes(ByteString.CopyFrom(value));
    }

    private static void WriteUInt32(CodedOutputStream output, int fieldNumber, uint value)
    {
        if (value == 0)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteUInt32(value);
    }

    private static void WriteDouble(CodedOutputStream output, int fieldNumber, double value)
    {
        if (value <= 0)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Fixed64);
        output.WriteDouble(value);
    }

    private static void WriteDoubleAlways(CodedOutputStream output, int fieldNumber, double value)
    {
        output.WriteTag(fieldNumber, WireFormat.WireType.Fixed64);
        output.WriteDouble(value);
    }

    private static void WriteBool(CodedOutputStream output, int fieldNumber, bool value)
    {
        if (!value)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteBool(value);
    }

    private static void WriteBoolAlways(CodedOutputStream output, int fieldNumber, bool value)
    {
        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteBool(value);
    }
}
