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

const baseOptions: DarkWsOptions = {
  host: "example.test",
  path: "/ws",
  query: {},
  secure: false,
  reconnect: true,
  reconnectTimeout: 100,
  pingTimeout: 1000,
};

const createClient = (options: Partial<DarkWsOptions> = {}) => new DarkWs({ ...baseOptions, ...options });

describe("DarkWs", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    MockWebSocket.instances = [];
    vi.stubGlobal("WebSocket", MockWebSocket);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  describe("pending request limit", () => {
    it("rejects request 257 immediately without adding requests, hooks, or timers", async () => {
      const requestOptions = vi.fn(() => undefined);
      const client = createClient({ reconnect: false, requestOptions });
      const pending = Promise.allSettled(Array.from({ length: 256 }, () => client.request("waiting")));
      expect(client.pendingRequestCount).toBe(256);
      const timers = vi.getTimerCount();
      const overflow = client.request("overflow");
      expect(client.pendingRequestCount).toBe(256);
      expect(vi.getTimerCount()).toBe(timers);
      expect(requestOptions).toHaveBeenCalledTimes(256);
      await expect(overflow).rejects.toThrow("Pending request limit of 256 reached");
      client.dispose();
      await pending;
      expect(client.pendingRequestCount).toBe(0);
      expect(vi.getTimerCount()).toBe(0);
    });

    it.each([0, -1, 1.5, NaN, Infinity, Number.MAX_SAFE_INTEGER + 1])("rejects invalid maxPendingRequests %s before allocating resources", maxPendingRequests => {
      expect(() => createClient({ maxPendingRequests, reconnectOnVisible: true })).toThrow(RangeError);
      expect(MockWebSocket.instances).toHaveLength(0);
      expect(vi.getTimerCount()).toBe(0);
    });

    it("retains its slot between retry attempts and releases it after success", async () => {
      vi.spyOn(Math, "random").mockReturnValue(0.5);
      const client = createClient({ maxPendingRequests: 1 }).connect();
      const socket = MockWebSocket.instances[0];
      socket.open();
      const result = client.request<number>("read", undefined, { timeout: 10, retry: { timeout: 1, jitter: 100 } });
      await vi.advanceTimersByTimeAsync(0);
      // The response timer has fired, but the retry continuation has not run yet.
      vi.advanceTimersByTime(10);
      await expect(client.request("between-attempts")).rejects.toBeInstanceOf(RangeError);
      const timers = vi.getTimerCount();
      await expect(client.request("during-jitter")).rejects.toBeInstanceOf(RangeError);
      expect(vi.getTimerCount()).toBe(timers);
      await vi.advanceTimersByTimeAsync(50);
      const retried = JSON.parse(socket.sent[1] as string);
      socket.serverMessage({ id: retried.id, data: 42 });
      await expect(result).resolves.toBe(42);
      const next = client.request<number>("next");
      await vi.advanceTimersByTimeAsync(0);
      socket.serverMessage({ id: JSON.parse(socket.sent[2] as string).id, data: 7 });
      await expect(next).resolves.toBe(7);
      client.dispose();
    });

    it("shares capacity with manual controls but allows automatic authentication when full", async () => {
      const client = createClient({ maxPendingRequests: 1, authenticationToken: () => "valid" });
      const request = client.request<number>("private");
      const socket = MockWebSocket.instances[0];
      const timers = vi.getTimerCount();
      await expect(client.authenticate("manual")).rejects.toBeInstanceOf(RangeError);
      await expect(client.logout()).rejects.toBeInstanceOf(RangeError);
      expect(vi.getTimerCount()).toBe(timers);
      socket.open();
      await vi.advanceTimersByTimeAsync(0);
      expect(socket.sent).toEqual(["auth:valid"]);
      socket.serverText("auth:success");
      await vi.advanceTimersByTimeAsync(0);
      socket.serverMessage({ id: JSON.parse(socket.sent[1] as string).id, data: 42 });
      await expect(request).resolves.toBe(42);
      const authentication = client.authenticate("manual");
      await expect(client.request("overflow")).rejects.toBeInstanceOf(RangeError);
      await expect(client.logout()).rejects.toBeInstanceOf(RangeError);
      await vi.advanceTimersByTimeAsync(0);
      socket.serverText("auth:failed");
      await expect(authentication).rejects.toMatchObject({ message: "auth:failed" });
      const logout = client.logout();
      await vi.advanceTimersByTimeAsync(0);
      socket.serverText("logout:success");
      await logout;
      client.dispose();
    });

    it("releases capacity after invalid options, a timeout, and an explicit close", async () => {
      const client = createClient({ maxPendingRequests: 1, reconnect: false });
      await expect(client.request("invalid", undefined, { retry: { timeout: -1 } })).rejects.toBeInstanceOf(RangeError);
      const timedOut = expect(client.request("waiting")).rejects.toBeInstanceOf(ConnectionClosedError);
      await vi.advanceTimersByTimeAsync(30000);
      await timedOut;
      const closed = expect(client.request("closed")).rejects.toBeInstanceOf(ConnectionClosedError);
      client.close();
      await closed;
      client.connect();
      const socket = MockWebSocket.instances.at(-1)!;
      socket.open();
      const next = client.request<number>("next");
      await vi.advanceTimersByTimeAsync(0);
      socket.serverMessage({ id: JSON.parse(socket.sent[0] as string).id, data: 1 });
      await expect(next).resolves.toBe(1);
      client.dispose();
    });
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
    const rejected = expect(request).rejects.toMatchObject({ name: "ConnectionClosedError", sent: false });
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

  it.each([null, 42, {}, { toString: null, valueOf: null }])("rejects invalid response error %j and releases capacity", async error => {
    const client = createClient({ maxPendingRequests: 1, requestTimeout: 10 }).connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    const rejected = expect(client.request("invalid-error")).rejects.toBeInstanceOf(TypeError);
    await vi.advanceTimersByTimeAsync(0);
    const id = JSON.parse(socket.sent[0] as string).id;
    socket.serverMessage({ id, error });
    await rejected;
    expect(client.pendingRequestCount).toBe(0);
    expect(vi.getTimerCount()).toBe(1);
    socket.serverMessage({ id, data: "late" });

    const timedOut = expect(client.request("next")).rejects.toBeInstanceOf(RequestTimeoutError);
    await vi.advanceTimersByTimeAsync(10);
    await timedOut;
    const disposed = expect(client.request("last")).rejects.toBeInstanceOf(ConnectionClosedError);
    client.dispose();
    await disposed;
    expect(client.pendingRequestCount).toBe(0);
    expect(vi.getTimerCount()).toBe(0);
  });

  it.each([null, [], 42, {}, { id: 1 }, { id: "@" }, { id: "@", action: 42 },
    { id: "@", action: "" }, { id: "@", action: "tick", error: null },
    { id: "@", action: "tick", error: "denied" },
  ])("ignores invalid envelopes without disturbing pending requests: %j", async envelope => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    const listener = vi.fn();
    client.on("message", listener);
    socket.open();
    const result = client.request("read");
    await vi.advanceTimersByTimeAsync(0);
    socket.serverMessage(envelope);
    expect(listener).not.toHaveBeenCalled();
    expect(client.pendingRequestCount).toBe(1);
    socket.serverMessage({ id: JSON.parse(socket.sent[0] as string).id, data: null });
    await expect(result).resolves.toBeNull();
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

  it("identifies current and stale socket events without changing their payloads", () => {
    const client = createClient();
    expect(client.isCurrentSocket(new Event("open"))).toBe(false);
    const opened: Event[] = [];
    const closed: boolean[] = [];
    client.on("open", event => opened.push(event));
    client.on("close", event => closed.push(client.isCurrentSocket(event)));
    client.connect();
    const first = MockWebSocket.instances[0];
    first.open();
    expect(opened[0].target).toBe(first);
    expect(client.isCurrentSocket(opened[0])).toBe(true);
    first.serverClose();
    expect(closed.at(-1)).toBe(true);

    client.reconnect();
    const second = MockWebSocket.instances[1];
    second.open();
    expect(client.isCurrentSocket(opened[0])).toBe(false);
    expect(client.isCurrentSocket(opened[1])).toBe(true);
    first.serverClose();
    expect(closed.at(-1)).toBe(false);
    expect(client.connected).toBe(true);
    second.serverClose();
    expect(closed.at(-1)).toBe(true);
    client.dispose();
    expect(client.isCurrentSocket(opened[1])).toBe(false);
  });

  describe("reconnectOnVisible", () => {
    let page: EventTarget & { visibilityState: string };
    const visibility = (state: string): void => {
      page.visibilityState = state;
      page.dispatchEvent(new Event("visibilitychange"));
    };

    beforeEach(() => {
      page = Object.assign(new EventTarget(), { visibilityState: "hidden" });
      vi.stubGlobal("document", page);
    });

    it("skips pending backoff on visibility without replacing an open or opening socket", () => {
      const client = createClient({ reconnectOnVisible: true, reconnectTimeout: 10000 });
      visibility("visible");
      expect(MockWebSocket.instances).toHaveLength(0);
      client.connect();
      const first = MockWebSocket.instances[0];
      first.open();
      first.serverClose();
      expect(vi.getTimerCount()).toBe(1);
      visibility("hidden");
      expect(MockWebSocket.instances).toHaveLength(1);
      visibility("visible");
      expect(MockWebSocket.instances).toHaveLength(2);
      expect(vi.getTimerCount()).toBe(0);
      visibility("visible");
      MockWebSocket.instances[1].open();
      visibility("visible");
      expect(MockWebSocket.instances).toHaveLength(2);
      client.dispose();
    });

    it.each<Partial<DarkWsOptions>>([{}, { reconnectOnVisible: false }, { reconnectOnVisible: true, reconnect: false }])(
      "does not accelerate reconnect when disabled: %j", options => {
        const client = createClient(options).connect();
        MockWebSocket.instances[0].open();
        MockWebSocket.instances[0].serverClose();
        visibility("visible");
        expect(MockWebSocket.instances).toHaveLength(1);
        client.dispose();
      },
    );

    it.each(["close", "dispose"] as const)("does not reconnect after %s cancels a pending retry", method => {
      const remove = vi.spyOn(page, "removeEventListener");
      const client = createClient({ reconnectOnVisible: true }).connect();
      MockWebSocket.instances[0].open();
      MockWebSocket.instances[0].serverClose();
      client[method]();
      visibility("visible");
      vi.advanceTimersByTime(1000);
      expect(MockWebSocket.instances).toHaveLength(1);
      expect(vi.getTimerCount()).toBe(0);
      client.dispose();
      expect(remove).toHaveBeenCalledExactlyOnceWith("visibilitychange", expect.any(Function));
    });

    it("still respects canConnect when the tab returns", () => {
      let allowed = false;
      const client = createClient({ reconnectOnVisible: true, canConnect: () => allowed }).connect();
      visibility("visible");
      expect(MockWebSocket.instances).toHaveLength(0);
      allowed = true;
      visibility("visible");
      expect(MockWebSocket.instances).toHaveLength(1);
      expect(vi.getTimerCount()).toBe(0);
      client.dispose();
    });

    it("keeps retrying if the immediate connection attempt throws", () => {
      let failing = false;
      const client = createClient({
        reconnectOnVisible: true,
        query: () => { if (failing) throw new Error("token storage unavailable"); return {}; },
      }).connect();
      MockWebSocket.instances[0].open();
      MockWebSocket.instances[0].serverClose();
      failing = true;
      visibility("visible");
      expect(MockWebSocket.instances).toHaveLength(1);
      expect(vi.getTimerCount()).toBe(1);
      failing = false;
      vi.advanceTimersByTime(1000);
      expect(MockWebSocket.instances).toHaveLength(2);
      client.dispose();
    });

    it("works without a browser document", () => {
      vi.stubGlobal("document", undefined);
      const client = createClient({ reconnectOnVisible: true }).connect();
      MockWebSocket.instances[0].serverClose();
      vi.advanceTimersByTime(100);
      expect(MockWebSocket.instances).toHaveLength(2);
      client.dispose();
    });
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

    await expect(request).rejects.toMatchObject({ name: "ConnectionClosedError", sent: true });
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

  it("rejects an empty token without connecting or queuing a command", async () => {
    const client = createClient();
    await expect(client.authenticate("")).rejects.toBeInstanceOf(TypeError);
    expect(MockWebSocket.instances).toHaveLength(0);
    expect(client.pendingRequestCount).toBe(0);
    expect(vi.getTimerCount()).toBe(0);
    client.dispose();
  });

  it("waits for authentication acknowledgement without an empty token disturbing the command", async () => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    const authenticated = client.authenticate("test-token");
    await vi.advanceTimersByTimeAsync(0);
    await expect(client.authenticate("")).rejects.toBeInstanceOf(TypeError);
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
    const client = createClient({ controlTimeout: 10 }).connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    const authentication = expect(client.authenticate("test-token")).rejects.toBeInstanceOf(RequestTimeoutError);
    const queued = expect(client.logout()).rejects.toMatchObject({ name: "ConnectionClosedError", sent: false });
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

  it.each([
    ["authenticate", {}],
    ["logout", {}],
    ["authenticate", { controlTimeout: undefined }],
  ] as const)("uses a separate 30-second default deadline for %s with %j", async (command, options) => {
    const client = createClient({ requestTimeout: 10, pingInterval: 60000, ...options }).connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    const ordinary = expect(client.request("read")).rejects.toBeInstanceOf(RequestTimeoutError);
    const control = command === "authenticate" ? client.authenticate("token") : client.logout();
    const rejected = expect(control).rejects.toBeInstanceOf(RequestTimeoutError);
    await vi.advanceTimersByTimeAsync(10);
    await ordinary;
    expect(client.pendingRequestCount).toBe(1);
    await vi.advanceTimersByTimeAsync(29989);
    expect(socket.readyState).toBe(MockWebSocket.OPEN);
    expect(client.pendingRequestCount).toBe(1);
    await vi.advanceTimersByTimeAsync(1);
    await rejected;
    expect(socket.readyState).toBe(MockWebSocket.CLOSED);
    expect(client.pendingRequestCount).toBe(0);
    client.dispose();
  });

  it("starts a queued logout deadline only when it is sent", async () => {
    const client = createClient({ controlTimeout: 10 }).connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    const auth = client.authenticate("token");
    const logout = expect(client.logout()).rejects.toBeInstanceOf(RequestTimeoutError);
    await vi.advanceTimersByTimeAsync(9);
    expect(socket.sent).toEqual(["auth:token"]);
    socket.serverText("auth:success");
    await auth;
    await vi.advanceTimersByTimeAsync(9);
    expect(socket.sent).toEqual(["auth:token", "logout"]);
    expect(socket.readyState).toBe(MockWebSocket.OPEN);
    await vi.advanceTimersByTimeAsync(1);
    await logout;
    expect(socket.readyState).toBe(MockWebSocket.CLOSED);
    expect(client.pendingRequestCount).toBe(0);
    client.dispose();
  });

  it.each(["authenticate", "logout"] as const)("disables the %s deadline with controlTimeout zero", async command => {
    const client = createClient({ controlTimeout: 0, requestTimeout: 10, pingInterval: 60000 }).connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    const control = command === "authenticate" ? client.authenticate("token") : client.logout();
    await vi.advanceTimersByTimeAsync(30001);
    expect(socket.readyState).toBe(MockWebSocket.OPEN);
    expect(client.pendingRequestCount).toBe(1);
    socket.serverText(command === "authenticate" ? "auth:success" : "logout:success");
    await expect(control).resolves.toBeUndefined();
    expect(client.pendingRequestCount).toBe(0);
    client.dispose();
  });

  it("uses controlTimeout for automatic session restoration", async () => {
    const client = createClient({ controlTimeout: 10, authenticationToken: () => "token" }).connect();
    const socket = MockWebSocket.instances[0];
    const opened = vi.fn();
    client.on("open", opened);
    socket.open();
    await vi.advanceTimersByTimeAsync(10);
    expect(socket.sent).toEqual(["auth:token"]);
    expect(socket.readyState).toBe(MockWebSocket.CLOSED);
    expect(client.pendingRequestCount).toBe(0);
    expect(opened).not.toHaveBeenCalled();
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
    const restoreFailed = vi.fn();
    client.on("sessionRestoreFailed", restoreFailed);
    client.on("open", opened);
    const queued = expect(client.request("queued")).rejects.toMatchObject({ message: "auth:failed" });
    socket.open();
    await vi.advanceTimersByTimeAsync(0);
    expect(opened).not.toHaveBeenCalled();
    socket.serverText("auth:failed");
    await queued;
    expect(restoreFailed).toHaveBeenCalledTimes(1);
    expect(opened).toHaveBeenCalledTimes(1);
    expect(restoreFailed.mock.calls[0][0]).toBeInstanceOf(ErrorResponse);
    expect(restoreFailed.mock.calls[0][0].message).toBe("auth:failed");
    expect(client.isCurrentSocket(restoreFailed.mock.calls[0][1])).toBe(true);
    expect(restoreFailed.mock.invocationCallOrder[0]).toBeLessThan(opened.mock.invocationCallOrder[0]);
    const anonymous = expect(client.request("public")).rejects.toBeInstanceOf(ConnectionClosedError);
    await vi.advanceTimersByTimeAsync(0);
    expect(JSON.parse(socket.sent[1] as string).action).toBe("public");
    client.dispose();
    await anonymous;
  });

  it("lets a restore-failure listener close the socket before anonymous readiness", async () => {
    const failure = new Error("Token provider failed");
    const client = createClient({ authenticationToken: () => { throw failure; } }).connect();
    const opened = vi.fn();
    const restoreFailed = vi.fn(() => client.close());
    client.on("sessionRestoreFailed", restoreFailed);
    client.on("open", opened);
    const queued = expect(client.request("private")).rejects.toBe(failure);
    MockWebSocket.instances[0].open();
    await queued;
    expect(restoreFailed).toHaveBeenCalledTimes(1);
    expect(restoreFailed).toHaveBeenCalledWith(failure, expect.anything());
    expect(opened).not.toHaveBeenCalled();
    expect(MockWebSocket.instances[0].sent).toEqual([]);
    expect(vi.getTimerCount()).toBe(0);
    client.dispose();
  });

  it("does not report session restoration failure from a replaced socket", async () => {
    let rejectToken!: (reason: Error) => void;
    const client = createClient({ authenticationToken: () => new Promise((_, reject) => { rejectToken = reject; }) }).connect();
    const restoreFailed = vi.fn();
    client.on("sessionRestoreFailed", restoreFailed);
    MockWebSocket.instances[0].open();
    client.close();
    client.connect();
    rejectToken(new Error("Old provider failed"));
    await vi.advanceTimersByTimeAsync(0);
    expect(restoreFailed).not.toHaveBeenCalled();
    client.dispose();
  });

  it.each([undefined, null, ""])("connects without authentication when the provider returns %j", async token => {
    const client = createClient({ authenticationToken: () => token }).connect();
    const restoreFailed = vi.fn();
    client.on("sessionRestoreFailed", restoreFailed);
    const socket = MockWebSocket.instances[0];
    const request = client.request<number>("public");
    socket.open();
    await vi.advanceTimersByTimeAsync(0);
    const sent = JSON.parse(socket.sent[0] as string);
    expect(sent.action).toBe("public");
    socket.serverMessage({ id: sent.id, data: 7 });
    await expect(request).resolves.toBe(7);
    expect(restoreFailed).not.toHaveBeenCalled();
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

  it.each([undefined, null, { id: 42 }, 0, false, ""])("onAction delivers only matching broadcast data %j", data => {
    const client = createClient().connect();
    const socket = MockWebSocket.instances[0];
    const listener = vi.fn();
    client.onAction("client:sync", listener);
    socket.serverMessage({ id: "@", action: "other", data });
    socket.serverMessage({ id: "@", action: "Client:Sync", data });
    socket.serverMessage({ id: "request-id", action: "client:sync", data });
    expect(listener).not.toHaveBeenCalled();
    socket.serverMessage({ id: "@", action: "client:sync", ...(data === undefined ? {} : { data }) });
    expect(listener).toHaveBeenCalledExactlyOnceWith(data);
    client.dispose();
  });

  it("keeps onAction subscriptions independent across unsubscribe and reconnect", () => {
    const client = createClient().connect();
    const received: number[] = [];
    const callback = (data: number): void => { received.push(data); };
    const first = client.onAction("count", callback);
    const second = client.onAction("count", callback);
    const messages = vi.fn();
    client.on("message", messages);
    first();
    first();
    const socket = MockWebSocket.instances[0];
    socket.serverMessage({ id: "@", action: "count", data: 1 });
    expect(received).toEqual([1]);
    socket.serverClose();
    client.reconnect();
    const next = MockWebSocket.instances[1];
    next.serverMessage({ id: "@", action: "count", data: 2 });
    expect(received).toEqual([1, 2]);
    second();
    next.serverMessage({ id: "@", action: "count", data: 3 });
    expect(received).toEqual([1, 2]);
    expect(messages).toHaveBeenCalledTimes(3);
    expect(messages).toHaveBeenLastCalledWith({ id: "@", action: "count", data: 3 }, expect.anything());
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

  it.each([10, { timeout: 10 }])("lets one request override the shared timeout with %j", async options => {
    const client = createClient({ requestTimeout: 1000 }).connect();
    MockWebSocket.instances[0].open();
    const request = client.request("test:read", undefined, options);
    const rejected = expect(request).rejects.toBeInstanceOf(RequestTimeoutError);

    await vi.advanceTimersByTimeAsync(10);

    await rejected;
    client.dispose();
  });

  describe("request retries", () => {
    beforeEach(() => vi.spyOn(Math, "random").mockReturnValue(0.5));

    it("retries a timeout with a fresh id and ignores the late response", async () => {
      const client = createClient({ reconnect: false }).connect();
      const socket = MockWebSocket.instances[0];
      socket.open();
      const request = client.request<number>("read", { key: "a" }, { timeout: 10, retry: { timeout: 1 } });
      await vi.advanceTimersByTimeAsync(11);
      const [first, second] = socket.sent.map(value => JSON.parse(value as string));
      expect(second).toEqual({ ...first, id: expect.any(String) });
      expect(second.id).not.toBe(first.id);
      socket.serverMessage({ id: first.id, data: 1 });
      expect(client.pendingRequestCount).toBe(1);
      socket.serverMessage({ id: second.id, data: 2 });
      await expect(request).resolves.toBe(2);
      expect(client.pendingRequestCount).toBe(0);
      client.dispose();
    });

    it("retries an unsent request after a connection fails to open", async () => {
      const client = createClient({ reconnect: false }).connect();
      const request = client.request("read", undefined, { retry: { connectionClosed: 1 } });
      const first = MockWebSocket.instances[0];
      first.serverClose();
      await vi.advanceTimersByTimeAsync(1);
      const second = MockWebSocket.instances[1];
      second.open();
      await vi.advanceTimersByTimeAsync(0);
      expect(first.sent).toEqual([]);
      const sent = JSON.parse(second.sent[0] as string);
      second.serverMessage({ id: sent.id });
      await expect(request).resolves.toBeUndefined();
      client.dispose();
    });

    it("keeps independent connection and timeout retry budgets", async () => {
      const client = createClient({ reconnect: false }).connect();
      const first = MockWebSocket.instances[0];
      first.open();
      const rejected = expect(client.request("read", undefined, {
        timeout: 10, retry: { connectionClosed: 1, timeout: 1 },
      })).rejects.toMatchObject({ name: "ConnectionClosedError", sent: true });
      await vi.advanceTimersByTimeAsync(0);
      first.serverClose();
      await vi.advanceTimersByTimeAsync(1);
      const second = MockWebSocket.instances[1];
      second.open();
      await vi.advanceTimersByTimeAsync(11);
      expect(first.sent).toHaveLength(1);
      expect(second.sent).toHaveLength(2);
      second.serverClose();
      await rejected;
      await vi.advanceTimersByTimeAsync(100);
      expect(MockWebSocket.instances).toHaveLength(2);
      expect(client.pendingRequestCount).toBe(0);
      expect(vi.getTimerCount()).toBe(0);
      client.dispose();
    });

    it("stops after the timeout retry budget is exhausted", async () => {
      const client = createClient().connect();
      const socket = MockWebSocket.instances[0];
      socket.open();
      const rejected = expect(client.request("read", undefined, {
        timeout: 10, retry: { timeout: 1, connectionClosed: 3 },
      })).rejects.toBeInstanceOf(RequestTimeoutError);
      await vi.advanceTimersByTimeAsync(22);
      await rejected;
      expect(socket.sent).toHaveLength(2);
      expect(client.pendingRequestCount).toBe(0);
      client.dispose();
    });

    it("delays retries by a random amount up to jitter milliseconds", async () => {
      const client = createClient().connect();
      const socket = MockWebSocket.instances[0];
      socket.open();
      const request = client.request("read", undefined, { timeout: 10, retry: { timeout: 1, jitter: 100 } });
      await vi.advanceTimersByTimeAsync(59);
      expect(socket.sent).toHaveLength(1);
      expect(client.pendingRequestCount).toBe(1);
      await vi.advanceTimersByTimeAsync(1);
      expect(socket.sent).toHaveLength(2);
      socket.serverMessage({ id: JSON.parse(socket.sent[1] as string).id });
      await request;
      client.dispose();
    });

    it.each(["connection", "timeout"])("remembers a sent %s attempt if the next attempt was unsent", async failure => {
      const client = createClient({ reconnect: false }).connect();
      const first = MockWebSocket.instances[0];
      first.open();
      const rejected = expect(client.request("read", undefined, {
        timeout: 10,
        retry: { connectionClosed: failure === "connection" ? 1 : 0, timeout: failure === "timeout" ? 1 : 0, jitter: 10 },
      })).rejects.toMatchObject({ name: "ConnectionClosedError", sent: true });
      await vi.advanceTimersByTimeAsync(failure === "timeout" ? 10 : 0);
      first.serverClose();
      await vi.advanceTimersByTimeAsync(5);
      const second = MockWebSocket.instances[1];
      second.serverClose();
      await rejected;
      expect(first.sent).toHaveLength(1);
      expect(second.sent).toEqual([]);
      client.dispose();
    });

    it.each(["close", "dispose"] as const)("cancels jitter timers and pending retries on %s", async method => {
      const client = createClient().connect();
      const socket = MockWebSocket.instances[0];
      socket.open();
      const rejected = expect(client.request("read", undefined, {
        timeout: 10, retry: { timeout: 2, jitter: 1000 },
      })).rejects.toMatchObject({ name: "ConnectionClosedError", sent: true });
      await vi.advanceTimersByTimeAsync(10);
      expect(client.pendingRequestCount).toBe(1);
      client[method]();
      await rejected;
      await vi.advanceTimersByTimeAsync(1000);
      expect(socket.sent).toHaveLength(1);
      expect(client.pendingRequestCount).toBe(0);
      expect(vi.getTimerCount()).toBe(0);
      client.dispose();
    });

    it.each([false, true])("does not revive a request across close/connect (retry timer fired: %s)", async timerFired => {
      const client = createClient({ reconnect: false }).connect();
      const first = MockWebSocket.instances[0];
      first.open();
      const rejected = expect(client.request("read", undefined, {
        timeout: 10, retry: { connectionClosed: 2, timeout: 2, jitter: 100 },
      })).rejects.toMatchObject({ name: "ConnectionClosedError", sent: true });
      await vi.advanceTimersByTimeAsync(timerFired ? 10 : 0);
      if (timerFired) vi.advanceTimersByTime(50);
      else first.serverClose();
      client.close();
      client.connect();
      const second = MockWebSocket.instances[1];
      second.open();
      await rejected;
      await vi.advanceTimersByTimeAsync(100);
      expect(first.sent).toHaveLength(1);
      expect(second.sent).toEqual([]);
      expect(client.pendingRequestCount).toBe(0);
      client.dispose();
    });

    it.each(["server", "send"])("does not retry %s errors", async failure => {
      const client = createClient().connect();
      const socket = MockWebSocket.instances[0];
      socket.open();
      const send = vi.spyOn(socket, "send");
      if (failure === "send") send.mockImplementation(() => { throw new Error("send failed"); });
      const request = client.request("read", undefined, { retry: { connectionClosed: 2, timeout: 2 } });
      const rejected = expect(request).rejects.toThrow(failure === "server" ? "denied" : "send failed");
      await vi.advanceTimersByTimeAsync(0);
      if (failure === "server") socket.serverMessage({ id: JSON.parse(socket.sent[0] as string).id, error: "denied" });
      await rejected;
      await vi.advanceTimersByTimeAsync(100);
      expect(send).toHaveBeenCalledTimes(1);
      expect(client.pendingRequestCount).toBe(0);
      client.dispose();
    });

    it.each([
      { connectionClosed: -1 }, { connectionClosed: Infinity }, { timeout: 0.5 },
      { jitter: -1 }, { jitter: NaN }, { jitter: 2147483648 },
    ])("rejects invalid retry options before connecting: %j", async retry => {
      const client = createClient();
      await expect(client.request("read", undefined, { retry })).rejects.toBeInstanceOf(RangeError);
      expect(MockWebSocket.instances).toHaveLength(0);
      expect(client.pendingRequestCount).toBe(0);
      client.dispose();
    });

    it("distinguishes sent and unsent requests during disposal", async () => {
      const client = createClient().connect();
      MockWebSocket.instances[0].open();
      const sent = expect(client.request("sent")).rejects.toMatchObject({ name: "ConnectionClosedError", sent: true });
      await vi.advanceTimersByTimeAsync(0);
      const unsent = expect(client.request("unsent")).rejects.toMatchObject({ name: "ConnectionClosedError", sent: false });
      client.dispose();
      await Promise.all([sent, unsent]);
      expect(client.pendingRequestCount).toBe(0);
    });

    it("marks a request sent before invoking send listeners", async () => {
      const client = createClient().connect();
      MockWebSocket.instances[0].open();
      client.on("send", () => client.close());
      await expect(client.request("read")).rejects.toMatchObject({ name: "ConnectionClosedError", sent: true });
      client.dispose();
    });
  });

  describe("per-action request options", () => {
    it("applies the defaults returned for each action", async () => {
      const requestOptions = vi.fn((action: string) => action === "read" ? { timeout: 10, retry: { timeout: 1 } } : undefined);
      const client = createClient({ requestOptions }).connect();
      const socket = MockWebSocket.instances[0];
      socket.open();
      const read = expect(client.request("read")).rejects.toBeInstanceOf(RequestTimeoutError);
      const write = client.request("write");
      await vi.advanceTimersByTimeAsync(21);
      await read;
      expect(requestOptions.mock.calls).toEqual([["read"], ["write"]]);
      expect(socket.sent.map(value => JSON.parse(value as string).action)).toEqual(["read", "write", "read"]);
      expect(client.pendingRequestCount).toBe(1);
      socket.serverMessage({ id: JSON.parse(socket.sent[1] as string).id, data: 1 });
      await expect(write).resolves.toBe(1);
      client.dispose();
    });

    it("lets explicit options override the defaults field by field", async () => {
      const client = createClient({
        reconnect: false,
        requestOptions: () => ({ timeout: 1000, retry: { timeout: 1, connectionClosed: 1 } }),
      }).connect();
      const socket = MockWebSocket.instances[0];
      socket.open();
      const rejected = expect(client.request("read", undefined, { timeout: 10, retry: { connectionClosed: 0 } }))
        .rejects.toMatchObject({ name: "ConnectionClosedError", sent: true });
      await vi.advanceTimersByTimeAsync(11);
      expect(socket.sent).toHaveLength(2);
      socket.serverClose();
      await rejected;
      expect(MockWebSocket.instances).toHaveLength(1);
      client.dispose();
    });

    it("keeps the default retries with a numeric timeout", async () => {
      const client = createClient({ requestOptions: () => ({ timeout: 1000, retry: { timeout: 1 } }) }).connect();
      const socket = MockWebSocket.instances[0];
      socket.open();
      const rejected = expect(client.request("read", undefined, 10)).rejects.toBeInstanceOf(RequestTimeoutError);
      await vi.advanceTimersByTimeAsync(21);
      await rejected;
      expect(socket.sent).toHaveLength(2);
      client.dispose();
    });

    it("rejects a failing provider or invalid defaults before connecting", async () => {
      const failing = createClient({ requestOptions: () => { throw new Error("options failed"); } });
      await expect(failing.request("read")).rejects.toThrow("options failed");
      const invalid = createClient({ requestOptions: () => ({ retry: { jitter: -1 } }) });
      await expect(invalid.request("read")).rejects.toBeInstanceOf(RangeError);
      expect(MockWebSocket.instances).toHaveLength(0);
      failing.dispose();
      invalid.dispose();
    });

    it("does not consult the provider for authentication commands", async () => {
      const requestOptions = vi.fn(() => undefined);
      const client = createClient({ requestOptions }).connect();
      const socket = MockWebSocket.instances[0];
      socket.open();
      const authenticated = client.authenticate("token");
      await vi.advanceTimersByTimeAsync(0);
      socket.serverText("auth:success");
      await authenticated;
      const loggedOut = client.logout();
      await vi.advanceTimersByTimeAsync(0);
      socket.serverText("logout:success");
      await loggedOut;
      expect(requestOptions).not.toHaveBeenCalled();
      client.dispose();
    });
  });

  describe("lazy client", () => {
    const reply = (socket: MockWebSocket, index: number, data?: unknown) =>
      socket.serverMessage({ id: JSON.parse(socket.sent[index] as string).id, data });

    it("creates and connects the client once on the first request", async () => {
      const options = vi.fn(() => baseOptions);
      const dws = DarkWs.lazy(options);
      expect(options).not.toHaveBeenCalled();
      expect(MockWebSocket.instances).toHaveLength(0);
      const first = dws.request<number>("first");
      const second = dws.request<number>("second");
      expect(MockWebSocket.instances).toHaveLength(1);
      const socket = MockWebSocket.instances[0];
      socket.open();
      await vi.advanceTimersByTimeAsync(0);
      reply(socket, 0, 1);
      reply(socket, 1, 2);
      await expect(Promise.all([first, second])).resolves.toEqual([1, 2]);
      expect(options).toHaveBeenCalledOnce();
      dws.reset(true);
    });

    it("connects on subscription and delivers broadcasts", () => {
      const dws = DarkWs.lazy(baseOptions);
      const messages: unknown[] = [];
      const actions: unknown[] = [];
      dws.on("message", message => messages.push(message));
      dws.onAction("user:updated", data => actions.push(data));
      expect(MockWebSocket.instances).toHaveLength(1);
      const socket = MockWebSocket.instances[0];
      socket.open();
      socket.serverMessage({ id: "@", action: "user:updated", data: 1 });
      expect(messages).toEqual([{ id: "@", action: "user:updated", data: 1 }]);
      expect(actions).toEqual([1]);
      dws.reset(true);
    });

    it("connects for send and authentication commands", async () => {
      const dws = DarkWs.lazy(baseOptions);
      const sent = dws.send("raw", false);
      const socket = MockWebSocket.instances[0];
      socket.open();
      await sent;
      const authenticated = dws.authenticate("token");
      await vi.advanceTimersByTimeAsync(0);
      socket.serverText("auth:success");
      await authenticated;
      const loggedOut = dws.logout();
      await vi.advanceTimersByTimeAsync(0);
      socket.serverText("logout:success");
      await loggedOut;
      expect(socket.sent).toEqual(["raw", "auth:token", "logout"]);
      dws.reset(true);
    });

    it("does not bypass reconnect backoff when subscribing again", () => {
      const dws = DarkWs.lazy(baseOptions);
      dws.on("open", () => {});
      MockWebSocket.instances[0].open();
      MockWebSocket.instances[0].serverClose();
      dws.onAction("later", () => {});
      expect(MockWebSocket.instances).toHaveLength(1);
      vi.advanceTimersByTime(100);
      expect(MockWebSocket.instances).toHaveLength(2);
      dws.reset(true);
    });

    it("connects again after close without creating a client just to close it", async () => {
      const options = vi.fn(() => baseOptions);
      const dws = DarkWs.lazy(options);
      dws.close();
      expect(options).not.toHaveBeenCalled();
      dws.on("open", () => {});
      MockWebSocket.instances[0].open();
      dws.close();
      expect(dws.instance.closed).toBe(true);
      const request = dws.request("read");
      expect(MockWebSocket.instances).toHaveLength(2);
      const socket = MockWebSocket.instances[1];
      socket.open();
      await vi.advanceTimersByTimeAsync(0);
      reply(socket, 0, "ok");
      await expect(request).resolves.toBe("ok");
      dws.reset(true);
    });

    it("creates the instance without connecting", () => {
      const dws = DarkWs.lazy(baseOptions);
      expect(dws.instance).toBe(dws.instance);
      expect(dws.instance.closed).toBe(true);
      expect(MockWebSocket.instances).toHaveLength(0);
      dws.reset(true);
    });

    it("restarts a started client on reset with fresh options and kept subscriptions", async () => {
      const hosts = ["first.test", "second.test"];
      const dws = DarkWs.lazy(() => ({ ...baseOptions, host: hosts.shift() }));
      const actions: unknown[] = [];
      dws.onAction("tick", data => actions.push(data));
      const previous = dws.instance;
      MockWebSocket.instances[0].open();
      const pending = expect(dws.request("read")).rejects.toMatchObject({ message: "DarkWs client was disposed" });
      dws.reset();
      await pending;
      expect(dws.instance).not.toBe(previous);
      const socket = MockWebSocket.instances[1];
      expect(socket.url).toBe("ws://second.test/ws");
      socket.open();
      socket.serverMessage({ id: "@", action: "tick", data: 2 });
      expect(actions).toEqual([2]);
      dws.reset(true);
    });

    it("does not connect on reset unless the client was started", () => {
      const dws = DarkWs.lazy(baseOptions);
      void dws.instance;
      dws.reset();
      dws.on("open", () => {});
      dws.close();
      dws.reset();
      expect(MockWebSocket.instances).toHaveLength(1);
      dws.reset(true);
    });

    it("drops subscriptions on a full reset", () => {
      const dws = DarkWs.lazy(baseOptions);
      const listener = vi.fn();
      dws.on("open", listener);
      dws.reset(true);
      expect(MockWebSocket.instances).toHaveLength(1);
      dws.reconnect();
      MockWebSocket.instances[1].open();
      expect(listener).not.toHaveBeenCalled();
      dws.reset(true);
    });

    it("removes unsubscribed listeners from the current and the next client", () => {
      const dws = DarkWs.lazy(baseOptions);
      const kept = vi.fn();
      const removed = vi.fn();
      const action = vi.fn();
      dws.on("open", kept);
      dws.on("open", removed);
      const unsubscribe = dws.onAction("tick", action);
      dws.off("open", removed);
      dws.off("open", removed);
      unsubscribe();
      unsubscribe();
      const first = MockWebSocket.instances[0];
      first.open();
      first.serverMessage({ id: "@", action: "tick" });
      dws.reset();
      const second = MockWebSocket.instances[1];
      second.open();
      second.serverMessage({ id: "@", action: "tick" });
      expect(kept).toHaveBeenCalledTimes(2);
      expect(removed).not.toHaveBeenCalled();
      expect(action).not.toHaveBeenCalled();
      dws.reset(true);
    });

    it("reports a failing options factory to the caller", async () => {
      const dws = DarkWs.lazy(() => { throw new Error("config missing"); });
      await expect(dws.request("read")).rejects.toThrow("config missing");
      expect(() => dws.on("open", () => {})).toThrow("config missing");
      expect(MockWebSocket.instances).toHaveLength(0);
    });
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
