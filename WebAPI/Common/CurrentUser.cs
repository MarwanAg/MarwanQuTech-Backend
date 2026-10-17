using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace MarwanQuTech.WebApi.Common;

/// <summary>Reads the admin id from the "sub" claim of the validated JWT.</summary>
internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public int? AdminId
    {
        get
        {
            var user = accessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                return null;

            var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return int.TryParse(sub, out var id) ? id : null;
        }
    }
}
