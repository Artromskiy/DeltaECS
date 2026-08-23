namespace Delta.ECS;

using System;

public ref partial struct ReadValues
{
    private readonly ref byte _data;
    internal ReadValues(Array row) => _data = ref ArrayAccess.DataReference(row);
}

public ref partial struct WriteValues
{
    private readonly ref byte _data;
    internal WriteValues(Array row) => _data = ref ArrayAccess.DataReference(row);
}
