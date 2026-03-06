using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDevProject.Data;
using WebDevProject.Models;

namespace WebDevProject.Controllers
{
    public class BoardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BoardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var boardQuery = _context.Boards
                .AsNoTracking()
                .Include(b => b.Author)
                .Include(b => b.Participants)
                    .ThenInclude(bp => bp.User)
                .Include(b => b.Tags);

            var activeBoards = string.IsNullOrWhiteSpace(userId)
                ? await boardQuery
                    .Where(b => b.CurrentStatus != BoardStatus.Archived)
                    .OrderByDescending(b => b.CreatedAt)
                    .ToListAsync()
                : await boardQuery
                    .Where(b => b.AuthorId == userId && b.CurrentStatus != BoardStatus.Archived)
                    .OrderByDescending(b => b.CreatedAt)
                    .ToListAsync();

            var participatingBoards = string.IsNullOrWhiteSpace(userId)
                ? new List<Board>()
                : await boardQuery
                    .Where(b => b.AuthorId != userId && b.Participants.Any(p => p.UserId == userId))
                    .OrderBy(b => b.EventDate)
                    .ToListAsync();

            var model = new BoardIndexViewModel
            {
                ActiveBoards = activeBoards,
                ParticipatingBoards = participatingBoards
            };

            return View(model);
        }

        public IActionResult Search()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BoardCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                ModelState.AddModelError(string.Empty, "You must be logged in to create a board.");
                return View(model);
            }

            // Validate dates
            if (model.Deadline > model.EventDate)
            {
                ModelState.AddModelError(nameof(model.Deadline), "Deadline must be before the event date.");
                return View(model);
            }

            if (model.Deadline < DateTime.UtcNow)
            {
                ModelState.AddModelError(nameof(model.Deadline), "Deadline must be in the future.");
                return View(model);
            }

            // Handle image upload
            string? imageUrl = null;
            if (model.BoardImage != null && model.BoardImage.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(model.BoardImage.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError(nameof(model.BoardImage), "Only image files are allowed (.jpg, .jpeg, .png, .gif, .webp).");
                    return View(model);
                }

                if (model.BoardImage.Length > 5 * 1024 * 1024) // 5MB limit
                {
                    ModelState.AddModelError(nameof(model.BoardImage), "Image file size must not exceed 5MB.");
                    return View(model);
                }

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "boards");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"{userId}_{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.BoardImage.CopyToAsync(fileStream);
                }

                imageUrl = $"/uploads/boards/{uniqueFileName}";
            }

            // Create the board
            var board = new Board
            {
                Title = model.Title,
                Description = model.Description,
                ImageUrl = imageUrl,
                Location = model.Location,
                EventDate = model.EventDate,
                Deadline = model.Deadline,
                MaxParticipants = model.MaxParticipants,
                AuthorId = userId,
                NotifyAuthorOnFull = model.NotifyAuthorOnFull,
                CurrentStatus = BoardStatus.Open,
                CreatedAt = DateTime.UtcNow
            };

            // Set group management options
            switch (model.GroupManagementOption)
            {
                case "closeOnFull":
                    board.CloseOnFull = true;
                    break;
                case "increaseMax":
                    board.IncreaseMaxParticipantsOnFull = true;
                    break;
                case "manualIncrease":
                    board.ManualIncreaseMaxParticipants = true;
                    break;
            }

            // Handle tags
            if (model.Tags != null && model.Tags.Any())
            {
                var validatedTags = new List<string>();
                
                foreach (var tag in model.Tags)
                {
                    var trimmedTag = tag?.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedTag))
                        continue;

                    // Validate and format tag
                    if (!IsValidTag(trimmedTag))
                    {
                        ModelState.AddModelError(nameof(model.Tags), $"Invalid tag '{trimmedTag}'. Tags must contain only letters and single hyphens (not at start or end).");
                        return View(model);
                    }

                    var formattedTag = FormatTag(trimmedTag);
                    if (!validatedTags.Contains(formattedTag, StringComparer.OrdinalIgnoreCase))
                    {
                        validatedTags.Add(formattedTag);
                    }
                }

                if (validatedTags.Any())
                {
                    var existingTags = await _context.Tags
                        .Where(t => validatedTags.Contains(t.Name))
                        .ToListAsync();

                    var existingNames = new HashSet<string>(existingTags.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);

                    // Add new tags
                    foreach (var name in validatedTags)
                    {
                        if (!existingNames.Contains(name))
                        {
                            var newTag = new Tag { Name = name };
                            _context.Tags.Add(newTag);
                            existingTags.Add(newTag);
                        }
                    }

                    await _context.SaveChangesAsync();

                    // Link tags to board
                    foreach (var tag in existingTags)
                    {
                        board.Tags.Add(tag);
                    }
                }
            }

            _context.Boards.Add(board);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // Tag validation
        private static bool IsValidTag(string tag)
        {
            // Check if tag starts or ends with hyphen
            if (tag.StartsWith('-') || tag.EndsWith('-'))
                return false;

            // Check if tag contains numbers
            if (tag.Any(char.IsDigit))
                return false;

            // Check if tag contains only letters and single hyphens
            for (int i = 0; i < tag.Length; i++)
            {
                char c = tag[i];
                
                // Allow letters
                if (char.IsLetter(c))
                    continue;

                // Allow single hyphen (not consecutive)
                if (c == '-')
                {
                    if (i > 0 && tag[i - 1] == '-')
                        return false; // Consecutive hyphens not allowed
                    continue;
                }

                // Any other character is invalid
                return false;
            }

            return true;
        }

        private static string FormatTag(string tag)
        {
            // Convert to lowercase first
            tag = tag.ToLowerInvariant();

            // Capitalize first letter
            if (tag.Length > 0)
            {
                tag = char.ToUpperInvariant(tag[0]) + tag.Substring(1);
            }

            return tag;
        }
    }
}
