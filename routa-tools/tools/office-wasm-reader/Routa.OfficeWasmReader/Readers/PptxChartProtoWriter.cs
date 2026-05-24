using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Google.Protobuf;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace Routa.OfficeWasmReader;

internal static class PptxChartProtoWriter
{
    private const int ChartTypeArea = 2;
    private const int ChartTypeBar = 4;
    private const int ChartTypeBubble = 5;
    private const int ChartTypeDoughnut = 8;
    private const int ChartTypeLine = 13;
    private const int ChartTypePie = 16;
    private const int ChartTypeRadar = 17;
    private const int ChartTypeScatter = 18;
    private const int ChartTypeSurface = 22;
    private const int BarDirectionColumn = 1;
    private const int BarDirectionBar = 2;

    public static byte[] WriteReference(string chartId)
    {
        return Message(output =>
        {
            WriteString(output, 1, chartId);
        });
    }

    public static byte[] WriteChart(ChartPart chartPart, Func<A.SolidFill, byte[]> writeFill)
    {
        var chartSpace = chartPart.ChartSpace;
        var series = chartSpace is null ? [] : ExtractChartSeries(chartSpace).ToList();
        var categories = series.SelectMany(item => item.Categories)
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return Message(output =>
        {
            WriteString(output, 1, ChartTitle(chartSpace));
            foreach (var category in categories)
            {
                WriteString(output, 2, category);
            }

            foreach (var item in series)
            {
                WriteMessage(output, 3, WriteChartSeries(item, writeFill));
            }

            WriteInt32(output, 5, ChartType(chartSpace));
            WriteString(output, 7, chartPart.Uri.OriginalString);
            WriteInt32(output, 10, BarDirection(chartSpace));
            WriteBoolValue(output, 11, chartSpace?.Descendants<C.Legend>().Any());
        });
    }

    private static byte[] WriteChartSeries(ChartSeriesData series, Func<A.SolidFill, byte[]> writeFill)
    {
        return Message(output =>
        {
            WriteString(output, 1, series.Name);
            foreach (var value in series.Values)
            {
                WriteDouble(output, 2, value);
            }

            foreach (var category in series.Categories)
            {
                WriteString(output, 5, category);
            }

            var fill = series.Element.Descendants<A.SolidFill>().FirstOrDefault();
            if (fill is not null)
            {
                WriteMessage(output, 7, writeFill(fill));
            }

            WriteString(output, 8, series.Id);
        });
    }

    private static IEnumerable<ChartSeriesData> ExtractChartSeries(C.ChartSpace chartSpace)
    {
        var index = 0;
        foreach (var series in ChartSeriesElements(chartSpace))
        {
            var name = TextNormalization.Clean(series.Elements<C.SeriesText>().FirstOrDefault()?.InnerText);
            var categories = ExtractChartCategories(series).ToList();
            var values = ExtractChartValues(series).ToList();
            yield return new ChartSeriesData(
                $"series-{index:x8}",
                name.Length > 0 ? name : $"Series {index + 1}",
                categories,
                values,
                series);
            index++;
        }
    }

    private static IEnumerable<OpenXmlElement> ChartSeriesElements(C.ChartSpace chartSpace)
    {
        return chartSpace.Descendants<C.BarChartSeries>().Cast<OpenXmlElement>()
            .Concat(chartSpace.Descendants<C.LineChartSeries>().Cast<OpenXmlElement>())
            .Concat(chartSpace.Descendants<C.PieChartSeries>().Cast<OpenXmlElement>())
            .Concat(chartSpace.Descendants<C.AreaChartSeries>().Cast<OpenXmlElement>())
            .Concat(chartSpace.Descendants<C.ScatterChartSeries>().Cast<OpenXmlElement>())
            .Concat(chartSpace.Descendants<C.BubbleChartSeries>().Cast<OpenXmlElement>())
            .Concat(chartSpace.Descendants<C.RadarChartSeries>().Cast<OpenXmlElement>())
            .Concat(chartSpace.Descendants<C.SurfaceChartSeries>().Cast<OpenXmlElement>());
    }

    private static IEnumerable<string> ExtractChartCategories(OpenXmlElement series)
    {
        var categoryContainers = series.Elements<C.CategoryAxisData>().Cast<OpenXmlElement>()
            .Concat(series.Elements<C.XValues>());
        return categoryContainers
            .SelectMany(container => container.Descendants<C.NumericValue>())
            .Select(value => TextNormalization.Clean(value.Text))
            .Where(value => value.Length > 0);
    }

    private static IEnumerable<double> ExtractChartValues(OpenXmlElement series)
    {
        var valueContainers = series.Elements<C.Values>().Cast<OpenXmlElement>()
            .Concat(series.Elements<C.YValues>())
            .Concat(series.Elements<C.BubbleSize>());
        return valueContainers
            .SelectMany(container => container.Descendants<C.NumericValue>())
            .Select(value => ParseDouble(value.Text))
            .Where(double.IsFinite);
    }

    private static string ChartTitle(C.ChartSpace? chartSpace)
    {
        if (chartSpace is null)
        {
            return "";
        }

        return TextNormalization.Clean(string.Concat(
            chartSpace.Descendants<C.Title>().FirstOrDefault()?.Descendants<A.Text>().Select(item => item.Text) ??
            Enumerable.Empty<string>()));
    }

    private static int ChartType(C.ChartSpace? chartSpace)
    {
        if (chartSpace is null)
        {
            return 0;
        }

        if (chartSpace.Descendants<C.AreaChart>().Any()) return ChartTypeArea;
        if (chartSpace.Descendants<C.BarChart>().Any()) return ChartTypeBar;
        if (chartSpace.Descendants<C.BubbleChart>().Any()) return ChartTypeBubble;
        if (chartSpace.Descendants<C.DoughnutChart>().Any()) return ChartTypeDoughnut;
        if (chartSpace.Descendants<C.LineChart>().Any()) return ChartTypeLine;
        if (chartSpace.Descendants<C.PieChart>().Any()) return ChartTypePie;
        if (chartSpace.Descendants<C.RadarChart>().Any()) return ChartTypeRadar;
        if (chartSpace.Descendants<C.ScatterChart>().Any()) return ChartTypeScatter;
        if (chartSpace.Descendants<C.SurfaceChart>().Any()) return ChartTypeSurface;
        return 0;
    }

    private static int BarDirection(C.ChartSpace? chartSpace)
    {
        var direction = chartSpace?.Descendants<C.BarDirection>().FirstOrDefault()?.Val?.Value.ToString();
        return string.Equals(direction, "bar", StringComparison.OrdinalIgnoreCase) ? BarDirectionBar :
            string.Equals(direction, "column", StringComparison.OrdinalIgnoreCase) ? BarDirectionColumn :
            0;
    }

    private static double ParseDouble(string? value)
    {
        return double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : double.NaN;
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

    private static void WriteString(CodedOutputStream output, int fieldNumber, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.LengthDelimited);
        output.WriteString(value);
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

    private static void WriteBoolValue(CodedOutputStream output, int fieldNumber, bool? value)
    {
        if (value is null)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteBool(value.Value);
    }

    private static void WriteDouble(CodedOutputStream output, int fieldNumber, double? value)
    {
        if (value is null or 0)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Fixed64);
        output.WriteDouble(value.Value);
    }

    private sealed record ChartSeriesData(
        string Id,
        string Name,
        IReadOnlyList<string> Categories,
        IReadOnlyList<double> Values,
        OpenXmlElement Element);
}
