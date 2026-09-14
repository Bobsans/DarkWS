using Microsoft.AspNetCore.Authorization;
using NUnit.Framework;

namespace DarkWS.Test;

public sealed class AuthorizationContractTests {
    [TestCase(typeof(AuthorizedHandler))]
    [TestCase(typeof(InheritedAuthorization))]
    [TestCase(typeof(AuthorizedAction))]
    [TestCase(typeof(RoleAction))]
    [TestCase(typeof(AnonymousAuthorizedAction))]
    [TestCase(typeof(CustomAuthorization))]
    public void UnsupportedAuthorizationFailsWithHandlerAndDiagnostic(Type handler) {
        var error = Assert.Throws<InvalidOperationException>(() => new DarkWsActionRegistry().Add(handler));
        Assert.That(error!.Message, Does.Contain(handler.FullName).And.Contain("[Authorize]").And.Contain("[AllowAnonymous]"));
    }

    // Abstract fixtures are excluded from regular assembly-wide registration.
    [Authorize(Policy = "restricted")]
    public abstract class AuthorizedHandler : HandlerBase { }

    public abstract class InheritedAuthorization : AuthorizedHandler { }

    public abstract class AuthorizedAction : HandlerBase {
        [Action("restricted"), Authorize]
        public IResponse Restricted() => Ok();
    }

    public abstract class RoleAction : HandlerBase {
        [Action("restricted"), Authorize(Roles = "admin")]
        public IResponse Restricted() => Ok();
    }

    public abstract class AnonymousAuthorizedAction : HandlerBase {
        [Action("restricted"), AllowAnonymous, Authorize(Policy = "restricted")]
        public IResponse Restricted() => Ok();
    }

    [CustomAuthorize]
    public abstract class CustomAuthorization : HandlerBase { }

    private sealed class CustomAuthorizeAttribute : Attribute, IAuthorizeData {
        public string? Policy { get; set; }
        public string? Roles { get; set; }
        public string? AuthenticationSchemes { get; set; }
    }
}
