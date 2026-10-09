namespace Delta.ECS;

using System.ComponentModel;

/// <summary>Executor used by API anchors that require a generated operation.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct ThrowingOperationInvoker : IEcsOperationInvoker
{
    /// <summary>Throws because the generated operation form was not selected.</summary>
    public void Invoke() => ThrowHelper.ThrowGeneratedIterationRequired();
}

/// <summary>Executor used by generated-operation anchors with caller-owned state.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct ThrowingOperationInvoker<TState> : IEcsOperationInvoker<TState>
{
    /// <summary>Throws because the generated operation form was not selected.</summary>
    public void Invoke(ref TState state) => ThrowHelper.ThrowGeneratedIterationRequired();
}
