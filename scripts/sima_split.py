# Build 971: V12_002.SIMA.cs modular split
# Boundaries verified against actual source content.

SRC = 'src/V12_002.SIMA.cs'

with open(SRC, 'r', encoding='utf-8') as f:
    lines = f.readlines()

total = len(lines)
print(f"Source: {total} lines")

USINGS = (
    "// V12 SIMA Module (Extracted)\n"
    "using System;\n"
    "using System.Collections.Generic;\n"
    "using System.Collections.Concurrent;\n"
    "using System.ComponentModel;\n"
    "using System.ComponentModel.DataAnnotations;\n"
    "using System.Linq;\n"
    "using System.Text;\n"
    "using System.Globalization;\n"
    "using System.Diagnostics;\n"
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

def make_header(comment, region):
    return (
        "// " + comment + "\n" +
        USINGS +
        "\nnamespace NinjaTrader.NinjaScript.Strategies\n"
        "{\n"
        "    public partial class V12_002 : Strategy\n"
        "    {\n"
        "        #region " + region + "\n\n"
    )

FOOTER = (
    "\n"
    "        #endregion\n"
    "    }\n"
    "}\n"
)

def extract(lo, hi):
    # lo, hi are 1-indexed inclusive
    return ''.join(lines[lo-1:hi])

def write_file(path, content):
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)
    lc = content.count('\n')
    print(f"  Written: {path} ({lc} lines)")

# --- 1. SIMA.Dispatch.cs (lines 216-621) ---
write_file(
    'src/V12_002.SIMA.Dispatch.cs',
    make_header(
        "Build 971: SIMA Dispatch -- ExecuteSmartDispatchEntry",
        "V12 SIMA Dispatch"
    ) +
    extract(216, 621) +
    FOOTER
)

# --- 2. SIMA.Fleet.cs (lines 622-788) ---
write_file(
    'src/V12_002.SIMA.Fleet.cs',
    make_header(
        "Build 971: SIMA Fleet -- PumpFleetDispatch, ShouldSkipFleetAccount, UnsubscribeFromFleetAccounts",
        "V12 SIMA Fleet"
    ) +
    extract(622, 788) +
    FOOTER
)

# --- 3. SIMA.Lifecycle.cs (lines 789-1114) ---
write_file(
    'src/V12_002.SIMA.Lifecycle.cs',
    make_header(
        "Build 971: SIMA Lifecycle -- ApplySimaState, EnumerateApexAccounts, Hydrate*, CancelAll*, Sweep*",
        "V12 SIMA Lifecycle"
    ) +
    extract(789, 1114) +
    FOOTER
)

# --- 4. SIMA.Execution.cs (lines 1115-1521) ---
write_file(
    'src/V12_002.SIMA.Execution.cs',
    make_header(
        "Build 971: SIMA Execution -- ExecuteMultiAccountMarket, ExecuteMultiAccountBracket, ExecuteRMAEntryV2",
        "V12 SIMA Execution"
    ) +
    extract(1115, 1521) +
    FOOTER
)

# --- 5. SIMA.Flatten.cs (lines 1522-1866) ---
write_file(
    'src/V12_002.SIMA.Flatten.cs',
    make_header(
        "Build 971: SIMA Flatten -- FlattenAllApexAccounts, EmergencyFlattenSingleFleetAccount, ClosePositionsOnlyApexAccounts",
        "V12 SIMA Flatten"
    ) +
    extract(1522, 1866) +
    FOOTER
)

# --- 6. Trim SIMA.cs: keep lines 1-213, add closing braces ---
trimmed = ''.join(lines[0:213]) + "    }\n}\n"
with open(SRC, 'w', encoding='utf-8') as f:
    f.write(trimmed)
lc = trimmed.count('\n')
print(f"  Trimmed: {SRC} ({lc} lines)")

print("Done.")
