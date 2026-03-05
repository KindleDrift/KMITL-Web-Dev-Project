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
                        CreatedAt = DateTime.UtcNow,
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

                logger.LogInformation("Seeding regular users...");
                var regularUserSeeds = new[]
                {
                    new { DisplayName = "Alice", Email = "alice@example.com" },
                    new { DisplayName = "Bob", Email = "bob@example.com" },
                    new { DisplayName = "Charlie", Email = "charlie@example.com" }
                };

                var regularUsersByEmail = new Dictionary<string, Users>(StringComparer.OrdinalIgnoreCase);

                foreach (var userSeed in regularUserSeeds)
                {
                    var user = await userManager.FindByEmailAsync(userSeed.Email);
                    if (user == null)
                    {
                        user = new Users
                        {
                            DisplayName = userSeed.DisplayName,
                            NormalizedDisplayName = userSeed.DisplayName.ToUpper(),
                            UserName = userSeed.Email,
                            NormalizedUserName = userSeed.Email.ToUpper(),
                            Email = userSeed.Email,
                            NormalizedEmail = userSeed.Email.ToUpper(),
                            EmailConfirmed = true,
                            SecurityStamp = Guid.NewGuid().ToString(),
                            CreatedAt = DateTime.UtcNow,
                        };

                        var createResult = await userManager.CreateAsync(user, "User123!");
                        if (!createResult.Succeeded)
                        {
                            logger.LogError("Failed to create user {Email}: {Errors}", userSeed.Email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                            continue;
                        }
                    }

                    if (!await userManager.IsInRoleAsync(user, "User"))
                    {
                        await userManager.AddToRoleAsync(user, "User");
                    }

                    regularUsersByEmail[userSeed.Email] = user;
                }

                logger.LogInformation("Seeding sample board...");
                const string seededBoardTitle = "Saturday Football Match";
                var existingBoard = await context.Boards
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Title == seededBoardTitle);

                if (existingBoard == null
                    && regularUsersByEmail.TryGetValue("alice@example.com", out var alice)
                    && regularUsersByEmail.TryGetValue("bob@example.com", out var bob))
                {
                    var board = new Board
                    {
                        Title = seededBoardTitle,
                        Category = "Sports",
                        Description = "Friendly 7-a-side football session for all skill levels.",
                        AuthorId = alice.Id,
                        MaxParticipants = 14,
                        Location = "KMITL Main Field",
                        EventDate = DateTime.UtcNow.Date.AddDays(7).AddHours(17),
                        Deadline = DateTime.UtcNow.Date.AddDays(5).AddHours(23),
                        CurrentStatus = BoardStatus.Open,
                        NotifyAuthorOnFull = true,
                        CloseOnFull = false,
                        IncreaseMaxParticipantsOnFull = false,
                        ManualIncreaseMaxParticipants = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    context.Boards.Add(board);
                    await context.SaveChangesAsync();

                    context.BoardParticipants.Add(new BoardParticipant
                    {
                        BoardId = board.Id,
                        UserId = bob.Id,
                        JoinedAt = DateTime.UtcNow
                    });

                    await context.SaveChangesAsync();
                }

                const string seededBoardTitle2 = "Sunday Study Group";
                var existingBoard2 = await context.Boards
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Title == seededBoardTitle2);

                if (existingBoard2 == null
                    && regularUsersByEmail.TryGetValue("alice@example.com", out var alice2))
                {
                    var board2 = new Board
                    {
                        Title = seededBoardTitle2,
                        Category = "Education",
                        Description = "Group study session for upcoming exams.",
                        AuthorId = alice2.Id,
                        MaxParticipants = 8,
                        Location = "Engineering Library Room 2",
                        EventDate = DateTime.UtcNow.Date.AddDays(10).AddHours(14),
                        Deadline = DateTime.UtcNow.Date.AddDays(9).AddHours(20),
                        CurrentStatus = BoardStatus.Open,
                        NotifyAuthorOnFull = true,
                        CloseOnFull = false,
                        IncreaseMaxParticipantsOnFull = false,
                        ManualIncreaseMaxParticipants = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    context.Boards.Add(board2);
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the database.");
            }
        }
    }
}