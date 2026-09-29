#### [DarkWS\.Client](Overview.md 'Overview')
### [DarkWS\.Client](DarkWS.Client.md 'DarkWS\.Client')

## DarkWsStateChangedEventArgs Class

An ordered connection state transition\.

```csharp
public sealed class DarkWsStateChangedEventArgs : System.EventArgs
```

Inheritance [System\.Object](https://learn.microsoft.com/en-us/dotnet/api/system.object 'System\.Object') → [System\.EventArgs](https://learn.microsoft.com/en-us/dotnet/api/system.eventargs 'System\.EventArgs') → DarkWsStateChangedEventArgs
### Constructors

<a id='DarkWS.Client.DarkWsStateChangedEventArgs.DarkWsStateChangedEventArgs(DarkWS.Client.DarkWsClientState,DarkWS.Client.DarkWsClientState,System.Exception)'></a>

## DarkWsStateChangedEventArgs\(DarkWsClientState, DarkWsClientState, Exception\) Constructor

Creates a state transition\.

```csharp
public DarkWsStateChangedEventArgs(DarkWS.Client.DarkWsClientState previousState, DarkWS.Client.DarkWsClientState state, System.Exception? reason=null);
```
#### Parameters

<a id='DarkWS.Client.DarkWsStateChangedEventArgs.DarkWsStateChangedEventArgs(DarkWS.Client.DarkWsClientState,DarkWS.Client.DarkWsClientState,System.Exception).previousState'></a>

`previousState` [DarkWsClientState](DarkWS.Client.DarkWsClientState.md 'DarkWS\.Client\.DarkWsClientState')

<a id='DarkWS.Client.DarkWsStateChangedEventArgs.DarkWsStateChangedEventArgs(DarkWS.Client.DarkWsClientState,DarkWS.Client.DarkWsClientState,System.Exception).state'></a>

`state` [DarkWsClientState](DarkWS.Client.DarkWsClientState.md 'DarkWS\.Client\.DarkWsClientState')

<a id='DarkWS.Client.DarkWsStateChangedEventArgs.DarkWsStateChangedEventArgs(DarkWS.Client.DarkWsClientState,DarkWS.Client.DarkWsClientState,System.Exception).reason'></a>

`reason` [System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception')
### Properties

<a id='DarkWS.Client.DarkWsStateChangedEventArgs.PreviousState'></a>

## DarkWsStateChangedEventArgs\.PreviousState Property

State before the transition\.

```csharp
public DarkWS.Client.DarkWsClientState PreviousState { get; }
```

#### Property Value
[DarkWsClientState](DarkWS.Client.DarkWsClientState.md 'DarkWS\.Client\.DarkWsClientState')

<a id='DarkWS.Client.DarkWsStateChangedEventArgs.Reason'></a>

## DarkWsStateChangedEventArgs\.Reason Property

Optional transport or protocol failure\.

```csharp
public System.Exception? Reason { get; }
```

#### Property Value
[System\.Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception 'System\.Exception')

<a id='DarkWS.Client.DarkWsStateChangedEventArgs.State'></a>

## DarkWsStateChangedEventArgs\.State Property

State after the transition\.

```csharp
public DarkWS.Client.DarkWsClientState State { get; }
```

#### Property Value
[DarkWsClientState](DarkWS.Client.DarkWsClientState.md 'DarkWS\.Client\.DarkWsClientState')