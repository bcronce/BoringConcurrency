using System;
using System.Collections.Generic;
using System.Text;

namespace BoringConcurrency
{
    public interface IRemoveable<TItem>
    {
        bool TryRemove(out TItem item);
    }
}
