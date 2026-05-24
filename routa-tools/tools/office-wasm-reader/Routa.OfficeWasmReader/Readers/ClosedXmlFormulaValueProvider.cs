using System.Globalization;
using ClosedXML.Excel;

namespace Routa.OfficeWasmReader;

internal sealed class ClosedXmlFormulaValueProvider : IDisposable
{
    private readonly XLWorkbook workbook;

    private ClosedXmlFormulaValueProvider(XLWorkbook workbook)
    {
        this.workbook = workbook;
    }

    public static ClosedXmlFormulaValueProvider? TryCreate(byte[] bytes)
    {
        try
        {
            return new ClosedXmlFormulaValueProvider(new XLWorkbook(new MemoryStream(bytes, writable: false)));
        }
        catch
        {
            return null;
        }
    }

    public bool TryGetFormulaValue(string sheetName, string address, out string value)
    {
        value = "";
        if (string.IsNullOrWhiteSpace(sheetName) || string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        try
        {
            if (!workbook.Worksheets.TryGetWorksheet(sheetName, out var worksheet))
            {
                return false;
            }

            var cell = worksheet.Cell(address);
            if (string.IsNullOrWhiteSpace(cell.FormulaA1))
            {
                return false;
            }

            var cellValue = cell.Value;
            if (cellValue.IsError &&
                cellValue.GetError() == XLError.NameNotRecognized &&
                TryEvaluateXLookupFallback(worksheet, cell.FormulaA1, out value))
            {
                return true;
            }

            value = FormulaValueToProtocolText(cellValue);
            return true;
        }
        catch
        {
            value = "";
            return false;
        }
    }

    public void Dispose()
    {
        workbook.Dispose();
    }

    private bool TryEvaluateXLookupFallback(IXLWorksheet worksheet, string formula, out string value)
    {
        value = "";
        var normalized = formula.Trim().TrimStart('=');
        if (normalized.StartsWith("_xlfn.", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["_xlfn.".Length..];
        }

        if (!normalized.StartsWith("XLOOKUP(", StringComparison.OrdinalIgnoreCase) || !normalized.EndsWith(')'))
        {
            return false;
        }

        var args = SplitFormulaArguments(normalized["XLOOKUP(".Length..^1]);
        if (args.Count < 3)
        {
            return false;
        }

        if (!TryFormulaLiteralOrCellValue(worksheet, args[0], out var lookupValue) ||
            !TryResolveRange(worksheet, args[1], out var lookupRange) ||
            !TryResolveRange(worksheet, args[2], out var returnRange))
        {
            return false;
        }

        var matchMode = args.Count >= 5 && int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMatchMode)
            ? parsedMatchMode
            : 0;
        if (matchMode != 0 && matchMode != 2)
        {
            return false;
        }

        var searchMode = args.Count >= 6 && int.TryParse(args[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSearchMode)
            ? parsedSearchMode
            : 1;
        if (searchMode != 1 && searchMode != -1)
        {
            return false;
        }

        if (TryFindLookupResult(lookupValue, lookupRange, returnRange, matchMode, searchMode, out var result))
        {
            value = FormulaValueToProtocolText(result);
            return true;
        }

        if (args.Count >= 4 && TryFormulaLiteralOrCellValue(worksheet, args[3], out var fallback))
        {
            value = FormulaValueToProtocolText(fallback);
            return true;
        }

        value = "#N/A";
        return true;
    }

    private bool TryResolveRange(IXLWorksheet currentWorksheet, string expression, out IXLRange range)
    {
        range = currentWorksheet.Range("A1:A1");
        var reference = expression.Trim();
        if (reference.Length == 0)
        {
            return false;
        }

        var worksheet = currentWorksheet;
        var bangIndex = LastSheetReferenceSeparator(reference);
        if (bangIndex >= 0)
        {
            var sheetName = UnquoteSheetName(reference[..bangIndex]);
            if (!workbook.Worksheets.TryGetWorksheet(sheetName, out worksheet))
            {
                return false;
            }

            reference = reference[(bangIndex + 1)..];
        }

        try
        {
            range = worksheet.Range(reference.Replace("$", "", StringComparison.Ordinal));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryFormulaLiteralOrCellValue(IXLWorksheet worksheet, string expression, out XLCellValue value)
    {
        value = Blank.Value;
        var text = expression.Trim();
        if (text.Length == 0)
        {
            return false;
        }

        if (text.StartsWith('"') && text.EndsWith('"'))
        {
            value = UnescapeFormulaString(text);
            return true;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            value = number;
            return true;
        }

        if (string.Equals(text, "TRUE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(text, "FALSE", StringComparison.OrdinalIgnoreCase))
        {
            value = string.Equals(text, "TRUE", StringComparison.OrdinalIgnoreCase);
            return true;
        }

        if (TryResolveRange(worksheet, text, out var range) && range.RowCount() == 1 && range.ColumnCount() == 1)
        {
            value = range.Cell(1, 1).Value;
            return true;
        }

        return false;
    }

    private static bool TryFindLookupResult(
        XLCellValue lookupValue,
        IXLRange lookupRange,
        IXLRange returnRange,
        int matchMode,
        int searchMode,
        out XLCellValue result)
    {
        result = Blank.Value;
        if (lookupRange.RowCount() == 1)
        {
            var start = searchMode == -1 ? lookupRange.ColumnCount() : 1;
            var end = searchMode == -1 ? 1 : lookupRange.ColumnCount();
            var step = searchMode == -1 ? -1 : 1;
            for (var column = start; searchMode == -1 ? column >= end : column <= end; column += step)
            {
                if (FormulaValuesMatch(lookupValue, lookupRange.Cell(1, column).Value, matchMode))
                {
                    result = returnRange.Cell(1, Math.Min(column, returnRange.ColumnCount())).Value;
                    return true;
                }
            }

            return false;
        }

        var rowStart = searchMode == -1 ? lookupRange.RowCount() : 1;
        var rowEnd = searchMode == -1 ? 1 : lookupRange.RowCount();
        var rowStep = searchMode == -1 ? -1 : 1;
        for (var row = rowStart; searchMode == -1 ? row >= rowEnd : row <= rowEnd; row += rowStep)
        {
            if (FormulaValuesMatch(lookupValue, lookupRange.Cell(row, 1).Value, matchMode))
            {
                result = returnRange.Cell(Math.Min(row, returnRange.RowCount()), 1).Value;
                return true;
            }
        }

        return false;
    }

    private static bool FormulaValuesMatch(XLCellValue expected, XLCellValue actual, int matchMode)
    {
        if (expected.IsNumber && actual.IsNumber)
        {
            return Math.Abs(expected.GetNumber() - actual.GetNumber()) < 0.000000001;
        }

        var expectedText = FormulaValueToProtocolText(expected);
        var actualText = FormulaValueToProtocolText(actual);
        return matchMode == 2
            ? WildcardMatches(expectedText, actualText)
            : string.Equals(expectedText, actualText, StringComparison.OrdinalIgnoreCase);
    }

    private static bool WildcardMatches(string pattern, string value)
    {
        return WildcardMatches(pattern, 0, value, 0);
    }

    private static bool WildcardMatches(string pattern, int patternIndex, string value, int valueIndex)
    {
        while (patternIndex < pattern.Length)
        {
            var patternChar = pattern[patternIndex];
            if (patternChar == '*')
            {
                for (var index = valueIndex; index <= value.Length; index += 1)
                {
                    if (WildcardMatches(pattern, patternIndex + 1, value, index))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (valueIndex >= value.Length)
            {
                return false;
            }

            if (patternChar != '?' && char.ToUpperInvariant(patternChar) != char.ToUpperInvariant(value[valueIndex]))
            {
                return false;
            }

            patternIndex += 1;
            valueIndex += 1;
        }

        return valueIndex == value.Length;
    }

    private static List<string> SplitFormulaArguments(string value)
    {
        var args = new List<string>();
        var start = 0;
        var depth = 0;
        var inString = false;
        for (var index = 0; index < value.Length; index += 1)
        {
            var current = value[index];
            if (current == '"')
            {
                if (inString && index + 1 < value.Length && value[index + 1] == '"')
                {
                    index += 1;
                    continue;
                }

                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (current == '(')
            {
                depth += 1;
            }
            else if (current == ')')
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (current == ',' && depth == 0)
            {
                args.Add(value[start..index].Trim());
                start = index + 1;
            }
        }

        args.Add(value[start..].Trim());
        return args;
    }

    private static int LastSheetReferenceSeparator(string reference)
    {
        var inString = false;
        for (var index = reference.Length - 1; index >= 0; index -= 1)
        {
            if (reference[index] == '\'')
            {
                inString = !inString;
                continue;
            }

            if (reference[index] == '!' && !inString)
            {
                return index;
            }
        }

        return -1;
    }

    private static string UnquoteSheetName(string sheetName)
    {
        var trimmed = sheetName.Trim();
        return trimmed.StartsWith('\'') && trimmed.EndsWith('\'')
            ? trimmed[1..^1].Replace("''", "'", StringComparison.Ordinal)
            : trimmed;
    }

    private static string UnescapeFormulaString(string text)
    {
        return text[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
    }

    private static string FormulaValueToProtocolText(XLCellValue value)
    {
        if (value.IsBlank)
        {
            return "";
        }

        if (value.IsBoolean)
        {
            return value.GetBoolean() ? "TRUE" : "FALSE";
        }

        if (value.IsNumber)
        {
            return value.GetNumber().ToString("G15", CultureInfo.InvariantCulture);
        }

        if (value.IsDateTime)
        {
            return value.GetUnifiedNumber().ToString("G15", CultureInfo.InvariantCulture);
        }

        if (value.IsTimeSpan)
        {
            return value.GetUnifiedNumber().ToString("G15", CultureInfo.InvariantCulture);
        }

        if (value.IsError)
        {
            return FormulaErrorToProtocolText(value.GetError());
        }

        return value.GetText();
    }

    private static string FormulaErrorToProtocolText(XLError error)
    {
        return error switch
        {
            XLError.NullValue => "#NULL!",
            XLError.DivisionByZero => "#DIV/0!",
            XLError.IncompatibleValue => "#VALUE!",
            XLError.CellReference => "#REF!",
            XLError.NameNotRecognized => "#NAME?",
            XLError.NumberInvalid => "#NUM!",
            XLError.NoValueAvailable => "#N/A",
            _ => error.ToString(),
        };
    }
}
