export interface DarkWsEvents {
  open: [Event];
  close: [CloseEvent];
  error: [Event];
  message: [unknown, MessageEvent];
  send: [unknown];
}

export interface DarkWsOptions {
  host?: string;
  path: string;
  query?: Record<string, string> | (() => Record<string, string>);
  secure: boolean;
  canConnect?: () => boolean;
  beforeConnect?: () => Promise<unknown>;
  authenticationToken?: () => string | null | undefined | Promise<string | null | undefined>;
  requestTimeout?: number;
  reconnect?: boolean;
  reconnectTimeout?: number;
  pingInterval?: number;
  /** @deprecated Use `pingInterval`: this value is the interval between pings, not a timeout. */
  pingTimeout?: number;
  pongTimeout?: number;
  waitConnectionTimeout?: number;
  debug?: boolean;
}

// crypto.randomUUID exists only in secure contexts; getRandomValues also works on plain http pages.
const createId = (): string => typeof crypto.randomUUID === "function"
  ? crypto.randomUUID()
  : Array.from(crypto.getRandomValues(new Uint8Array(16)), byte => byte.toString(16).padStart(2, "0")).join("");

export interface DarkWsRequest<TPayload = unknown> {
  id: string;
  action: string;
  data?: TPayload;
}

interface ResponseMessage {
  id: string;
  action?: string;
  data?: unknown;
  error?: string;
}

interface RequestResolver {
  request: DarkWsRequest;
  control?: boolean;
  socket?: WebSocket;
  timeout?: ReturnType<typeof setTimeout>;
  resolve: (value: unknown) => void;
  reject: (error: Error) => void;
}

interface ConnectionWaiter {
  resolve: (socket: WebSocket) => void;
  reject: (error: Error) => void;
  timeout: ReturnType<typeof setTimeout>;
}

export class ErrorResponse<TData = unknown> extends Error {
  constructor(
    error: string,
    public readonly data: TData,
    public readonly request: DarkWsRequest,
  ) {
    super(error);
    this.name = "ErrorResponse";
  }
}

export class ConnectionClosedError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "ConnectionClosedError";
  }
}

export class RequestTimeoutError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "RequestTimeoutError";
  }
}

export default class DarkWs {
  static readonly LOG_PREFIX = "[DarkWs]";

  private readonly listeners: {
    [K in keyof DarkWsEvents]: ((...args: DarkWsEvents[K]) => void)[]
  } = {
    open: [],
    close: [],
    error: [],
    message: [],
    send: [],
  };

  private readonly requests = new Map<string, RequestResolver>();
  private controlTail: Promise<void> = Promise.resolve();
  private controlRequest?: RequestResolver;
  private readonly connectionWaiters = new Set<ConnectionWaiter>();
  private readonly options: Required<Pick<DarkWsOptions,
    "reconnect" | "reconnectTimeout" | "requestTimeout" | "pongTimeout" |
    "waitConnectionTimeout" | "debug">> & DarkWsOptions;
  private readonly pingInterval: number;
  private socket?: WebSocket;
  private reconnectAttempts = 0;
  private reconnectTimer?: ReturnType<typeof setTimeout>;
  private pingTimer?: ReturnType<typeof setTimeout>;
  private pongTimer?: ReturnType<typeof setTimeout>;
  private connecting = false;
  private ready = false;
  private closedByClient = false;
  private disposed = false;

  constructor(options: DarkWsOptions) {
    this.options = {
      reconnect: true,
      reconnectTimeout: 5000,
      requestTimeout: 300000,
      pongTimeout: 30000,
      waitConnectionTimeout: 30000,
      debug: false,
      ...options,
    };
    this.pingInterval = options.pingInterval ?? options.pingTimeout ?? 30000;
  }

  public get closing(): boolean {
    return this.socket?.readyState === WebSocket.CLOSING;
  }

  public get closed(): boolean {
    return !this.socket || this.socket.readyState === WebSocket.CLOSED;
  }

  public get connected(): boolean {
    return this.socket?.readyState === WebSocket.OPEN;
  }

  public get pendingRequestCount(): number {
    return this.requests.size;
  }

  public connect(): this {
    this.assertNotDisposed();
    this.closedByClient = false;
    // An open or opening socket is kept; close() or reconnect() replaces it deliberately.
    if (this.socket && (
      this.socket.readyState === WebSocket.OPEN ||
      this.socket.readyState === WebSocket.CONNECTING
    )) {
      return this;
    }
    if (this.options.beforeConnect) {
      if (!this.connecting) {
        this.connecting = true;
        void this.connectAfterHook();
      }
      return this;
    }
    return this.openConnection();
  }

  public reconnect(): void {
    this.assertNotDisposed();
    if (this.socket && (
      this.socket.readyState === WebSocket.OPEN ||
      this.socket.readyState === WebSocket.CONNECTING
    )) {
      return;
    }
    this.reconnectAttempts = 0;
    this.clearReconnectTimer();
    this.connect();
  }

  public on<K extends keyof DarkWsEvents>(
    event: K,
    callback: (...args: DarkWsEvents[K]) => void,
  ): () => void {
    this.listeners[event].push(callback);
    return () => this.off(event, callback);
  }

  public off<K extends keyof DarkWsEvents>(
    event: K,
    callback: (...args: DarkWsEvents[K]) => void,
  ): void {
    const index = this.listeners[event].indexOf(callback);
    if (index >= 0) {
      this.listeners[event].splice(index, 1);
    }
  }

  public async send<T>(data: T, jsonify = true): Promise<void> {
    const socket = await this.waitForConnection();
    this.assertSocketOpen(socket);
    socket.send(jsonify ? JSON.stringify(data) : String(data));
    this.emit("send", data);
    this.debug("Data sent", data);
  }

  public request<TResult, TPayload = unknown>(
    action: string,
    payload?: TPayload,
    timeoutMs?: number,
  ): Promise<TResult> {
    this.assertNotDisposed();
    const request: DarkWsRequest<TPayload> = {
      id: createId(),
      action,
      ...(payload === undefined ? {} : { data: payload }),
    };

    return new Promise<TResult>((resolve, reject) => {
      const resolver: RequestResolver = {
        request,
        resolve: (value) => resolve(value as TResult),
        reject,
      };
      this.requests.set(request.id, resolver);
      void this.sendRequest(resolver, timeoutMs);
    });
  }

  public authenticate(token: string): Promise<void> {
    return this.systemRequest("auth", "auth:" + token);
  }

  public logout(): Promise<void> {
    return this.systemRequest("logout", "logout");
  }

  private systemRequest(action: string, text: string, socket?: WebSocket): Promise<void> {
    this.assertNotDisposed();
    // Session restore on a new socket runs before queued commands, so it cannot join their chain.
    const previous = socket ? Promise.resolve() : this.controlTail;
    const result = new Promise<void>((resolve, reject) => {
      const resolver: RequestResolver = {
        request: { id: createId(), action },
        control: true,
        resolve: () => resolve(),
        reject,
      };
      this.requests.set(resolver.request.id, resolver);
      void this.sendRequest(resolver, undefined, { text, previous, socket });
    });
    if (!socket) this.controlTail = result.catch(() => {});
    return result;
  }

  public close(code = 1000): void {
    // Browsers accept only these codes and throw InvalidAccessError otherwise; check before any state changes.
    if (code !== 1000 && !(Number.isInteger(code) && code >= 3000 && code <= 4999)) {
      throw new RangeError("Close code must be 1000 or an integer from 3000 to 4999");
    }
    this.closedByClient = true;
    this.clearReconnectTimer();
    this.clearPingTimer();
    this.rejectConnectionWaiters(new ConnectionClosedError("WebSocket connection was closed by the client"));
    if (!this.socket || this.socket.readyState === WebSocket.CLOSED) {
      return;
    }
    this.socket.close(code, code === 1000 ? "Normal closure" : "");
  }

  public dispose(): void {
    if (this.disposed) {
      return;
    }
    this.disposed = true;
    this.closedByClient = true;
    this.clearReconnectTimer();
    this.clearPingTimer();
    this.rejectConnectionWaiters(new ConnectionClosedError("DarkWs client was disposed"));
    this.rejectAll(new ConnectionClosedError("DarkWs client was disposed"));
    if (this.socket && this.socket.readyState !== WebSocket.CLOSED) {
      this.socket.close(1000, "Normal closure");
    }
    this.socket = undefined;
  }

  private async connectAfterHook(): Promise<void> {
    try {
      await this.options.beforeConnect?.();
      if (!this.disposed && !this.closedByClient) {
        this.openConnection();
      }
    } catch {
      this.scheduleReconnect();
    } finally {
      this.connecting = false;
    }
  }

  private openConnection(): this {
    if (this.options.canConnect && !this.options.canConnect()) {
      this.scheduleReconnect(100);
      return this;
    }

    this.clearReconnectTimer();
    const previous = this.socket;
    this.clearPingTimer();
    if (previous) {
      this.socket = undefined;
      this.rejectRequestsFor(previous);
      previous.close();
    }

    const socket = new WebSocket(this.buildUrl());
    this.socket = socket;
    this.ready = false;
    socket.addEventListener("open", (event) => {
      if (socket !== this.socket) return;
      this.reconnectAttempts = 0;
      this.clearPingTimer();
      this.schedulePing();
      const provider = this.options.authenticationToken;
      if (provider) void this.restoreSession(socket, event, provider);
      else this.markReady(socket, event);
    });
    socket.addEventListener("error", (event) => this.emit("error", event));
    socket.addEventListener("close", (event) => {
      if (socket === this.socket) this.lost();
      this.rejectRequestsFor(socket);
      this.emit("close", event);
    });
    socket.addEventListener("message", (event) => this.handleMessage(event));
    return this;
  }

  private buildUrl(): string {
    const protocol = this.options.secure ? "wss" : "ws";
    const host = this.options.host ?? globalThis.location?.host;
    if (!host) {
      throw new Error("DarkWs host is required outside a browser window");
    }
    const path = this.options.path.replace(/^\/+|\/+$/g, "");
    const queryValue = typeof this.options.query === "function"
      ? this.options.query()
      : this.options.query ?? {};
    const query = new URLSearchParams(queryValue).toString();
    return `${protocol}://${host}/${path}${query ? `?${query}` : ""}`;
  }

  // Queued requests are released only after the new socket carries the session, so none precedes auth.
  private async restoreSession(socket: WebSocket, event: Event, provider: NonNullable<DarkWsOptions["authenticationToken"]>): Promise<void> {
    try {
      const token = await provider();
      if (token) await this.systemRequest("auth", "auth:" + token, socket);
    } catch (error) {
      if (socket !== this.socket || socket.readyState !== WebSocket.OPEN) return;
      this.rejectConnectionWaiters(error instanceof Error ? error : new Error(String(error)));
    }
    if (socket === this.socket && socket.readyState === WebSocket.OPEN) this.markReady(socket, event);
  }

  private markReady(socket: WebSocket, event: Event): void {
    this.ready = true;
    for (const waiter of this.connectionWaiters) {
      clearTimeout(waiter.timeout);
      waiter.resolve(socket);
    }
    this.connectionWaiters.clear();
    this.emit("open", event);
  }

  private async waitForConnection(): Promise<WebSocket> {
    this.assertNotDisposed();
    if (this.closedByClient) throw new ConnectionClosedError("WebSocket connection was closed by the client");
    if (this.closed) this.connect();
    if (this.ready && this.socket?.readyState === WebSocket.OPEN) {
      return this.socket;
    }
    return new Promise<WebSocket>((resolve, reject) => {
      const waiter: ConnectionWaiter = {
        resolve,
        reject,
        timeout: setTimeout(() => {
          this.connectionWaiters.delete(waiter);
          reject(new ConnectionClosedError("WebSocket connection wait timeout"));
        }, this.options.waitConnectionTimeout),
      };
      this.connectionWaiters.add(waiter);
    });
  }

  private rejectConnectionWaiters(error: Error): void {
    for (const waiter of this.connectionWaiters) {
      clearTimeout(waiter.timeout);
      waiter.reject(error);
    }
    this.connectionWaiters.clear();
  }

  private async sendRequest(resolver: RequestResolver, timeoutMs?: number, control?: { text: string; previous: Promise<void>; socket?: WebSocket }): Promise<void> {
    try {
      const socket = control?.socket ?? await this.waitForConnection();
      resolver.socket = socket;
      if (control) await control.previous;
      if (!this.requests.has(resolver.request.id)) return;
      this.assertSocketOpen(socket);
      if (control) this.controlRequest = resolver;
      socket.send(control ? control.text : JSON.stringify(resolver.request));
      this.emit("send", resolver.request);
      if (!this.requests.has(resolver.request.id)) {
        return;
      }
      const timeout = timeoutMs ?? this.options.requestTimeout;
      if (timeout > 0) {
        resolver.timeout = setTimeout(() => {
          this.clearResolver(resolver.request.id, resolver);
          resolver.reject(new RequestTimeoutError("Request cancelled by timeout"));
          if (control) {
            // Text acknowledgements cannot be correlated after a timeout.
            this.rejectRequestsFor(socket);
            socket.close(1000);
          }
        }, timeout);
      }
    } catch (error) {
      const ambiguousSocket = control && this.controlRequest === resolver ? resolver.socket : undefined;
      this.clearResolver(resolver.request.id, resolver);
      resolver.reject(error instanceof Error ? error : new Error(String(error)));
      if (ambiguousSocket) {
        this.rejectRequestsFor(ambiguousSocket);
        ambiguousSocket.close(1000);
      }
    }
  }

  private handleMessage(event: MessageEvent): void {
    if (event.data === "pong") {
      this.clearPongTimer();
      return;
    }
    if (event.data === "auth:success" || event.data === "auth:failed" || event.data === "logout:success") {
      const resolver = this.controlRequest;
      if (resolver && String(event.data).startsWith(resolver.request.action + ":")) {
        this.clearResolver(resolver.request.id, resolver);
        if (event.data === "auth:failed") resolver.reject(new ErrorResponse("auth:failed", undefined, resolver.request));
        else resolver.resolve(undefined);
      }
      return;
    }
    try {
      const response = JSON.parse(String(event.data)) as ResponseMessage;
      if (response.id === "@") {
        this.emit("message", response, event);
        return;
      }
      const resolver = this.requests.get(response.id);
      if (!resolver || resolver.control) {
        return;
      }
      this.clearResolver(response.id, resolver);
      if ("error" in response) {
        resolver.reject(new ErrorResponse(response.error ?? "", response.data, resolver.request));
      } else {
        resolver.resolve(response.data);
      }
    } catch (error) {
      this.debug("Invalid message", error);
    }
  }

  private rejectRequestsFor(socket: WebSocket): void {
    for (const [id, resolver] of this.requests) {
      if (resolver.socket === socket) {
        this.clearResolver(id, resolver);
        resolver.reject(new ConnectionClosedError("Request cancelled because the WebSocket connection closed"));
      }
    }
  }

  private rejectAll(error: Error): void {
    for (const [id, resolver] of this.requests) {
      this.clearResolver(id, resolver);
      resolver.reject(error);
    }
  }

  private clearResolver(id: string, resolver: RequestResolver): void {
    if (this.controlRequest === resolver) this.controlRequest = undefined;
    if (resolver.timeout) {
      clearTimeout(resolver.timeout);
    }
    this.requests.delete(id);
  }

  private scheduleReconnect(delay = this.getReconnectDelay()): void {
    if (this.reconnectTimer || !this.options.reconnect || this.disposed || this.closedByClient) {
      return;
    }
    this.reconnectAttempts++;
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = undefined;
      try {
        this.connect();
      } catch (error) {
        // A failing query() or WebSocket constructor must not end reconnecting: try again later.
        this.debug("Reconnect failed", error);
        this.scheduleReconnect();
      }
    }, delay);
  }

  // The current socket is gone: reconnect, or fail waiters at once when nothing would open another socket.
  private lost(): void {
    this.clearPingTimer();
    if (this.closedByClient || this.disposed) return;
    if (this.options.reconnect) this.scheduleReconnect();
    else this.rejectConnectionWaiters(new ConnectionClosedError("WebSocket connection closed and reconnect is disabled"));
  }

  // A missing pong reveals a half-open connection that readyState still reports as open.
  private pongTimedOut(socket: WebSocket): void {
    this.pongTimer = undefined;
    if (socket !== this.socket) return;
    this.debug("Pong timeout");
    this.socket = undefined;
    this.ready = false;
    this.rejectRequestsFor(socket);
    socket.close(1000, "Pong timeout");
    this.lost();
  }

  private getReconnectDelay(): number {
    const ceiling = Math.min(
      this.options.reconnectTimeout * 2 ** Math.min(this.reconnectAttempts, 6),
      30000,
    );
    return ceiling / 2 + Math.random() * ceiling / 2;
  }

  private clearReconnectTimer(): void {
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = undefined;
    }
  }

  private schedulePing(): void {
    this.pingTimer = setTimeout(() => {
      const socket = this.socket;
      if (socket?.readyState === WebSocket.OPEN) {
        socket.send("ping");
        if (this.options.pongTimeout > 0 && this.pongTimer === undefined) {
          this.pongTimer = setTimeout(() => this.pongTimedOut(socket), this.options.pongTimeout);
        }
      }
      if (!this.disposed && this.connected && !this.closedByClient) {
        this.schedulePing();
      }
    }, this.pingInterval);
  }

  private clearPingTimer(): void {
    if (this.pingTimer !== undefined) {
      clearTimeout(this.pingTimer);
      this.pingTimer = undefined;
    }
    this.clearPongTimer();
  }

  private clearPongTimer(): void {
    if (this.pongTimer !== undefined) {
      clearTimeout(this.pongTimer);
      this.pongTimer = undefined;
    }
  }

  private emit<K extends keyof DarkWsEvents>(event: K, ...args: DarkWsEvents[K]): void {
    for (const listener of [...this.listeners[event]]) {
      try {
        listener(...args);
      } catch (error) {
        this.debug(`Listener '${event}' failed`, error);
      }
    }
  }

  private assertNotDisposed(): void {
    if (this.disposed) {
      throw new ConnectionClosedError("DarkWs client was disposed");
    }
  }

  private assertSocketOpen(socket: WebSocket): void {
    this.assertNotDisposed();
    if (this.closedByClient || socket !== this.socket || socket.readyState !== WebSocket.OPEN) {
      throw new ConnectionClosedError("WebSocket connection closed before sending");
    }
  }

  private debug(...args: unknown[]): void {
    if (this.options.debug) {
      console.debug(DarkWs.LOG_PREFIX, ...args);
    }
  }
}
