// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components.Platform;

internal sealed class BrowserPlatform : IBrowserPlatform, IAsyncDisposable
{
    public BrowserPlatform(Microsoft.JSInterop.IJSRuntime jsRuntime, IServiceProvider services)
    {
        Window = new Window(jsRuntime, services);
    }

    public Window Window { get; }

    public ValueTask DisposeAsync()
    {
        return ((IInternalAsyncDisposal)Window).InternalDisposeAsync();
    }
}
