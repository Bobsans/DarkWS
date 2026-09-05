# DarkWS.Redis

Redis backplane for `DarkWS`.

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddDarkWsRedis("my-app:production");
```

Register `DarkWS` before this package. Use a unique channel per
application and environment.
