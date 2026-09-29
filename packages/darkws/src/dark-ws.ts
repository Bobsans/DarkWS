export interface DarkWsEvents {
  open: [Event];
  /** Automatic authentication failed on an open socket, before the anonymous-ready open event. */
  sessionRestoreFailed: [Error, Event];
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
  /** Maximum pending request/authenticate/logout calls, including waits and retries. Default 256; positive safe integer. */
  maxPendingRequests?: number;
  /** Default options per action; explicit request options override them field by field. Not used by authenticate/logout. */
  requestOptions?: (action: string) => DarkWsRequestOptions | undefined;
  /** Reply timeout for authentication and logout, including session restore. Default 30000 ms; 0 disables it. */
  controlTimeout?: number;
  reconnect?: boolean;
  reconnectTimeout?: number;
  /** Retry a pending automatic reconnect immediately when the document becomes visible. Default false. */
  reconnectOnVisible?: boolean;
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

export interface DarkWsRetryOptions {
  /** Additional attempts after ConnectionClosedError. Default 0; use only for idempotent actions. */
  connectionClosed?: number;
  /** Additional attempts after RequestTimeoutError. Default 0; use only for idempotent actions. */
  timeout?: number;
  /** Maximum random delay before each retry, in milliseconds. Default 0. */
  jitter?: number;
}

export interface DarkWsRequestOptions {
  /** Reply timeout per attempt, in milliseconds. Defaults to requestTimeout; 0 disables it. */
  timeout?: number;
  retry?: DarkWsRetryOptions;
}

interface ResponseMessage {
  id: string;
  action?: string;
  data?: unknown;
  error?: string;
}

interface RequestResolver {
  request: DarkWsRequest;
  sent: boolean;
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
  /** sent means at least one attempt was accepted by WebSocket.send, not that the server acknowledged it. */
  constructor(message: string, public readonly sent = false) {
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

  /** Creates a facade that builds its client on first use and connects it on the first request or subscription. */
  static lazy(options: DarkWsOptions | (() => DarkWsOptions)): LazyDarkWs {
    return new LazyDarkWs(options);
  }

  private readonly listeners: {
    [K in keyof DarkWsEvents]: ((...args: DarkWsEvents[K]) => void)[]
  } = {
    open: [],
    sessionRestoreFailed: [],
    close: [],
    error: [],
    message: [],
    send: [],
  };

  private readonly requests = new Map<string, RequestResolver>();
  private controlTail: Promise<void> = Promise.resolve();
  private controlRequest?: RequestResolver;
  private readonly connectionWaiters = new Set<ConnectionWaiter>();
  private readonly retryTimers = new Map<ReturnType<typeof setTimeout>, (message: string) => void>();
  private closeGeneration = 0;
  private pendingOperations = 0;
  private readonly options: Required<Pick<DarkWsOptions,
    "reconnect" | "reconnectTimeout" | "requestTimeout" | "controlTimeout" | "pongTimeout" |
    "waitConnectionTimeout" | "maxPendingRequests" | "debug">> & DarkWsOptions;
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
  private readonly visibilityDocument?: Document;
  private readonly onVisibilityChange = (): void => {
    if (this.visibilityDocument?.visibilityState === "visible" && this.reconnectTimer !== undefined) {
      this.clearReconnectTimer();
      this.tryConnect();
    }
  };

  constructor(options: DarkWsOptions) {
    this.options = {
      reconnect: true,
      reconnectTimeout: 5000,
      requestTimeout: 300000,
      pongTimeout: 30000,
      waitConnectionTimeout: 30000,
      debug: false,
      ...options,
      controlTimeout: options.controlTimeout ?? 30000,
      maxPendingRequests: options.maxPendingRequests ?? 256,
    };
    if (!Number.isSafeInteger(this.options.maxPendingRequests) || this.options.maxPendingRequests <= 0) {
      throw new RangeError("maxPendingRequests must be a positive safe integer");
    }
    this.pingInterval = options.pingInterval ?? options.pingTimeout ?? 30000;
    if (options.reconnectOnVisible && typeof document !== "undefined") {
      this.visibilityDocument = document;
      this.visibilityDocument.addEventListener("visibilitychange", this.onVisibilityChange);
    }
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
    return this.requests.size + this.retryTimers.size;
  }

  /** Whether the native event belongs to the currently assigned socket, regardless of its ready state. */
  public isCurrentSocket(event: Event): boolean {
    return this.socket !== undefined && event.target === this.socket;
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

  /** Subscribes to one exact broadcast action and returns an unsubscribe function. Omitted data is undefined. */
  public onAction<TData = unknown>(action: string, callback: (data: TData) => void): () => void {
    return this.on("message", message => {
      const response = message as ResponseMessage;
      if (response.action === action) callback(response.data as TData);
    });
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
    options?: number | DarkWsRequestOptions,
  ): Promise<TResult> {
    this.assertNotDisposed();
    return this.withRequestSlot(() => this.requestWithRetry<TResult, TPayload>(action, payload, typeof options === "number" ? { timeout: options } : options ?? {}));
  }

  private async withRequestSlot<TResult>(operation: () => Promise<TResult>): Promise<TResult> {
    if (this.pendingOperations >= this.options.maxPendingRequests) {
      throw new RangeError(`Pending request limit of ${this.options.maxPendingRequests} reached`);
    }
    this.pendingOperations++;
    try {
      return await operation();
    } finally {
      this.pendingOperations--;
    }
  }

  private async requestWithRetry<TResult, TPayload>(action: string, payload: TPayload | undefined, options: DarkWsRequestOptions): Promise<TResult> {
    const defaults = this.options.requestOptions?.(action) ?? {};
    let connectionRetries = options.retry?.connectionClosed ?? defaults.retry?.connectionClosed ?? 0;
    let timeoutRetries = options.retry?.timeout ?? defaults.retry?.timeout ?? 0;
    const jitter = options.retry?.jitter ?? defaults.retry?.jitter ?? 0;
    const timeout = options.timeout ?? defaults.timeout;
    if (!Number.isSafeInteger(connectionRetries) || connectionRetries < 0 ||
        !Number.isSafeInteger(timeoutRetries) || timeoutRetries < 0 ||
        !Number.isFinite(jitter) || jitter < 0 || jitter > 2147483647) {
      throw new RangeError("Retry counts must be non-negative safe integers; jitter must be between 0 and 2147483647 ms");
    }

    const generation = this.closeGeneration;
    let sent = false;
    for (;;) {
      try {
        return await this.requestOnce<TResult, TPayload>(action, payload, timeout);
      } catch (error) {
        if (error instanceof ConnectionClosedError) sent ||= error.sent;
        if (error instanceof RequestTimeoutError) sent = true;
        const canRetry = !this.closedByClient && !this.disposed && generation === this.closeGeneration;
        if (canRetry && error instanceof ConnectionClosedError && connectionRetries > 0) connectionRetries--;
        else if (canRetry && error instanceof RequestTimeoutError && timeoutRetries > 0) timeoutRetries--;
        else throw error instanceof ConnectionClosedError ? new ConnectionClosedError(error.message, sent) : error;

        await new Promise<void>((resolve, reject) => {
          const timer = setTimeout(() => {
            this.retryTimers.delete(timer);
            resolve();
          }, Math.random() * jitter);
          this.retryTimers.set(timer, message => reject(new ConnectionClosedError(message, sent)));
        });
        // An explicit close stays terminal for this request even if the client was reopened meanwhile.
        if (generation !== this.closeGeneration) throw new ConnectionClosedError("Request retry cancelled because the client was closed", sent);
      }
    }
  }

  private requestOnce<TResult, TPayload>(action: string, payload: TPayload | undefined, timeoutMs?: number): Promise<TResult> {
    const request: DarkWsRequest<TPayload> = {
      id: createId(),
      action,
      ...(payload === undefined ? {} : { data: payload }),
    };

    return new Promise<TResult>((resolve, reject) => {
      const resolver: RequestResolver = {
        request,
        sent: false,
        resolve: (value) => resolve(value as TResult),
        reject,
      };
      this.requests.set(request.id, resolver);
      void this.sendRequest(resolver, timeoutMs);
    });
  }

  public authenticate(token: string): Promise<void> {
    if (token === "") return Promise.reject(new TypeError("Token must not be empty; use logout() to sign out"));
    this.assertNotDisposed();
    return this.withRequestSlot(() => this.systemRequest("auth", "auth:" + token));
  }

  public logout(): Promise<void> {
    this.assertNotDisposed();
    return this.withRequestSlot(() => this.systemRequest("logout", "logout"));
  }

  private systemRequest(action: string, text: string, socket?: WebSocket): Promise<void> {
    this.assertNotDisposed();
    // Session restore on a new socket runs before queued commands, so it cannot join their chain.
    const previous = socket ? Promise.resolve() : this.controlTail;
    const result = new Promise<void>((resolve, reject) => {
      const resolver: RequestResolver = {
        request: { id: createId(), action },
        sent: false,
        control: true,
        resolve: () => resolve(),
        reject,
      };
      this.requests.set(resolver.request.id, resolver);
      void this.sendRequest(resolver, this.options.controlTimeout, { text, previous, socket });
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
    this.closeGeneration++;
    this.cancelRetries("WebSocket connection was closed by the client");
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
    this.closeGeneration++;
    this.cancelRetries("DarkWs client was disposed");
    this.visibilityDocument?.removeEventListener("visibilitychange", this.onVisibilityChange);
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
      const failure = error instanceof Error ? error : new Error(String(error));
      this.rejectConnectionWaiters(failure);
      this.emit("sessionRestoreFailed", failure, event);
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
      resolver.sent = true;
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
      resolver.reject(error instanceof ConnectionClosedError
        ? new ConnectionClosedError(error.message, resolver.sent || error.sent)
        : error instanceof Error ? error : new Error(String(error)));
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
        resolver.reject(new ConnectionClosedError("Request cancelled because the WebSocket connection closed", resolver.sent));
      }
    }
  }

  private rejectAll(error: ConnectionClosedError): void {
    for (const [id, resolver] of this.requests) {
      this.clearResolver(id, resolver);
      resolver.reject(new ConnectionClosedError(error.message, resolver.sent));
    }
  }

  private cancelRetries(message: string): void {
    for (const [timer, reject] of this.retryTimers) {
      clearTimeout(timer);
      reject(message);
    }
    this.retryTimers.clear();
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
      this.tryConnect();
    }, delay);
  }

  private tryConnect(): void {
    try {
      this.connect();
    } catch (error) {
      // A failing query() or WebSocket constructor must not end reconnecting: try again later.
      this.debug("Reconnect failed", error);
      this.scheduleReconnect();
    }
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

interface LazySubscription {
  // Set only for on(); onAction() subscriptions cannot be removed by off().
  event?: keyof DarkWsEvents;
  callback?: unknown;
  attach: (client: DarkWs) => () => void;
  detach?: () => void;
}

/** A client created on first use; subscriptions are kept by the facade and survive reset(). */
export class LazyDarkWs {
  private client?: DarkWs;
  private started = false;
  private readonly subscriptions: LazySubscription[] = [];

  constructor(private readonly options: DarkWsOptions | (() => DarkWsOptions)) {}

  /** The current client, created (without connecting) if needed. Close it through the facade. */
  public get instance(): DarkWs {
    if (!this.client) {
      const client = new DarkWs(typeof this.options === "function" ? this.options() : this.options);
      for (const subscription of this.subscriptions) subscription.detach = subscription.attach(client);
      this.client = client;
    }
    return this.client;
  }

  public async request<TResult, TPayload = unknown>(
    action: string,
    payload?: TPayload,
    options?: number | DarkWsRequestOptions,
  ): Promise<TResult> {
    return this.connected().request<TResult, TPayload>(action, payload, options);
  }

  public async send<T>(data: T, jsonify = true): Promise<void> {
    return this.connected().send(data, jsonify);
  }

  public async authenticate(token: string): Promise<void> {
    return this.connected().authenticate(token);
  }

  public async logout(): Promise<void> {
    return this.connected().logout();
  }

  public on<K extends keyof DarkWsEvents>(
    event: K,
    callback: (...args: DarkWsEvents[K]) => void,
  ): () => void {
    return this.subscribe({ event, callback, attach: client => client.on(event, callback) });
  }

  public off<K extends keyof DarkWsEvents>(
    event: K,
    callback: (...args: DarkWsEvents[K]) => void,
  ): void {
    const subscription = this.subscriptions.find(item => item.event === event && item.callback === callback);
    if (subscription) this.unsubscribe(subscription);
  }

  public onAction<TData = unknown>(action: string, callback: (data: TData) => void): () => void {
    return this.subscribe({ attach: client => client.onAction(action, callback) });
  }

  public reconnect(): void {
    this.instance.reconnect();
    this.started = true;
  }

  /** Closes the client if it exists; the next request or subscription connects again. */
  public close(code = 1000): void {
    this.client?.close(code);
    this.started = false;
  }

  /**
   * Disposes the client, rejecting its pending requests, and re-reads the options on next use.
   * Subscriptions move to the new client, which connects at once if the old one was started;
   * `full` also drops them and leaves the facade idle.
   */
  public reset(full = false): void {
    const restart = this.started && !full;
    this.client?.dispose();
    this.client = undefined;
    this.started = false;
    if (full) this.subscriptions.length = 0;
    if (restart) this.connected();
  }

  private connected(): DarkWs {
    const client = this.instance;
    if (!this.started) {
      client.connect();
      this.started = true;
    }
    return client;
  }

  private subscribe(subscription: LazySubscription): () => void {
    const client = this.instance;
    this.subscriptions.push(subscription);
    subscription.detach = subscription.attach(client);
    this.connected();
    return () => this.unsubscribe(subscription);
  }

  private unsubscribe(subscription: LazySubscription): void {
    const index = this.subscriptions.indexOf(subscription);
    if (index < 0) return;
    this.subscriptions.splice(index, 1);
    subscription.detach?.();
  }
}
