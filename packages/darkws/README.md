<img src="https://raw.githubusercontent.com/Bobsans/DarkWS/main/assets/icon.png" alt="DarkWS" width="96" align="right">

# darkws

Dependency-free DarkWS request/response client for browsers.

```ts
import DarkWs from "darkws";

const client = new DarkWs({
  secure: location.protocol === "https:",
  host: location.host,
  path: "/ws",
  query: () => ({ token: getToken() }),
}).connect();

const result = await client.request<User>("user:get", { id: "42" });

const unsubscribe = client.on("message", (message) => {
  console.log(message);
});

await client.authenticate(getToken());
unsubscribe();
client.close();
client.dispose();
```

Package ships ESM JavaScript and TypeScript declarations. It has no runtime
dependencies. Call `dispose()` when the client will not be used again.

Requests use `{ id, action, data? }`. The `message` event receives the full flat
broadcast `{ id: "@", action, data? }`. Update the server and clients together:
the previous `payload` request field and nested broadcast envelope are incompatible.

Use `onAction` to receive only one exact, case-sensitive broadcast action:

```ts
const unsubscribe = client.onAction<User>("user:updated", user => {
  console.log(user);
});
unsubscribe();
```

The callback receives `data` directly, including `undefined` when it is omitted
and `null` when sent explicitly. The optional type argument describes the expected
payload; it does not validate incoming data. Subscriptions survive reconnects.
The returned function removes only that subscription and is safe to call more
than once. Other subscriptions and the full-envelope `message` event are unchanged.

Requests accept either the existing numeric timeout or a `DarkWsRequestOptions`
object as the third argument:

```ts
const user = await client.request<User>("user:get", { id: "42" }, {
  timeout: 5000,
  retry: { connectionClosed: 2, timeout: 1, jitter: 250 },
});
```

`timeout` is the reply deadline per attempt in milliseconds (`0` disables it),
defaulting to `requestTimeout`. `retry.connectionClosed` and `retry.timeout` are
independent counts of **additional** attempts for `ConnectionClosedError` and
`RequestTimeoutError`; both default to `0`. `retry.jitter` defaults to `0` and adds
a random delay from zero up to that many milliseconds before each retry. Counts
must be non-negative safe integers; jitter must be finite and between `0` and
`2147483647`. Invalid retry options reject with `RangeError` before connecting.

Enable retries **only for idempotent actions**, typically reads. Each attempt has
a new request id; a late reply to an earlier attempt is ignored. A timeout or
disconnect can occur after the server executed the action, so retrying a mutation
can execute it twice. Server error replies and other send errors are not retried.
Authentication/logout commands do not use this request retry policy.

For a request, `ConnectionClosedError.sent` is `true` if **any** attempt was
accepted by `WebSocket.send()`, even when the final attempt was unsent. It does
not confirm server receipt or execution. `false` means no attempt was sent, so
retrying cannot duplicate that request's execution. The error constructor's
optional second argument defaults to `false` for backward compatibility.

Retries use the normal connection hooks, gates, and session restoration, and may
open a connection even with background reconnection disabled (`reconnect: false`).
`waitConnectionTimeout` applies to each connection wait, separately from the reply
deadline and jitter. Explicit `close()` and `dispose()` cancel retries and their
timers, including if `connect()` is called again immediately. Pending retry delays
are included in `pendingRequestCount`.

`authenticate(token)` waits for a server acknowledgement and rejects authentication
errors. `authenticate("")` rejects immediately with `TypeError`, without connecting
or sending a command. Use `logout()` to sign out: it clears the current server
session without closing the connection.
They send plain text `auth:<token>` / `logout` and wait for `auth:success` /
`logout:success`. `auth:failed` rejects with `ErrorResponse`; the server clears the
session by default, or retains it with `KeepSessionOnFailedAuthentication`.
System commands run sequentially; a response timeout closes the socket
and rejects queued commands so late replies cannot confirm a later command.
The `send` event and error request context contain local metadata without the token;
their local ids are not sent on the wire. JSON replies cannot acknowledge these commands.
Update the application-owned token/query source on logout so reconnect does not
restore stale credentials. Upgrade server and clients together.

Putting a token in `query` exposes it to infrastructure URL logging. Prefer a
short-lived ticket or omit it and authenticate after connecting when the server
permits that flow. Use WSS and redact credential logs; validation, expiry, and
revocation belong to the host application.

To authenticate every socket, including after reconnect, pass
`authenticationToken`. It is called when each socket opens; a returned token is
sent as `auth:<token>`, and queued and new requests wait until `auth:success`
before they are sent. The `open` event fires after that exchange. `auth:failed` or
a provider error rejects the queued requests with that error, and the connection
continues without a session. Returning no token connects anonymously.

```ts
const client = new DarkWs({
  secure: true,
  path: "/ws",
  authenticationToken: () => tokenStore.current(),
}).connect();
```

Calling `authenticate(token)` from an `open` listener instead does not delay
requests that were queued during reconnect: they can reach the server first.

Waiting for a connection is event-driven and bounded by `waitConnectionTimeout`
(30 seconds by default); with `reconnect: false` waiting requests fail as soon as
the socket closes. `requestTimeout` starts after sending (5 minutes by
default), so the total wait can be the sum of both. A response timeout of zero
disables expiry; the request remains pending until a response, disconnect, or
disposal. Ping starts only on a successful connection; constructing a client
starts no timers. Error responses reject whenever the `error` field is present,
even when it is empty. Request ids `@` and `@auth` are reserved for server messages.

`controlTimeout` independently bounds replies to `authenticate()` and `logout()`,
including automatic session restoration via `authenticationToken`. It defaults to
30000 ms (30 seconds), starts after each command is sent, and excludes connection
and command-queue waits. Set it to `0` to disable control reply timeouts. A timeout
rejects with `RequestTimeoutError`, closes the socket, and rejects queued commands
with `ConnectionClosedError` so a late reply cannot acknowledge the wrong command.
`requestTimeout` still applies only to ordinary requests. To retain the previous
control timeout, explicitly set `controlTimeout` to your old `requestTimeout`
value (300000 ms by default). An empty `authenticationToken` provider result still
skips session restoration and connects anonymously.

Text `ping` is sent every `pingInterval` (30 seconds; the older `pingTimeout`
name is still accepted). When no `pong` arrives within `pongTimeout` (30 seconds,
`0` disables the check), the socket is treated as dead: its requests fail and the
client reconnects, instead of waiting for TCP to notice a half-open connection.

`connect()` keeps an open or opening socket; use `reconnect()` after a close, or
`close()` and then `connect()` to replace it. `close(code)` accepts only 1000 or
3000–4999 (the codes browsers allow) and throws `RangeError` otherwise without
changing state. Request ids use `crypto.randomUUID()` when available and
`crypto.getRandomValues()` otherwise, so plain `http:` pages outside a secure
context work too.

`isCurrentSocket(event)` identifies native events from the currently assigned
socket, including its `close` event. After replacement, events from the old socket
return `false`; after `dispose()`, all events return `false`. Event payloads are
unchanged. Use it in lifecycle listeners or when processing a saved event later:

```ts
client.on("close", event => {
  if (!client.isCurrentSocket(event)) return;
  console.log("Current connection closed", event.code);
});
```

Set `reconnectOnVisible: true` (default `false`) to retry a pending automatic
reconnect immediately when the document becomes visible, skipping the remaining
backoff delay. It still respects `canConnect` and `beforeConnect`, keeps open or
opening sockets, and does nothing after `close()`, after `dispose()`, or with
`reconnect: false`. It does not connect an idle client that has never called
`connect()`. The listener is removed on disposal; outside a browser, normal
timer-based reconnect continues without a visibility listener.

`npm pack` runs the build automatically through `prepack`, including on a clean
checkout without `dist`.
