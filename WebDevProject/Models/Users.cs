using Microsoft.AspNetCore.Identity;

namespace WebDevProject.Models
{
    public class Users : IdentityUser
    {
        public string? DisplayName { get; set; }
    }
}
