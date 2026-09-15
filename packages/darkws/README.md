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

`authenticate(token)` waits for a server acknowledgement and rejects authentication
errors; `logout()` clears the current server session without closing the connection.
They send plain text `auth:<token>` / `logout` and wait for `auth:success` /
`logout:success`. `auth:failed` rejects with `ErrorResponse` and clears the server
session. System commands run sequentially; a response timeout closes the socket
and rejects queued commands so late replies cannot confirm a later command.
The `send` event and error request context contain local metadata without the token;
their local ids are not sent on the wire. JSON replies cannot acknowledge these commands.
Update the application-owned token/query source on logout so reconnect does not
restore stale credentials. Upgrade server and clients together.

Putting a token in `query` exposes it to infrastructure URL logging. Prefer a
short-lived ticket or omit it and await `authenticate(token)` before protected
requests when the server permits that flow. Use WSS and redact credential logs;
validation, expiry, and revocation belong to the host application.

Waiting for a connection is event-driven and bounded by `waitConnectionTimeout`
(30 seconds by default). `requestTimeout` starts after sending (5 minutes by
default), so the total wait can be the sum of both. A response timeout of zero
disables expiry; the request remains pending until a response, disconnect, or
disposal. Ping starts only on a successful connection; constructing a client
starts no timers. Error responses reject whenever the `error` field is present,
even when it is empty. Request ids `@` and `@auth` are reserved for server messages.

`npm pack` runs the build automatically through `prepack`, including on a clean
checkout without `dist`.
