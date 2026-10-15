using Microsoft.AspNetCore.Identity;

namespace HRMS.Infrastructure.Identity;

public sealed class HRMSUser : IdentityUser
{
    public bool IsActive { get; set; } = true;
}
