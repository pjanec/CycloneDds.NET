#pragma warning disable CS1591
using System;

namespace CycloneDDS.Schema;

public unsafe ref struct FixedString32View
{
    private readonly FixedString32* _ptr;

    public FixedString32View(FixedString32* ptr)
    {
        _ptr = ptr;
    }

    public FixedString32 ToManaged()
    {
        if (_ptr == null) return default;
        return *_ptr;
    }

    public override string ToString() => _ptr == null ? string.Empty : _ptr->ToString();
}

public unsafe ref struct FixedString64View
{
    private readonly FixedString64* _ptr;

    public FixedString64View(FixedString64* ptr)
    {
        _ptr = ptr;
    }

    public FixedString64 ToManaged()
    {
        if (_ptr == null) return default;
        return *_ptr;
    }

    public override string ToString() => _ptr == null ? string.Empty : _ptr->ToString();
}

public unsafe ref struct FixedString128View
{
    private readonly FixedString128* _ptr;

    public FixedString128View(FixedString128* ptr)
    {
        _ptr = ptr;
    }

    public FixedString128 ToManaged()
    {
        if (_ptr == null) return default;
        return *_ptr;
    }

    public override string ToString() => _ptr == null ? string.Empty : _ptr->ToString();
}

public unsafe ref struct FixedString256View
{
    private readonly FixedString256* _ptr;

    public FixedString256View(FixedString256* ptr)
    {
        _ptr = ptr;
    }

    public FixedString256 ToManaged()
    {
        if (_ptr == null) return default;
        return *_ptr;
    }

    public override string ToString() => _ptr == null ? string.Empty : _ptr->ToString();
}
