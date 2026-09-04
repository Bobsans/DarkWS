using DarkBoy.DarkWS.Abstractions;

namespace DarkBoy.DarkWS.Test.Project;

[Handler("test")]
public sealed class TestHandler(
    TestSession session,
    ScopedProbe scopedProbe,
    IDarkWsContextAccessor context
) : HandlerBase<TestSession> {
    [Action("get")]
    public IResponse Get() => Ok("simple get result");

    [Action("session")]
    public IResponse GetSession() {
        return Ok(new SessionResult(session.Id, Session.Id, context.Session?.Id, AspNetSession is not null));
    }

    [Action("scope")]
    public IResponse GetScope() => Ok(scopedProbe.Id);

    [Action("fail")]
    public IResponse Fail() => throw new InvalidOperationException("secret failure text");

    [Action("controlled-error")]
    public IResponse ControlledError() => throw new ErrorResponseException<int>("rejected", 42);

    [Action("broadcast")]
    public async Task<IResponse> BroadcastAsync(string action) {
        await BroadcastToSelfAsync(action);
        return Ok();
    }

    public sealed record SessionResult(string Injected, string Handler, string? Accessor, bool HasAspNetSession);
}

[Handler("public"), Microsoft.AspNetCore.Authorization.AllowAnonymous]
public sealed class PublicHandler : HandlerBase {
    [Action("get")]
    public IResponse Get() => Ok("public result");
}

public sealed class ScopedProbe {
    public Guid Id { get; } = Guid.NewGuid();
}

public sealed class LifecycleProbe {
    public int OpenCount { get; set; }
    public int AuthenticationCount { get; set; }
    public int CloseCount { get; set; }
    public int ScopeInitializationCount { get; set; }
    public string? PreviousSessionId { get; set; }
}

public sealed class TestMiddleware(LifecycleProbe probe) : DarkWsMiddleware {
    public override Task OnOpenAsync(IDarkWsContextAccessor context) {
        probe.OpenCount++;
        return Task.CompletedTask;
    }

    public override Task OnAuthenticatedAsync(
        IDarkWsContextAccessor context,
        IDarkWsSession? previousSession
    ) {
        probe.AuthenticationCount++;
        probe.PreviousSessionId = previousSession?.Id;
        return Task.CompletedTask;
    }

    public override Task OnCloseAsync(IDarkWsContextAccessor context) {
        probe.CloseCount++;
        return Task.CompletedTask;
    }
}

public sealed class TestScopeInitializer(LifecycleProbe probe) : IDarkWsScopeInitializer {
    public ValueTask InitializeAsync(
        IServiceProvider scopedServices,
        IDarkWsContextAccessor context,
        CancellationToken cancellationToken
    ) {
        probe.ScopeInitializationCount++;
        return ValueTask.CompletedTask;
    }
}
