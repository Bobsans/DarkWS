#### [DarkWS](Overview.md 'Overview')

## DarkWS Namespace

| Classes | |
| :--- | :--- |
| [ActionAttribute](DarkWS.ActionAttribute.md 'DarkWS\.ActionAttribute') | Exposes a declared public instance method returning IResponse or Task of IResponse with zero or one payload parameter\. |
| [AspNetDarkWsSession](DarkWS.AspNetDarkWsSession.md 'DarkWS\.AspNetDarkWsSession') | Default session for an authenticated ASP\.NET principal, without broadcast groups\. |
| [BroadcastActionMessage](DarkWS.BroadcastActionMessage.md 'DarkWS\.BroadcastActionMessage') | Notification envelope containing the reserved id and application action\. |
| [BroadcastActionMessage&lt;T&gt;](DarkWS.BroadcastActionMessage_T_.md 'DarkWS\.BroadcastActionMessage\<T\>') | Notification envelope containing the reserved id, application action, and data\. |
| [Configuration](DarkWS.Configuration.md 'DarkWS\.Configuration') | Compatibility entry points; use the dedicated service and endpoint extension classes\. |
| [ConnectionStorage](DarkWS.ConnectionStorage.md 'DarkWS\.ConnectionStorage') | Thread\-safe local registry with session and group indexes\. Re\-add a connection to refresh externally changed membership\. |
| [DarkWsBroadcast](DarkWS.DarkWsBroadcast.md 'DarkWS\.DarkWsBroadcast') | Backplane message carrying the recipient selector, action name, and optional JSON data\. |
| [DarkWsBuilder](DarkWS.DarkWsBuilder.md 'DarkWS\.DarkWsBuilder') | Composes handlers, authentication, and message\-scope services after a single AddDarkWs call\. |
| [DarkWsEndpointRouteBuilderExtensions](DarkWS.DarkWsEndpointRouteBuilderExtensions.md 'DarkWS\.DarkWsEndpointRouteBuilderExtensions') | Maps DarkWS endpoints in the ASP\.NET Core request pipeline\. |
| [DarkWsException](DarkWS.DarkWsException.md 'DarkWS\.DarkWsException') | Base for controlled handler errors that supply their own protocol response\. |
| [DarkWsMiddleware](DarkWS.DarkWsMiddleware.md 'DarkWS\.DarkWsMiddleware') | Connection lifecycle hooks\. Use the supplied context; an injected message context is uninitialized in this scope\. |
| [DarkWsOptions](DarkWS.DarkWsOptions.md 'DarkWS\.DarkWsOptions') | Validated startup settings for serialization, limits, and wire errors; supports the standard \.NET Options pipeline\. |
| [DarkWsProtocol](DarkWS.DarkWsProtocol.md 'DarkWS\.DarkWsProtocol') | Reserved identifiers in the DarkWS wire protocol\. |
| [DarkWsServiceCollectionExtensions](DarkWS.DarkWsServiceCollectionExtensions.md 'DarkWS\.DarkWsServiceCollectionExtensions') | Registers DarkWS services and validated options\. |
| [ErrorMessage](DarkWS.ErrorMessage.md 'DarkWS\.ErrorMessage') | Error response carrying a stable code and optional typed details\. |
| [ErrorMessage&lt;T&gt;](DarkWS.ErrorMessage_T_.md 'DarkWS\.ErrorMessage\<T\>') | Error response carrying a stable code and optional typed details\. |
| [ErrorResponse](DarkWS.ErrorResponse.md 'DarkWS\.ErrorResponse') | Writes an error code and optional typed details to the client\. |
| [ErrorResponse&lt;T&gt;](DarkWS.ErrorResponse_T_.md 'DarkWS\.ErrorResponse\<T\>') | Writes an error code and optional typed details to the client\. |
| [ErrorResponseException](DarkWS.ErrorResponseException.md 'DarkWS\.ErrorResponseException') | Controlled handler exception translated to an error code and optional typed details\. |
| [ErrorResponseException&lt;T&gt;](DarkWS.ErrorResponseException_T_.md 'DarkWS\.ErrorResponseException\<T\>') | Controlled handler exception translated to an error code and optional typed details\. |
| [HandlerAttribute](DarkWS.HandlerAttribute.md 'DarkWS\.HandlerAttribute') | Defines the wire action prefix for a handler class\. |
| [HandlerBase](DarkWS.HandlerBase.md 'DarkWS\.HandlerBase') | Base for per\-message handlers\. Context\-dependent members are available only during action invocation\. |
| [HandlerBase&lt;TSession&gt;](DarkWS.HandlerBase_TSession_.md 'DarkWS\.HandlerBase\<TSession\>') | Base for per\-message handlers\. Context\-dependent members are available only during action invocation\. |
| [InputMessage](DarkWS.InputMessage.md 'DarkWS\.InputMessage') | Incoming request envelope\. Id and Action are required; payload nullability follows the action signature\. |
| [OkMessage](DarkWS.OkMessage.md 'DarkWS\.OkMessage') | Successful response without data, correlated by request id\. |
| [ReceivedMessage](DarkWS.ReceivedMessage.md 'DarkWS\.ReceivedMessage') | Complete received message, or a close result with an empty data buffer\. |
| [ResponseContext](DarkWS.ResponseContext.md 'DarkWS\.ResponseContext') | Serializes an action response and sends it to the requesting connection\. |
| [ResponseMessage&lt;T&gt;](DarkWS.ResponseMessage_T_.md 'DarkWS\.ResponseMessage\<T\>') | Successful response carrying correlated result data\. |
| [SuccessResponse](DarkWS.SuccessResponse.md 'DarkWS\.SuccessResponse') | Writes a successful response with optional typed result data\. |
| [SuccessResponse&lt;T&gt;](DarkWS.SuccessResponse_T_.md 'DarkWS\.SuccessResponse\<T\>') | Writes a successful response with optional typed result data\. |
| [WebSocketConnection](DarkWS.WebSocketConnection.md 'DarkWS\.WebSocketConnection') | Owns a WebSocket with serialized, bounded writes and coordinated disposal\. Send buffers are immutable\. |

| Interfaces | |
| :--- | :--- |
| [IResponse](DarkWS.IResponse.md 'DarkWS\.IResponse') | Writes one action result using a correlation context and cancellation token\. |

| Enums | |
| :--- | :--- |
| [DarkWsTarget](DarkWS.DarkWsTarget.md 'DarkWS\.DarkWsTarget') | Selects a recipient set for a backplane broadcast\. |
