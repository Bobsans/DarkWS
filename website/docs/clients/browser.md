---
sidebar_position: 1
title: Browser client
---

# Browser client

`darkws` is a dependency-free ESM client with TypeScript declarations.

```bash
npm install darkws
```

```ts
import DarkWs, { ErrorResponse } from "darkws";

const client = new DarkWs({
  secure: location.protocol === "https:",
  path: "/ws",
  authenticationToken: () => auth.accessToken(),
}).connect();

const user = await client.request<User>("user:get", { id: "42" });

const unsubscribe = client.onAction<User>("user:updated", updated => render(updated));

unsubscribe();
client.dispose();
```

Construction starts no socket and no timer. `connect()` opens the socket; requests
made before it opens wait for the connection.

## Requests

```ts
const result = await client.request<Result, Input>("handler:action", input);
```

- The promise resolves with the response `data`.
- An error response rejects with `ErrorResponse`: `message` is the error code, `data`
  the optional details, and `request` the sent request.
- A lost connection rejects with `ConnectionClosedError`, a missing reply with
  `RequestTimeoutError`.

```ts
try {
  await client.request("message:delete", { id });
} catch (error) {
  if (error instanceof ErrorResponse && error.message === "message:forbidden") {
    showForbidden();
  } else {
    throw error;
  }
}
```

A response counts as an error whenever it has a string `error` field, even an empty
string. A correlated response with a non-string `error` rejects with `TypeError`
and releases its request slot. Invalid envelopes and broadcasts are ignored;
broadcasts require a non-empty string `action` and no `error`. The SDK does not
validate the application-specific shape of `data`.

### Timeouts

Waiting for a request has two phases:

- `waitConnectionTimeout` (30 s) bounds waiting for an open socket.
- `requestTimeout` (5 min) bounds waiting for the reply and starts after sending.

A call can take up to the sum of both. `requestTimeout: 0` disables reply expiry: the
request stays pending until a reply, disconnect, or disposal.

Override the reply timeout per call with a number or an options object:

```ts
await client.request("report:build", input, 60000);
await client.request("report:build", input, { timeout: 60000 });
```

### Retries

Retries are off by default. Enable them **only for idempotent actions**, typically
reads: a timeout or disconnect can happen after the server already ran the action.

```ts
const user = await client.request<User>("user:get", { id: "42" }, {
  timeout: 5000,
  retry: { connectionClosed: 2, timeout: 1, jitter: 250 },
});
```

- `connectionClosed` and `timeout` are the number of **additional** attempts after
  `ConnectionClosedError` and `RequestTimeoutError`.
- `jitter` adds a random delay of up to that many milliseconds before each retry.
- Each attempt gets a fresh request id; a late reply to an earlier attempt is ignored.
- Server error replies are never retried, and neither are authentication commands.
- Retries may open a connection even with `reconnect: false`. `close()` and
  `dispose()` cancel them.
- Counts must be non-negative safe integers and jitter within 0–2147483647 ms, or the
  call rejects with `RangeError` before connecting.

`ConnectionClosedError.sent` is true if any attempt reached `WebSocket.send()`. It
does not prove the server ran the action; `false` means nothing was sent, so a retry
cannot run it twice.

### Default options per action

`requestOptions` supplies defaults by action name:

```ts
const READ = { timeout: 45000, retry: { connectionClosed: 3, timeout: 1, jitter: 2000 } };

const client = new DarkWs({
  secure: true,
  path: "/ws",
  requestOptions: action => action.endsWith(":get") || action.endsWith(":list") ? READ : undefined,
});
```

Explicit options override the defaults field by field (`timeout`,
`retry.connectionClosed`, `retry.timeout`, `retry.jitter`). A numeric third argument
overrides only `timeout`. If the provider throws, the request rejects before sending.
`authenticate()` and `logout()` do not use it.

### Pending request limit

`maxPendingRequests` (256) bounds pending `request()`, `authenticate()`, and
`logout()` calls together, including connection waits and retry delays. One call
keeps one slot until it settles; retries reuse it. An excess call rejects at once
with `RangeError` and sends nothing. Raise the limit if the application needs more
concurrency. `pendingRequestCount` reports current usage.

Automatic session restoration is exempt, so a full queue can still authenticate.
Raw `send()` calls are not counted.

## Broadcasts and events

Subscribe to one broadcast action:

```ts
const off = client.onAction<Message>("message:created", message => append(message));
off(); // idempotent
```

The callback receives `data` directly (`undefined` when omitted, `null` when sent as
null). The type argument is not validated at runtime. Action names are case-sensitive.
Subscriptions survive reconnects.

`on(event, listener)` subscribes to client events and returns an unsubscribe function;
`off(event, listener)` also removes it:

| Event | Arguments | When |
| --- | --- | --- |
| `open` | `Event` | The socket is ready, after automatic authentication if configured |
| `sessionRestoreFailed` | `Error`, `Event` | Automatic authentication failed; fires before `open` |
| `close` | `CloseEvent` | A socket closed |
| `error` | `Event` | A socket error |
| `message` | `unknown`, `MessageEvent` | Any broadcast, as the full `{ id: "@", action, data }` envelope |
| `send` | `unknown` | Data was sent; for commands, local metadata without the token |

Use `client.isCurrentSocket(event)` in `open` and `close` listeners to ignore events
from a socket that has already been replaced. It returns false for every event after
disposal:

```ts
client.on("close", event => {
  if (!client.isCurrentSocket(event)) return;
  showOffline(event.code);
});
```

## Authentication

### Manual

```ts
await client.authenticate(token); // sends auth:<token>, waits for auth:success
await client.logout();            // sends logout, waits for logout:success
```

- `auth:failed` rejects with `ErrorResponse`.
- `authenticate("")` rejects with `TypeError` without connecting; use `logout()`.
- Commands run one at a time. `controlTimeout` (30 s, `0` disables it) bounds each
  reply after it is sent. A timeout rejects with `RequestTimeoutError`, closes the
  socket, and rejects queued commands with `ConnectionClosedError`, so a late reply
  cannot confirm the wrong command.
- The client does not keep the token. After a reconnect the server session is gone
  unless you restore it.

### Automatic on every socket

`authenticationToken` is called whenever a socket opens:

```ts
const client = new DarkWs({
  secure: true,
  path: "/ws",
  authenticationToken: () => tokenStore.current(),
});
```

The returned token is sent as `auth:<token>`, and queued and new requests are held
until `auth:success`, so no request made during a reconnect reaches the server
before authentication. `open` fires after that exchange. Returning no token connects
anonymously.

If the server replies `auth:failed` or the provider throws, held requests reject with
that error, `sessionRestoreFailed(error, event)` fires, and the client continues
anonymously. Close in the listener if anonymous use is not acceptable:

```ts
client.on("sessionRestoreFailed", (error, event) => {
  if (client.isCurrentSocket(event)) client.close();
  redirectToLogin();
});
```

`sessionRestoreFailed` also fires on the first connection. It does not fire for
manual `authenticate()`, success, an intentionally empty token, or a socket that has
already closed. Calling `authenticate()` from an `open` listener does **not** give
this ordering: requests queued during reconnect can reach the server first.

`open` and `connected` report the transport; they do not prove the session was
restored.

### Tokens in the URL

`query` adds parameters to the socket URL, and the server passes `token` to its
authenticator on upgrade:

```ts
new DarkWs({ secure: true, path: "/ws", query: () => ({ token: ticket() }) });
```

URLs end up in proxy and access logs. Prefer a short-lived ticket, or leave the URL
clean and use `authenticationToken`. After logout, clear whatever source `query` or
`authenticationToken` read, so a reconnect cannot restore old credentials.

## Connection lifecycle

| Method | Behavior |
| --- | --- |
| `connect()` | Opens a socket; keeps an open or opening one |
| `reconnect()` | Resets backoff and connects if no socket is open |
| `close(code = 1000)` | Immediately rejects pending calls, closes, and stops reconnecting; `code` must be 1000 or 3000–4999 |
| `dispose()` | Terminal: stops timers, closes the socket, rejects pending requests |
| `send(data, jsonify = true)` | Sends raw data without waiting for a reply |

State getters: `connected`, `closing`, `closed`, `pendingRequestCount`.

- After an unexpected close the client reconnects with exponential backoff and
  jitter, starting from `reconnectTimeout` (5 s) and capped at 30 s.
- With `reconnect: false` there is no background retry, but the next request or
  `send()` opens a new socket.
- After an explicit `close()`, call `connect()` before using the client again.
- To replace an open socket, call `close()` and then `connect()`.
- `reconnectOnVisible: true` retries a pending reconnect as soon as the tab becomes
  visible instead of waiting out the backoff.

`close()` rejects requests and queued authentication/logout commands with
`ConnectionClosedError` before the native closing handshake finishes. The `sent`
flag still reports whether a request was sent; retries stop even if you reconnect
immediately. The native `close` event can arrive later.

### Heartbeat

The client sends text `ping` every `pingInterval` (30 s). If no `pong` arrives within
`pongTimeout` (30 s, `0` disables it), the socket is treated as dead: its requests
fail and the client reconnects. This detects half-open connections that TCP would
notice only much later. The .NET 8 server relies on this traffic to keep idle
connections open.

### Connection gates

- `canConnect()` is checked before each attempt; while it returns false the client
  retries every 100 ms. Use it to wait for a condition such as being online.
- `beforeConnect()` is awaited before each attempt, for example to refresh a token.
  A rejection schedules a reconnect.

## Options

Omitted options and explicit `undefined` use the same defaults. Timer values must
be finite and between 0 and 2147483647 ms; `pingInterval` (or `pingTimeout`) must be
greater than 0. Zero disables the request, control, and PONG deadlines;
`reconnect: false` disables automatic reconnect. Zero connection-wait and reconnect
delays mean an immediate deadline or retry, respectively.

| Option | Default | Meaning |
| --- | --- | --- |
| `secure` | required | `wss` when true, `ws` when false |
| `path` | required | Endpoint path, such as `/ws` |
| `host` | `location.host` | Host and port |
| `query` | none | Object or function returning URL query parameters |
| `authenticationToken` | none | Token provider for automatic authentication |
| `requestTimeout` | 300000 | Reply deadline in ms; `0` disables it |
| `requestOptions` | none | Default request options per action |
| `maxPendingRequests` | 256 | Pending request and command limit |
| `controlTimeout` | 30000 | Reply deadline for `authenticate()` and `logout()`; `0` disables it |
| `waitConnectionTimeout` | 30000 | Wait for an open socket, in ms |
| `reconnect` | `true` | Reconnect in the background after a drop |
| `reconnectTimeout` | 5000 | Base reconnect delay in ms |
| `reconnectOnVisible` | `false` | Skip backoff when the tab becomes visible |
| `pingInterval` | 30000 | Heartbeat interval in ms (`pingTimeout` is a deprecated alias) |
| `pongTimeout` | 30000 | Heartbeat reply deadline in ms; `0` disables it |
| `canConnect` | none | Gate checked before connecting |
| `beforeConnect` | none | Async hook awaited before connecting |
| `debug` | `false` | Log sent and invalid messages to the console |

Request ids use `crypto.randomUUID()` when available and `crypto.getRandomValues()`
otherwise, so plain `http:` pages work too.

## Application-wide client

`DarkWs.lazy()` returns a `LazyDarkWs` facade that creates the client on first use.
Options can be a function, read when the client is created:

```ts
export const dws = DarkWs.lazy(() => ({
  secure: config.apiUrl.protocol === "https:",
  host: config.apiUrl.host,
  path: "/ws",
  authenticationToken: () => session.token,
}));

const user = await dws.request<User>("user:get", { id: "42" });
dws.onAction<User>("user:updated", render);
```

- Nothing is created by `lazy()` itself. The first `request()`, `send()`,
  `authenticate()`, `logout()`, `on()`, `onAction()`, or `reconnect()` creates the
  client and connects it. Subscribing connects too, so broadcasts arrive at once.
- `instance` returns the underlying client, created without connecting, for state
  such as `connected`.
- `close(code)` closes the client; the next use connects again. Close through the
  facade, not through `instance.close()`.
- The facade keeps its own subscriptions. `reset()` disposes the client, rejecting its
  pending requests, and re-reads the options. Subscriptions move to the new client,
  which connects at once if the previous one was started. Use it after switching
  user or server.
- `reset(true)` also drops all subscriptions and leaves the facade idle, which is
  useful between tests.

## Comparison with the .NET client

Both clients share the protocol but differ in a few lifecycle policies. See
[Protocol](../protocol.md#client-lifecycle-contract).
