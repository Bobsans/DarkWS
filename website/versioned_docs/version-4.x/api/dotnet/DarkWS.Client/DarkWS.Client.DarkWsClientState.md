#### [DarkWS\.Client](Overview.md 'Overview')
### [DarkWS\.Client](DarkWS.Client.md 'DarkWS\.Client')

## DarkWsClientState Enum

Readiness of a logical client connection\.

```csharp
public enum DarkWsClientState
```
### Fields

<a id='DarkWS.Client.DarkWsClientState.Disconnected'></a>

`Disconnected` 0

No usable connection\.

<a id='DarkWS.Client.DarkWsClientState.Connecting'></a>

`Connecting` 1

Opening the first connection of a cycle\.

<a id='DarkWS.Client.DarkWsClientState.Connected'></a>

`Connected` 2

Socket and optional automatic authentication are ready\.

<a id='DarkWS.Client.DarkWsClientState.Reconnecting'></a>

`Reconnecting` 3

Recovering a transport connection\.

<a id='DarkWS.Client.DarkWsClientState.Disposed'></a>

`Disposed` 4

The client has been permanently released\.