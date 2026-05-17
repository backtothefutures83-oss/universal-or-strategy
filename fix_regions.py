with open("tests/ExecutionEngineIntegrationTests.cs", "r", encoding="utf-8") as f:
    content = f.read()

# 1. Remove line 313 `#endregion` right before MockExecutionEngine
mock_infra_close = """        public long CreatedTicks { get; set; }
        }

        #endregion


        /// <summary>
        /// Mock ExecutionEngine main test harness."""

mock_infra_fixed = """        public long CreatedTicks { get; set; }
        }


        /// <summary>
        /// Mock ExecutionEngine main test harness."""

if mock_infra_close in content:
    content = content.replace(mock_infra_close, mock_infra_fixed)

# 2. Add #endregion before Phase 1
phase1_header = """    #region Phase 1: Callback Flow Tests (T01-T08)"""
if phase1_header in content and "#endregion\n    #region Phase 1" not in content and "#endregion\n\n    #region Phase 1" not in content:
    content = content.replace(phase1_header, "    #endregion\n\n" + phase1_header)

# 3. Add #endregion before Phase 2
phase2_header = """    #region Phase 2: Order Management Tests (T09-T18)"""
if phase2_header in content and "#endregion\n    #region Phase 2" not in content and "#endregion\n\n    #region Phase 2" not in content:
    content = content.replace(phase2_header, "    #endregion\n\n" + phase2_header)

# 4. Add #endregion before Phase 3
phase3_header = """    #region Phase 3: Trailing Stop Tests (T19-T26)"""
if phase3_header in content and "#endregion\n    #region Phase 3" not in content and "#endregion\n\n    #region Phase 3" not in content:
    content = content.replace(phase3_header, "    #endregion\n\n" + phase3_header)

# 5. Remove duplicate T26 and extra #endregions between Phase 4 and Phase 5
# Look at the block from end of T32 to Phase 5
dup_block_start = """        Assert.Single(engine.FollowerReplaceSpecs); // Still only 1 spec
    }

    #endregion"""

phase5_header = """    #region Phase 5: Edge Case Tests (T33-T40)"""

# Let's find what's between dup_block_start and phase5_header
idx1 = content.find(dup_block_start)
idx2 = content.find(phase5_header)

if idx1 != -1 and idx2 != -1 and idx1 < idx2:
    fixed_block = dup_block_start + "\n\n" + phase5_header
    content = content[:idx1] + fixed_block + content[idx2 + len(phase5_header):]

with open("tests/ExecutionEngineIntegrationTests.cs", "w", encoding="utf-8") as f:
    f.write(content)

print("Regions fixed successfully!")
