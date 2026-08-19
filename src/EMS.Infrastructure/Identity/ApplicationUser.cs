using Microsoft.AspNetCore.Identity;

namespace EMS.Infrastructure.Identity;

/// <summary>
/// The Identity user. Credentials, login email, and role membership only.
/// </summary>
/// <remarks>
/// Business data belongs on <see cref="Domain.Entities.Employee"/>, which points back here through
/// its UserId. Identity stays authoritative for anything both types carry; see spec section 3.1.6.
/// </remarks>
public class ApplicationUser : IdentityUser
{
}
