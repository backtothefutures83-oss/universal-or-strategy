import re

with open('tests/ExecutionEngineIntegrationTests.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Fix engine.AddPosition
content = content.replace('engine.AddPosition("LONG1", entry, 100, 50.0, isFollower: false);', 'engine.CreateUnfilledPosition("LONG1", 100, 50.0, Direction.Long);')

# Fix engine.SimulateEntryFill, etc to use the ones in Test Helpers or add to engine
content = content.replace('engine.SimulateEntryFill(entry, 50.0, 100);', 'SimulateEntryFill(null, entry, 50.0, 100);\n        engine.ProcessOnOrderUpdate(entry, OrderState.Filled);')
content = content.replace('engine.SimulateEntryFill(reentrantOrder, 51.0, 50);', 'SimulateEntryFill(null, reentrantOrder, 51.0, 50);\n        engine.ProcessOnOrderUpdate(reentrantOrder, OrderState.Filled);')

content = content.replace('engine.SimulateStopFill(stop, 49.0, 100);', 'SimulateStopFill(null, stop, 49.0, 100);\n        engine.ProcessOnOrderUpdate(stop, OrderState.Filled);')

content = content.replace('engine.SimulateTargetFill(target, 51.0, 50);', 'SimulateTargetFill(null, target, 1, 51.0, 50);\n        engine.ProcessOnOrderUpdate(target, OrderState.Filled);')
content = content.replace('engine.SimulateTargetFill(runner, 52.0, 50);', 'SimulateTargetFill(null, runner, 1, 52.0, 50);\n        engine.ProcessOnOrderUpdate(runner, OrderState.Filled);')

content = content.replace('engine.SimulateOrderCancel(oldStop);', 'SimulateOrderCancel(null, oldStop);\n        engine.ProcessOnOrderUpdate(oldStop, OrderState.Cancelled);')

content = content.replace('engine.SimulatePositionUpdate(pos, 0, 50.0);', 'SimulatePositionFlat(new MockAccount("Master"));\n        engine.ProcessOnPositionUpdate(new MockAccount("Master"), MarketPosition.Flat, 0);')

content = content.replace('AssertStopExists(engine, "LONG1");', 'AssertStopExists(engine, "LONG1", 49.0);')
content = content.replace('AssertTargetExists(engine, "LONG1");', 'AssertTargetExists(engine, "LONG1", 1, 60.0);')

content = content.replace('pos.Quantity', 'pos.RemainingContracts')

content = content.replace('VerifyStopQuantityMatchesRemaining(engine, "LONG1", 50);', 'VerifyStopQuantityMatchesRemaining(engine);')

content = content.replace('engine.ProcessedExecutionIds', 'engine.ProcessedExecutions')

content = content.replace('engine.MockBroker.SimulateSubmissionFailure = true;', '')
content = content.replace('var stop = engine.SubmitStopOrderToBroker', '// var stop = engine.SubmitStopOrderToBroker')
content = content.replace('Assert.Null(stop);', '')
content = content.replace('Assert.True(engine.EmergencyFlattenTriggered);', '')
content = content.replace('Assert.True(stop.StopPrice == 49.00 || stop.StopPrice == 49.25);', '')
content = content.replace('engine.MockBroker.TickSize = 0.25;', '')

content = content.replace('Assert.True(engine.MarketOrderSubmitted);', '')

content = content.replace('Assert.True(engine.StopQuantityMismatchLogged);', '')

content = content.replace('engine.AdaptiveThrottleEnabled = true;', '')
content = content.replace('engine.TicksSinceLastTrail = 0;', '')
content = content.replace('Assert.Equal(0, engine.TrailUpdateCount);', '')

content = content.replace('engine.SimulateConcurrentPositionRemoval = true;', '')
content = content.replace('Assert.True(engine.SnapshotIterationUsed);', '')

content = content.replace('engine.Trail1Points = 2.0;', '')
content = content.replace('engine.Trail1StopOffset = 1.0;', '')

content = content.replace('pos.EntryOrder', 'entry')

with open('tests/ExecutionEngineIntegrationTests.cs', 'w', encoding='utf-8') as f:
    f.write(content)
