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
                
                if (await context.Users.AnyAsync() || await context.Boards.AnyAsync())
                {
                    logger.LogInformation("Database already contains data. Skipping seed.");
                    return;
                }

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
                var musicTag = await context.Tags.FirstOrDefaultAsync(t => t.Name == "Music");
                var foodTag = await context.Tags.FirstOrDefaultAsync(t => t.Name == "Food");
                var travelTag = await context.Tags.FirstOrDefaultAsync(t => t.Name == "Travel");
                var outsideTag = await context.Tags.FirstOrDefaultAsync(t => t.Name == "Outside");

                logger.LogInformation("Seeding sample boards...");
                
                async Task<bool> BoardExists(string title) => 
                    await context.Boards.AsNoTracking().AnyAsync(b => b.Title == title);
                
                if (!await BoardExists("Saturday Football Match")
                    && regularUsersByEmail.TryGetValue("alice@example.com", out var alice)
                    && regularUsersByEmail.TryGetValue("bob@example.com", out var bob)
                    && regularUsersByEmail.TryGetValue("eve@example.com", out var eve)
                    && sportsTag != null && outsideTag != null)
                {
                    var board = new Board
                    {
                        Title = "Saturday Football Match",
                        Tags = new List<Tag> { sportsTag, outsideTag },
                        Description = "Friendly 7-a-side football session for all skill levels.",
                        AuthorId = alice.Id,
                        MaxParticipants = 14,
                        Location = "KMITL Main Field",
                        EventDate = DateTime.UtcNow.Date.AddDays(7).AddHours(17),
                        Deadline = DateTime.UtcNow.Date.AddDays(5).AddHours(23),
                        CurrentStatus = BoardStatus.Open,
                        NotifyAuthorOnFull = true,
                        GroupManagementOption = GroupManagement.CloseOnFull,
                        JoinPolicy = BoardJoinPolicy.Application,
                        CreatedAt = DateTime.UtcNow.AddDays(-2)
                    };
                    context.Boards.Add(board);
                    await context.SaveChangesAsync();

                    context.BoardParticipants.AddRange(
                        new BoardParticipant { BoardId = board.Id, UserId = bob.Id, JoinedAt = DateTime.UtcNow.AddDays(-1) },
                        new BoardParticipant { BoardId = board.Id, UserId = eve.Id, JoinedAt = DateTime.UtcNow.AddHours(-12) }
                    );
                    await context.SaveChangesAsync();
                }
                
                if (!await BoardExists("Sunday Study Group")
                    && regularUsersByEmail.TryGetValue("charlie@example.com", out var charlie)
                    && regularUsersByEmail.TryGetValue("david@example.com", out var david)
                    && regularUsersByEmail.TryGetValue("frank@example.com", out var frank)
                    && studyTag != null)
                {
                    var board = new Board
                    {
                        Title = "Sunday Study Group",
                        Tags = new List<Tag> { studyTag },
                        Description = "Group study session for upcoming exams.",
                        AuthorId = charlie.Id,
                        MaxParticipants = 8,
                        Location = "Engineering Library Room 2",
                        EventDate = DateTime.UtcNow.Date.AddDays(10).AddHours(14),
                        Deadline = DateTime.UtcNow.Date.AddDays(9).AddHours(20),
                        CurrentStatus = BoardStatus.Open,
                        NotifyAuthorOnFull = true,
                        GroupManagementOption = GroupManagement.KeepOpenWhenFull,
                        JoinPolicy = BoardJoinPolicy.Application,
                        CreatedAt = DateTime.UtcNow.AddDays(-3)
                    };
                    context.Boards.Add(board);
                    await context.SaveChangesAsync();

                    context.BoardParticipants.AddRange(
                        new BoardParticipant { BoardId = board.Id, UserId = david.Id, JoinedAt = DateTime.UtcNow.AddDays(-2) },
                        new BoardParticipant { BoardId = board.Id, UserId = frank.Id, JoinedAt = DateTime.UtcNow.AddDays(-1) }
                    );
                    await context.SaveChangesAsync();
                }
                
                if (!await BoardExists("Live Jazz Night")
                    && regularUsersByEmail.TryGetValue("grace@example.com", out var grace)
                    && regularUsersByEmail.TryGetValue("heidi@example.com", out var heidi)
                    && regularUsersByEmail.TryGetValue("ivan@example.com", out var ivan)
                    && regularUsersByEmail.TryGetValue("judy@example.com", out var judy)
                    && regularUsersByEmail.TryGetValue("karl@example.com", out var karl)
                    && musicTag != null)
                {
                    var board = new Board
                    {
                        Title = "Live Jazz Night",
                        Tags = new List<Tag> { musicTag },
                        Description = "Evening jazz performance at campus cafe. Limited seating!",
                        AuthorId = grace.Id,
                        MaxParticipants = 5,
                        Location = "Campus Cafe",
                        EventDate = DateTime.UtcNow.Date.AddDays(3).AddHours(19),
                        Deadline = DateTime.UtcNow.Date.AddDays(2).AddHours(18),
                        CurrentStatus = BoardStatus.Full,
                        NotifyAuthorOnFull = true,
                        GroupManagementOption = GroupManagement.CloseOnFull,
                        JoinPolicy = BoardJoinPolicy.FirstComeFirstServe,
                        CreatedAt = DateTime.UtcNow.AddDays(-5)
                    };
                    context.Boards.Add(board);
                    await context.SaveChangesAsync();

                    context.BoardParticipants.AddRange(
                        new BoardParticipant { BoardId = board.Id, UserId = heidi.Id, JoinedAt = DateTime.UtcNow.AddDays(-4) },
                        new BoardParticipant { BoardId = board.Id, UserId = ivan.Id, JoinedAt = DateTime.UtcNow.AddDays(-4) },
                        new BoardParticipant { BoardId = board.Id, UserId = judy.Id, JoinedAt = DateTime.UtcNow.AddDays(-3) },
                        new BoardParticipant { BoardId = board.Id, UserId = karl.Id, JoinedAt = DateTime.UtcNow.AddDays(-2) }
                    );
                    await context.SaveChangesAsync();

                    context.BoardExternalParticipants.Add(
                        new BoardExternalParticipant { BoardId = board.Id, Name = "Guest Walk-in", Note = "Door ticket", AddedAt = DateTime.UtcNow.AddDays(-2) }
                    );
                    await context.SaveChangesAsync();
                }
                
                if (!await BoardExists("Weekend Hiking Trip")
                    && regularUsersByEmail.TryGetValue("leo@example.com", out var leo)
                    && regularUsersByEmail.TryGetValue("mallory@example.com", out var mallory)
                    && travelTag != null && outsideTag != null)
                {
                    var board = new Board
                    {
                        Title = "Weekend Hiking Trip",
                        Tags = new List<Tag> { travelTag, outsideTag },
                        Description = "Two-day hiking trip to nearby national park. Registration closed.",
                        AuthorId = leo.Id,
                        MaxParticipants = 12,
                        Location = "National Park Trailhead",
                        EventDate = DateTime.UtcNow.Date.AddDays(6).AddHours(8),
                        Deadline = DateTime.UtcNow.Date.AddDays(4).AddHours(23),
                        CurrentStatus = BoardStatus.Closed,
                        NotifyAuthorOnFull = false,
                        GroupManagementOption = GroupManagement.CloseOnFull,
                        JoinPolicy = BoardJoinPolicy.Application,
                        CreatedAt = DateTime.UtcNow.AddDays(-10)
                    };
                    context.Boards.Add(board);
                    await context.SaveChangesAsync();

                    context.BoardParticipants.Add(
                        new BoardParticipant { BoardId = board.Id, UserId = mallory.Id, JoinedAt = DateTime.UtcNow.AddDays(-8) }
                    );
                    await context.SaveChangesAsync();
                }
                
                if (!await BoardExists("Thursday Food Tour")
                    && regularUsersByEmail.TryGetValue("nina@example.com", out var nina)
                    && foodTag != null)
                {
                    var board = new Board
                    {
                        Title = "Thursday Food Tour",
                        Tags = new List<Tag> { foodTag },
                        Description = "Exploring local street food vendors around campus area.",
                        AuthorId = nina.Id,
                        MaxParticipants = 10,
                        Location = "Campus Main Gate",
                        EventDate = DateTime.UtcNow.Date.AddDays(4).AddHours(18),
                        Deadline = DateTime.UtcNow.Date.AddDays(3).AddHours(12),
                        CurrentStatus = BoardStatus.Open,
                        NotifyAuthorOnFull = true,
                        GroupManagementOption = GroupManagement.AllowOverbooking,
                        JoinPolicy = BoardJoinPolicy.Application,
                        CreatedAt = DateTime.UtcNow.AddHours(-6)
                    };
                    context.Boards.Add(board);
                    await context.SaveChangesAsync();
                }
                
                if (!await BoardExists("Monday Basketball Practice")
                    && regularUsersByEmail.TryGetValue("oscar@example.com", out var oscar)
                    && regularUsersByEmail.TryGetValue("peggy@example.com", out var peggy)
                    && regularUsersByEmail.TryGetValue("quentin@example.com", out var quentin)
                    && sportsTag != null && outsideTag != null)
                {
                    var board = new Board
                    {
                        Title = "Monday Basketball Practice",
                        Tags = new List<Tag> { sportsTag, outsideTag },
                        Description = "Weekly basketball practice session. All levels welcome!",
                        AuthorId = oscar.Id,
                        MaxParticipants = 10,
                        Location = "Sports Complex Court 1",
                        EventDate = DateTime.UtcNow.Date.AddDays(8).AddHours(16),
                        Deadline = DateTime.UtcNow.Date.AddDays(-1).AddHours(23),
                        CurrentStatus = BoardStatus.Open,
                        NotifyAuthorOnFull = false,
                        GroupManagementOption = GroupManagement.KeepOpenWhenFull,
                        JoinPolicy = BoardJoinPolicy.FirstComeFirstServe,
                        CreatedAt = DateTime.UtcNow.AddDays(-15)
                    };
                    context.Boards.Add(board);
                    await context.SaveChangesAsync();

                    context.BoardParticipants.AddRange(
                        new BoardParticipant { BoardId = board.Id, UserId = peggy.Id, JoinedAt = DateTime.UtcNow.AddDays(-10) },
                        new BoardParticipant { BoardId = board.Id, UserId = quentin.Id, JoinedAt = DateTime.UtcNow.AddDays(-5) }
                    );
                    await context.SaveChangesAsync();
                }
                
                if (!await BoardExists("Last Week Movie Night")
                    && regularUsersByEmail.TryGetValue("rupert@example.com", out var rupert)
                    && regularUsersByEmail.TryGetValue("sybil@example.com", out var sybil)
                    && regularUsersByEmail.TryGetValue("trent@example.com", out var trent)
                    && regularUsersByEmail.TryGetValue("uma@example.com", out var uma))
                {
                    var board = new Board
                    {
                        Title = "Last Week Movie Night",
                        Tags = new List<Tag>(),
                        Description = "Movie screening at student center (past event).",
                        AuthorId = rupert.Id,
                        MaxParticipants = 20,
                        Location = "Student Center Auditorium",
                        EventDate = DateTime.UtcNow.Date.AddDays(-3).AddHours(19),
                        Deadline = DateTime.UtcNow.Date.AddDays(-5).AddHours(23),
                        CurrentStatus = BoardStatus.Closed,
                        NotifyAuthorOnFull = false,
                        GroupManagementOption = GroupManagement.CloseOnFull,
                        JoinPolicy = BoardJoinPolicy.Application,
                        CreatedAt = DateTime.UtcNow.AddDays(-20)
                    };
                    context.Boards.Add(board);
                    await context.SaveChangesAsync();

                    context.BoardParticipants.AddRange(
                        new BoardParticipant { BoardId = board.Id, UserId = sybil.Id, JoinedAt = DateTime.UtcNow.AddDays(-15) },
                        new BoardParticipant { BoardId = board.Id, UserId = trent.Id, JoinedAt = DateTime.UtcNow.AddDays(-12) },
                        new BoardParticipant { BoardId = board.Id, UserId = uma.Id, JoinedAt = DateTime.UtcNow.AddDays(-10) }
                    );
                    await context.SaveChangesAsync();
                }
                
                if (!await BoardExists("Friday Night Campus Party")
                    && regularUsersByEmail.TryGetValue("victor@example.com", out var victor)
                    && regularUsersByEmail.TryGetValue("wendy@example.com", out var wendy)
                    && regularUsersByEmail.TryGetValue("xavier@example.com", out var xavier)
                    && regularUsersByEmail.TryGetValue("yvonne@example.com", out var yvonne)
                    && regularUsersByEmail.TryGetValue("zara@example.com", out var zara)
                    && regularUsersByEmail.TryGetValue("alice@example.com", out var alice2)
                    && regularUsersByEmail.TryGetValue("bob@example.com", out var bob2)
                    && regularUsersByEmail.TryGetValue("charlie@example.com", out var charlie2)
                    && regularUsersByEmail.TryGetValue("david@example.com", out var david2)
                    && regularUsersByEmail.TryGetValue("eve@example.com", out var eve2)
                    && musicTag != null && foodTag != null)
                {
                    var board = new Board
                    {
                        Title = "Friday Night Campus Party",
                        Tags = new List<Tag> { musicTag, foodTag },
                        Description = "End of semester celebration with music, food, and games!",
                        AuthorId = victor.Id,
                        MaxParticipants = 50,
                        Location = "Student Union Hall",
                        EventDate = DateTime.UtcNow.Date.AddDays(14).AddHours(20),
                        Deadline = DateTime.UtcNow.Date.AddDays(12).AddHours(23),
                        CurrentStatus = BoardStatus.Open,
                        NotifyAuthorOnFull = true,
                        GroupManagementOption = GroupManagement.AllowOverbooking,
                        JoinPolicy = BoardJoinPolicy.FirstComeFirstServe,
                        CreatedAt = DateTime.UtcNow.AddDays(-1)
                    };
                    context.Boards.Add(board);
                    await context.SaveChangesAsync();

                    context.BoardParticipants.AddRange(
                        new BoardParticipant { BoardId = board.Id, UserId = wendy.Id, JoinedAt = DateTime.UtcNow.AddHours(-20) },
                        new BoardParticipant { BoardId = board.Id, UserId = xavier.Id, JoinedAt = DateTime.UtcNow.AddHours(-18) },
                        new BoardParticipant { BoardId = board.Id, UserId = yvonne.Id, JoinedAt = DateTime.UtcNow.AddHours(-15) },
                        new BoardParticipant { BoardId = board.Id, UserId = zara.Id, JoinedAt = DateTime.UtcNow.AddHours(-12) },
                        new BoardParticipant { BoardId = board.Id, UserId = alice2.Id, JoinedAt = DateTime.UtcNow.AddHours(-10) },
                        new BoardParticipant { BoardId = board.Id, UserId = bob2.Id, JoinedAt = DateTime.UtcNow.AddHours(-8) },
                        new BoardParticipant { BoardId = board.Id, UserId = charlie2.Id, JoinedAt = DateTime.UtcNow.AddHours(-6) },
                        new BoardParticipant { BoardId = board.Id, UserId = david2.Id, JoinedAt = DateTime.UtcNow.AddHours(-4) },
                        new BoardParticipant { BoardId = board.Id, UserId = eve2.Id, JoinedAt = DateTime.UtcNow.AddHours(-2) }
                    );
                    await context.SaveChangesAsync();

                    context.BoardExternalParticipants.AddRange(
                        new BoardExternalParticipant { BoardId = board.Id, Name = "Alumni Guest", Note = "Guest from alumni office", AddedAt = DateTime.UtcNow.AddHours(-1) },
                        new BoardExternalParticipant { BoardId = board.Id, Name = "Exchange Student", Note = "Walk-in", AddedAt = DateTime.UtcNow.AddMinutes(-30) }
                    );
                    await context.SaveChangesAsync();
                }
                
                if (!await BoardExists("Calculus Study Session")
                    && regularUsersByEmail.TryGetValue("frank@example.com", out var frank2)
                    && regularUsersByEmail.TryGetValue("grace@example.com", out var grace2)
                    && studyTag != null)
                {
                    var board = new Board
                    {
                        Title = "Calculus Study Session",
                        Tags = new List<Tag> { studyTag },
                        Description = "Focused study group for Calculus II final exam preparation.",
                        AuthorId = frank2.Id,
                        MaxParticipants = 4,
                        Location = "Library Study Room 305",
                        EventDate = DateTime.UtcNow.Date.AddDays(2).AddHours(15),
                        Deadline = DateTime.UtcNow.Date.AddDays(1).AddHours(23),
                        CurrentStatus = BoardStatus.Open,
                        NotifyAuthorOnFull = true,
                        GroupManagementOption = GroupManagement.CloseOnFull,
                        JoinPolicy = BoardJoinPolicy.Application,
                        CreatedAt = DateTime.UtcNow.AddDays(-1)
                    };
                    context.Boards.Add(board);
                    await context.SaveChangesAsync();

                    context.BoardParticipants.Add(
                        new BoardParticipant { BoardId = board.Id, UserId = grace2.Id, JoinedAt = DateTime.UtcNow.AddHours(-10) }
                    );
                    await context.SaveChangesAsync();
                }
                
                if (!await BoardExists("Beach Trip Next Month")
                    && regularUsersByEmail.TryGetValue("heidi@example.com", out var heidi2)
                    && regularUsersByEmail.TryGetValue("ivan@example.com", out var ivan2)
                    && regularUsersByEmail.TryGetValue("judy@example.com", out var judy2)
                    && regularUsersByEmail.TryGetValue("karl@example.com", out var karl2)
                    && regularUsersByEmail.TryGetValue("leo@example.com", out var leo2)
                    && travelTag != null && outsideTag != null && foodTag != null)
                {
                    var board = new Board
                    {
                        Title = "Beach Trip Next Month",
                        Tags = new List<Tag> { travelTag, outsideTag, foodTag },
                        Description = "Day trip to the beach with seafood lunch. Transport provided.",
                        AuthorId = heidi2.Id,
                        MaxParticipants = 15,
                        Location = "Campus Parking Lot (Departure)",
                        EventDate = DateTime.UtcNow.Date.AddDays(30).AddHours(7),
                        Deadline = DateTime.UtcNow.Date.AddDays(25).AddHours(23),
                        CurrentStatus = BoardStatus.Open,
                        NotifyAuthorOnFull = true,
                        GroupManagementOption = GroupManagement.KeepOpenWhenFull,
                        JoinPolicy = BoardJoinPolicy.FirstComeFirstServe,
                        CreatedAt = DateTime.UtcNow.AddDays(-7)
                    };
                    context.Boards.Add(board);
                    await context.SaveChangesAsync();

                    context.BoardParticipants.AddRange(
                        new BoardParticipant { BoardId = board.Id, UserId = ivan2.Id, JoinedAt = DateTime.UtcNow.AddDays(-6) },
                        new BoardParticipant { BoardId = board.Id, UserId = judy2.Id, JoinedAt = DateTime.UtcNow.AddDays(-5) },
                        new BoardParticipant { BoardId = board.Id, UserId = karl2.Id, JoinedAt = DateTime.UtcNow.AddDays(-4) },
                        new BoardParticipant { BoardId = board.Id, UserId = leo2.Id, JoinedAt = DateTime.UtcNow.AddDays(-3) }
                    );
                    await context.SaveChangesAsync();
                }
                
                if (!await BoardExists("Postponed Workshop")
                    && regularUsersByEmail.TryGetValue("mallory@example.com", out var mallory2)
                    && studyTag != null)
                {
                    var board = new Board
                    {
                        Title = "Postponed Workshop",
                        Tags = new List<Tag> { studyTag },
                        Description = "Python programming workshop (cancelled due to venue issues).",
                        AuthorId = mallory2.Id,
                        MaxParticipants = 25,
                        Location = "Computer Lab B",
                        EventDate = DateTime.UtcNow.Date.AddDays(5).AddHours(13),
                        Deadline = DateTime.UtcNow.Date.AddDays(4).AddHours(12),
                        CurrentStatus = BoardStatus.Cancelled,
                        NotifyAuthorOnFull = false,
                        GroupManagementOption = GroupManagement.CloseOnFull,
                        JoinPolicy = BoardJoinPolicy.Application,
                        CreatedAt = DateTime.UtcNow.AddDays(-8)
                    };
                    context.Boards.Add(board);
                    await context.SaveChangesAsync();
                }
                
                if (!await BoardExists("Wednesday Badminton")
                    && regularUsersByEmail.TryGetValue("nina@example.com", out var nina2)
                    && regularUsersByEmail.TryGetValue("oscar@example.com", out var oscar2)
                    && regularUsersByEmail.TryGetValue("peggy@example.com", out var peggy2)
                    && regularUsersByEmail.TryGetValue("quentin@example.com", out var quentin2)
                    && sportsTag != null)
                {
                    var board = new Board
                    {
                        Title = "Wednesday Badminton",
                        Tags = new List<Tag> { sportsTag },
                        Description = "Doubles badminton matches. Only 2 spots left!",
                        AuthorId = nina2.Id,
                        MaxParticipants = 6,
                        Location = "Sports Complex Badminton Courts",
                        EventDate = DateTime.UtcNow.Date.AddDays(9).AddHours(18),
                        Deadline = DateTime.UtcNow.Date.AddDays(8).AddHours(12),
                        CurrentStatus = BoardStatus.Open,
                        NotifyAuthorOnFull = true,
                        GroupManagementOption = GroupManagement.CloseOnFull,
                        JoinPolicy = BoardJoinPolicy.FirstComeFirstServe,
                        CreatedAt = DateTime.UtcNow.AddDays(-4)
                    };
                    context.Boards.Add(board);
                    await context.SaveChangesAsync();

                    context.BoardParticipants.AddRange(
                        new BoardParticipant { BoardId = board.Id, UserId = oscar2.Id, JoinedAt = DateTime.UtcNow.AddDays(-3) },
                        new BoardParticipant { BoardId = board.Id, UserId = peggy2.Id, JoinedAt = DateTime.UtcNow.AddDays(-2) },
                        new BoardParticipant { BoardId = board.Id, UserId = quentin2.Id, JoinedAt = DateTime.UtcNow.AddDays(-1) }
                    );
                    await context.SaveChangesAsync();

                    context.BoardExternalParticipants.Add(
                        new BoardExternalParticipant { BoardId = board.Id, Name = "Court Guest", Note = "Friend of organizer", AddedAt = DateTime.UtcNow.AddHours(-12) }
                    );
                    await context.SaveChangesAsync();
                }
                
                logger.LogInformation("Seeding board applicants and denied users...");
                
                var footballBoard = await context.Boards.FirstOrDefaultAsync(b => b.Title == "Saturday Football Match");
                if (footballBoard != null 
                    && regularUsersByEmail.TryGetValue("charlie@example.com", out var charlieApplicant)
                    && regularUsersByEmail.TryGetValue("david@example.com", out var davidApplicant)
                    && regularUsersByEmail.TryGetValue("frank@example.com", out var frankApplicant))
                {
                    if (!await context.BoardApplicants.AnyAsync(ba => ba.BoardId == footballBoard.Id && ba.UserId == charlieApplicant.Id))
                    {
                        context.BoardApplicants.Add(new BoardApplicant 
                        { 
                            BoardId = footballBoard.Id, 
                            UserId = charlieApplicant.Id, 
                            AppliedAt = DateTime.UtcNow.AddHours(-6) 
                        });
                    }
                    
                    if (!await context.BoardApplicants.AnyAsync(ba => ba.BoardId == footballBoard.Id && ba.UserId == davidApplicant.Id))
                    {
                        context.BoardApplicants.Add(new BoardApplicant 
                        { 
                            BoardId = footballBoard.Id, 
                            UserId = davidApplicant.Id, 
                            AppliedAt = DateTime.UtcNow.AddHours(-3) 
                        });
                    }
                    
                    if (!await context.BoardDenied.AnyAsync(bd => bd.BoardId == footballBoard.Id && bd.UserId == frankApplicant.Id))
                    {
                        context.BoardDenied.Add(new BoardDenied 
                        { 
                            BoardId = footballBoard.Id, 
                            UserId = frankApplicant.Id, 
                            DeniedAt = DateTime.UtcNow.AddDays(-1) 
                        });
                    }
                    
                    await context.SaveChangesAsync();
                }
                
                var studyBoard = await context.Boards.FirstOrDefaultAsync(b => b.Title == "Sunday Study Group");
                if (studyBoard != null 
                    && regularUsersByEmail.TryGetValue("grace@example.com", out var graceApplicant)
                    && regularUsersByEmail.TryGetValue("heidi@example.com", out var heidiApplicant))
                {
                    if (!await context.BoardApplicants.AnyAsync(ba => ba.BoardId == studyBoard.Id && ba.UserId == graceApplicant.Id))
                    {
                        context.BoardApplicants.Add(new BoardApplicant 
                        { 
                            BoardId = studyBoard.Id, 
                            UserId = graceApplicant.Id, 
                            AppliedAt = DateTime.UtcNow.AddHours(-8) 
                        });
                    }
                    
                    if (!await context.BoardApplicants.AnyAsync(ba => ba.BoardId == studyBoard.Id && ba.UserId == heidiApplicant.Id))
                    {
                        context.BoardApplicants.Add(new BoardApplicant 
                        { 
                            BoardId = studyBoard.Id, 
                            UserId = heidiApplicant.Id, 
                            AppliedAt = DateTime.UtcNow.AddHours(-2) 
                        });
                    }
                    
                    await context.SaveChangesAsync();
                }
                
                var foodBoard = await context.Boards.FirstOrDefaultAsync(b => b.Title == "Thursday Food Tour");
                if (foodBoard != null 
                    && regularUsersByEmail.TryGetValue("ivan@example.com", out var ivanApplicant)
                    && regularUsersByEmail.TryGetValue("judy@example.com", out var judyApplicant)
                    && regularUsersByEmail.TryGetValue("karl@example.com", out var karlApplicant)
                    && regularUsersByEmail.TryGetValue("leo@example.com", out var leoApplicant))
                {
                    if (!await context.BoardApplicants.AnyAsync(ba => ba.BoardId == foodBoard.Id && ba.UserId == ivanApplicant.Id))
                    {
                        context.BoardApplicants.Add(new BoardApplicant 
                        { 
                            BoardId = foodBoard.Id, 
                            UserId = ivanApplicant.Id, 
                            AppliedAt = DateTime.UtcNow.AddHours(-4) 
                        });
                    }
                    
                    if (!await context.BoardApplicants.AnyAsync(ba => ba.BoardId == foodBoard.Id && ba.UserId == judyApplicant.Id))
                    {
                        context.BoardApplicants.Add(new BoardApplicant 
                        { 
                            BoardId = foodBoard.Id, 
                            UserId = judyApplicant.Id, 
                            AppliedAt = DateTime.UtcNow.AddHours(-1) 
                        });
                    }
                    
                    if (!await context.BoardDenied.AnyAsync(bd => bd.BoardId == foodBoard.Id && bd.UserId == karlApplicant.Id))
                    {
                        context.BoardDenied.Add(new BoardDenied 
                        { 
                            BoardId = foodBoard.Id, 
                            UserId = karlApplicant.Id, 
                            DeniedAt = DateTime.UtcNow.AddDays(-2) 
                        });
                    }
                    
                    if (!await context.BoardDenied.AnyAsync(bd => bd.BoardId == foodBoard.Id && bd.UserId == leoApplicant.Id))
                    {
                        context.BoardDenied.Add(new BoardDenied 
                        { 
                            BoardId = foodBoard.Id, 
                            UserId = leoApplicant.Id, 
                            DeniedAt = DateTime.UtcNow.AddDays(-1) 
                        });
                    }
                    
                    await context.SaveChangesAsync();
                }

                logger.LogInformation("Board seeding completed successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the database.");
            }
        }
    }
}