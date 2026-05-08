using Microsoft.AspNetCore.Identity;

namespace Geogrid.Domain.Entities;

public class AppUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
