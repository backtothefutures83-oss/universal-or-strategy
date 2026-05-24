using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace Routa.OfficeWasmReader;

internal static class OpenXmlChartReader
{
    public static ChartModel? Read(OpenXmlPartContainer container, ChartPart chartPart, string path, string sheetName = "")
    {
        var relationshipId = container.GetIdOfPart(chartPart);
        if (string.IsNullOrEmpty(relationshipId))
        {
            return null;
        }

        var chartSpace = chartPart.ChartSpace;
        if (chartSpace is null)
        {
            return new ChartModel(relationshipId, path, "", "unknown", sheetName, FindAnchor(container, relationshipId), []);
        }

        var title = TextNormalization.Clean(string.Concat(
            chartSpace.Descendants<C.Title>().FirstOrDefault()?.Descendants<A.Text>().Select(item => item.Text) ??
            Enumerable.Empty<string>()));

        return new ChartModel(
            relationshipId,
            path,
            title,
            DetectChartType(chartSpace),
            sheetName,
            FindAnchor(container, relationshipId),
            ReadSeries(chartSpace));
    }

    private static string DetectChartType(C.ChartSpace chartSpace)
    {
        if (chartSpace.Descendants<C.BarChart>().Any()) return "bar";
        if (chartSpace.Descendants<C.LineChart>().Any()) return "line";
        if (chartSpace.Descendants<C.PieChart>().Any()) return "pie";
        if (chartSpace.Descendants<C.AreaChart>().Any()) return "area";
        if (chartSpace.Descendants<C.ScatterChart>().Any()) return "scatter";
        if (chartSpace.Descendants<C.BubbleChart>().Any()) return "bubble";
        if (chartSpace.Descendants<C.DoughnutChart>().Any()) return "doughnut";
        if (chartSpace.Descendants<C.RadarChart>().Any()) return "radar";
        if (chartSpace.Descendants<C.SurfaceChart>().Any()) return "surface";
        return "unknown";
    }

    private static ChartAnchorModel? FindAnchor(OpenXmlPartContainer container, string relationshipId)
    {
        if (container is not DrawingsPart drawingPart)
        {
            return null;
        }

        var anchor = drawingPart.WorksheetDrawing?.Elements<Xdr.TwoCellAnchor>()
            .FirstOrDefault(item => item.Descendants<C.ChartReference>().Any(reference => reference.Id?.Value == relationshipId));
        if (anchor is null)
        {
            return FindOneCellAnchor(drawingPart, relationshipId);
        }

        var from = anchor.FromMarker;
        var to = anchor.ToMarker;
        return new ChartAnchorModel(
            ParseUInt(from?.ColumnId?.Text),
            ParseUInt(from?.RowId?.Text),
            ParseUInt(to?.ColumnId?.Text),
            ParseUInt(to?.RowId?.Text),
            ParseDouble(from?.ColumnOffset?.Text),
            ParseDouble(from?.RowOffset?.Text),
            ParseDouble(to?.ColumnOffset?.Text),
            ParseDouble(to?.RowOffset?.Text));
    }

    private static ChartAnchorModel? FindOneCellAnchor(DrawingsPart drawingPart, string relationshipId)
    {
        var anchor = drawingPart.WorksheetDrawing?.Elements<Xdr.OneCellAnchor>()
            .FirstOrDefault(item => item.Descendants<C.ChartReference>().Any(reference => reference.Id?.Value == relationshipId));
        if (anchor is null)
        {
            return null;
        }

        var from = anchor.FromMarker;
        var fromCol = ParseUInt(from?.ColumnId?.Text);
        var fromRow = ParseUInt(from?.RowId?.Text);
        var ext = anchor.Extent;
        return new ChartAnchorModel(
            fromCol,
            fromRow,
            fromCol,
            fromRow,
            ParseDouble(from?.ColumnOffset?.Text),
            ParseDouble(from?.RowOffset?.Text),
            ext?.Cx?.Value ?? 0,
            ext?.Cy?.Value ?? 0);
    }

    private static IReadOnlyList<ChartSeriesModel> ReadSeries(C.ChartSpace chartSpace)
    {
        var series = chartSpace.Descendants<C.BarChartSeries>()
            .Select((item, index) => ReadSeries(item, index))
            .Concat(chartSpace.Descendants<C.LineChartSeries>().Select((item, index) => ReadSeries(item, index)))
            .ToArray();
        return series;
    }

    private static ChartSeriesModel ReadSeries(OpenXmlElement series, int index)
    {
        var label = TextNormalization.Clean(series.Elements<C.SeriesText>().FirstOrDefault()?.InnerText);
        var categories = series.Elements<C.CategoryAxisData>().FirstOrDefault()
            ?.Descendants<C.NumericValue>()
            .Select(value => TextNormalization.Clean(value.Text))
            .Where(value => value.Length > 0)
            .ToArray() ?? [];
        var values = series.Elements<C.Values>().FirstOrDefault()
            ?.Descendants<C.NumericValue>()
            .Select(value => ParseDouble(value.Text))
            .Where(value => double.IsFinite(value))
            .ToArray() ?? [];
        var color = series.Descendants<A.SolidFill>().FirstOrDefault()?.Descendants<A.RgbColorModelHex>().FirstOrDefault()?.Val?.Value ?? "";
        return new ChartSeriesModel(
            label.Length > 0 ? label : $"Series {index + 1}",
            categories,
            values,
            color);
    }

    private static uint ParseUInt(string? value)
    {
        return uint.TryParse(value, out var parsed) ? parsed : 0;
    }

    private static double ParseDouble(string? value)
    {
        return double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }
}
