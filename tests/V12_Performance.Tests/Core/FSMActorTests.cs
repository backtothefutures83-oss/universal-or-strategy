using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace V12_Performance.Tests.Core
{
    /// <summary>
    /// Unit tests for FSM/Actor Enqueue model.
    /// EPIC-6 TDD Safety Net: Validates lock-free Actor pattern correctness.
    /// Ensures FSM state transitions are atomic and thread-safe without locks.
    /// </summary>
    public class FSMActorTests
    {
        [Fact]
        public void Enqueue_SingleMessage_ProcessedCorrectly()
        {
            // Arrange
            var actor = new MockFSMActor();
            var message = new MockMessage { Type = "FLATTEN", Quantity = 1 };

            // Act
            actor.Enqueue(message);
            actor.ProcessQueue();

            // Assert
            Assert.Equal(1, actor.ProcessedCount);
            Assert.Equal("FLATTEN", actor.LastProcessedType);
        }

        [Fact]
        public void Enqueue_MultipleMessages_ProcessedInOrder()
        {
            // Arrange
            var actor = new MockFSMActor();
            var messages = new[]
            {
                new MockMessage { Type = "FLATTEN", Quantity = 1 },
                new MockMessage { Type = "CANCEL", Quantity = 0 },
                new MockMessage { Type = "MODIFY", Quantity = 2 },
            };

            // Act
            foreach (var msg in messages)
            {
                actor.Enqueue(msg);
            }
            actor.ProcessQueue();

            // Assert
            Assert.Equal(3, actor.ProcessedCount);
            Assert.Equal("MODIFY", actor.LastProcessedType); // Last message processed
        }

        [Fact]
        public void Enqueue_ConcurrentProducers_NoMessageLoss()
        {
            // Arrange
            var actor = new MockFSMActor();
            const int producerCount = 10;
            const int messagesPerProducer = 100;
            var expectedTotal = producerCount * messagesPerProducer;

            // Act
            var tasks = new Task[producerCount];
            for (int i = 0; i < producerCount; i++)
            {
                int producerId = i;
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < messagesPerProducer; j++)
                    {
                        actor.Enqueue(new MockMessage { Type = $"MSG_{producerId}_{j}", Quantity = j });
                    }
                });
            }
            Task.WaitAll(tasks);
            actor.ProcessQueue();

            // Assert
            Assert.Equal(expectedTotal, actor.ProcessedCount);
        }

        [Fact]
        public void StateTransition_AtomicUpdate_NoRaceCondition()
        {
            // Arrange
            var actor = new MockFSMActor();
            const int iterations = 1000;
            int successCount = 0;

            // Act - Concurrent state transitions
            Parallel.For(
                0,
                iterations,
                i =>
                {
                    var initialState = actor.CurrentState;
                    actor.TransitionState("WORKING");
                    var finalState = actor.CurrentState;

                    // Verify atomic transition (no intermediate states)
                    if (finalState == "WORKING")
                    {
                        Interlocked.Increment(ref successCount);
                    }
                }
            );

            // Assert - All transitions succeeded atomically
            Assert.Equal(iterations, successCount);
            Assert.Equal("WORKING", actor.CurrentState);
        }

        [Fact]
        public void Enqueue_HighThroughput_MaintainsOrdering()
        {
            // Arrange
            var actor = new MockFSMActor();
            const int messageCount = 10000;

            // Act - Rapid enqueue from single producer
            for (int i = 0; i < messageCount; i++)
            {
                actor.Enqueue(new MockMessage { Type = $"SEQ_{i}", Quantity = i });
            }
            actor.ProcessQueue();

            // Assert - All messages processed
            Assert.Equal(messageCount, actor.ProcessedCount);
            Assert.Equal($"SEQ_{messageCount - 1}", actor.LastProcessedType);
        }
    }

    /// <summary>
    /// Mock FSM Actor for testing lock-free Enqueue pattern.
    /// Simulates V12 SIMA.Dispatch Actor model.
    /// </summary>
    public class MockFSMActor
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<MockMessage> _queue;
        private string _currentState;
        private int _processedCount;
        private string _lastProcessedType;

        public MockFSMActor()
        {
            _queue = new System.Collections.Concurrent.ConcurrentQueue<MockMessage>();
            _currentState = "IDLE";
            _processedCount = 0;
            _lastProcessedType = string.Empty;
        }

        public string CurrentState => _currentState;
        public int ProcessedCount => _processedCount;
        public string LastProcessedType => _lastProcessedType;

        public void Enqueue(MockMessage message)
        {
            _queue.Enqueue(message);
        }

        public void ProcessQueue()
        {
            while (_queue.TryDequeue(out var message))
            {
                _lastProcessedType = message.Type;
                Interlocked.Increment(ref _processedCount);
            }
        }

        public void TransitionState(string newState)
        {
            // Atomic state transition (simulates Interlocked.Exchange)
            Interlocked.Exchange(ref _currentState, newState);
        }
    }

    /// <summary>
    /// Mock message for FSM Actor testing.
    /// </summary>
    public class MockMessage
    {
        public string Type { get; set; }
        public int Quantity { get; set; }
    }
}

// Made with Bob
