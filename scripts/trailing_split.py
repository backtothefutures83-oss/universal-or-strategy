# Build 971: V12_002.Trailing.cs modular split

SRC = 'src/V12_002.Trailing.cs'

with open(SRC, 'r', encoding='utf-8') as f:
    lines = f.readlines()

total = len(lines)
print(f"Source: {total} lines")

USINGS = (
    "// V12 Trailing Module (Extracted)\n"
    "using System;\n"
    "using System.Collections.Generic;\n"
    "using System.Collections.Concurrent;\n"
    "using System.ComponentModel;\n"
    "using System.ComponentModel.DataAnnotations;\n"
    "using System.Linq;\n"
    "using System.Text;\n"
    "using System.Globalization;\n"
    "using System.Threading;\n"
    "using System.Threading.Tasks;\n"
    "using System.Windows;\n"
    "using System.Windows.Controls;\n"
    "using System.Windows.Controls.Primitives;\n"
    "using System.Windows.Input;\n"
    "using System.Windows.Media;\n"
    "using System.Windows.Shapes;\n"
    "using NinjaTrader.Cbi;\n"
    "using NinjaTrader.Gui;\n"
    "using NinjaTrader.Gui.Chart;\n"
    "using NinjaTrader.Gui.Tools;\n"
    "using NinjaTrader.Data;\n"
    "using NinjaTrader.NinjaScript;\n"
    "using NinjaTrader.NinjaScript.DrawingTools;\n"
    "using NinjaTrader.NinjaScript.Indicators;\n"
    "using NinjaTrader.NinjaScript.Strategies;\n"
    "using System.Net;\n"
    "using System.Net.Sockets;\n"
)

def make_header_wrapped(comment, region):
    return (
        "// " + comment + "\n" +
        USINGS +
        "\nnamespace NinjaTrader.NinjaScript.Strategies\n"
        "{\n"
        "    public partial class V12_002 : Strategy\n"
        "    {\n"
        "        #region " + region + "\n\n"
    )

def make_header_simple(comment):
    return (
        "// " + comment + "\n" +
        USINGS +
        "\nnamespace NinjaTrader.NinjaScript.Strategies\n"
        "{\n"
        "    public partial class V12_002 : Strategy\n"
        "    {\n"
    )

FOOTER_WRAPPED = "\n        #endregion\n    }\n}\n"
FOOTER_SIMPLE  = "    }\n}\n"

def extract(lo, hi):
    return ''.join(lines[lo-1:hi])

def write_file(path, content):
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)
    lc = content.count('\n')
    print(f"  Written: {path} ({lc} lines)")

# --- 1. Trailing.StopUpdate.cs (lines 449-751) ---
# Content has no #region -- use wrapper
write_file(
    'src/V12_002.Trailing.StopUpdate.cs',
    make_header_wrapped(
        "Build 971: Trailing.StopUpdate -- CleanupStalePendingReplacements, UpdateStopOrder, CalculateStopForLevel",
        "Trailing Stop Update"
    ) +
    extract(449, 751) +
    FOOTER_WRAPPED
)

# --- 2. Trailing.Breakeven.cs (lines 752-819 + 822-1058) ---
# Skips line 820 (#endregion from original Trailing Stops region) and 821 (blank)
# Content 822-1058 has its own #region Stop Management Helpers / #endregion pair
write_file(
    'src/V12_002.Trailing.Breakeven.cs',
    make_header_simple(
        "Build 971: Trailing.Breakeven -- OnBreakevenButtonClick, MoveStopsToBreakevenWithOffset, MoveSpecificTarget + Stop Management Helpers"
    ) +
    extract(752, 819) +
    "\n" +
    extract(822, 1058) +
    FOOTER_SIMPLE
)

# --- 3. Trim Trailing.cs: lines 1-448 + close the original #region Trailing Stops ---
# The #region at line 34 is in the trimmed file; add #endregion to balance it
trimmed = ''.join(lines[0:448]) + "        #endregion\n    }\n}\n"
with open(SRC, 'w', encoding='utf-8') as f:
    f.write(trimmed)
lc = trimmed.count('\n')
print(f"  Trimmed: {SRC} ({lc} lines)")

print("Done.")
