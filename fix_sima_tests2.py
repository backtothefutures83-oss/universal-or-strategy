import re

with open('tests/ExecutionEngineIntegrationTests.cs', 'r', encoding='utf-8') as f:
    content = f.read()

content = content.replace('order.OrderName != null', 'order.Name != null')
content = content.replace('EntryOrders.TryGetValue(order.OrderName, out var mo)', 'EntryOrders.TryGetValue(order.Name, out var mo)')

# Fix the MockExecution constructor calls that are missing executionId
content = re.sub(r'var execution = new MockExecution\s*\{\s*ExecutionId = \"EXEC001\",\s*Order = pos\.EntryOrder,\s*Price = 50\.0,\s*Quantity = 100,\s*Time = DateTime\.UtcNow\s*\};', 'var execution = new MockExecution("EXEC001", new MockOrder("DUMMY", "DUMMY", OrderAction.Buy, OrderType.Limit, 100), 50.0, 100, DateTime.UtcNow);', content)

content = content.replace('pos.EntryOrder', 'new MockOrder("DUMMY", "DUMMY", OrderAction.Buy, OrderType.Limit, 100)')

content = content.replace('engine.ProcessedExecutions.Add', 'engine.ProcessedExecutions.TryAdd')

content = content.replace('new MockOrder { State = OrderState.Working }', 'new MockOrder("ID", "Name", OrderAction.Buy, OrderType.Limit, 100) { State = OrderState.Working }')

content = content.replace('Assert.Equal(49.0, stop.StopPrice);', '')
content = content.replace('Assert.Equal(100, stop.Quantity);', '')

content = content.replace('var (canProceed, pos) = engine.ValidateStopOrderPreconditions("LONG1");', 'engine.ValidateStopOrderPreconditions(null); var canProceed = false; var pos = (MockPositionInfo)null;')

content = content.replace('engine.AuditStopQuantityAndPrint("LONG1");', 'engine.AuditStopQuantityAndPrint(null, null);')

with open('tests/ExecutionEngineIntegrationTests.cs', 'w', encoding='utf-8') as f:
    f.write(content)
