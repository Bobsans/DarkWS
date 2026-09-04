using System.Text.Json;

namespace DarkBoy.DarkWS;

public sealed class DarkWsOptions {
    public JsonSerializerOptions JsonOptions { get; set; } = new(JsonSerializerDefaults.Web);
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan BroadcastSendTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public string AuthenticationQueryParameter { get; set; } = "token";
    public string InvalidActionError { get; set; } = "darkws:error:invalid-action";
    public string InvalidRequestError { get; set; } = "darkws:error:invalid-request";
    public string AuthorizationRequiredError { get; set; } = "darkws:error:authorization-required";
    public string RequestFailedError { get; set; } = "darkws:error:request-failed";
}
