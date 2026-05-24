namespace Routa.OfficeWasmReader;

internal sealed class OfficeArtifactModel
{
    public string SourceKind { get; init; } = "";
    public string Title { get; set; } = "";
    public List<TextBlockModel> TextBlocks { get; } = [];
    public List<SheetModel> Sheets { get; } = [];
    public List<SlideModel> Slides { get; } = [];
    public List<DiagnosticModel> Diagnostics { get; } = [];
    public Dictionary<string, string> Metadata { get; } = [];
    public List<ImageAssetModel> Images { get; } = [];
    public List<TableModel> Tables { get; } = [];
    public List<ChartModel> Charts { get; } = [];
    public SpreadsheetStylesModel Styles { get; } = new();
    public List<SpreadsheetShapeModel> Shapes { get; } = [];
}

internal sealed record TextBlockModel(string Path, string Text);

internal sealed class SheetModel
{
    public string Name { get; init; } = "";
    public List<RowModel> Rows { get; } = [];
    public List<MergedRangeModel> MergedRanges { get; } = [];
    public List<SheetTableModel> Tables { get; } = [];
    public List<DataValidationModel> DataValidations { get; } = [];
    public List<ConditionalFormatModel> ConditionalFormats { get; } = [];
    public List<ColumnModel> Columns { get; } = [];
    public double DefaultColWidth { get; set; }
    public double DefaultRowHeight { get; set; }
}

internal sealed class TableModel
{
    public string Path { get; init; } = "";
    public List<RowModel> Rows { get; } = [];
}

internal sealed class RowModel
{
    public List<CellModel> Cells { get; } = [];
    public uint Index { get; set; }
    public double Height { get; set; }
}

internal sealed record CellModel(
    string Address,
    string Text,
    string Formula,
    string DataType = "",
    uint StyleIndex = 0,
    bool HasValue = true);

internal sealed record ColumnModel(uint Min, uint Max, double Width, bool Hidden);

internal sealed class SlideModel
{
    public uint Index { get; init; }
    public string Title { get; set; } = "";
    public List<TextBlockModel> TextBlocks { get; } = [];
}

internal sealed record DiagnosticModel(string Level, string Message);

internal sealed record ImageAssetModel(string Id, string Path, string ContentType, byte[] Bytes);

internal sealed record ChartModel(
    string Id,
    string Path,
    string Title,
    string ChartType,
    string SheetName = "",
    ChartAnchorModel? Anchor = null,
    IReadOnlyList<ChartSeriesModel>? Series = null);

internal sealed record ChartAnchorModel(
    uint FromCol,
    uint FromRow,
    uint ToCol,
    uint ToRow,
    double FromColOffsetEmu,
    double FromRowOffsetEmu,
    double ToColOffsetEmu,
    double ToRowOffsetEmu);

internal sealed record ChartSeriesModel(
    string Label,
    IReadOnlyList<string> Categories,
    IReadOnlyList<double> Values,
    string Color);

internal sealed record SpreadsheetShapeModel(
    string Id,
    string SheetName,
    uint FromCol,
    uint FromRow,
    double FromColOffsetEmu,
    double FromRowOffsetEmu,
    double WidthEmu,
    double HeightEmu,
    string FillColor,
    string LineColor,
    string Text,
    string Geometry);

internal sealed record MergedRangeModel(string Reference);

internal sealed record SheetTableModel(string Name, string Reference, string Style = "", bool ShowFilterButton = true);

internal sealed record DataValidationModel(
    string Type,
    string Operator,
    string Formula1,
    string Formula2,
    IReadOnlyList<string> Ranges);

internal sealed record ConditionalFormatModel(
    string Type,
    uint Priority,
    IReadOnlyList<string> Ranges,
    string Operator = "",
    IReadOnlyList<string>? Formulas = null,
    string Text = "",
    string FillColor = "",
    string FontColor = "",
    bool Bold = false,
    ColorScaleModel? ColorScale = null,
    DataBarModel? DataBar = null,
    IconSetModel? IconSet = null);

internal sealed class SpreadsheetStylesModel
{
    public List<NumberFormatModel> NumberFormats { get; } = [];
    public List<CellFormatModel> CellFormats { get; } = [];
    public List<FontStyleModel> Fonts { get; } = [];
    public List<FillStyleModel> Fills { get; } = [];
    public List<BorderStyleModel> Borders { get; } = [];
}

internal sealed record NumberFormatModel(uint Id, string FormatCode);

internal sealed record CellFormatModel(
    uint NumFmtId,
    uint FontId,
    uint FillId,
    uint BorderId,
    string HorizontalAlignment,
    string VerticalAlignment);

internal sealed record FontStyleModel(bool Bold, bool Italic, double FontSize, string Typeface, string Color);

internal sealed record FillStyleModel(string Color);

internal sealed record BorderStyleModel(string BottomColor);

internal sealed record ColorScaleModel(IReadOnlyList<string> Colors);

internal sealed record DataBarModel(string Color);

internal sealed record IconSetModel(string Name, bool ShowValue, bool Reverse);
