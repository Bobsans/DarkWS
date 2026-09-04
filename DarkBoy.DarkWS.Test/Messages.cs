namespace DarkBoy.DarkWS.Test;

public sealed record RequestMessage(string Id, string Action);
public sealed record RequestMessage<T>(string Id, string Action, T? Payload = default);
public sealed record ResponseMessageNoData(string Id);
