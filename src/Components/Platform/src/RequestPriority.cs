// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Specifies the priority of a request relative to other requests of the same type.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RequestPriority>))]
public enum RequestPriority
{
    /// <summary>
    /// A higher priority.
    /// </summary>
    [JsonStringEnumMemberName("high")]
    High,

    /// <summary>
    /// A lower priority.
    /// </summary>
    [JsonStringEnumMemberName("low")]
    Low,

    /// <summary>
    /// The priority that the browser chooses.
    /// </summary>
    [JsonStringEnumMemberName("auto")]
    Auto,
}
