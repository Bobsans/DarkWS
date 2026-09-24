using DarkWS.Test.Project;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace DarkWS.Test;

public sealed class RegistrationTests {
    [Test]
    public void RepeatedRegistrationFailsWithoutChangingServicesOrActions() {
        var services = new ServiceCollection();
        services.AddDarkWs().AddHandlersFromAssemblyContaining<TestHandler>();
        var descriptors = services.ToArray();
        var configured = false;

        var error = Assert.Throws<InvalidOperationException>(() => services.AddDarkWs(_ => configured = true));

        Assert.That(error!.Message, Does.Contain("AddDarkWs"));
        Assert.That(configured, Is.False);
        Assert.That(services, Is.EqualTo(descriptors));
        using var provider = services.BuildServiceProvider();
        Assert.That(provider.GetRequiredService<DarkWsActionRegistry>().TryGet("test:get", out _), Is.True);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void InvalidConcurrencyLimitFailsWhenOptionsAreResolved(int limit) {
        var services = new ServiceCollection();
        services.AddDarkWs(options => options.MaxConcurrentRequestsPerConnection = limit);
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => _ = provider.GetRequiredService<IOptions<DarkWsOptions>>().Value);
    }

    [TestCase(nameof(DarkWsOptions.KeepAliveInterval))]
    [TestCase(nameof(DarkWsOptions.KeepAliveTimeout))]
    [TestCase(nameof(DarkWsOptions.ReceiveIdleTimeout))]
    [TestCase(nameof(DarkWsOptions.SendTimeout))]
    [TestCase(nameof(DarkWsOptions.BroadcastSendTimeout))]
    [TestCase(nameof(DarkWsOptions.ShutdownTimeout))]
    [TestCase(nameof(DarkWsOptions.RequestQueueTimeout))]
    public void InvalidTimeoutFailsWhenOptionsAreResolved(string property) {
        foreach (var value in new[] { TimeSpan.Zero, TimeSpan.FromMilliseconds(-1), TimeSpan.MaxValue }) {
            var services = new ServiceCollection();
            services.AddDarkWs(options => typeof(DarkWsOptions).GetProperty(property)!.SetValue(options, value));
            using var provider = services.BuildServiceProvider();
            var error = Assert.Throws<OptionsValidationException>(() => _ = provider.GetRequiredService<IOptions<DarkWsOptions>>().Value);
            Assert.That(error!.Message, Does.Contain(property));
        }
    }

    [TestCase(typeof(TwoParameters), "zero or one")]
    [TestCase(typeof(ValueTaskReturn), "IResponse or Task<IResponse>")]
    [TestCase(typeof(VoidReturn), "IResponse or Task<IResponse>")]
    [TestCase(typeof(GenericMethod), "generic")]
    [TestCase(typeof(RefParameter), "by-reference")]
    [TestCase(typeof(SpanParameter), "byref-like")]
    [TestCase(typeof(StaticMethod), "public instance")]
    [TestCase(typeof(PrivateMethod), "public instance")]
    public void UnsupportedActionFailsWithTypeMethodAndReason(Type type, string reason) {
        var error = Assert.Throws<InvalidOperationException>(() => new DarkWsActionRegistry().Add(type));
        Assert.That(error!.Message, Does.Contain(type.FullName + ".Invalid").And.Contain(reason));
    }

    [TestCase(typeof(BlankHandlerName))]
    [TestCase(typeof(PaddedHandlerName))]
    [TestCase(typeof(NullHandlerName))]
    [TestCase(typeof(BlankActionName))]
    [TestCase(typeof(PaddedActionName))]
    public void BlankOrPaddedNamesFailRegistration(Type type) {
        var error = Assert.Throws<InvalidOperationException>(() => new DarkWsActionRegistry().Add(type));
        Assert.That(error!.Message, Does.Contain(type.FullName).And.Contain("invalid").And.Contain("surrounding whitespace"));
    }

    // Abstract fixtures are excluded from assembly scanning by DarkWsBuilder.
    [Handler("")]
    public abstract class BlankHandlerName : HandlerBase {
        [Action("valid")]
        public IResponse Valid() => Ok();
    }

    [Handler(" padded")]
    public abstract class PaddedHandlerName : HandlerBase {
        [Action("valid")]
        public IResponse Valid() => Ok();
    }

    [Handler(null!)]
    public abstract class NullHandlerName : HandlerBase {
        [Action("valid")]
        public IResponse Valid() => Ok();
    }

    [Handler("names")]
    public abstract class BlankActionName : HandlerBase {
        [Action(" ")]
        public IResponse Invalid() => Ok();
    }

    [Handler("names")]
    public abstract class PaddedActionName : HandlerBase {
        [Action("padded ")]
        public IResponse Invalid() => Ok();
    }

    public abstract class TwoParameters : HandlerBase {
        [Action("invalid")]
        public IResponse Invalid(int first, int second) => Ok();
    }

    public abstract class ValueTaskReturn : HandlerBase {
        [Action("invalid")]
        public ValueTask<IResponse> Invalid() => ValueTask.FromResult(Ok());
    }

    public abstract class VoidReturn : HandlerBase {
        [Action("invalid")]
        public void Invalid() { }
    }

    public abstract class GenericMethod : HandlerBase {
        [Action("invalid")]
        public IResponse Invalid<T>() => Ok();
    }

    public abstract class RefParameter : HandlerBase {
        [Action("invalid")]
        public IResponse Invalid(ref int value) => Ok();
    }

    public abstract class SpanParameter : HandlerBase {
        [Action("invalid")]
        public IResponse Invalid(Span<int> value) => Ok();
    }

    public abstract class StaticMethod : HandlerBase {
        [Action("invalid")]
        public static IResponse Invalid() => Ok();
    }

    public abstract class PrivateMethod : HandlerBase {
        [Action("invalid")]
        private IResponse Invalid() => Ok();
    }
}
