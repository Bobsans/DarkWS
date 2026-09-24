import DarkWs, {
  ConnectionClosedError,
  ErrorResponse,
  RequestTimeoutError,
  type DarkWsOptions,
} from "../src/dark-ws.js";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

class MockWebSocket {
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSING = 2;
  static readonly CLOSED = 3;
  static instances: MockWebSocket[] = [];

  readyState = MockWebSocket.CONNECTING;
  sent: unknown[] = [];
  readonly url: string;
  private readonly listeners = new Map<string, ((event: Event) => void)[]>();

  constructor(url: string) {
    this.url = url;
    MockWebSocket.instances.push(this);
  }

  addEventListener(type: string, callback: (event: Event) => void): void {
    const listeners = this.listeners.get(type) ?? [];
    listeners.push(callback);
    this.listeners.set(type, listeners);
  }

  send(data: unknown): void {
    this.sent.push(data);
  }

  close(code = 1000, reason = ""): void {
    this.readyState = MockWebSocket.CLOSED;
    this.emit("close", { target: this, wasClean: true, code, reason });
  }

  open(): void {
    this.readyState = MockWebSocket.OPEN;
    this.emit("open", { target: this });
  }

  serverClose(wasClean = true, code = 1000): void {
    this.readyState = MockWebSocket.CLOSED;
    this.emit("close", { target: this, wasClean, code, reason: "" });
  }

  serverMessage(message: unknown): void {
    this.emit("message", { target: this, data: JSON.stringify(message) });
  }

  serverText(text: string): void {
    this.emit("message", { target: this, data: text });
  }

  serverError(): void {
    this.emit("error", { target: this });
  }

  private emit(type: string, event: object): void {
    for (const callback of this.listeners.get(type) ?? []) {
      callback(event as Event);
    }
  }
}

const createClient = (options: Partial<DarkWsOptions> = {}) => new DarkWs({
  host: "example.test",
  path: "/ws",
  query: {},
  secure: false,
  reconnect: true,
  reconnectTimeout: 100,
  pingTimeout: 1000,
  ...options,
});

describe("DarkWs", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    MockWebSocket.instances = [];
    vi.stubGlobal("WebSocket", MockWebSocket);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it("starts ping only while a connection is open", () => {
    const client = createClient({ reconnect: false });
    expect(vi.getTimerCount()).toBe(0);
    client.connect();
    expect(vi.getTimerCount()).toBe(0);
    const socket = MockWebSocket.instances[0];
    socket.open();
    expect(vi.getTimerCount()).toBe(1);
    socket.serverClose();
    expect(vi.getTimerCount()).toBe(0);
    client.dispose();
  });

  it("releases connection waiters immediately on open without polling", async () => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    const first = client.send("first", false);
    const second = client.send("second", false);
    expect(socket.sent).toHaveLength(0);
    const before = Date.now();
    socket.open();
    await Promise.all([first, second]);
    expect(Date.now()).toBe(before);
    expect(socket.sent).toEqual(["first", "second"]);
    expect(vi.getTimerCount()).toBe(1);
    client.dispose();
  });

  it("rejects waiters on intentional close and prevents sends after an open-close race", async () => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    const request = client.request("waiting");
    const rejected = expect(request).rejects.toBeInstanceOf(ConnectionClosedError);
    socket.open();
    client.close();
    await rejected;
    expect(socket.sent).toHaveLength(0);
    expect(client.pendingRequestCount).toBe(0);
    expect(vi.getTimerCount()).toBe(0);
    client.dispose();
  });

  it("rejects an explicit empty error field", async () => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    const request = client.request("empty-error");
    await Promise.resolve();
    socket.serverMessage({ id: JSON.parse(socket.sent[0] as string).id, error: "" });
    await expect(request).rejects.toBeInstanceOf(ErrorResponse);
    expect(client.pendingRequestCount).toBe(0);
    client.dispose();
  });

  it("does not open after an intentional close during beforeConnect", async () => {
    let finish!: () => void;
    const client = createClient({ beforeConnect: () => new Promise<void>(resolve => { finish = resolve; }) }).connect();
    client.close();
    finish();
    await Promise.resolve();
    expect(MockWebSocket.instances).toHaveLength(0);
    expect(vi.getTimerCount()).toBe(0);
    client.dispose();
  });

  it("reconnects after a clean server close", () => {
    const client = createClient().connect();
    MockWebSocket.instances[0].open();
    MockWebSocket.instances[0].serverClose(true);
    vi.advanceTimersByTime(100);

    expect(MockWebSocket.instances).toHaveLength(2);
    client.dispose();
  });

  it("does not reconnect after an intentional close", () => {
    const client = createClient().connect();
    MockWebSocket.instances[0].open();
    client.close();
    vi.advanceTimersByTime(1000);

    expect(MockWebSocket.instances).toHaveLength(1);
    client.dispose();
  });

  it("rejects pending requests when the socket closes", async () => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    const request = client.request("test:read");
    await Promise.resolve();
    socket.serverClose(false, 1006);

    await expect(request).rejects.toBeInstanceOf(ConnectionClosedError);
    client.dispose();
  });

  it("rejects and removes timed out requests", async () => {
    const client = createClient({ requestTimeout: 10 }).connect();
    MockWebSocket.instances[0].open();
    const request = client.request("test:read");
    const rejected = expect(request).rejects.toBeInstanceOf(RequestTimeoutError);
    await vi.advanceTimersByTimeAsync(10);

    await rejected;
    expect(client.pendingRequestCount).toBe(0);
    client.dispose();
  });

  it("resolves responses and removes their request", async () => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    const request = client.request<string>("test:read");
    await Promise.resolve();
    const sent = JSON.parse(socket.sent[0] as string) as { id: string };
    socket.serverMessage({ id: sent.id, data: "ok" });

    await expect(request).resolves.toBe("ok");
    expect(client.pendingRequestCount).toBe(0);
    client.dispose();
  });

  it.each([undefined, null, { text: "hello" }])("sends request data %j without a payload field", async (data) => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    const response = client.request("message:send", data);
    await Promise.resolve();
    const request = JSON.parse(socket.sent[0] as string);
    expect(request).toEqual({
      id: expect.any(String),
      action: "message:send",
      ...(data === undefined ? {} : { data }),
    });
    socket.serverMessage({ id: request.id });
    await expect(response).resolves.toBeUndefined();
    client.dispose();
  });

  it("rejects server errors with request context", async () => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    const request = client.request("test:write", { value: 1 });
    await Promise.resolve();
    const sent = JSON.parse(socket.sent[0] as string) as { id: string };
    socket.serverMessage({ id: sent.id, error: "denied", data: { field: "value" } });

    await expect(request).rejects.toMatchObject<Partial<ErrorResponse>>({ message: "denied" });
    client.dispose();
  });

  it("keeps remaining listeners when an unsubscribe runs twice", () => {
    const client = createClient().connect();
    const calls: string[] = [];
    const off = client.on("open", () => calls.push("first"));
    client.on("open", () => calls.push("second"));
    off();
    off();
    MockWebSocket.instances[0].open();

    expect(calls).toEqual(["second"]);
    client.dispose();
  });

  it("disposes timers and rejects pending requests", async () => {
    const client = createClient().connect();
    MockWebSocket.instances[0].open();
    const request = client.request("test:read");
    await Promise.resolve();
    client.dispose();

    await expect(request).rejects.toBeInstanceOf(ConnectionClosedError);
    expect(vi.getTimerCount()).toBe(0);
  });

  it("waits for authentication acknowledgement", async () => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    const authenticated = client.authenticate("test-token");
    await vi.advanceTimersByTimeAsync(0);
    expect(client.pendingRequestCount).toBe(1);
    expect(socket.sent).toEqual(["auth:test-token"]);
    socket.serverText("auth:success");
    await expect(authenticated).resolves.toBeUndefined();
    expect(client.pendingRequestCount).toBe(0);
    client.dispose();
  });

  it("reports authentication rejection and permits logout without reconnect", async () => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    const authentication = client.authenticate("expired");
    const rejected = expect(authentication).rejects.toMatchObject({ message: "auth:failed" });
    await vi.advanceTimersByTimeAsync(0);
    socket.serverText("auth:failed");
    await rejected;
    const logout = client.logout();
    await vi.advanceTimersByTimeAsync(0);
    expect(socket.sent[1]).toBe("logout");
    socket.serverText("logout:success");
    await expect(logout).resolves.toBeUndefined();
    expect(client.connected).toBe(true);
    expect(MockWebSocket.instances).toHaveLength(1);
    client.dispose();
  });

  it("bounds authentication waits and rejects logout on disconnect", async () => {
    const client = createClient({ requestTimeout: 10 }).connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    const authentication = expect(client.authenticate("test-token")).rejects.toBeInstanceOf(RequestTimeoutError);
    const queued = expect(client.logout()).rejects.toBeInstanceOf(ConnectionClosedError);
    await vi.advanceTimersByTimeAsync(10);
    await authentication;
    await queued;
    expect(socket.readyState).toBe(MockWebSocket.CLOSED);
    expect(socket.sent).toEqual(["auth:test-token"]);
    client.connect();
    const next = MockWebSocket.instances[1];
    next.open();
    const logout = expect(client.logout()).rejects.toBeInstanceOf(ConnectionClosedError);
    await vi.advanceTimersByTimeAsync(0);
    next.serverClose();
    await logout;
    expect(client.pendingRequestCount).toBe(0);
    client.dispose();
  });

  it("discards a system command when the socket send fails", async () => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    vi.spyOn(socket, "send").mockImplementationOnce(() => { throw new Error("send failed"); });
    const authentication = expect(client.authenticate("token")).rejects.toThrow("send failed");
    const logout = expect(client.logout()).rejects.toBeInstanceOf(ConnectionClosedError);
    await vi.advanceTimersByTimeAsync(0);
    await authentication;
    await logout;
    expect(socket.sent).toEqual([]);
    expect(socket.readyState).toBe(MockWebSocket.CLOSED);
    expect(client.pendingRequestCount).toBe(0);
    client.dispose();
  });

  it("serializes system commands and keeps JSON responses and pong independent", async () => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    const sent: unknown[] = [];
    client.on("send", value => sent.push(value));
    const auth = client.authenticate("private-token");
    const logout = client.logout();
    const request = client.request<number>("read");
    await vi.advanceTimersByTimeAsync(0);
    expect(socket.sent.filter(value => String(value).startsWith("auth:"))).toEqual(["auth:private-token"]);
    expect(socket.sent).not.toContain("logout");
    expect(JSON.stringify(sent)).not.toContain("private-token");
    const metadata = sent.find(value => (value as { action: string }).action === "auth") as { id: string };
    socket.serverMessage({ id: metadata.id });
    socket.serverText("logout:success");
    socket.serverText("pong");
    await vi.advanceTimersByTimeAsync(0);
    expect(socket.sent).not.toContain("logout");
    const json = JSON.parse(socket.sent.find(value => String(value).startsWith("{")) as string);
    socket.serverMessage({ id: json.id, data: 42 });
    await expect(request).resolves.toBe(42);
    socket.serverText("auth:success");
    await auth;
    await vi.advanceTimersByTimeAsync(0);
    expect(socket.sent.at(-1)).toBe("logout");
    socket.serverText("logout:success");
    await logout;
    socket.serverText("auth:failed");
    expect(client.pendingRequestCount).toBe(0);
    client.dispose();
  });

  it("restores the session before releasing requests on every socket", async () => {
    let token = "first";
    const client = createClient({ authenticationToken: async () => token }).connect();
    const socket = MockWebSocket.instances[0];
    const queued = client.request<number>("queued");
    socket.open();
    const late = client.request<number>("late");
    await vi.advanceTimersByTimeAsync(0);
    expect(socket.sent).toEqual(["auth:first"]);
    socket.serverText("auth:success");
    await vi.advanceTimersByTimeAsync(0);
    const [first, second] = socket.sent.slice(1).map(value => JSON.parse(value as string));
    expect([first.action, second.action]).toEqual(["queued", "late"]);
    socket.serverMessage({ id: first.id, data: 1 });
    socket.serverMessage({ id: second.id, data: 2 });
    await expect(Promise.all([queued, late])).resolves.toEqual([1, 2]);

    token = "second";
    socket.serverClose();
    const reconnecting = client.request<number>("after-reconnect");
    await vi.advanceTimersByTimeAsync(200);
    const next = MockWebSocket.instances[1];
    next.open();
    await vi.advanceTimersByTimeAsync(0);
    expect(next.sent).toEqual(["auth:second"]);
    next.serverText("auth:success");
    await vi.advanceTimersByTimeAsync(0);
    const request = JSON.parse(next.sent[1] as string);
    expect(request.action).toBe("after-reconnect");
    next.serverMessage({ id: request.id, data: 3 });
    await expect(reconnecting).resolves.toBe(3);
    client.dispose();
  });

  it("rejects queued requests when session restore fails and continues without a session", async () => {
    const client = createClient({ authenticationToken: () => "expired" }).connect();
    const socket = MockWebSocket.instances[0];
    const opened = vi.fn();
    client.on("open", opened);
    const queued = expect(client.request("queued")).rejects.toMatchObject({ message: "auth:failed" });
    socket.open();
    await vi.advanceTimersByTimeAsync(0);
    expect(opened).not.toHaveBeenCalled();
    socket.serverText("auth:failed");
    await queued;
    expect(opened).toHaveBeenCalledTimes(1);
    const anonymous = expect(client.request("public")).rejects.toBeInstanceOf(ConnectionClosedError);
    await vi.advanceTimersByTimeAsync(0);
    expect(JSON.parse(socket.sent[1] as string).action).toBe("public");
    client.dispose();
    await anonymous;
  });

  it("connects without authentication when the provider has no token", async () => {
    const client = createClient({ authenticationToken: () => undefined }).connect();
    const socket = MockWebSocket.instances[0];
    const request = client.request<number>("public");
    socket.open();
    await vi.advanceTimersByTimeAsync(0);
    const sent = JSON.parse(socket.sent[0] as string);
    expect(sent.action).toBe("public");
    socket.serverMessage({ id: sent.id, data: 7 });
    await expect(request).resolves.toBe(7);
    client.dispose();
  });

  it("builds a URL without an empty question mark", () => {
    const client = createClient().connect();
    expect(MockWebSocket.instances[0].url).toBe("ws://example.test/ws");
    client.dispose();
  });

  it.each([undefined, null, { id: 42 }])("delivers a flat broadcast with data %j", (data) => {
    const client = createClient().connect();
    const received: unknown[] = [];
    client.on("message", (data) => received.push(data));
    const notification = {
      id: "@",
      action: "client:sync",
      ...(data === undefined ? {} : { data }),
    };
    MockWebSocket.instances[0].serverMessage(notification);

    expect(received).toEqual([notification]);
    client.dispose();
  });

  it("builds secure URLs from dynamic query values", () => {
    const client = createClient({
      secure: true,
      path: "socket/",
      query: () => ({ token: "a b" }),
    }).connect();

    expect(MockWebSocket.instances[0].url).toBe("wss://example.test/socket?token=a+b");
    client.dispose();
  });

  it("waits for beforeConnect before opening a socket", async () => {
    let release!: () => void;
    const beforeConnect = () => new Promise<void>((resolve) => {
      release = resolve;
    });
    const client = createClient({ beforeConnect }).connect();

    expect(MockWebSocket.instances).toHaveLength(0);
    release();
    await Promise.resolve();
    await Promise.resolve();
    expect(MockWebSocket.instances).toHaveLength(1);
    client.dispose();
  });

  it("does not run concurrent beforeConnect hooks", () => {
    const beforeConnect = vi.fn(() => new Promise<void>(() => undefined));
    const client = createClient({ beforeConnect });

    client.connect();
    client.connect();

    expect(beforeConnect).toHaveBeenCalledTimes(1);
    client.dispose();
  });

  it("defers connection while canConnect is false", () => {
    let allowed = false;
    const client = createClient({ canConnect: () => allowed }).connect();
    expect(MockWebSocket.instances).toHaveLength(0);

    allowed = true;
    vi.advanceTimersByTime(100);

    expect(MockWebSocket.instances).toHaveLength(1);
    client.dispose();
  });

  it("emits error close and send events", async () => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    const events: string[] = [];
    client.on("error", () => events.push("error"));
    client.on("close", () => events.push("close"));
    client.on("send", () => events.push("send"));
    socket.open();

    await client.send({ value: 1 });
    socket.serverError();
    client.close();

    expect(events).toEqual(["send", "error", "close"]);
    client.dispose();
  });

  it("sends periodic pings only while connected", () => {
    const client = createClient({ pingTimeout: 100 }).connect();
    const socket = MockWebSocket.instances[0];
    vi.advanceTimersByTime(100);
    expect(socket.sent).toEqual([]);

    socket.open();
    vi.advanceTimersByTime(100);
    expect(socket.sent).toEqual(["ping"]);
    client.dispose();
  });

  it("treats a text pong as a heartbeat reply, not a message", async () => {
    const client = createClient({ pingInterval: 100, pongTimeout: 50 }).connect();
    const listener = vi.fn();
    client.on("message", listener);
    const socket = MockWebSocket.instances[0];
    socket.open();
    for (let ping = 0; ping < 3; ping++) {
      await vi.advanceTimersByTimeAsync(100);
      socket.serverText("pong");
    }
    await vi.advanceTimersByTimeAsync(60);
    expect(listener).not.toHaveBeenCalled();
    expect(socket.readyState).toBe(MockWebSocket.OPEN);
    expect(MockWebSocket.instances).toHaveLength(1);
    client.dispose();
  });

  it("drops a socket whose pong does not arrive and reconnects", async () => {
    const client = createClient({ pingInterval: 100, pongTimeout: 50 }).connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    const pending = expect(client.request("pending")).rejects.toBeInstanceOf(ConnectionClosedError);
    await vi.advanceTimersByTimeAsync(100);
    expect(socket.sent.at(-1)).toBe("ping");
    await vi.advanceTimersByTimeAsync(50);
    await pending;
    expect(socket.readyState).toBe(MockWebSocket.CLOSED);
    await vi.advanceTimersByTimeAsync(200);
    expect(MockWebSocket.instances).toHaveLength(2);
    client.dispose();
  });

  it("keeps reconnecting when query() throws in the reconnect timer", async () => {
    let failing = false;
    const client = createClient({ query: () => { if (failing) throw new Error("token storage unavailable"); return {}; } }).connect();
    MockWebSocket.instances[0].open();
    failing = true;
    MockWebSocket.instances[0].serverClose();
    await vi.advanceTimersByTimeAsync(200);
    expect(MockWebSocket.instances).toHaveLength(1);
    failing = false;
    await vi.advanceTimersByTimeAsync(1000);
    expect(MockWebSocket.instances).toHaveLength(2);
    client.dispose();
  });

  it("keeps an open socket when connect() is called again", async () => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    const request = client.request<number>("pending");
    await vi.advanceTimersByTimeAsync(0);
    client.connect();
    expect(MockWebSocket.instances).toHaveLength(1);
    socket.serverMessage({ id: JSON.parse(socket.sent[0] as string).id, data: 5 });
    await expect(request).resolves.toBe(5);
    client.dispose();
  });

  it("refuses close codes that browsers reject before changing state", async () => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    expect(() => client.close(1001)).toThrow(RangeError);
    expect(() => client.close(3000.5)).toThrow(RangeError);
    const request = client.request<number>("still-open");
    await vi.advanceTimersByTimeAsync(0);
    socket.serverMessage({ id: JSON.parse(socket.sent[0] as string).id, data: 1 });
    await expect(request).resolves.toBe(1);
    client.close(4000);
    expect(socket.readyState).toBe(MockWebSocket.CLOSED);
    client.dispose();
  });

  it("creates request ids without crypto.randomUUID outside secure contexts", async () => {
    const real = globalThis.crypto;
    vi.stubGlobal("crypto", { getRandomValues: real.getRandomValues.bind(real) });
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    const request = client.request("insecure");
    await vi.advanceTimersByTimeAsync(0);
    const sent = JSON.parse(socket.sent[0] as string);
    expect(sent.id).toMatch(/^[0-9a-f]{32}$/);
    socket.serverMessage({ id: sent.id });
    await expect(request).resolves.toBeUndefined();
    client.dispose();
  });

  it("rejects connection waiters at once when the socket closes without reconnect", async () => {
    const client = createClient({ reconnect: false }).connect();
    const waiting = expect(client.request("waiting")).rejects.toBeInstanceOf(ConnectionClosedError);
    MockWebSocket.instances[0].serverClose(false, 1006);
    await waiting;
    expect(vi.getTimerCount()).toBe(0);
    client.dispose();
  });

  it("forces immediate reconnect only for closed sockets", () => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    client.reconnect();
    expect(MockWebSocket.instances).toHaveLength(1);

    socket.serverClose();
    client.reconnect();
    expect(MockWebSocket.instances).toHaveLength(2);
    client.dispose();
  });

  it("does not reconnect when reconnect is disabled", () => {
    const client = createClient({ reconnect: false }).connect();
    MockWebSocket.instances[0].serverClose(false, 1006);
    vi.advanceTimersByTime(1000);

    expect(MockWebSocket.instances).toHaveLength(1);
    client.dispose();
  });

  it("rejects requests when connection wait times out", async () => {
    const client = createClient({ waitConnectionTimeout: 100 }).connect();
    const request = client.request("test:read");
    const rejected = expect(request).rejects.toBeInstanceOf(ConnectionClosedError);

    await vi.advanceTimersByTimeAsync(150);

    await rejected;
    expect(client.pendingRequestCount).toBe(0);
    client.dispose();
  });

  it("lets one request override the shared timeout", async () => {
    const client = createClient({ requestTimeout: 1000 }).connect();
    MockWebSocket.instances[0].open();
    const request = client.request("test:read", undefined, 10);
    const rejected = expect(request).rejects.toBeInstanceOf(RequestTimeoutError);

    await vi.advanceTimersByTimeAsync(10);

    await rejected;
    client.dispose();
  });

  it("sends raw strings without JSON encoding", async () => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    socket.open();

    await client.send("raw", false);

    expect(socket.sent).toContain("raw");
    client.dispose();
  });

  it("keeps notifying listeners after one listener throws", () => {
    const client = createClient().connect();
    const called = vi.fn();
    client.on("open", () => {
      throw new Error("listener failed");
    });
    client.on("open", called);

    MockWebSocket.instances[0].open();

    expect(called).toHaveBeenCalledOnce();
    client.dispose();
  });

  it("ignores invalid server JSON", () => {
    const client = createClient().connect();
    const listener = vi.fn();
    client.on("message", listener);
    const socket = MockWebSocket.instances[0];

    socket.serverMessage(undefined);

    expect(listener).not.toHaveBeenCalled();
    client.dispose();
  });

  it("makes dispose idempotent and terminal", () => {
    const client = createClient();
    client.dispose();
    client.dispose();

    expect(() => client.connect()).toThrow(ConnectionClosedError);
    expect(() => client.request("test:read")).toThrow(ConnectionClosedError);
  });

  it("requires a host when location is unavailable", () => {
    const client = new DarkWs({ path: "/ws", secure: false });

    expect(() => client.connect()).toThrow("DarkWs host is required");
    client.dispose();
  });
});
