#### [DarkWS\.Client](Overview.md 'Overview')
### [DarkWS\.Client](DarkWS.Client.md 'DarkWS\.Client')

## DarkWsTimeoutStage Enum

The stage that exceeded its timeout\.

```csharp
public enum DarkWsTimeoutStage
```
### Fields

<a id='DarkWS.Client.DarkWsTimeoutStage.Connection'></a>

`Connection` 0

Waiting for a ready connection\.

<a id='DarkWS.Client.DarkWsTimeoutStage.Send'></a>

`Send` 1

Waiting for the send gate or writing to the socket\.

<a id='DarkWS.Client.DarkWsTimeoutStage.Response'></a>

`Response` 2

Waiting for a response after a successful send\.