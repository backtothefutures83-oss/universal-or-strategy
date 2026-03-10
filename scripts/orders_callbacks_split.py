# Build 971: V12_002.Orders.Callbacks.cs modular split

SRC = 'src/V12_002.Orders.Callbacks.cs'

with open(SRC, 'r', encoding='utf-8') as f:
    lines = f.readlines()

total = len(lines)
print(f"Source: {total} lines")

USINGS = (
    "// V12 Orders.Callbacks Module (Extracted)\n"
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

# --- 1. Orders.Callbacks.AccountOrders.cs (lines 489-834) ---
write_file(
    'src/V12_002.Orders.Callbacks.AccountOrders.cs',
    make_header(
        "Build 971: Orders.Callbacks.AccountOrders -- OnAccountOrderUpdate, ProcessAccountOrderQueue, TryFindOrderInPosition, HandleMatchedFollowerOrder, ExecuteFollowerCascadeCleanup, ProcessQueuedAccountOrder",
        "Orders Callbacks Account Orders"
    ) +
    extract(489, 834) +
    FOOTER
)

# --- 2. Orders.Callbacks.Execution.cs (lines 835-1229) ---
write_file(
    'src/V12_002.Orders.Callbacks.Execution.cs',
    make_header(
        "Build 971: Orders.Callbacks.Execution -- OnPositionUpdate, ProcessOnPositionUpdate, HandleFlatPositionUpdate, BroadcastSyncTargetState, OnExecutionUpdate, ProcessOnExecutionUpdate",
        "Orders Callbacks Execution"
    ) +
    extract(835, 1229) +
    FOOTER
)

# --- 3. Orders.Callbacks.Propagation.cs (lines 1230-1704) ---
write_file(
    'src/V12_002.Orders.Callbacks.Propagation.cs',
    make_header(
        "Build 971: Orders.Callbacks.Propagation -- PropagateMasterPriceMove, PropagateMasterStopMove, PropagateMasterTargetMove, PropagateMasterEntryMove, PropagateFollowerEntryReplace, SubmitFollowerReplacement, SubmitFollowerTargetReplacement",
        "Orders Callbacks Propagation"
    ) +
    extract(1230, 1704) +
    FOOTER
)

# --- 4. Trim Orders.Callbacks.cs: lines 1-488 + close ---
trimmed = ''.join(lines[0:488]) + "    }\n}\n"
with open(SRC, 'w', encoding='utf-8') as f:
    f.write(trimmed)
lc = trimmed.count('\n')
print(f"  Trimmed: {SRC} ({lc} lines)")

print("Done.")
