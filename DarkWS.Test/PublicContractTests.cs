using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DarkWS.Test.Project;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace DarkWS.Test;

public sealed class PublicContractTests {
    [Test]
    public void WebSocketEnvelopesUseDataAndFlatBroadcasts() {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper };
        var data = JsonSerializer.SerializeToElement(new { text = "hello" });
        var request = JsonSerializer.SerializeToElement(new InputMessage("1", "message:send", data), options);
        Assert.That(request.EnumerateObject().Select(property => property.Name), Is.EquivalentTo(new[] { "id", "action", "data" }));
        Assert.That(request.GetProperty("data").GetProperty("text").GetString(), Is.EqualTo("hello"));

        var notification = JsonSerializer.SerializeToElement(new BroadcastActionMessage<JsonElement>("message:created", data), options);
        Assert.That(notification.EnumerateObject().Select(property => property.Name), Is.EquivalentTo(new[] { "id", "action", "data" }));
        Assert.That(notification.GetProperty("id").GetString(), Is.EqualTo("@"));
        Assert.That(notification.GetProperty("action").GetString(), Is.EqualTo("message:created"));
        Assert.That(notification.GetProperty("data").GetProperty("text").GetString(), Is.EqualTo("hello"));

        var empty = JsonSerializer.SerializeToElement(new BroadcastActionMessage("changed"), options);
        Assert.That(empty.EnumerateObject().Select(property => property.Name), Is.EquivalentTo(new[] { "id", "action" }));
        Assert.That(empty.GetProperty("id").GetString(), Is.EqualTo("@"));
    }

    [TestCase(DarkWsTarget.All, 0)]
    [TestCase(DarkWsTarget.Connection, 1)]
    [TestCase(DarkWsTarget.Session, 2)]
    [TestCase(DarkWsTarget.Group, 3)]
    public void BroadcastWireValuesAndNamesAreStable(DarkWsTarget target, int code) {
        var json = JsonSerializer.SerializeToElement(new DarkWsBroadcast(target, "id", "event", null), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        Assert.That((int)target, Is.EqualTo(code));
        Assert.That(json.GetProperty("target").GetInt32(), Is.EqualTo(code));
        Assert.That(json.GetProperty("targetId").GetString(), Is.EqualTo("id"));
        Assert.That(json.GetProperty("action").GetString(), Is.EqualTo("event"));
    }

    [Test]
    public async Task DomainExceptionMessageAndCausePreserveWireResponse() {
        var cause = new InvalidOperationException("cause");
        var plain = new ErrorResponseException("domain:plain", cause);
        var typed = new ErrorResponseException<int>("domain:typed", 42, cause);
        Assert.That(plain.Message, Is.EqualTo("domain:plain"));
        Assert.That(typed.Message, Is.EqualTo("domain:typed"));
        Assert.That(plain.InnerException, Is.SameAs(cause));
        Assert.That(typed.InnerException, Is.SameAs(cause));
        var socket = new TestWebSocket();
        using var connection = new WebSocketConnection(socket, new DefaultHttpContext(), null);
        await typed.GetResponse().WriteResultAsync(new ResponseContext(connection, "id", new DarkWsOptions()));
        var response = JsonSerializer.Deserialize<ErrorMessage<int>>(socket.Sent.Single(), Utils.JsonOptions)!;
        Assert.That(response.Error, Is.EqualTo("domain:typed"));
        Assert.That(response.Data, Is.EqualTo(42));
    }

    [TestCase("")]
    [TestCase(" ")]
    public void EmptyErrorCodesAreRejected(string error) {
        Assert.Throws<ArgumentException>(() => new ErrorResponse(error));
        Assert.Throws<ArgumentException>(() => new ErrorResponse<int>(error, 1));
        Assert.Throws<ArgumentException>(() => new ErrorResponseException(error));
        Assert.Throws<ArgumentException>(() => new ErrorResponseException<int>(error, 1));
    }

    [Test]
    public void PublicBoundariesReportTheNullArgument() {
        using var connection = new WebSocketConnection(new TestWebSocket(), new DefaultHttpContext(), null);
        var storage = new ConnectionStorage();
        var options = new DarkWsOptions();
        var cases = new (TestDelegate Call, string Parameter)[] {
            (() => storage.Add(null!), "connection"),
            (() => storage.Remove(null!), "connection"),
            (() => storage.GetByConnection(null!), "connectionId"),
            (() => storage.GetBySession(null!), "sessionId"),
            (() => storage.GetByGroup(null!), "group"),
            (() => new WebSocketConnection(null!, new DefaultHttpContext(), null), "webSocket"),
            (() => new WebSocketConnection(new TestWebSocket(), null!, null), "context"),
            (() => new ResponseContext(null!, "id", options), "connection"),
            (() => new ResponseContext(connection, null!, options), "requestId"),
            (() => new ResponseContext(connection, "id", null!), "options"),
            (() => new ReceivedMessage(null!, []), "result"),
            (() => new ReceivedMessage(new WebSocketReceiveResult(0, WebSocketMessageType.Text, true), null!), "data"),
            (() => new ErrorResponse(null!), "error"),
            (() => new ErrorResponse<int>(null!, 1), "error"),
            (() => new ErrorResponseException(null!), "error"),
            (() => DarkWsServiceCollectionExtensions.AddDarkWs(null!), "services"),
            (() => DarkWsEndpointRouteBuilderExtensions.MapDarkWs(null!), "endpoints"),
            (() => new ServiceCollection().AddDarkWs().AddHandlersFromAssembly(null!), "assembly")
        };
        foreach (var (call, parameter) in cases) Assert.That(Assert.Throws<ArgumentNullException>(call)!.ParamName, Is.EqualTo(parameter));
        Assert.That(Assert.ThrowsAsync<ArgumentNullException>(async () => await connection.SendAsync(null!))!.ParamName, Is.EqualTo("data"));
        foreach (var response in new IResponse[] { new SuccessResponse(), new SuccessResponse<int>(1), new ErrorResponse("error"), new ErrorResponse<int>("error", 1) }) {
            Assert.That(Assert.Throws<ArgumentNullException>(() => response.WriteResultAsync(null!))!.ParamName, Is.EqualTo("context"));
        }
    }

    [TestCase(nameof(DarkWsOptions.AuthenticationQueryParameter))]
    [TestCase(nameof(DarkWsOptions.InvalidActionError))]
    [TestCase(nameof(DarkWsOptions.InvalidRequestError))]
    [TestCase(nameof(DarkWsOptions.AuthorizationRequiredError))]
    [TestCase(nameof(DarkWsOptions.RequestFailedError))]
    [TestCase(nameof(DarkWsOptions.AuthenticationFailedError))]
    public void InvalidStringOptionsFailValidation(string property) {
        foreach (var value in new string?[] { null, "", " " }) {
            var services = new ServiceCollection();
            services.AddDarkWs(options => typeof(DarkWsOptions).GetProperty(property)!.SetValue(options, value));
            using var provider = services.BuildServiceProvider();
            Assert.Throws<OptionsValidationException>(() => _ = provider.GetRequiredService<IOptions<DarkWsOptions>>().Value);
        }
    }

    [Test]
    public void NullJsonOptionsFailValidation() {
        var services = new ServiceCollection();
        services.AddDarkWs(options => options.JsonOptions = null!);
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => _ = provider.GetRequiredService<IOptions<DarkWsOptions>>().Value);
    }

    [Test]
    public void LegacyStaticRegistrationForwardsWithoutExtensionAmbiguity() {
        var wrapper = typeof(DarkWsBuilder).Assembly.GetType("DarkWS.Configuration")!;
        var method = wrapper.GetMethod("AddDarkWs")!;
        Assert.That(wrapper.GetCustomAttribute<ObsoleteAttribute>(), Is.Not.Null);
        Assert.That(method.IsDefined(typeof(ExtensionAttribute)), Is.False);
        var services = new ServiceCollection();
        var builder = method.Invoke(null, [services, null]);
        Assert.That(builder, Is.TypeOf<DarkWsBuilder>());
        using var provider = services.BuildServiceProvider();
        Assert.That(provider.GetRequiredService<IOptions<DarkWsOptions>>().Value.MaxMessageSizeBytes, Is.Positive);
    }

    [Test]
    public void DocumentedDeclaredOnlyActionContractIsPreserved() {
        var registry = new DarkWsActionRegistry();
        registry.Add(typeof(ConcreteHandler));
        Assert.That(registry.TryGet("inheritance-contract:own", out _), Is.True);
        Assert.That(registry.TryGet("inheritance-contract:inherited", out _), Is.False);
    }

    public abstract class BaseHandler : HandlerBase {
        [Action("inherited")]
        public IResponse Inherited() => Ok();
    }

    [Handler("inheritance-contract")]
    public sealed class ConcreteHandler : BaseHandler {
        [Action("own")]
        public IResponse Own() => Ok();
    }
}
