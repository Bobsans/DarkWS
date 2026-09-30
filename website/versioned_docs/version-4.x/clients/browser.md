---
sidebar_position: 1
title: Browser client
---

# Browser client

`darkws` is a dependency-free ESM client with TypeScript declarations.

```bash
npm install darkws@4
```

```ts
import DarkWs, { ErrorResponse } from "darkws";

const client = new DarkWs({
  secure: location.protocol === "https:",
  path: "/ws",
}).connect();

await client.authenticate(auth.accessToken());
const user = await client.request<User>("user:get", { id: "42" });

const unsubscribe = client.on("message", message => console.log(message));

unsubscribe();
client.dispose();
```

Construction starts no socket and no timer. `connect()` opens the socket; requests
made before it opens wait for the connection. A request on a client without a socket
opens one itself.

## Requests

```ts
const result = await client.request<Result, Input>("handler:action", input);
```

- The promise resolves with the response `data`.
- An error response rejects with `ErrorResponse`: `message` is the error code, `data`
  the optional details, and `request` the sent request.
- A lost connection, or no open socket within `waitConnectionTimeout`, rejects with
  `ConnectionClosedError`; a missing reply rejects with `RequestTimeoutError`.

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

A response counts as an error whenever it has an `error` field, even an empty one.

Requests are never retried: a request whose socket closes fails, and the application
decides whether sending it again is safe.

### Timeouts

Waiting for a request has two phases:

- `waitConnectionTimeout` (30 s) bounds waiting for an open socket.
- `requestTimeout` (5 min) bounds waiting for the reply and starts after sending.

A call can take up to the sum of both. `requestTimeout: 0` disables reply expiry: the
request stays pending until a reply, disconnect, or disposal.

Override the reply timeout per call with a number of milliseconds as the third
argument:

```ts
await client.request("report:build", input, 60000);
```

## Broadcasts and events

Broadcasts arrive through the `message` event as the full `{ id: "@", action, data }`
envelope. Filter by action yourself:

```ts
const off = client.on("message", message => {
  const { action, data } = message as { action: string; data?: unknown };
  if (action === "message:created") append(data as Message);
});
off();
```

The envelope is not validated at runtime. Action names are case-sensitive. Listeners
belong to the client, so they survive reconnects.

`on(event, listener)` subscribes to client events and returns an unsubscribe function;
`off(event, listener)` also removes it:

| Event | Arguments | When |
| --- | --- | --- |
| `open` | `Event` | The current socket opened |
| `close` | `CloseEvent` | A socket closed |
| `error` | `Event` | A socket error |
| `message` | `unknown`, `MessageEvent` | Any broadcast, as the full `{ id: "@", action, data }` envelope |
| `send` | `unknown` | Data was sent: the request for `request()`, the raw data for `send()`, local metadata without the token for commands |

`open` fires only for the current socket. `close` and `error` also fire for a socket
that `connect()` has already replaced. An exception in a listener does not stop the
other listeners.

## Authentication

### Manual

```ts
await client.authenticate(token); // sends auth:<token>, waits for auth:success
await client.logout();            // sends logout, waits for logout:success
```

- `auth:failed` rejects with `ErrorResponse`.
- Commands run one at a time. `requestTimeout` (5 min, `0` disables it) bounds each
  reply after it is sent. A timeout rejects with `RequestTimeoutError`, closes the
  socket, and rejects the other requests and queued commands on it with
  `ConnectionClosedError`, so a late reply cannot confirm the wrong command.
- The client does not keep the token. After a reconnect the server session is gone
  unless you restore it.

### Restoring the session after reconnect

The client has no automatic authentication option. To restore the session on every
socket, authenticate again when it opens:

```ts
client.on("open", () => {
  const token = tokenStore.current();
  if (token) client.authenticate(token).catch(() => redirectToLogin());
});
```

Requests queued while the client was reconnecting are released when the socket
opens, before `open` listeners run, so they can reach the server before this
authentication and run without a session. If that matters, send the token with the
upgrade request instead (see below).

`open` and `connected` report the transport; they do not prove the session was
restored.

### Tokens in the URL

`query` adds parameters to the socket URL, and the server passes `token` to its
authenticator on upgrade. A `query` function is called for every socket, so each
reconnect sends a current value:

```ts
new DarkWs({ secure: true, path: "/ws", query: () => ({ token: ticket() }) });
```

URLs end up in proxy and access logs. Prefer a short-lived ticket, or leave the URL
clean and call `authenticate()` after connecting. After logout, clear whatever source
`query` or your `open` listener reads, so a reconnect cannot restore old credentials.

## Connection lifecycle

| Method | Behavior |
| --- | --- |
| `connect()` | Opens a socket; an existing socket is closed and replaced, and its pending requests reject |
| `reconnect()` | Resets backoff and connects if no socket is open or opening |
| `close(code = 1000)` | Closes the socket and stops reconnecting |
| `dispose()` | Terminal: stops timers, closes the socket, rejects pending requests |
| `send(data, jsonify = true)` | Sends raw data without waiting for a reply; waits for an open socket first |

State getters: `connected`, `closing`, `closed`, `pendingRequestCount` (pending
requests and commands, including those waiting for a socket).

- After an unexpected close the client reconnects with exponential backoff and
  jitter, starting from `reconnectTimeout` (5 s) and capped at 30 s.
- With `reconnect: false` there is no background retry, but the next request or
  `send()` opens a new socket.
- After an explicit `close()`, requests and `send()` reject with
  `ConnectionClosedError`; call `connect()` before using the client again.

### Heartbeat

The client sends text `ping` every `pingTimeout` (30 s) while the socket is open.
Despite its name, `pingTimeout` is the interval: the client does not wait for or
check the `pong` reply, so it does not detect a half-open connection by itself. The
.NET 8 server relies on this traffic to keep idle connections open.

### Connection gates

- `canConnect()` is checked before each attempt; while it returns false the client
  retries every 100 ms. Use it to wait for a condition such as being online.
- `beforeConnect()` is awaited before each attempt, for example to refresh a token.
  A rejection schedules a reconnect.

Both retries are scheduled only when `reconnect` is enabled.

## Options

| Option | Default | Meaning |
| --- | --- | --- |
| `secure` | required | `wss` when true, `ws` when false |
| `path` | required | Endpoint path, such as `/ws` |
| `host` | `location.host` | Host and port; required outside a browser window |
| `query` | none | Object or function returning URL query parameters |
| `requestTimeout` | 300000 | Reply deadline in ms for requests, `authenticate()`, and `logout()`; `0` disables it |
| `waitConnectionTimeout` | 30000 | Wait for an open socket, in ms |
| `reconnect` | `true` | Reconnect in the background after a drop |
| `reconnectTimeout` | 5000 | Base reconnect delay in ms |
| `pingTimeout` | 30000 | Interval between heartbeat pings in ms |
| `canConnect` | none | Gate checked before connecting |
| `beforeConnect` | none | Async hook awaited before connecting |
| `debug` | `false` | Log `send()` data, invalid messages, and failing listeners to the console |

Request ids use `crypto.randomUUID()`, which browsers provide only in secure contexts
(HTTPS or `localhost`).

## Comparison with the .NET client

Both clients share the protocol but differ in a few lifecycle policies. See
[Protocol](../protocol.md).

- The .NET client can authenticate every socket automatically with
  `AuthenticationTokenProvider`; the browser client authenticates only when you call
  `authenticate()`.
- The .NET client fails a connection whose `pong` does not arrive within
  `PongTimeout`; the browser client only sends `ping`.
- The .NET client stops retrying after permanent failures such as failed automatic
  authentication, HTTP 401 or 403, and protocol errors; the browser client reconnects
  after every unexpected close while `reconnect` is enabled.
- With reconnect disabled, .NET requests fail until an explicit `ConnectAsync`; the
  next browser request opens a new socket.
