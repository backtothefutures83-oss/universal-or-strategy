# Build 971: V12_002.cs modular split

SRC = 'src/V12_002.cs'

with open(SRC, 'r', encoding='utf-8') as f:
    lines = f.readlines()

total = len(lines)
print(f"Source: {total} lines")

# Usings shared across all extracted files (no inline comments to avoid issues)
USINGS = (
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

# Simple header -- content already has its own #region blocks
def make_header(comment):
    return (
        "// " + comment + "\n" +
        USINGS +
        "\nnamespace NinjaTrader.NinjaScript.Strategies\n"
        "{\n"
        "    public partial class V12_002 : Strategy\n"
        "    {\n"
    )

FOOTER = "    }\n}\n"

def extract(lo, hi):
    return ''.join(lines[lo-1:hi])

def write_file(path, content):
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)
    lc = content.count('\n')
    print(f"  Written: {path} ({lc} lines)")

# --- 1. V12_002.PositionInfo.cs (lines 344-668) ---
# PositionInfo is a private nested class inside partial class V12_002 : Strategy
write_file(
    'src/V12_002.PositionInfo.cs',
    make_header("Build 971: V12_002 PositionInfo nested class") +
    extract(344, 668) +
    FOOTER
)

# --- 2. V12_002.Lifecycle.cs (lines 672-1046) ---
# Contains: OnStateChange (672-1002), OnConnectionStatusUpdate (1004-1028), OnMarketData (1030-1046)
write_file(
    'src/V12_002.Lifecycle.cs',
    make_header("Build 971: V12_002 Lifecycle -- OnStateChange, OnConnectionStatusUpdate, OnMarketData") +
    extract(672, 1046) +
    FOOTER
)

# --- 3. V12_002.BarUpdate.cs (lines 1048-1248) ---
write_file(
    'src/V12_002.BarUpdate.cs',
    make_header("Build 971: V12_002 BarUpdate -- OnBarUpdate") +
    extract(1048, 1248) +
    FOOTER
)

# --- 4. V12_002.DrawingHelpers.cs (lines 1253-1427) ---
# Contains: Drawing #region (1253-1357) + Helpers #region (1359-1427)
write_file(
    'src/V12_002.DrawingHelpers.cs',
    make_header("Build 971: V12_002 DrawingHelpers -- Drawing, Helpers regions") +
    extract(1253, 1427) +
    FOOTER
)

# --- 5. Trim V12_002.cs: lines 1-342 (Variables region complete) + close class/namespace ---
# Lines 343+ (PositionInfo, Lifecycle, BarUpdate, Drawing, Helpers, comments) are extracted
trimmed = ''.join(lines[0:342]) + FOOTER
with open(SRC, 'w', encoding='utf-8') as f:
    f.write(trimmed)
lc = trimmed.count('\n')
print(f"  Trimmed: {SRC} ({lc} lines)")

print("Done.")
