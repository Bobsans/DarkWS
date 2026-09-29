<div align="center">

<img src="https://raw.githubusercontent.com/Bobsans/DarkWS/main/assets/icon.png" alt="DarkWS logo" width="96">

# darkws

[![npm](https://img.shields.io/npm/v/darkws.svg?label=npm)](https://www.npmjs.com/package/darkws)
[![CI](https://github.com/Bobsans/DarkWS/actions/workflows/ci.yml/badge.svg)](https://github.com/Bobsans/DarkWS/actions/workflows/ci.yml)
[![Types](https://img.shields.io/npm/types/darkws.svg)](https://www.npmjs.com/package/darkws)
[![Docs](https://img.shields.io/badge/docs-bobsans.github.io%2FDarkWS-blue)](https://bobsans.github.io/DarkWS/clients/browser)
[![License](https://img.shields.io/github/license/Bobsans/DarkWS)](https://github.com/Bobsans/DarkWS/blob/main/LICENSE)

</div>

Dependency-free browser client for [DarkWS](https://github.com/Bobsans/DarkWS)
servers: typed requests, broadcast subscriptions, reconnects with backoff, heartbeats,
opt-in retries, and automatic session restoration. ESM with TypeScript declarations.

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

try {
  await client.request("user:delete", { id: "42" });
} catch (error) {
  if (error instanceof ErrorResponse) console.warn(error.message); // the server's error code
}

unsubscribe();
client.dispose();
```

For an application-wide client created on first use, see `DarkWs.lazy()`.

## Documentation

- [Browser client](https://bobsans.github.io/DarkWS/clients/browser): requests,
  timeouts, retries, events, authentication, lifecycle, options
- [Protocol and client lifecycle contract](https://bobsans.github.io/DarkWS/protocol#client-lifecycle-contract)
- [Документация на русском](https://bobsans.github.io/DarkWS/ru/clients/browser)
