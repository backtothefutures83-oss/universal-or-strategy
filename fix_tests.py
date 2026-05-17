import re

with open("tests/ExecutionEngineIntegrationTests.cs", "r", encoding="utf-8") as f:
    content = f.read()

# 1. T06 body was deleted. We need to restore it.
# It should be placed right after T06 signature.
t06_sig = """    [Fact]
    public void T06_OnPositionUpdate_Flat_TriggersCleanup()
    {
        // Arrange
        // [Given: Position with filled entry and working orders]"""

t06_body = """
        var engine = new MockExecutionEngine();
        var pos = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        engine.StopOrders["LONG1"] = new MockOrder { State = OrderState.Working };
        engine.TargetOrders["LONG1"] = new MockOrder { State = OrderState.Working };

        // Act
        // [When: Position quantity goes flat]
        engine.SimulatePositionUpdate(pos, 0, 50.0);

        // Assert
        // [Then: Cleanup sequence triggered, orders cancelled]
        Assert.False(engine.ActivePositions.ContainsKey("LONG1"));
        Assert.False(engine.StopOrders.ContainsKey("LONG1"));
        Assert.False(engine.TargetOrders.ContainsKey("LONG1"));
        Assert.False(engine.Target2Orders.ContainsKey("LONG1"));
        Assert.False(engine.Target3Orders.ContainsKey("LONG1"));
    }
"""

if t06_sig in content:
    content = content.replace(t06_sig, t06_sig + t06_body)

# 2. Extract T07 and T08 and put them right after T06
t07_t08_pattern = r"(\s*\[Fact\]\s*public void T07_OnAccountOrderUpdate_Queue_Drains\(\).*?Assert\.True\(engine\.ActivePositions\.ContainsKey\(\"LONG2\"\)\);\s*VerifyOrderDictionariesConsistent\(engine\);\s*\})"
match = re.search(t07_t08_pattern, content, re.DOTALL)
if match:
    t07_t08_text = match.group(1)
    content = content.replace(t07_t08_text, "")
    insert_point = t06_sig + t06_body
    content = content.replace(insert_point, insert_point + "\n" + t07_t08_text + "\n")

# 3. T17 is separated. Signature is at 1591, body is at 1962.
t17_sig_wrong = """    [Fact]
    public void T17_AuditStopQuantityAndPrint_Mismatch_Logged()
    #region Phase 3: Trailing Stop Tests (T19-T26)"""

if t17_sig_wrong in content:
    content = content.replace(t17_sig_wrong, "    #region Phase 3: Trailing Stop Tests (T19-T26)")

# Now the body of T17 and T18
t17_body_and_t18_pattern = r"(    \{\s*// Arrange\s*// \[Given: Position with 100 contracts, stop with 90 contracts \(mismatch\)\].*?VerifyStopQuantityMatchesRemaining\(engine, \"LONG1\", 50\);\s*\})"
match = re.search(t17_body_and_t18_pattern, content, re.DOTALL)
if match:
    t17_18_text = match.group(1)
    content = content.replace(t17_18_text, "")
    
    # We must add the signature to T17
    t17_18_fixed = """    [Fact]
    public void T17_AuditStopQuantityAndPrint_Mismatch_Logged()
""" + t17_18_text

    # We need to insert it right before `#region Phase 3`
    content = content.replace("    #region Phase 3: Trailing Stop Tests (T19-T26)", t17_18_fixed + "\n    #region Phase 3: Trailing Stop Tests (T19-T26)")

# 4. Clean up stray `#endregion` and `#region` Phase 5
content = re.sub(r"    #endregion\s*#endregion\s*\[Fact\]\s*public void T33", "    #endregion\n\n    #region Phase 5: Edge Case Tests (T33-T40)\n\n    [Fact]\n    public void T33", content)
content = re.sub(r"    #endregion\s*\[Fact\]\s*public void T33", "    #endregion\n\n    #region Phase 5: Edge Case Tests (T33-T40)\n\n    [Fact]\n    public void T33", content)

content = re.sub(r"\s*#endregion\s*\}\s*\}", "\n    #endregion\n    }\n}", content)

with open("tests/ExecutionEngineIntegrationTests.cs", "w", encoding="utf-8") as f:
    f.write(content)

print("Fixed!")
