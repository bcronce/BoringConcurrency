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
    }
}
