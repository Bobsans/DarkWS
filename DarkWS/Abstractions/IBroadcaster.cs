namespace DarkWS.Abstractions;

/// <summary>Publishes notifications through the configured backplane. Recipients share an immutable envelope with id @. Cancellation stops publishing, not delivery that has started.</summary>
public interface IBroadcaster {
    /// <summary>Publishes an action and optional data to all connections through the backplane.</summary>
    Task BroadcastAsync(string action, CancellationToken cancellationToken = default);

    /// <summary>Publishes an action and optional data to all connections through the backplane.</summary>
    Task BroadcastAsync<T>(string action, T? data, CancellationToken cancellationToken = default);

    /// <summary>Publishes an action and optional data to the specified connection id.</summary>
    Task BroadcastToConnectionAsync(string connectionId, string action, CancellationToken cancellationToken = default);

    /// <summary>Publishes an action and optional data to the specified connection id.</summary>
    Task BroadcastToConnectionAsync<T>(string connectionId, string action, T? data, CancellationToken cancellationToken = default);

    /// <summary>Publishes an action and optional data to connections in the specified session.</summary>
    Task BroadcastToSessionAsync(string sessionId, string action, CancellationToken cancellationToken = default);

    /// <summary>Publishes an action and optional data to connections in the specified session.</summary>
    Task BroadcastToSessionAsync<T>(string sessionId, string action, T? data, CancellationToken cancellationToken = default);

    /// <summary>Publishes an action and optional data to members of the specified group.</summary>
    Task BroadcastToGroupAsync(string group, string action, CancellationToken cancellationToken = default);

    /// <summary>Publishes an action and optional data to members of the specified group.</summary>
    Task BroadcastToGroupAsync<T>(string group, string action, T? data, CancellationToken cancellationToken = default);

    /// <summary>Publishes to one group with explicit connection/session exclusions.</summary>
    Task BroadcastToGroupAsync(string group, string action, DarkWsBroadcastExclusion except) =>
        BroadcastToGroupAsync(group, action, except, CancellationToken.None);

    /// <summary>Publishes to one group with explicit connection/session exclusions.</summary>
    Task BroadcastToGroupAsync(string group, string action, DarkWsBroadcastExclusion except, CancellationToken cancellationToken) =>
        BroadcastToGroupsAsync([group], action, except, cancellationToken);

    /// <summary>Publishes data to one group with explicit connection/session exclusions.</summary>
    Task BroadcastToGroupAsync<T>(string group, string action, T? data, DarkWsBroadcastExclusion except) =>
        BroadcastToGroupAsync(group, action, data, except, CancellationToken.None);

    /// <summary>Publishes data to one group with explicit connection/session exclusions.</summary>
    Task BroadcastToGroupAsync<T>(string group, string action, T? data, DarkWsBroadcastExclusion except, CancellationToken cancellationToken) =>
        BroadcastToGroupsAsync([group], action, data, except, cancellationToken);

    /// <summary>Publishes once to the union of groups, delivering once per connection. Empty groups are a no-op.</summary>
    Task BroadcastToGroupsAsync(IEnumerable<string> groups, string action) =>
        BroadcastToGroupsAsync(groups, action, null, CancellationToken.None);

    /// <summary>Publishes once to the union of groups, with optional exclusions. Empty groups are a no-op.</summary>
    Task BroadcastToGroupsAsync(IEnumerable<string> groups, string action, DarkWsBroadcastExclusion? except) =>
        BroadcastToGroupsAsync(groups, action, except, CancellationToken.None);

    /// <summary>Publishes once to the union of groups, delivering once per connection. Empty groups are a no-op.</summary>
    /// <remarks>Custom broadcasters must implement this operation to support group unions and exclusions.</remarks>
    Task BroadcastToGroupsAsync(IEnumerable<string> groups, string action, DarkWsBroadcastExclusion? except, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This broadcaster does not support group unions and exclusions.");

    /// <summary>Publishes data once to the union of groups, delivering once per connection. Empty groups are a no-op.</summary>
    Task BroadcastToGroupsAsync<T>(IEnumerable<string> groups, string action, T? data) =>
        BroadcastToGroupsAsync(groups, action, data, null, CancellationToken.None);

    /// <summary>Publishes data once to the union of groups, with optional exclusions. Empty groups are a no-op.</summary>
    Task BroadcastToGroupsAsync<T>(IEnumerable<string> groups, string action, T? data, DarkWsBroadcastExclusion? except) =>
        BroadcastToGroupsAsync(groups, action, data, except, CancellationToken.None);

    /// <summary>Publishes data once to the union of groups, delivering once per connection. Empty groups are a no-op.</summary>
    /// <remarks>Custom broadcasters must implement this operation to support group unions and exclusions.</remarks>
    Task BroadcastToGroupsAsync<T>(IEnumerable<string> groups, string action, T? data, DarkWsBroadcastExclusion? except, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This broadcaster does not support group unions and exclusions.");
}
