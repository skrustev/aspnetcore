// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Represents the body of a request: a string, bytes, a <see cref="Blob"/>, or a <see cref="FormData"/>.
/// Each case converts to <see cref="BodyInit"/> implicitly.
/// </summary>
public union BodyInit(string, byte[], Blob, FormData);
