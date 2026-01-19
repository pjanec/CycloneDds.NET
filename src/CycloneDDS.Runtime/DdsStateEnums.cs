using System;

namespace CycloneDDS.Runtime
{
    [Flags]
    public enum DdsSampleState : uint
    {
        Read = 1,
        NotRead = 2,
        Any = Read | NotRead
    }

    [Flags]
    public enum DdsViewState : uint
    {
        New = 4,
        NotNew = 8,
        Any = New | NotNew
    }

    [Flags]
    public enum DdsInstanceState : uint
    {
        Alive = 16,
        NotAliveDisposed = 32,
        NotAliveNoWriters = 64,
        Any = Alive | NotAliveDisposed | NotAliveNoWriters
    }
}
