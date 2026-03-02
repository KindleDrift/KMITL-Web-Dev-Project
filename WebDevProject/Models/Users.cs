using Microsoft.AspNetCore.Identity;

namespace WebDevProject.Models
{
    public class Users : IdentityUser
    {
        public required string DisplayName { get; set; }
    }
}
