
refactor(complexity): Reduce CYC to <=8 in PositionInfo.cs - Calculat… 
783ff09
@codeant-ai
codeant-ai Bot
commented
53 minutes ago
CodeAnt AI is running Incremental review

greptile-apps[bot]
greptile-apps Bot reviewed 53 minutes ago
greptile-apps Bot
left a comment
backtothefutures83-oss has reached the 50-review limit for trial accounts. To continue receiving code reviews, upgrade your plan.

codefactor-io[bot]
codefactor-io Bot reviewed 53 minutes ago
src/V12_002.PositionInfo.cs
        /// the fixed Scalp (T1), causing price inversion and incorrect order slotting.
        /// Call this after computing target prices and again after fill-price re-anchoring.
        /// Slots that are zero (unused/runner) are skipped.
        /// CYC = 8 (was 11: combined continue checks, inlined inversion logic)
@codefactor-io
codefactor-io Bot
53 minutes ago
Documentation text should end with a period.

Suggested change
        /// CYC = 8 (was 11: combined continue checks, inlined inversion logic)
        /// CYC = 8 (was 11: combined continue checks, inlined inversion logic).
@backtothefutures83-oss	Reply...
@codeant-ai codeant-ai Bot removed the size:M label 53 minutes ago
gitar-bot[bot]
gitar-bot Bot reviewed 53 minutes ago
src/V12_002.PositionInfo.cs
Comment on lines +278 to +288
            if (targetNumber < 1 || targetNumber > 5)
                return 0;
            int[] contracts = new int[]
            {
                case 1:
                    return pos.T1Contracts;
                case 2:
                    return pos.T2Contracts;
                case 3:
                    return pos.T3Contracts;
                case 4:
                    return pos.T4Contracts;
                case 5:
                    return pos.T5Contracts;
                default:
                    return 0;
            }
                pos.T1Contracts,
                pos.T2Contracts,
                pos.T3Contracts,
                pos.T4Contracts,
                pos.T5Contracts,
            };
            return contracts[targetNumber - 1];
@gitar-bot
gitar-bot Bot
53 minutes ago
⚠️ Performance: Array-based accessors allocate on every call, breaking zero-alloc goal
@backtothefutures83-oss	Reply...
@gitar-bot
This comment has been minimized.
Show comment
@gitar-bot
This comment has been minimized.
Show comment
@codeant-ai
codeant-ai Bot
commented
52 minutes ago
CodeAnt AI Incremental review completed.

codescene-delta-analysis[bot]
codescene-delta-analysis Bot approved these changes 52 minutes ago
codescene-delta-analysis Bot
left a comment
 Code Health Improved (1 files improve in Code Health)

Our agent can fix these. Install it.

Gates Passed
 6 Quality Gates Passed

View Improvements
Quality Gate Profile: Pay Down Tech Debt
Install CodeScene MCP: safeguard and uplift AI-generated code. Catch issues early with our IDE extension and CLI tool.

@sonarqubecloud
sonarqubecloud Bot
commented
52 minutes ago
Quality Gate Passed Quality Gate passed
Issues
 0 New issues
 0 Accepted issues

Measures
 0 Security Hotspots
 0.0% Coverage on New Code
 0.0% Duplication on New Code

See analysis details on SonarQube Cloud

coderabbitai[bot]
coderabbitai Bot requested changes 47 minutes ago
coderabbitai Bot
left a comment
Actionable comments posted: 2

🤖 Prompt for all review comments with AI agents
🪄 Autofix (Beta)
ℹ️ Review info
src/V12_002.PositionInfo.cs
Comment on lines +217 to +223
                // Skip if current or previous slot is unused/runner
                if (prices[i] <= 0 || prices[i - 1] <= 0)
                    continue;

                double minValid = isLong ? prices[i - 1] + tickSize : prices[i - 1] - tickSize;

                bool inverted = isLong ? (prices[i] < minValid) : (prices[i] > minValid);

@coderabbitai
coderabbitai Bot
47 minutes ago
⚠️ Potential issue | 🟠 Major | ⚡ Quick win

Compare against the last active rung, not just the previous slot.

If a middle target is unused/runner (price <= 0), the prices[i - 1] <= 0 check skips validation for the next active target too. That lets inversions survive across gaps, e.g. T1 valid, T2 unused, T3 inside T1. Track the previous non-zero rung and validate against that instead.

Possible fix
🧰 Tools
🤖 Prompt for AI Agents
@backtothefutures83-oss	Reply...
src/V12_002.PositionInfo.cs
Comment on lines +280 to +288
            int[] contracts = new int[]
            {
                case 1:
                    return pos.T1Contracts;
                case 2:
                    return pos.T2Contracts;
                case 3:
                    return pos.T3Contracts;
                case 4:
                    return pos.T4Contracts;
                case 5:
                    return pos.T5Contracts;
                default:
                    return 0;
            }
                pos.T1Contracts,
                pos.T2Contracts,
                pos.T3Contracts,
                pos.T4Contracts,
                pos.T5Contracts,
            };
            return contracts[targetNumber - 1];
@coderabbitai
coderabbitai Bot
47 minutes ago
⚠️ Potential issue | 🟠 Major | ⚡ Quick win

These getters now allocate on every call.

Lines 280, 295, 310, and 342 create fresh arrays just to index one value. In this hot path that adds GC pressure and breaks the zero-allocation behavior this refactor is supposed to preserve. Keep the switch-based accessors, or store these values in real arrays on PositionInfo so lookup stays allocation-free.

As per coding guidelines, "AMAL Empirical Vetting Protocol (V12.15): ALL high-performance C# submissions (SPSC/MPMC/Atomic) MUST pass the AMAL automated vetting gate via scripts/amal_harness.py before architectural promotion. Pass gate requires Allocated = 0 B and Mean Latency < Baseline."

Also applies to: 295-303, 310-311, 342-350

🤖 Prompt for AI Agents
@backtothefutures83-oss	Reply...
@gitar-bot
gitar-bot Bot
commented
47 minutes ago
Code Review ⚠️ Changes requested 0 resolved / 1 findings
Restores .NET Framework 4.8 compatibility by modifying the PendingStopReplacement struct, but introduces new array allocations in PositionInfo.cs that violate the zero-allocation performance requirement.

⚠️ Performance: Array-based accessors allocate on every call, breaking zero-alloc goal
🤖 Prompt for agents
Tip

Comment Gitar fix CI or enable auto-apply: gitar auto-apply:on

Options
Was this helpful? React with 👍 / 👎 | Gitar

@gitar-bot
gitar-bot Bot
commented
47 minutes ago
Code Review ⚠️ Changes requested 0 resolved / 1 findings
Restores .NET Framework 4.8 compatibility by modifying the PendingStopReplacement struct, but introduces new array allocations in PositionInfo.cs that violate the zero-allocation performance requirement.

⚠️ Performance: Array-based accessors allocate on every call, breaking zero-alloc goal
📄 src/V12_002.PositionInfo.cs:278-288 📄 src/V12_002.PositionInfo.cs:293-303 📄 src/V12_002.PositionInfo.cs:310 📄 src/V12_002.PositionInfo.cs:340-350

The refactored GetTargetContracts, GetTargetPrice, IsTargetFilled, and GetTargetFilledQuantity methods each allocate a new array on every invocation (e.g., new int[] { ... }). In a trading strategy that explicitly targets zero allocation (per the PR description and AMAL harness gate), these heap allocations on every target lookup will generate GC pressure on hot paths (order fills, bar updates). The original switch statements were allocation-free.

Note: MarkTargetFilled and SetTargetFilledQuantity correctly kept the switch pattern because they need to mutate individual fields -- but the read-only accessors should also avoid allocation.

Use if/else or a direct indexed field access without allocating an array. Example for GetTargetContracts (apply same pattern to all four methods):
🤖 Prompt for agents
Tip

Comment Gitar fix CI or enable auto-apply: gitar auto-apply:on

Options