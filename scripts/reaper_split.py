import subprocess
from pathlib import Path

SRC_SPEC = "origin/main:src/V12_002.REAPER.cs"
ROOT = Path(__file__).resolve().parents[1]

REAPER_REPAIR_HEADER = """// V12 REAPER Repair Engine -- Re-issues missed entry orders for desynced follower accounts
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region V12 REAPER Repair Engine

"""

REAPER_NAKED_STOP_HEADER = """// V12 REAPER Emergency Stop -- Naked-position hard stop protection (Build 1102R)
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region V12 REAPER Emergency Stop

"""

FOOTER = """
        #endregion
    }
}
"""


def read_source_lines():
    result = subprocess.run(
        ["git", "show", SRC_SPEC],
        cwd=ROOT,
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    return result.stdout.splitlines(keepends=True)


def write_file(path, content):
    path.write_text(content, encoding="utf-8")
    print(f"[OK] Wrote {path.relative_to(ROOT)} ({content.count(chr(10))} lines)")


def extract(lines, start_line, end_line):
    return "".join(lines[start_line - 1:end_line])


def main():
    lines = read_source_lines()
    print(f"Source: {SRC_SPEC} ({len(lines)} lines)")

    write_file(
        ROOT / "src" / "V12_002.REAPER.Repair.cs",
        REAPER_REPAIR_HEADER + extract(lines, 523, 715) + FOOTER,
    )

    write_file(
        ROOT / "src" / "V12_002.REAPER.NakedStop.cs",
        REAPER_NAKED_STOP_HEADER + extract(lines, 717, 781) + FOOTER,
    )

    trimmed = "".join(lines[0:168]) + "\n        #endregion\n    }\n}\n"
    write_file(ROOT / "src" / "V12_002.REAPER.cs", trimmed)


if __name__ == "__main__":
    main()
