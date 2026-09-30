import assert from "node:assert/strict";
import { setTimeout as delay } from "node:timers/promises";
import { pathToFileURL } from "node:url";

const { default: DarkWs } = await import(pathToFileURL(process.argv[2]).href);
const legacy = process.argv[4] === "4";
const clients = [];
const timeout = setTimeout(() => { console.error("Chat example timed out"); process.exit(1); }, 20000);
const client = (name, upgrade = legacy) => {
  const result = new DarkWs({
    host: process.argv[3], path: "/ws", secure: false, reconnect: false,
    ...(name ? upgrade ? { query: () => ({ token: name }) } : { authenticationToken: () => name } : {}),
  });
  clients.push(result);
  return result;
};
const open = value => new Promise(resolve => { value.on("open", resolve); value.connect(); });
const close = value => new Promise(resolve => { value.on("close", resolve); value.close(); });

try {
  const events = [];
  const expected = [];
  const alice = client("Alice");
  alice.on("message", message => {
    if (message.action === "chat:joined" || message.action === "chat:left") {
      events.push(`${message.action}:${message.data.name}`);
    }
  });
  const verify = async (...next) => {
    expected.push(...next);
    while (events.length < expected.length) await delay(5);
    // The reply follows prior writes on Alice's socket and exposes duplicate/unexpected events.
    await alice.request("chat:history");
    assert.deepEqual(events, expected);
  };
  await open(alice);
  await verify(...(legacy ? ["chat:joined:Alice"] : []));

  const bob = client("Bob");
  await open(bob);
  await verify("chat:joined:Bob");
  await close(bob);
  await verify("chat:left:Bob");
  await open(bob);
  await verify("chat:joined:Bob");
  if (!legacy) {
    await bob.authenticate("Charlie");
    await verify("chat:left:Bob", "chat:joined:Charlie");
    await bob.logout();
    await verify("chat:left:Charlie");
  }
  await close(bob);
  await verify(...(legacy ? ["chat:left:Bob"] : []));

  if (!legacy) {
    const upgraded = client("Upgrade", true);
    await open(upgraded);
    await verify("chat:joined:Upgrade");
    await upgraded.authenticate("Replacement");
    await verify("chat:left:Upgrade", "chat:joined:Replacement");
    await close(upgraded);
    await verify("chat:left:Replacement");
  }
  const anonymous = client(null);
  await open(anonymous);
  await close(anonymous);
  await verify();
  console.log(`Chat browser SDK ${legacy ? "4.0.0 query" : "current authenticationToken"} smoke passed`);
} finally {
  for (const value of clients) value.dispose();
  clearTimeout(timeout);
}
