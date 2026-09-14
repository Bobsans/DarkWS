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
    await Promise.resolve();
    expect(client.pendingRequestCount).toBe(1);
    const request = JSON.parse(socket.sent[0] as string);
    expect(request).toMatchObject({ action: "darkws:authenticate", payload: "test-token" });
    socket.serverMessage({ id: request.id });
    await expect(authenticated).resolves.toBeUndefined();
    expect(client.pendingRequestCount).toBe(0);
    client.dispose();
  });

  it("reports authentication rejection and permits logout without reconnect", async () => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    const authentication = client.authenticate("expired");
    const rejected = expect(authentication).rejects.toMatchObject({ message: "darkws:error:authentication-failed" });
    await Promise.resolve();
    socket.serverMessage({ id: JSON.parse(socket.sent[0] as string).id, error: "darkws:error:authentication-failed" });
    await rejected;
    const logout = client.logout();
    await Promise.resolve();
    const request = JSON.parse(socket.sent[1] as string);
    expect(request.action).toBe("darkws:logout");
    socket.serverMessage({ id: request.id });
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
    await vi.advanceTimersByTimeAsync(10);
    await authentication;
    const logout = expect(client.logout()).rejects.toBeInstanceOf(ConnectionClosedError);
    await Promise.resolve();
    socket.serverClose();
    await logout;
    expect(client.pendingRequestCount).toBe(0);
    client.dispose();
  });

  it("builds a URL without an empty question mark", () => {
    const client = createClient().connect();
    expect(MockWebSocket.instances[0].url).toBe("ws://example.test/ws");
    client.dispose();
  });

  it("delivers broadcast action and data", () => {
    const client = createClient().connect();
    const received: unknown[] = [];
    client.on("message", (data) => received.push(data));
    MockWebSocket.instances[0].serverMessage({
      id: "@",
      data: { action: "client:sync", data: { id: 42 } },
    });

    expect(received).toEqual([{ action: "client:sync", data: { id: 42 } }]);
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

  it("ignores pong control messages", () => {
    const client = createClient().connect();
    const listener = vi.fn();
    client.on("message", listener);
    MockWebSocket.instances[0].serverMessage("pong");

    expect(listener).not.toHaveBeenCalled();
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
