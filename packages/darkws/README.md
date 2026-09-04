# @darkboy/darkws

Dependency-free DarkWS request/response client for browsers.

```ts
import DarkWs from "@darkboy/darkws";

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
