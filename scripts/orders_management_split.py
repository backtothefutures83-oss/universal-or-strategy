# Build 971: V12_002.Orders.Management.cs modular split

SRC = 'src/V12_002.Orders.Management.cs'

with open(SRC, 'r', encoding='utf-8') as f:
    lines = f.readlines()

total = len(lines)
print(f"Source: {total} lines")

# Get usings from original (lines 1-29)
usings_block = ''.join(lines[0:2]) + ''.join(lines[2:29])
USINGS = (
    "// V12 Orders.Management Module (Extracted)\n"
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
    return ''.join(lines[lo-1:hi])

def write_file(path, content):
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)
    lc = content.count('\n')
    print(f"  Written: {path} ({lc} lines)")

# --- 1. Orders.Management.StopSync.cs (lines 238-744) ---
# [Build 971] Group >400 lines -- future refactor candidate
write_file(
    'src/V12_002.Orders.Management.StopSync.cs',
    make_header(
        "Build 971: Orders.Management.StopSync -- RefreshActivePositionOrders, UpdateStopQuantity, CreateNewStopOrder, RestoreCascadedTargets, ValidateStopPrice [Build 971] Group >400 lines -- future refactor candidate",
        "Orders Management Stop Sync"
    ) +
    extract(238, 744) +
    FOOTER
)

# --- 2. Orders.Management.Flatten.cs (lines 753-1162) ---
# Skip lines 751-752 (#region + blank) -- HEADER provides its own #region
write_file(
    'src/V12_002.Orders.Management.Flatten.cs',
    make_header(
        "Build 971: Orders.Management.Flatten -- SyncPositionState, ManageCIT, FlattenAll, FlattenPositionByName, IsOrderTerminal, HasActiveOrPendingOrderForEntry",
        "Orders Management Flatten"
    ) +
    extract(753, 1162) +
    FOOTER
)

# --- 3. Orders.Management.Cleanup.cs (lines 1163-1572) ---
write_file(
    'src/V12_002.Orders.Management.Cleanup.cs',
    make_header(
        "Build 971: Orders.Management.Cleanup -- CleanupPosition, RemoveGhostOrderRef, ReconcileOrphanedOrders",
        "Orders Management Cleanup"
    ) +
    extract(1163, 1572) +
    FOOTER
)

# --- 4. Trim Orders.Management.cs: lines 1-237 + close #region + class + namespace ---
# The original #region "Order Submission & Stop Management" is at line 35 and is in lines 1-237.
# We close it here; lines 745-750 (#endregion + blanks + comment) are dropped.
trimmed = ''.join(lines[0:237]) + "        #endregion\n    }\n}\n"
with open(SRC, 'w', encoding='utf-8') as f:
    f.write(trimmed)
lc = trimmed.count('\n')
print(f"  Trimmed: {SRC} ({lc} lines)")

print("Done.")
