using System;
using System.Collections.Generic;
using System.Text;

namespace BoringConcurrency
{
    public interface IRemovableCollection<TItem>
    {
        IRemoveable<TItem> Enqueue(TItem item);
        bool TryDequeue(out TItem item);
        long Count();
        bool Any();
    }
}
