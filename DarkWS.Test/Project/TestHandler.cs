using DarkWS.Abstractions;

namespace DarkWS.Test.Project;

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

// Hooks of parallel connections run on server threads, so the counters are fields updated with Interlocked.
public sealed class LifecycleProbe {
    public int OpenCount;
    public int AuthenticationCount;
    public int CloseCount;
    public int ScopeInitializationCount;
    public volatile string? PreviousSessionId;
}

public sealed class TestMiddleware(LifecycleProbe probe) : DarkWsMiddleware {
    public override Task OnOpenAsync(IDarkWsContextAccessor context) {
        Interlocked.Increment(ref probe.OpenCount);
        return Task.CompletedTask;
    }

    public override Task OnAuthenticatedAsync(
        IDarkWsContextAccessor context,
        IDarkWsSession? previousSession
    ) {
        Interlocked.Increment(ref probe.AuthenticationCount);
        probe.PreviousSessionId = previousSession?.Id;
        return Task.CompletedTask;
    }

    public override Task OnCloseAsync(IDarkWsContextAccessor context) {
        Interlocked.Increment(ref probe.CloseCount);
        return Task.CompletedTask;
    }
}

public sealed class TestScopeInitializer(LifecycleProbe probe) : IDarkWsScopeInitializer {
    public ValueTask InitializeAsync(
        IServiceProvider scopedServices,
        IDarkWsContextAccessor context,
        CancellationToken cancellationToken
    ) {
        Interlocked.Increment(ref probe.ScopeInitializationCount);
        return ValueTask.CompletedTask;
    }
}
