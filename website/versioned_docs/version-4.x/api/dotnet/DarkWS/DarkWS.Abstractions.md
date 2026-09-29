#### [DarkWS](Overview.md 'Overview')

## DarkWS\.Abstractions Namespace

| Interfaces | |
| :--- | :--- |
| [IBroadcaster](DarkWS.Abstractions.IBroadcaster.md 'DarkWS\.Abstractions\.IBroadcaster') | Publishes notifications through the configured backplane\. Recipients share an immutable envelope with id @\. |
| [IDarkWsAuthenticator](DarkWS.Abstractions.IDarkWsAuthenticator.md 'DarkWS\.Abstractions\.IDarkWsAuthenticator') | Resolves a session for a request or replacement token\. Return null on rejection; expiry remains application policy\. |
| [IDarkWsBackplane](DarkWS.Abstractions.IDarkWsBackplane.md 'DarkWS\.Abstractions\.IDarkWsBackplane') | Transport for broadcast delivery across application instances\. |
| [IDarkWsContextAccessor](DarkWS.Abstractions.IDarkWsContextAccessor.md 'DarkWS\.Abstractions\.IDarkWsContextAccessor') | Current message context\. Uninitialized access throws InvalidOperationException; lifecycle hooks receive context explicitly\. |
| [IDarkWsScopeInitializer](DarkWS.Abstractions.IDarkWsScopeInitializer.md 'DarkWS\.Abstractions\.IDarkWsScopeInitializer') | Initializes scoped application services before each handler invocation\. |
| [IDarkWsSession](DarkWS.Abstractions.IDarkWsSession.md 'DarkWS\.Abstractions\.IDarkWsSession') | Application session identity, principal, and groups\. Membership is snapshotted on add or re\-authentication\. |
| [IWebSocketConnection](DarkWS.Abstractions.IWebSocketConnection.md 'DarkWS\.Abstractions\.IWebSocketConnection') | Public connection contract implementable by consumers and test doubles\. Send buffers must not be mutated\. |
