using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebDevProject.Models;

namespace WebDevProject.Data
{
    public class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Users>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DbSeeder>>();

            var roles = new[] { "Admin", "User" };

            try
            {
                logger.LogInformation("Starting database seeding...");
                await context.Database.EnsureCreatedAsync();

                logger.LogInformation("Seeding roles...");
                foreach (var role in roles)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                    {
                        await roleManager.CreateAsync(new IdentityRole(role));
                    }
                }

                logger.LogInformation("Seeding admin user...");
                var adminDisplayName = "Admin";
                var adminEmail = "admin@tinytender.local";
                var adminUser = await userManager.FindByEmailAsync(adminEmail);
                if (adminUser == null)
                {
                    adminUser = new Users
                    {
                        DisplayName = adminDisplayName,
                        NormalizedDisplayName = adminDisplayName.ToUpper(),
                        UserName = adminEmail,
                        NormalizedUserName = adminEmail.ToUpper(),
                        Email = adminEmail,
                        NormalizedEmail = adminEmail.ToUpper(),
                        EmailConfirmed = true,
                        SecurityStamp = Guid.NewGuid().ToString(),
                        CreatedAt = DateTime.Now,
                    };
                    var result = await userManager.CreateAsync(adminUser, "Admin123!");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(adminUser, "Admin");
                    }
                    else
                    {
                        logger.LogError("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the database.");
            }
        }
    }
}