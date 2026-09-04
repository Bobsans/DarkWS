# DarkBoy.DarkWS.Redis

Redis backplane for `DarkBoy.DarkWS`.

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddDarkWsRedis("my-app:production");
```

Register `DarkBoy.DarkWS` before this package. Use a unique channel per
application and environment.
