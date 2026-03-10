# Build 971: V12_002.Symmetry.cs modular split

SRC = 'src/V12_002.Symmetry.cs'

with open(SRC, 'r', encoding='utf-8') as f:
    lines = f.readlines()

total = len(lines)
print(f"Source: {total} lines")

USINGS = (
    "// V12 Symmetry Module (Extracted)\n"
    "using System;\n"
    "using System.Collections.Generic;\n"
    "using System.Collections.Concurrent;\n"
    "using System.Linq;\n"
    "using NinjaTrader.Cbi;\n"
    "using NinjaTrader.NinjaScript;\n"
    "using NinjaTrader.NinjaScript.Strategies;\n"
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

FOOTER_WRAPPED = "\n        #endregion\n    }\n}\n"

def extract(lo, hi):
    return ''.join(lines[lo-1:hi])

def write_file(path, content):
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)
    lc = content.count('\n')
    print(f"  Written: {path} ({lc} lines)")

# --- 1. Symmetry.Follower.cs (lines 198-497) ---
# Content has no #region -- use wrapper
write_file(
    'src/V12_002.Symmetry.Follower.cs',
    make_header_wrapped(
        "Build 971: Symmetry.Follower -- SymmetryGuardOnFollowerFill, IsAnchorPending, ProcessPendingFollowerFills, TryResolveFollower, ApplyMasterAnchor, SubmitFollowerBracket",
        "Symmetry Follower"
    ) +
    extract(198, 497) +
    FOOTER_WRAPPED
)

# --- 2. Symmetry.Replace.cs (lines 498-778) ---
# Content has no #region -- use wrapper. Line 780 (#endregion) is dropped.
write_file(
    'src/V12_002.Symmetry.Replace.cs',
    make_header_wrapped(
        "Build 971: Symmetry.Replace -- SymmetryGuardRetargetExistingFollowerBracket, ReplaceExistingFollowerTarget, SkipFollower",
        "Symmetry Replace"
    ) +
    extract(498, 778) +
    FOOTER_WRAPPED
)

# --- 3. Trim Symmetry.cs: lines 1-197 + close the original #region V12.50 Symmetry Guard ---
trimmed = ''.join(lines[0:197]) + "        #endregion\n    }\n}\n"
with open(SRC, 'w', encoding='utf-8') as f:
    f.write(trimmed)
lc = trimmed.count('\n')
print(f"  Trimmed: {SRC} ({lc} lines)")

print("Done.")
