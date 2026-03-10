# Build 971: V12_002.UI.IPC.cs modular split

SRC = 'src/V12_002.UI.IPC.cs'

with open(SRC, 'r', encoding='utf-8') as f:
    lines = f.readlines()

total = len(lines)
print(f"Source: {total} lines")

USINGS = (
    "// V12 UI.IPC Module (Extracted)\n"
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

# --- 1. UI.IPC.Server.cs (lines 61-310) ---
write_file(
    'src/V12_002.UI.IPC.Server.cs',
    make_header(
        "Build 971: UI.IPC.Server -- StartIpcServer, ListenForRemote, HandleClient, ProcessClientStream, HandleIncomingIpcLine, StopIpcServer",
        "IPC Server"
    ) +
    extract(61, 310) +
    FOOTER
)

# --- 2. UI.IPC.Commands.Config.cs (lines 592-821) ---
write_file(
    'src/V12_002.UI.IPC.Commands.Config.cs',
    make_header(
        "Build 971: UI.IPC.Commands.Config -- HandleTrimCommand, HandleConfigCommand, TryApplyConfig*, HandleToggleAccountCommand",
        "IPC Commands Config"
    ) +
    extract(592, 821) +
    FOOTER
)

# --- 3. UI.IPC.Commands.Mode.cs (lines 822-1054) ---
write_file(
    'src/V12_002.UI.IPC.Commands.Mode.cs',
    make_header(
        "Build 971: UI.IPC.Commands.Mode -- TryHandleDiagCommand, TryHandleModeCommand, TryHandleRiskCommand",
        "IPC Commands Mode"
    ) +
    extract(822, 1054) +
    FOOTER
)

# --- 4. UI.IPC.Commands.Fleet.cs (lines 1055-1540) ---
# [Build 971] Single method >400 lines -- future refactor candidate
write_file(
    'src/V12_002.UI.IPC.Commands.Fleet.cs',
    make_header(
        "Build 971: UI.IPC.Commands.Fleet -- TryHandleFleetCommand [Build 971] Single method >400 lines -- future refactor candidate",
        "IPC Commands Fleet"
    ) +
    extract(1055, 1540) +
    FOOTER
)

# --- 5. UI.IPC.Commands.Misc.cs (lines 1541-1848) ---
write_file(
    'src/V12_002.UI.IPC.Commands.Misc.cs',
    make_header(
        "Build 971: UI.IPC.Commands.Misc -- TryHandleConfigCommand, TryHandleComplianceCommand, HandleFleetCommand, SendResponseToRemote, FlattenSpecificTarget, ToggleStrategyMode",
        "IPC Commands Misc"
    ) +
    extract(1541, 1848) +
    FOOTER
)

# --- 6. Trim UI.IPC.cs: lines 1-60 + 311-591 + close ---
trimmed = (
    ''.join(lines[0:60]) +
    ''.join(lines[310:591]) +
    "        #endregion\n"
    "    }\n"
    "}\n"
)
with open(SRC, 'w', encoding='utf-8') as f:
    f.write(trimmed)
lc = trimmed.count('\n')
print(f"  Trimmed: {SRC} ({lc} lines)")

print("Done.")
