import re

with open("tests/ExecutionEngineIntegrationTests.cs", "r", encoding="utf-8") as f:
    content = f.read()

# The blocks to move:
# 1. Class closing tags that were placed too early
bad_class_close = """    #endregion

    #endregion // Test Methods
}"""

if bad_class_close in content:
    content = content.replace(bad_class_close, "    #endregion")

# 2. T25 body part 2
t25_body_part2 = """        engine.StopOrders["LONG1"] = oldStop;
        engine.PendingStopReplacements["LONG1"] = new PendingStopReplacement
        {
            OldStopOrder = oldStop,
            NewStopPrice = 49.5,
            InitiatedAt = engine.MockTime.GetTicks() - (6 * TimeSpan.TicksPerSecond)
        };

        // Act
        // [When: Update stop order (detects stale pending)]
        engine.UpdateStopOrder("LONG1", 49.75);

        // Assert
        // [Then: Stale pending cleared, new replacement initiated]
        AssertPendingReplacement(engine, "LONG1", 49.75);
    }"""

# 3. T26 full method
t26_full = """    [Fact]
    public void T26_ManageTrail_FleetSymmetrySync_FollowerIndependent()
    {
        // Arrange
        // [Given: Master position at 50.0 with Trail1, follower at 50.25 (different fill)]
        var engine = new MockExecutionEngine();
        var master = engine.CreateFilledPosition("LONG1", 100, 50.0, Direction.Long);
        master.CurrentStopPrice = 51.0;
        master.CurrentTrailLevel = 1;
        master.ExtremePriceSinceEntry = 52.5;
        
        var follower = engine.CreateFollowerPosition("LONG1", 50, 50.25, Direction.Long, "Follower1");
        follower.CurrentStopPrice = 50.75; // Different entry, different stop
        follower.CurrentTrailLevel = 0;
        follower.ExtremePriceSinceEntry = 50.25;

        // Act
        // [When: ManageTrailingStops executes]
        engine.ManageTrailingStops();

        // Assert
        // [Then: Follower uses own entry price (50.25), not master's (50.0)]
        // Follower profit = 52.5 - 50.25 = 2.25 points (>= Trail1)
        // Follower Trail1 stop = 50.25 + 1.0 = 51.25
        Assert.Equal(51.25, follower.CurrentStopPrice);
        AssertTrailLevel(engine, "LONG1_Follower1", 1);
    }"""

# Remove T25 body part 2 and T26 from their wrong place
if t25_body_part2 in content:
    content = content.replace(t25_body_part2, "")
    
if t26_full in content:
    content = content.replace(t26_full, "")
    
# Now, find where to insert them.
# They should go right before:
#     #region Phase 4: Propagation Tests (T27-T32)
# But wait, T25 part 1 is right before that!
# Let's find T25 part 1:
t25_part1 = """        var oldStop = engine.CreateOrder("LONG1_STOP", OrderAction.Sell, OrderType.StopMarket, 100, 0, 49.0);
        oldStop.State = OrderState.Working;
    #region Phase 4: Propagation Tests (T27-T32)"""

t25_fixed = """        var oldStop = engine.CreateOrder("LONG1_STOP", OrderAction.Sell, OrderType.StopMarket, 100, 0, 49.0);
        oldStop.State = OrderState.Working;
""" + t25_body_part2 + "\n\n" + t26_full + """
    #endregion

    #region Phase 4: Propagation Tests (T27-T32)"""

if t25_part1 in content:
    content = content.replace(t25_part1, t25_fixed)

# Now check if there are any remaining syntax errors in the file.
# We also have: `    #endregion\n\n\n        #endregion\n        [Fact]\n        public void T33`
# Wait, my previous python script might have left some weird stuff.
# Let's just fix the class ends.
# Make sure the end of the file has `    #endregion // Test Methods\n}\n`
# If we look at the end of the file:
#             Assert.Equal(2, pos.RemainingContracts); // Not flattened
#         }
#     #endregion
#     }
# }

with open("tests/ExecutionEngineIntegrationTests.cs", "w", encoding="utf-8") as f:
    f.write(content)

print("Fixed T25, T26 and Braces!")
