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
  requestTimeout?: number;
  reconnect?: boolean;
  reconnectTimeout?: number;
  pingTimeout?: number;
  waitConnectionTimeout?: number;
  debug?: boolean;
}

export interface DarkWsRequest<TPayload = unknown> {
  id: string;
  action: string;
  payload?: TPayload;
}

interface ResponseMessage {
  id: string;
  data?: unknown;
  error?: string;
}

interface RequestResolver {
  request: DarkWsRequest;
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
  private readonly connectionWaiters = new Set<ConnectionWaiter>();
  private readonly options: Required<Pick<DarkWsOptions,
    "reconnect" | "reconnectTimeout" | "requestTimeout" | "pingTimeout" |
    "waitConnectionTimeout" | "debug">> & DarkWsOptions;
  private socket?: WebSocket;
  private reconnectAttempts = 0;
  private reconnectTimer?: ReturnType<typeof setTimeout>;
  private pingTimer?: ReturnType<typeof setTimeout>;
  private connecting = false;
  private closedByClient = false;
  private disposed = false;

  constructor(options: DarkWsOptions) {
    this.options = {
      reconnect: true,
      reconnectTimeout: 5000,
      requestTimeout: 300000,
      pingTimeout: 30000,
      waitConnectionTimeout: 30000,
      debug: false,
      ...options,
    };
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
      id: crypto.randomUUID(),
      action,
      ...(payload === undefined ? {} : { payload }),
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
    return this.request<void>("darkws:authenticate", token);
  }

  public logout(): Promise<void> {
    return this.request<void>("darkws:logout");
  }

  public close(code = 1000): void {
    this.closedByClient = true;
    this.clearReconnectTimer();
    this.clearPingTimer();
    this.rejectConnectionWaiters(new ConnectionClosedError("WebSocket connection was closed by the client"));
    if (!this.socket || this.socket.readyState === WebSocket.CLOSED) {
      return;
    }
    this.socket.close(code, DarkWs.closeReasons[code] ?? "");
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
      this.socket.close(1000, DarkWs.closeReasons[1000]);
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
    this.clearPingTimer();
    if (previous) {
      this.socket = undefined;
      this.rejectRequestsFor(previous);
      previous.close();
    }

    const socket = new WebSocket(this.buildUrl());
    this.socket = socket;
    socket.addEventListener("open", (event) => {
      if (socket !== this.socket) return;
      this.reconnectAttempts = 0;
      this.clearPingTimer();
      this.schedulePing();
      for (const waiter of this.connectionWaiters) {
        clearTimeout(waiter.timeout);
        waiter.resolve(socket);
      }
      this.connectionWaiters.clear();
      this.emit("open", event);
    });
    socket.addEventListener("error", (event) => this.emit("error", event));
    socket.addEventListener("close", (event) => {
      const isCurrent = socket === this.socket;
      if (isCurrent) this.clearPingTimer();
      if (isCurrent && !this.closedByClient && !this.disposed) {
        this.scheduleReconnect();
      }
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

  private async waitForConnection(): Promise<WebSocket> {
    this.assertNotDisposed();
    if (this.closedByClient) throw new ConnectionClosedError("WebSocket connection was closed by the client");
    if (this.closed) this.connect();
    if (this.socket?.readyState === WebSocket.OPEN) {
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

  private async sendRequest(resolver: RequestResolver, timeoutMs?: number): Promise<void> {
    try {
      const socket = await this.waitForConnection();
      if (!this.requests.has(resolver.request.id)) return;
      this.assertSocketOpen(socket);
      resolver.socket = socket;
      socket.send(JSON.stringify(resolver.request));
      this.emit("send", resolver.request);
      if (!this.requests.has(resolver.request.id)) {
        return;
      }
      const timeout = timeoutMs ?? this.options.requestTimeout;
      if (timeout > 0) {
        resolver.timeout = setTimeout(() => {
          this.requests.delete(resolver.request.id);
          resolver.reject(new RequestTimeoutError("Request cancelled by timeout"));
        }, timeout);
      }
    } catch (error) {
      this.requests.delete(resolver.request.id);
      resolver.reject(error instanceof Error ? error : new Error(String(error)));
    }
  }

  private handleMessage(event: MessageEvent): void {
    if (event.data === "pong") {
      return;
    }
    try {
      const response = JSON.parse(String(event.data)) as ResponseMessage;
      if (response.id === "@") {
        this.emit("message", response.data, event);
        return;
      }
      const resolver = this.requests.get(response.id);
      if (!resolver) {
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
      this.connect();
    }, delay);
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
      if (this.socket?.readyState === WebSocket.OPEN) {
        this.socket.send("ping");
      }
      if (!this.disposed && this.connected && !this.closedByClient) {
        this.schedulePing();
      }
    }, this.options.pingTimeout);
  }

  private clearPingTimer(): void {
    if (this.pingTimer !== undefined) {
      clearTimeout(this.pingTimer);
      this.pingTimer = undefined;
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

  private static readonly closeReasons: Record<number, string> = {
    1000: "Normal closure",
    1001: "Going away",
    1002: "Protocol error",
    1003: "Unsupported data",
    1005: "No status received",
    1006: "Abnormal closure",
    1007: "Invalid frame payload data",
    1008: "Policy violation",
    1009: "Message too big",
    1010: "Mandatory extension",
    1011: "Internal server error",
    1015: "TLS handshake",
  };
}
