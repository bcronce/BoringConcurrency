using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using BoringConcurrency.Common;
using System.Diagnostics;

namespace BoringConcurrency
{
    public class FifoishQueue<TItem> : IRemovableCollection<TItem>
    {
        const int maxLoopCount = 100_000_000; //Prevent infinite loop
        private volatile Node m_Head = null;
        private volatile Node m_Tail = null;

        private volatile int m_NodeCount = 0;

        public bool Any() => this.m_NodeCount > 0;

        public long Count() => this.m_NodeCount;

        public IRemoveable<TItem> Enqueue(TItem item)
        {
            Debug.Assert(this.m_Head != null);
            Debug.Assert(this.m_Tail != null);
            var currentCount = Interlocked.Increment(ref m_NodeCount);
            if (currentCount < 0)
            {
                Interlocked.Decrement(ref m_NodeCount);
                throw new InvalidOperationException("Too many items added to collection");
            }

            var newTail = new Node(item);
            var assumedTail = this.m_Tail;

            newTail.SetLast(assumedTail);
            while (!assumedTail.TrySetNext(newTail))
            {
                assumedTail = assumedTail.Next;
                newTail.SetLast(assumedTail);
            }
            this.m_Tail = newTail;

            return newTail;
        }

        public bool TryDequeue(out TItem item)
        {
            Debug.Assert(this.m_Head != null);
            Debug.Assert(this.m_Tail != null);
            if (this.m_NodeCount == 0)
            {
                item = default;
                return false;
            }

            Node localHead = this.m_Head;
            while (true)
            {
                if (localHead.TryTake(out item))
                {
                    Interlocked.Decrement(ref m_NodeCount);
                    this.m_Head = localHead.Next == null ? localHead : localHead.Next;
                    return true;
                }
                else if (localHead.Next == null) return false; //We reached the end
                else localHead = localHead.Next; //Get next node and try again
            }
        }

        public FifoishQueue()
        {
            var placeholder = Node.GetDoneNode();
            this.m_Head = placeholder;
            this.m_Tail = placeholder;
        }

        protected class Node : IRemoveable<TItem>
        {
            private TItem m_Value;
            private volatile Node m_Last;
            private volatile Node m_Next;

            public Node Next => this.m_Next;
            public bool IsReady => this.m_Status == Status.Ready;

            private Status m_Status;
            protected Status GetStatus => this.m_Status;

            protected enum Status
            {
                Ready,
                Consuming,
                MarkedForRemoval,
                Removing,
                Done
            }

            public static Node GetDoneNode() => new Node();

            protected Node() => this.m_Status = Status.Done;

            public void SetLast(Node last) => this.m_Last = last;

            protected void UpdateNext(Node next)
            {
                Debug.Assert(this.m_Next.GetStatus == Status.Removing, $"Status expected to be {Status.Removing}");
                Interlocked.Exchange(ref this.m_Next, next);
            }

            public bool TryTake(out TItem item)
            {
                var currentStatus = this.m_Status;
                if (currentStatus != Status.Ready)
                {
                    item = default;
                    return false;
                }

                if (InterlockedEnum.CompareExchange(ref this.m_Status, Status.Done, currentStatus) == Status.Ready)
                {
                    item = this.m_Value;
                    this.m_Value = default;
                    this.m_Last = null; //Unanchor the last node.
                    return true;
                }
                else
                {
                    item = default;
                    return false;
                }
            }

            private void Remove()
            {
                if (this.m_Next == null)
                {
                    throw new NotImplementedException();
                }

                var result = InterlockedEnum.Exchange(ref m_Status, Status.Done);
                Debug.Assert(result == Status.Removing, $"Unexpected status {result}");
            }
            public bool TryRemove(out TItem item)
            {
                var result = InterlockedEnum.CompareExchange(ref this.m_Status, Status.MarkedForRemoval, Status.Ready);

                if (result == Status.Ready)
                {
                    Remove();
                    item = this.m_Value;
                    this.m_Value = default;
                    return true;
                }
                else
                {
                    item = default;
                    return false;
                }
            }

            public bool TrySetNext(Node next)
            {
                Debug.Assert(this.m_Status == Status.Ready || this.m_Status == Status.Done);
                return Interlocked.CompareExchange(ref this.m_Next, next, null) == null;
            }

            public Node(TItem item)
            {
                this.m_Status = Status.Ready;
                this.m_Value = item;
                this.m_Last = null;
                this.m_Next = null;
            }
        }
    }
}
