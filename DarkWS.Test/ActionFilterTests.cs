using System.Collections.Concurrent;
using DarkWS.Abstractions;
using DarkWS.Test.Project;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace DarkWS.Test;

public sealed class ActionFilterTests {
    [Test]
    public async Task FiltersSeeActionScopeAndCanWrapShortCircuitAndCatch() {
        using var host = await Setup.CreateBuilder((services, darkWs) => {
            services.AddSingleton<FilterProbe>();
            darkWs.AddScopeInitializer<MetadataInitializer>()
                .AddActionFilter<OuterFilter>()
                .AddActionFilter<InnerFilter>();
        }).StartAsync();
        var probe = host.Services.GetRequiredService<FilterProbe>();
        using var socket = await host.GetTestServer().CreateWebSocketClient()
            .ConnectAsync(new Uri("ws://localhost/ws?token=first"), CancellationToken.None);

        await socket.SendMessage(new RequestMessage<string>("run", "filters:run", "hello"));
        var run = await socket.ReceiveMessage<ResponseMessage<string>>();
        Assert.Multiple(() => {
            Assert.That(run, Is.EqualTo(new ResponseMessage<string>("run", "hello")));
            Assert.That(probe.Events.ToArray(), Is.EqualTo(new[] { "outer:before", "inner:before", "handler", "inner:after", "outer:after" }));
            Assert.That(probe.Action, Is.SameAs(probe.InitializerAction));
            Assert.That(probe.Action, Is.SameAs(probe.AccessorAction));
            Assert.That(probe.Action?.Name, Is.EqualTo("filters:run"));
            Assert.That(probe.Action?.HandlerType, Is.EqualTo(typeof(FilterHandler)));
            Assert.That(probe.Action?.Method.Name, Is.EqualTo(nameof(FilterHandler.Run)));
            Assert.That(probe.Action?.Attributes.OfType<FilterMarkerAttribute>().Any(), Is.True);
            Assert.That(probe.Payload, Is.EqualTo("hello"));
            Assert.That(probe.SessionId, Is.EqualTo("first"));
            Assert.That(probe.FilterScope, Is.Not.Null);
            Assert.That(probe.HandlerServicesMatch, Is.True);
        });

        probe.Events.Clear();
        await socket.SendMessage(new RequestMessage("block", "filters:block"));
        Assert.That(await socket.ReceiveMessage<ErrorMessage>(), Is.EqualTo(new ErrorMessage("block", "blocked")));
        Assert.That(probe.Events.ToArray(), Is.EqualTo(new[] { "outer:before" }));
        Assert.That(probe.BlockCalls, Is.Zero);

        probe.Events.Clear();
        await socket.SendMessage(new RequestMessage("throw", "filters:throw"));
        Assert.That(await socket.ReceiveMessage<ResponseMessage<string>>(), Is.EqualTo(new ResponseMessage<string>("throw", "caught")));
        Assert.That(probe.Events.ToArray(), Is.EqualTo(new[] { "outer:before", "inner:before", "handler:throw", "outer:catch" }));

        probe.Events.Clear();
        await socket.SendTextAsync("{\"id\":\"invalid\",\"action\":\"filters:run\",\"data\":null}");
        Assert.That(await socket.ReceiveMessage<ErrorMessage>(), Is.EqualTo(new ErrorMessage("invalid", "darkws:error:invalid-request")));
        Assert.That(probe.Events, Is.Empty);

        await socket.SendMessage(new RequestMessage("missing", "missing:action"));
        Assert.That(await socket.ReceiveMessage<ErrorMessage>(), Is.EqualTo(new ErrorMessage("missing", "darkws:error:invalid-action")));
        Assert.That(probe.Events, Is.Empty);

        using var anonymous = await host.GetTestServer().CreateWebSocketClient()
            .ConnectAsync(new Uri("ws://localhost/ws"), CancellationToken.None);
        await anonymous.SendMessage(new RequestMessage("denied", "test:get"));
        Assert.That(await anonymous.ReceiveMessage<ErrorMessage>(), Is.EqualTo(new ErrorMessage("denied", "darkws:error:authorization-required")));
        Assert.That(probe.Events, Is.Empty);
    }

    public sealed class FilterProbe {
        public ConcurrentQueue<string> Events { get; } = new();
        public DarkWsActionInfo? Action;
        public DarkWsActionInfo? InitializerAction;
        public DarkWsActionInfo? AccessorAction;
        public object? Payload;
        public string? SessionId;
        public ScopedProbe? FilterScope;
        public bool HandlerServicesMatch;
        public int BlockCalls;
    }

    public sealed class MetadataInitializer(FilterProbe probe) : IDarkWsScopeInitializer {
        public ValueTask InitializeAsync(IServiceProvider scopedServices, IDarkWsContextAccessor context, CancellationToken cancellationToken) {
            probe.InitializerAction = context.Action;
            return ValueTask.CompletedTask;
        }
    }

    public sealed class OuterFilter(FilterProbe probe, IDarkWsContextAccessor accessor) : IDarkWsActionFilter {
        public async ValueTask<IResponse> InvokeAsync(DarkWsActionContext context, Func<ValueTask<IResponse>> next) {
            probe.Events.Enqueue("outer:before");
            probe.Action = context.Action;
            probe.AccessorAction = accessor.Action;
            probe.Payload = context.Payload;
            probe.SessionId = context.Session?.Id;
            probe.FilterScope = context.Services.GetRequiredService<ScopedProbe>();
            if (context.Action.Name == "filters:block") {
                return new ErrorResponse("blocked");
            }

            try {
                var result = await next();
                probe.Events.Enqueue("outer:after");
                return result;
            } catch (InvalidOperationException) when (context.Action.Name == "filters:throw") {
                probe.Events.Enqueue("outer:catch");
                return new SuccessResponse<string>("caught");
            }
        }
    }

    public sealed class InnerFilter(FilterProbe probe) : IDarkWsActionFilter {
        public async ValueTask<IResponse> InvokeAsync(DarkWsActionContext context, Func<ValueTask<IResponse>> next) {
            probe.Events.Enqueue("inner:before");
            var result = await next();
            probe.Events.Enqueue("inner:after");
            return result;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class FilterMarkerAttribute : Attribute { }

    [Handler("filters"), AllowAnonymous]
    public sealed class FilterHandler(FilterProbe probe, ScopedProbe scoped) : HandlerBase {
        [Action("run"), FilterMarker]
        public IResponse Run(string value) {
            probe.Events.Enqueue("handler");
            probe.HandlerServicesMatch = ReferenceEquals(scoped, Services.GetRequiredService<ScopedProbe>())
                && ReferenceEquals(scoped, probe.FilterScope);
            return new SuccessResponse<string>(value);
        }

        [Action("block")]
        public IResponse Block() {
            Interlocked.Increment(ref probe.BlockCalls);
            return new SuccessResponse();
        }

        [Action("throw")]
        public IResponse Throw() {
            probe.Events.Enqueue("handler:throw");
            throw new InvalidOperationException("filter catches this");
        }
    }
}
