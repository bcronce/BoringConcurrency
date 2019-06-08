using System;
using Xunit;
using BoringConcurrency;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace UnitTests
{
    public class RemoveableCollection
    {
        [Fact]
        public void EnqueueDequeue_Once()
        {
            var collection = new FifoishQueue<int>();
            var expected = 1;

            collection.Enqueue(expected);

            if (collection.TryDequeue(out var item))
            {
                Assert.Equal(expected, item);
                return;
            }

            Assert.False(true, "Should not have gotten here");
        }

        [Fact]
        public void DequeueEmpty()
        {
            var collection = new FifoishQueue<int>();

            if (!collection.TryDequeue(out var item))
            {
                Assert.Equal(default(int), item);
                return;
            }

            Assert.False(true, "Should not have gotten here");
        }

        [Fact]
        public void EnqueueDequeue_Many()
        {
            var collection = new FifoishQueue<int>();
            var expected = new HashSet<int>(Enumerable.Range(0, 3));

            foreach (var item in expected)
            {
                collection.Enqueue(item);
            }

            var result = new HashSet<int>();
            while (collection.TryDequeue(out var item))
            {
                result.Add(item);
            }

            Assert.Subset(expected, result);
            Assert.Superset(expected, result);
        }

        [Fact]
        public void EnqueueDequeue_Any()
        {
            var collection = new FifoishQueue<int>();

            Assert.False(collection.Any());
            collection.Enqueue(default);
            Assert.True(collection.Any());

            collection.TryDequeue(out var item);
            Assert.False(collection.Any());
        }

        [Fact]
        public void EnqueueDequeue_Count()
        {
            var collection = new FifoishQueue<int>();
            const int iterations = 100;

            int expectedCount = 0;
            foreach (var item in Enumerable.Range(0, iterations))
            {
                collection.Enqueue(item);
                expectedCount++;
                Assert.Equal(expectedCount, collection.Count());
            }

            Assert.Equal(iterations, expectedCount);

            while (collection.TryDequeue(out var item))
            {
                expectedCount--;
                Assert.Equal(expectedCount, collection.Count());
            }
            Assert.Equal(0, collection.Count());
        }

        [Fact]
        public void EnqueueDequeue_Count_Removal()
        {
            var collection = new FifoishQueue<int>();
            const int iterations = 100;

            int expectedCount = 0;
            var removalCollection = new List<IRemoveable<int>>();
            foreach (var item in Enumerable.Range(0, iterations))
            {
                removalCollection.Add(collection.Enqueue(item));
                expectedCount++;
                Assert.Equal(expectedCount, collection.Count());
            }

            Assert.Equal(iterations, expectedCount);

            foreach(var node in removalCollection)
            {
                Assert.True(node.TryRemove(out var ignore));
                expectedCount--;
            }
            Assert.Equal(0, collection.Count());
        }

        [Fact]
        public void EnqueueDequeue_ManyConcurrent()
        {
            var collection = new FifoishQueue<int>();
            var expected = new HashSet<int>(Enumerable.Range(0, 10_000_000));

            Parallel.ForEach(expected, item => collection.Enqueue(item));

            var result = new System.Collections.Concurrent.ConcurrentQueue<int>();
            Parallel.ForEach(expected, ignore => {
                if (collection.TryDequeue(out var dequeuedItem)) result.Enqueue(dequeuedItem);
            });
            var resultSet = new HashSet<int>(result);

            Assert.Subset(expected, resultSet);
            Assert.Superset(expected, resultSet);
        }

        [Fact]
        public void ProducerConsumer_Concurrent()
        {
            var collection = new FifoishQueue<int>();
            var expected = new HashSet<int>(Enumerable.Range(0, 100_000_000));

            var produce = Task.Run(() => Parallel.ForEach(expected, item => collection.Enqueue(item)));

            var result = new System.Collections.Concurrent.ConcurrentQueue<int>();
            var consume = Task.Run(() =>
                Parallel.ForEach(expected, ignore => {
                    int dequeuedItem;
                    while (!collection.TryDequeue(out dequeuedItem)) ;
                    result.Enqueue(dequeuedItem);
                })
            );

            Task.WaitAll(produce, consume);

            var resultSet = new HashSet<int>(result);

            Assert.Subset(expected, resultSet);
            Assert.Superset(expected, resultSet);
        }

        [Fact]
        public void Removal()
        {
            var collection = new FifoishQueue<long>();
            var expected = new HashSet<long>(Enumerable.Range(0, 100).Select(x => (long)x));
            long expectedValue = expected.Sum();

            var removal = new List<IRemoveable<long>>();
            //Prime the collection
            foreach (var item in expected)
            {
                removal.Add(collection.Enqueue(item));
            }

            long result = 0;
            foreach (var remove in removal)
            {
                if (remove.TryRemove(out var item)) result += item;
            }

            Assert.Equal(expectedValue, result);
        }

        [Fact]
        public void ConcurrentRemoval()
        {
            var collection = new FifoishQueue<long>();
            var expected = new HashSet<long>(Enumerable.Range(0, 10_000_000).Select(x => (long)x));
            long expectedValue = expected.Sum();

            var removal = new List<IRemoveable<long>>();
            //Prime the collection
            foreach (var item in expected)
            {
                removal.Add(collection.Enqueue(item));
            }

            long result = 0;
            Parallel.ForEach(removal, node => {
                if (node.TryRemove(out var item)) Interlocked.Add(ref result, item);
            });

            Assert.Equal(expectedValue, result);
        }
    }
}
