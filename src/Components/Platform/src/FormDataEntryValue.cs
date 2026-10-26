// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Represents a value of a <see cref="FormData"/> entry: a <see cref="Platform.File"/> or a string.
/// </summary>
public union FormDataEntryValue(File, string);
