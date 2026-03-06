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
                    new { DisplayName = "Charlie", Email = "charlie@example.com" },
                    new { DisplayName = "David", Email = "david@example.com" },
                    new { DisplayName = "Eve", Email = "eve@example.com" },
                    new { DisplayName = "Frank", Email = "frank@example.com" },
                    new { DisplayName = "Grace", Email = "grace@example.com" },
                    new { DisplayName = "Heidi", Email = "heidi@example.com" },
                    new { DisplayName = "Ivan", Email = "ivan@example.com" },
                    new { DisplayName = "Judy", Email = "judy@example.com" },
                    new { DisplayName = "Karl", Email = "karl@example.com" },
                    new { DisplayName = "Leo", Email = "leo@example.com" },
                    new { DisplayName = "Mallory", Email = "mallory@example.com" },
                    new { DisplayName = "Nina", Email = "nina@example.com" },
                    new { DisplayName = "Oscar", Email = "oscar@example.com" },
                    new { DisplayName = "Peggy", Email = "peggy@example.com" },
                    new { DisplayName = "Quentin", Email = "quentin@example.com" },
                    new { DisplayName = "Rupert", Email = "rupert@example.com" },
                    new { DisplayName = "Sybil", Email = "sybil@example.com" },
                    new { DisplayName = "Trent", Email = "trent@example.com" },
                    new { DisplayName = "Uma", Email = "uma@example.com" },
                    new { DisplayName = "Victor", Email = "victor@example.com" },
                    new { DisplayName = "Wendy", Email = "wendy@example.com" },
                    new { DisplayName = "Xavier", Email = "xavier@example.com" },
                    new { DisplayName = "Yvonne", Email = "yvonne@example.com" },
                    new { DisplayName = "Zara", Email = "zara@example.com" }
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
                
                logger.LogInformation("Seeding tags...");
                var tagNames = new[] { "Sports", "Study", "Music", "Food", "Travel", "Outside" };
                foreach (var tagName in tagNames) {
                    if (!await context.Tags.AnyAsync(t => t.Name == tagName))
                    {
                        context.Tags.Add(new Tag { Name = tagName });
                    }
                }

                await context.SaveChangesAsync();

                var sportsTag = await context.Tags.FirstOrDefaultAsync(t => t.Name == "Sports");
                var studyTag = await context.Tags.FirstOrDefaultAsync(t => t.Name == "Study");
                var outsideTag = await context.Tags.FirstOrDefaultAsync(t => t.Name == "Outside");

                logger.LogInformation("Seeding sample board...");
                const string seededBoardTitle = "Saturday Football Match";
                var existingBoard = await context.Boards
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Title == seededBoardTitle);

                if (existingBoard == null
                    && regularUsersByEmail.TryGetValue("alice@example.com", out var alice)
                    && regularUsersByEmail.TryGetValue("bob@example.com", out var bob)
                    && regularUsersByEmail.TryGetValue("eve@example.com", out var eve)
                    && sportsTag != null)
                {
                    var board = new Board
                    {
                        Title = seededBoardTitle,
                        Tags = new List<Tag> { sportsTag, outsideTag },
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

                    context.BoardParticipants.Add(new BoardParticipant
                    {
                        BoardId = board.Id,
                        UserId = eve.Id,
                        JoinedAt = DateTime.UtcNow
                    });

                    await context.SaveChangesAsync();
                }

                const string seededBoardTitle2 = "Sunday Study Group";
                var existingBoard2 = await context.Boards
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Title == seededBoardTitle2);

                if (existingBoard2 == null
                    && regularUsersByEmail.TryGetValue("alice@example.com", out var alice2)
                    && studyTag != null)
                {
                    var board2 = new Board
                    {
                        Title = seededBoardTitle2,
                        Tags = new List<Tag> { studyTag },
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