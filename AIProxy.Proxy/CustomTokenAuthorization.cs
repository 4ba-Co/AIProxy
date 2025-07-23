using Microsoft.AspNetCore.Authorization;

namespace AIProxy.Proxy;

public sealed class CustomToken : IAuthorizationRequirement
{
}

public sealed class CustomTokenHandler : AuthorizationHandler<CustomToken>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, CustomToken requirement)
    {
        throw new NotImplementedException();
    }
}