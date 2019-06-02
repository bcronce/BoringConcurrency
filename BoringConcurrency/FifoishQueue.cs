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
        private volatile Node m_Head = null;
        private volatile Node m_Tail = null;

        private long m_NodeCount = 0;

        public bool Any() => Interlocked.Read(ref this.m_NodeCount) > 0;

        public long Count() => Interlocked.Read(ref this.m_NodeCount);

        //Enqueue is responsible for manipulating tail
        public IRemoveable<TItem> Enqueue(TItem item)
        {
            Interlocked.Increment(ref m_NodeCount);

            var newTail = new Node(item);
            SpinWait spin = new SpinWait();
            do
            {
                var expectedTail = this.m_Tail;
                if (expectedTail != null) newTail.SetLast(expectedTail);
                var success = ReferenceEquals(Interlocked.CompareExchange(ref this.m_Tail, newTail, expectedTail), expectedTail);
                if (success) return newTail;
                spin.SpinOnce();
            } while (true);
        }

        private void SetHeadToTail() => Interlocked.CompareExchange(ref this.m_Head, this.m_Tail, null);
        private void SetHeadToNext(Node current)
        {
            var next = current.Next;

            while (next != null && !next.IsReady) next = current.Next;

            Interlocked.CompareExchange(ref this.m_Head, next, current);
        }

        //Dequeue is responsible for manipulating head
        public bool TryDequeue(out TItem item)
        {
            if (Interlocked.Read(ref this.m_NodeCount) == 0)
            {
                item = default;
                return false;
            }

            SpinWait spin = new SpinWait();
            Node localHead;
            do
            {
                localHead = this.m_Head;
                if (localHead == null)
                {
                    SetHeadToTail();
                }
                else
                {
                    if (localHead.TryTake(out item))
                    {
                        Interlocked.Decrement(ref m_NodeCount);
                        SetHeadToNext(localHead);
                        return true;
                    }
                }

                if (Interlocked.Read(ref m_NodeCount) == 0)
                {
                    item = default;
                    return false;
                }

                spin.SpinOnce();
            } while (true);
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

            public void SetLast(Node last) => this.m_Last = last;

            protected void UpdateNext(Node next)
            {
                Debug.Assert(this.m_Next.GetStatus == Status.Removing, $"Status expected to be {Status.Removing}");
                Interlocked.Exchange(ref this.m_Next, next);
            }

            public void Clear()
            {
                throw new NotImplementedException();
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
                Debug.Assert(this.m_Status == Status.Ready);
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
