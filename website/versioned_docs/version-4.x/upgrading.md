---
sidebar_position: 8
title: Upgrading to 4.0
---

# Upgrading to 4.0

All packages share one version. DarkWS 4.0 changes the wire protocol and has no
fallback to the 3.x format, so upgrade the server and every client together, and roll
them back together if needed. There is no persisted data to migrate. The full list of
changes is in the
[changelog](https://github.com/Bobsans/DarkWS/blob/main/CHANGELOG.md).

## Wire protocol

| | 3.x | 4.0 |
| --- | --- | --- |
| Request arguments | `payload` field | `data` field: `{ "id", "action", "data"? }` |
| Broadcasts | Nested envelope | Flat envelope: `{ "id": "@", "action", "data"? }` |
| Authentication | JSON request `darkws:authenticate`; text authentication answered with a JSON `@auth` message | Text `auth:<token>`, answered with text `auth:success` or `auth:failed` |
| Logout | JSON request `darkws:logout` | Text `logout`, answered with text `logout:success` |

Response and error envelopes, the text `ping`/`pong` heartbeat, and the Redis
backplane envelope are unchanged. `darkws:authenticate` and `darkws:logout` are no
longer system commands, and the server no longer emits `@auth` replies. `@auth`
stays a reserved request id. See [Protocol](protocol.md).

## Server code

- The changelog lists no source changes for handlers, authenticators, middleware,
  or broadcasts.
- `InputMessage.Payload` and the payload parameters of SDK methods keep their names;
  on the wire they map to `data`.
- `DarkWsOptions.AuthenticationFailedError` still compiles but has no effect: a
  rejected text authentication always replies `auth:failed`.

## Browser client

- Upgrade the `darkws` package together with the server.
- The `message` event receives the full flat broadcast envelope. Read `action` and
  `data` from it:

  ```ts
  import DarkWs from "darkws";

  const client = new DarkWs({ secure: location.protocol === "https:", path: "/ws" });
  client.on("message", message => {
    const { action, data } = message as { id: "@"; action: string; data?: unknown };
    console.log(action, data);
  });
  ```

- `authenticate(token)` sends `auth:<token>` and resolves on `auth:success`;
  `auth:failed` rejects with `ErrorResponse` and clears the server session.
  `logout()` sends `logout` and resolves on `logout:success`.
- Authentication and logout run one at a time. If a reply does not arrive within
  `requestTimeout`, the client closes the socket and rejects the queued commands, so
  a late reply cannot confirm a later command.
- The `send` event reports these commands as local metadata without the token.

## .NET client

4.0 adds `DarkWS.Client`, a standalone .NET client, and
`DarkWS.Client.DependencyInjection` for optional DI registration. Both speak only
the 4.0 protocol. See [.NET client](clients/dotnet.md).

## Custom clients

- Rename the request field `payload` to `data`.
- Read a broadcast's `action` and `data` at the top level of the message.
- Replace JSON authentication and logout requests with the text commands `auth:<token>`
  and `logout`, and do not wait for a JSON `@auth` reply.
- Send at most one `auth:`/`logout` at a time. If its reply does not arrive in time,
  close the socket instead of sending the next command.

## Coming from 2.x

Apply the 3.0 migration as well:

- Declare payload parameters nullable where missing or null input is intentional.
  Invalid payloads return `darkws:error:invalid-request`; malformed registrations and
  invalid options fail at startup.
- Remove `[Authorize]` and policy attributes from handlers and actions; they now fail
  registration. Enforce domain permissions in handlers.
- Call `AddDarkWs()` once; a second call throws `InvalidOperationException`.
- Review connection limits: 16 in-flight requests per connection, a 30-second send
  timeout, 30-second keep-alive and pong deadlines on .NET 9/10, and a 2-minute
  receive-idle timeout on .NET 8. Idle .NET 8 clients must send application traffic,
  such as `ping`.
- After changing a connection's groups outside authentication, call
  `ConnectionStorage.Add(connection)` to refresh the indexes.
- The static `Configuration` and `RedisConfiguration` classes still work but are
  obsolete. Use the extension methods `services.AddDarkWs()`, `endpoints.MapDarkWs()`,
  and `services.AddDarkWsRedis(channel)` instead.
- Since 2.1.0, incoming messages are limited to 1 MiB and larger ones close the
  connection with 1009. Set `MaxMessageSizeBytes` if you need more.
