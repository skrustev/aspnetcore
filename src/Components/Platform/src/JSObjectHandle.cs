// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Platform;

// Releases a browser object when its owner is disposed.
internal interface IJSObjectChild
{
    ValueTask ReleaseAsync();
}

// Owns the browser reference of a projected object that a call created, together with the objects that belong to it,
// such as the headers of a response. Disposal is idempotent, never throws, and releases the children first.
internal sealed class JSObjectHandle : IJSObjectChild
{
    private readonly object _owner;
    private readonly IJSObjectReference _reference;
    private List<IJSObjectChild>? _children;
    private Action<JSObjectHandle>? _onDisposed;

    public JSObjectHandle(object owner, IJSObjectReference reference)
    {
        _owner = owner;
        _reference = reference;
    }

    public bool IsDisposed { get; private set; }

    public IJSObjectReference Reference
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, _owner);

            return _reference;
        }
    }

    // The reference without the disposal check, for a converter that runs after the owner was checked.
    public IJSObjectReference UncheckedReference => _reference;

    public void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, _owner);

    public void AddChild(IJSObjectChild child)
    {
        ThrowIfDisposed();

        (_children ??= []).Add(child);
    }

    public void OnDisposed(Action<JSObjectHandle> callback) => _onDisposed = callback;

    public async ValueTask DisposeAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        _onDisposed?.Invoke(this);

        if (_children is { } children)
        {
            _children = null;

            foreach (var child in children)
            {
                await child.ReleaseAsync().ConfigureAwait(false);
            }
        }

        await DisposeQuietlyAsync(_reference).ConfigureAwait(false);
    }

    ValueTask IJSObjectChild.ReleaseAsync() => DisposeAsync();

    // A disconnected circuit makes the release throw. Disposal must not throw, so the error is ignored.
    public static async ValueTask DisposeQuietlyAsync(IAsyncDisposable reference)
    {
        try
        {
            await reference.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or OperationCanceledException or ObjectDisposedException)
        {
        }
    }
}

// Acquires a browser object that belongs to another one, such as response.headers, on first use. Callers share a
// pending acquisition, and a failed or canceled one is not kept. Release does not wait for a pending acquisition.
internal sealed class LazyJSObjectReference : IJSObjectChild
{
    private readonly Func<ValueTask<IJSObjectReference>> _acquire;
    private Task<IJSObjectReference>? _referenceTask;
    private bool _released;

    public LazyJSObjectReference(Func<ValueTask<IJSObjectReference>> acquire)
    {
        _acquire = acquire;
    }

    public Task<IJSObjectReference> GetAsync()
    {
        if (_referenceTask is null || _referenceTask.IsFaulted || _referenceTask.IsCanceled)
        {
            _referenceTask = _acquire().AsTask();
        }

        return _referenceTask;
    }

    public async ValueTask ReleaseAsync()
    {
        if (_released)
        {
            return;
        }

        _released = true;

        if (_referenceTask is { IsCompletedSuccessfully: true } referenceTask)
        {
            await JSObjectHandle.DisposeQuietlyAsync(referenceTask.Result).ConfigureAwait(false);
        }
    }
}
