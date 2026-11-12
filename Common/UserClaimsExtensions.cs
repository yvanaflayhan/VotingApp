using System.Security.Claims;

namespace Voting_app.Common
{
    public static class UserClaimsExtensions//to know who is logged in 
    {
        public static string? GetUserId(this ClaimsPrincipal user) =>
            user.FindFirstValue(ClaimTypes.NameIdentifier);

        public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);
    }
}
