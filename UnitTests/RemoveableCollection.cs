using System;
using Xunit;
using BoringConcurrency;
using System.Linq;
using System.Collections.Generic;

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
    }
}
