#### [DarkWS](Overview.md 'Overview')
### [DarkWS](DarkWS.md 'DarkWS')

## DarkWsOptions Class

Validated startup settings for serialization, limits, and wire errors; supports the standard \.NET Options pipeline\.

```csharp
public sealed class DarkWsOptions
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → DarkWsOptions
### Fields

<a id='DarkWS.DarkWsOptions.DefaultMaxMessageSizeBytes'></a>

## DarkWsOptions\.DefaultMaxMessageSizeBytes Field

Default complete incoming message limit: 1 MiB \(1048576 bytes\)\.

```csharp
public const int DefaultMaxMessageSizeBytes = 1048576;
```

#### Field Value
[System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')
### Properties

<a id='DarkWS.DarkWsOptions.AuthenticationFailedError'></a>

## DarkWsOptions\.AuthenticationFailedError Property

Legacy JSON authentication error code\. Text authentication always replies auth:failed\.

```csharp
public string AuthenticationFailedError { get; set; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.DarkWsOptions.AuthenticationQueryParameter'></a>

## DarkWsOptions\.AuthenticationQueryParameter Property

Connection token query parameter\. Default token; must not be blank\.

```csharp
public string AuthenticationQueryParameter { get; set; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.DarkWsOptions.AuthorizationRequiredError'></a>

## DarkWsOptions\.AuthorizationRequiredError Property

Authentication\-required error code\. Default darkws:error:authorization\-required\.

```csharp
public string AuthorizationRequiredError { get; set; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.DarkWsOptions.BroadcastSendTimeout'></a>

## DarkWsOptions\.BroadcastSendTimeout Property

Per\-recipient broadcast deadline\. Default 10 seconds; expiration aborts that recipient\.

```csharp
public System.TimeSpan BroadcastSendTimeout { get; set; }
```

#### Property Value
[System\.TimeSpan](https://learn.microsoft.com/en-us/dotnet/api/system.timespan 'System\.TimeSpan')

<a id='DarkWS.DarkWsOptions.InvalidActionError'></a>

## DarkWsOptions\.InvalidActionError Property

Unknown action error code\. Default darkws:error:invalid\-action\.

```csharp
public string InvalidActionError { get; set; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.DarkWsOptions.InvalidRequestError'></a>

## DarkWsOptions\.InvalidRequestError Property

Invalid envelope or payload code\. Default darkws:error:invalid\-request\.

```csharp
public string InvalidRequestError { get; set; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.DarkWsOptions.JsonOptions'></a>

## DarkWsOptions\.JsonOptions Property

JSON settings for envelopes and payloads\. Defaults to web conventions; must not be null\.

```csharp
public System.Text.Json.JsonSerializerOptions JsonOptions { get; set; }
```

#### Property Value
[System\.Text\.Json\.JsonSerializerOptions](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializeroptions 'System\.Text\.Json\.JsonSerializerOptions')

<a id='DarkWS.DarkWsOptions.KeepAliveInterval'></a>

## DarkWsOptions\.KeepAliveInterval Property

Transport keep\-alive interval\. Default 30 seconds; must be a positive timer duration\.

```csharp
public System.TimeSpan KeepAliveInterval { get; set; }
```

#### Property Value
[System\.TimeSpan](https://learn.microsoft.com/en-us/dotnet/api/system.timespan 'System\.TimeSpan')

<a id='DarkWS.DarkWsOptions.KeepAliveTimeout'></a>

## DarkWsOptions\.KeepAliveTimeout Property

Transport PONG deadline on \.NET 9 and later\. Default 30 seconds; unused on \.NET 8\.

```csharp
public System.TimeSpan KeepAliveTimeout { get; set; }
```

#### Property Value
[System\.TimeSpan](https://learn.microsoft.com/en-us/dotnet/api/system.timespan 'System\.TimeSpan')

<a id='DarkWS.DarkWsOptions.MaxConcurrentRequestsPerConnection'></a>

## DarkWsOptions\.MaxConcurrentRequestsPerConnection Property

Maximum in\-flight requests per connection\. Default 16; saturation applies read backpressure\.

```csharp
public int MaxConcurrentRequestsPerConnection { get; set; }
```

#### Property Value
[System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

<a id='DarkWS.DarkWsOptions.MaxMessageSizeBytes'></a>

## DarkWsOptions\.MaxMessageSizeBytes Property

Complete incoming message limit including fragments\. Default 1 MiB; must be positive\.

```csharp
public int MaxMessageSizeBytes { get; set; }
```

#### Property Value
[System\.Int32](https://learn.microsoft.com/en-us/dotnet/api/system.int32 'System\.Int32')

<a id='DarkWS.DarkWsOptions.ReceiveIdleTimeout'></a>

## DarkWsOptions\.ReceiveIdleTimeout Property

Pending\-read deadline on \.NET 8\. Default 2 minutes; data fragments reset it\. Idle clients must send application traffic\.

```csharp
public System.TimeSpan ReceiveIdleTimeout { get; set; }
```

#### Property Value
[System\.TimeSpan](https://learn.microsoft.com/en-us/dotnet/api/system.timespan 'System\.TimeSpan')

<a id='DarkWS.DarkWsOptions.RequestFailedError'></a>

## DarkWsOptions\.RequestFailedError Property

Unexpected handler failure code\. Default darkws:error:request\-failed\.

```csharp
public string RequestFailedError { get; set; }
```

#### Property Value
[System\.String](https://learn.microsoft.com/en-us/dotnet/api/system.string 'System\.String')

<a id='DarkWS.DarkWsOptions.SendTimeout'></a>

## DarkWsOptions\.SendTimeout Property

Combined lock wait and socket write deadline\. Default 30 seconds; expiration aborts the connection\.

```csharp
public System.TimeSpan SendTimeout { get; set; }
```

#### Property Value
[System\.TimeSpan](https://learn.microsoft.com/en-us/dotnet/api/system.timespan 'System\.TimeSpan')

<a id='DarkWS.DarkWsOptions.ShutdownTimeout'></a>

## DarkWsOptions\.ShutdownTimeout Property

Shared deadline for cancellation, close hooks, and handshake\. Default 10 seconds; uncooperative tasks retain resources until completion\.

```csharp
public System.TimeSpan ShutdownTimeout { get; set; }
```

#### Property Value
[System\.TimeSpan](https://learn.microsoft.com/en-us/dotnet/api/system.timespan 'System\.TimeSpan')