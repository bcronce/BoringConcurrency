﻿using System;
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

        private volatile int m_NodeCount = 0;
        private readonly Action m_OnRemoval;

        public bool Any() => this.m_NodeCount > 0;

        public long Count() => this.m_NodeCount;

        public IRemoveable<TItem> Enqueue(TItem item)
        {
            Debug.Assert(this.m_Head != null);
            Debug.Assert(this.m_Tail != null);
            var currentCount = Interlocked.Increment(ref this.m_NodeCount);
            if (currentCount < 0)
            {
                Interlocked.Decrement(ref this.m_NodeCount);
                throw new InvalidOperationException("Too many items added to collection");
            }

            var newTail = new Node(item, this.m_OnRemoval);
            var localTail = this.m_Tail;
            newTail.SetLast(localTail);
            if (localTail.TrySetNext(newTail))
            {
                this.m_Tail = newTail;
                return newTail;
            }

            var spin = new SpinWait();
            do
            {
                localTail = this.m_Tail;
                newTail.SetLast(localTail);

                if (localTail.TrySetNext(newTail))
                {
                    this.m_Tail = newTail;
                    return newTail;
                }
                spin.SpinOnce();
            }
            while (true);
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
            if (localHead.TryTake(out item))
            {
                Interlocked.Decrement(ref this.m_NodeCount);
                if (localHead.Next != null) this.m_Head = localHead.Next;
                return true;
            }
            else if (localHead.Next == null) return false; //We reached the end

            var spin = new SpinWait();
            do
            {
                localHead = this.m_Head;
                if (localHead.TryTake(out item))
                {
                    Interlocked.Decrement(ref this.m_NodeCount);
                    if (localHead.Next != null) this.m_Head = localHead.Next;
                    return true;
                }
                else if (localHead.Next == null) return false; //We reached the end
                else this.m_Head = localHead.Next;
                spin.SpinOnce();
            }
            while (true);
        }

        public FifoishQueue()
        {
            var placeholder = Node.GetDoneNode();
            this.m_Head = placeholder;
            this.m_Tail = placeholder;
            this.m_OnRemoval = () => { Interlocked.Decrement(ref this.m_NodeCount); };
        }

        protected class Node : IRemoveable<TItem>
        {
            private TItem m_Value;
            private volatile Node m_Last;
            private volatile Node m_Next;

            public Node Next => this.m_Next;
            public bool IsReady => this.m_Status == Status.Ready;

            private Status m_Status;
            private Action m_OnRemoval;
            protected Status GetStatus => this.m_Status;

            protected enum Status
            {
                Ready,
                Consuming,
                MarkedForRemoval,
                Done
            }

            public static Node GetDoneNode() => new Node();

            protected Node() => this.m_Status = Status.Done;

            public void SetLast(Node last) => this.m_Last = last;

            protected void UpdateNext(Node next) => this.m_Next = next;

            public bool TryTake(out TItem item)
            {
                var currentStatus = this.m_Status;
                if (currentStatus != Status.Ready)
                {
                    item = default;
                    return false;
                }

                if (InterlockedEnum<Status>.CompareExchange(ref this.m_Status, Status.Done, currentStatus) == Status.Ready)
                {
                    this.m_Last = null;
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
                Node localNext = this.m_Next;
                if (localNext != null && localNext.m_Status != Status.Ready)
                {
                    localNext = localNext.Next;
                    if (localNext != null && localNext.m_Status != Status.Ready)
                    {
                        do
                        {
                            localNext = localNext.Next;
                        }
                        while (localNext != null && localNext.m_Status != Status.Ready);
                    }
                }
                this.m_Next = localNext;

                if (this.m_Last != null) this.m_Last.UpdateNext(this.m_Next);
            }
            public bool TryRemove(out TItem item)
            {
                var result = InterlockedEnum<Status>.CompareExchange(ref this.m_Status, Status.MarkedForRemoval, Status.Ready);

                if (result == Status.Ready)
                {
                    this.Remove();
                    this.m_Status = Status.Done;
                    this.m_Last = null;
                    this.m_OnRemoval();
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
                Debug.Assert(!ReferenceEquals(this, next));
                if (this.m_Next != null) return false;
                return Interlocked.CompareExchange(ref this.m_Next, next, null) == null;
            }

            public Node(TItem item, Action onRemoval)
            {
                this.m_OnRemoval = onRemoval;
                this.m_Status = Status.Ready;
                this.m_Value = item;
                this.m_Last = null;
                this.m_Next = null;
            }
        }
    }
}
