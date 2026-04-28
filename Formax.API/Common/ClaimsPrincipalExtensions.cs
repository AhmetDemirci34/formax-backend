using System.Security.Claims;

namespace Formax.API.Common
{
    public static class ClaimsPrincipalExtensions
    {
        public static int? GetUserId(this ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true)
                return null;

            // 1️⃣ Standard NameIdentifier
            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier);

            // 2️⃣ JWT standard: sub
            if (idClaim == null)
                idClaim = user.FindFirst("sub");

            // 3️⃣ Custom id claim
            if (idClaim == null)
                idClaim = user.FindFirst("id");

            if (idClaim == null)
                return null;

            return int.TryParse(idClaim.Value, out var id)
                ? id
                : null;
        }
    }
}