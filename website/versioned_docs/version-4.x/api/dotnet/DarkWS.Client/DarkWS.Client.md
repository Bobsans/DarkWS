#### [DarkWS\.Client](Overview.md 'Overview')

## DarkWS\.Client Namespace

| Classes | |
| :--- | :--- |
| [DarkWsClient](DarkWS.Client.DarkWsClient.md 'DarkWS\.Client\.DarkWsClient') | A concurrent, lazy DarkWS client\. One instance owns one server session\. |
| [DarkWsClientErrorEventArgs](DarkWS.Client.DarkWsClientErrorEventArgs.md 'DarkWS\.Client\.DarkWsClientErrorEventArgs') | A background or subscriber error\. |
| [DarkWsClientLimitException](DarkWS.Client.DarkWsClientLimitException.md 'DarkWS\.Client\.DarkWsClientLimitException') | A bounded local request or notification queue is full\. |
| [DarkWsClientOptions](DarkWS.Client.DarkWsClientOptions.md 'DarkWS\.Client\.DarkWsClientOptions') | Settings captured when a client is constructed\. |
| [DarkWsConnectionException](DarkWS.Client.DarkWsConnectionException.md 'DarkWS\.Client\.DarkWsConnectionException') | A socket or connection lifecycle failure\. |
| [DarkWsProtocolException](DarkWS.Client.DarkWsProtocolException.md 'DarkWS\.Client\.DarkWsProtocolException') | A malformed or unsupported incoming message\. |
| [DarkWsResponseException](DarkWS.Client.DarkWsResponseException.md 'DarkWS\.Client\.DarkWsResponseException') | A controlled server error\. Payloads are excluded from the exception message\. |
| [DarkWsStateChangedEventArgs](DarkWS.Client.DarkWsStateChangedEventArgs.md 'DarkWS\.Client\.DarkWsStateChangedEventArgs') | An ordered connection state transition\. |
| [DarkWsTimeoutException](DarkWS.Client.DarkWsTimeoutException.md 'DarkWS\.Client\.DarkWsTimeoutException') | A bounded client operation expired\. |

| Interfaces | |
| :--- | :--- |
| [IDarkWsClient](DarkWS.Client.IDarkWsClient.md 'DarkWS\.Client\.IDarkWsClient') | One logical connection and server session\. Safe for concurrent callers\. |

| Enums | |
| :--- | :--- |
| [DarkWsClientState](DarkWS.Client.DarkWsClientState.md 'DarkWS\.Client\.DarkWsClientState') | Readiness of a logical client connection\. |
| [DarkWsTimeoutStage](DarkWS.Client.DarkWsTimeoutStage.md 'DarkWS\.Client\.DarkWsTimeoutStage') | The stage that exceeded its timeout\. |
