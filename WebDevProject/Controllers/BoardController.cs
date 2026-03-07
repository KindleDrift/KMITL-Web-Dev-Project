using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebDevProject.Data;
using WebDevProject.Filters;
using WebDevProject.Models;

namespace WebDevProject.Controllers
{
    [RequireOnboarding]
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
                .Include(b => b.Applicants)
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
        
        public async Task<IActionResult> Search(string name)
        {
            var boardQuery = _context.Boards
                .AsNoTracking()
                .Include(b => b.Author)
                .Include(b => b.Participants)
                    .ThenInclude(bp => bp.User)
                .Where(b => b.CurrentStatus != BoardStatus.Archived);

            if (!string.IsNullOrWhiteSpace(name))
            {
                // SQL-friendly LIKE search; DB collation determines case sensitivity
                boardQuery = boardQuery.Where(b => EF.Functions.Like(b.Title, $"%{name}%"));
            }

            var existingBoards = await boardQuery
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return View(existingBoards);
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

            var board = new Board
            {
                Title = model.Title,
                Description = model.Description,
                Location = model.Location,
                EventDate = model.EventDate,
                Deadline = model.Deadline,
                MaxParticipants = model.MaxParticipants,
                AuthorId = userId,
                NotifyAuthorOnFull = model.NotifyAuthorOnFull,
                CurrentStatus = BoardStatus.Open,
                CreatedAt = DateTime.UtcNow,
                GroupManagementOption = ParseGroupManagementOption(model.GroupManagementOption)
            };

            board.ImageUrl = await SaveBoardImageAsync(model.BoardImage, userId, null);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await ApplyTagsToBoardAsync(board, model.Tags);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _context.Boards.Add(board);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var board = await _context.Boards
                .Include(b => b.Tags)
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id);

            if (board == null)
            {
                return NotFound();
            }

            if (board.AuthorId != userId)
            {
                return Forbid();
            }

            var model = new BoardCreateViewModel
            {
                Title = board.Title,
                Description = board.Description,
                Location = board.Location,
                EventDate = board.EventDate,
                Deadline = board.Deadline,
                MaxParticipants = board.MaxParticipants,
                NotifyAuthorOnFull = board.NotifyAuthorOnFull,
                GroupManagementOption = ToGroupManagementOptionValue(board.GroupManagementOption),
                Tags = board.Tags.Select(t => t.Name).ToList()
            };

            ViewBag.BoardId = board.Id;
            ViewBag.CurrentImageUrl = board.ImageUrl;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BoardCreateViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var board = await _context.Boards
                .Include(b => b.Tags)
                .Include(b => b.Participants)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (board == null)
            {
                return NotFound();
            }

            if (board.AuthorId != userId)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                ViewBag.BoardId = board.Id;
                ViewBag.CurrentImageUrl = board.ImageUrl;
                return View(model);
            }

            if (model.Deadline > model.EventDate)
            {
                ModelState.AddModelError(nameof(model.Deadline), "Deadline must be before the event date.");
                ViewBag.BoardId = board.Id;
                ViewBag.CurrentImageUrl = board.ImageUrl;
                return View(model);
            }

            board.Title = model.Title;
            board.Description = model.Description;
            board.Location = model.Location;
            board.EventDate = model.EventDate;
            board.Deadline = model.Deadline;
            board.MaxParticipants = model.MaxParticipants;
            board.NotifyAuthorOnFull = model.NotifyAuthorOnFull;
            board.GroupManagementOption = ParseGroupManagementOption(model.GroupManagementOption);

            board.ImageUrl = await SaveBoardImageAsync(model.BoardImage, userId, board.ImageUrl);
            if (!ModelState.IsValid)
            {
                ViewBag.BoardId = board.Id;
                ViewBag.CurrentImageUrl = board.ImageUrl;
                return View(model);
            }

            await ApplyTagsToBoardAsync(board, model.Tags);
            if (!ModelState.IsValid)
            {
                ViewBag.BoardId = board.Id;
                ViewBag.CurrentImageUrl = board.ImageUrl;
                return View(model);
            }

            UpdateBoardStatusByCapacity(board, board.Participants.Count);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Board updated successfully.";
            return RedirectToAction(nameof(Details), new { id = board.Id });
        }

        public async Task<IActionResult> Details(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var board = await _context.Boards
                .Include(b => b.Author)
                .Include(b => b.Tags)
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Applicants)
                    .ThenInclude(a => a.User)
                .Include(b => b.DeniedUsers)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (board == null)
            {
                return NotFound();
            }

            var isAuthor = !string.IsNullOrWhiteSpace(userId) && board.AuthorId == userId;
            var applicationStatus = ApplicationStatus.NotApplied;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                if (board.Participants.Any(p => p.UserId == userId))
                {
                    applicationStatus = ApplicationStatus.Approved;
                }
                else if (board.Applicants.Any(a => a.UserId == userId))
                {
                    applicationStatus = ApplicationStatus.Pending;
                }
                else if (board.DeniedUsers.Any(d => d.UserId == userId))
                {
                    applicationStatus = ApplicationStatus.Denied;
                }
            }

            var model = new BoardDetailsViewModel
            {
                Board = board,
                IsAuthor = isAuthor,
                UserApplicationStatus = applicationStatus,
                Participants = board.Participants.OrderBy(p => p.JoinedAt).ToList(),
                Applicants = board.Applicants.OrderBy(a => a.AppliedAt).ToList(),
                DeniedUsers = board.DeniedUsers.OrderBy(d => d.DeniedAt).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var board = await _context.Boards
                .Include(b => b.Participants)
                .Include(b => b.Applicants)
                .Include(b => b.DeniedUsers)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (board == null)
            {
                return NotFound();
            }

            // Check if user is the author
            if (board.AuthorId == userId)
            {
                TempData["Error"] = "You cannot apply to your own board.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Check if user is already a participant
            if (board.Participants.Any(p => p.UserId == userId))
            {
                TempData["Error"] = "You are already a participant.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Check if user is already an applicant
            if (board.Applicants.Any(a => a.UserId == userId))
            {
                TempData["Error"] = "You have already applied.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Check if user is denied
            if (board.DeniedUsers.Any(d => d.UserId == userId))
            {
                TempData["Error"] = "You have been denied access to this board.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Check if board is open
            if (board.CurrentStatus != BoardStatus.Open)
            {
                TempData["Error"] = "This board is not accepting applications.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Add application
            var applicant = new BoardApplicant
            {
                BoardId = id,
                UserId = userId,
                AppliedAt = DateTime.UtcNow
            };

            _context.BoardApplicants.Add(applicant);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your application has been submitted successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveApplicant(int boardId, string applicantId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var board = await _context.Boards
                .Include(b => b.Participants)
                .Include(b => b.Applicants)
                .FirstOrDefaultAsync(b => b.Id == boardId);

            if (board == null)
            {
                return NotFound();
            }

            if (board.AuthorId != userId)
            {
                return Forbid();
            }

            var applicant = board.Applicants.FirstOrDefault(a => a.UserId == applicantId);
            if (applicant == null)
            {
                TempData["Error"] = "Applicant not found.";
                return RedirectToAction(nameof(Details), new { id = boardId });
            }

            var canOverbook = board.GroupManagementOption == GroupManagement.AllowOverbooking;
            if (!canOverbook && board.Participants.Count >= board.MaxParticipants)
            {
                TempData["Error"] = "Board is full. Cannot approve more participants.";
                return RedirectToAction(nameof(Details), new { id = boardId });
            }

            _context.BoardApplicants.Remove(applicant);

            var participant = new BoardParticipant
            {
                BoardId = boardId,
                UserId = applicantId,
                JoinedAt = DateTime.UtcNow
            };

            _context.BoardParticipants.Add(participant);
            UpdateBoardStatusByCapacity(board, board.Participants.Count + 1);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Applicant approved successfully.";
            return RedirectToAction(nameof(Details), new { id = boardId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DenyApplicant(int boardId, string applicantId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var board = await _context.Boards
                .Include(b => b.Applicants)
                .Include(b => b.DeniedUsers)
                .FirstOrDefaultAsync(b => b.Id == boardId);

            if (board == null)
            {
                return NotFound();
            }

            // Check if user is the author
            if (board.AuthorId != userId)
            {
                return Forbid();
            }

            // Find the applicant
            var applicant = board.Applicants.FirstOrDefault(a => a.UserId == applicantId);
            if (applicant == null)
            {
                TempData["Error"] = "Applicant not found.";
                return RedirectToAction(nameof(Details), new { id = boardId });
            }

            // Remove from applicants and add to denied list
            _context.BoardApplicants.Remove(applicant);
            
            var denied = new BoardDenied
            {
                BoardId = boardId,
                UserId = applicantId,
                DeniedAt = DateTime.UtcNow
            };

            _context.BoardDenied.Add(denied);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Applicant denied.";
            return RedirectToAction(nameof(Details), new { id = boardId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveDenial(int boardId, string deniedUserId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var board = await _context.Boards
                .Include(b => b.DeniedUsers)
                .FirstOrDefaultAsync(b => b.Id == boardId);

            if (board == null)
            {
                return NotFound();
            }

            // Check if user is the author
            if (board.AuthorId != userId)
            {
                return Forbid();
            }

            // Find the denied user
            var deniedUser = board.DeniedUsers.FirstOrDefault(d => d.UserId == deniedUserId);
            if (deniedUser == null)
            {
                TempData["Error"] = "Denied user not found.";
                return RedirectToAction(nameof(Details), new { id = boardId });
            }

            _context.BoardDenied.Remove(deniedUser);
            await _context.SaveChangesAsync();

            TempData["Success"] = "User removed from deny list.";
            return RedirectToAction(nameof(Details), new { id = boardId });
        }

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
                        return false;
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

        private async Task<string?> SaveBoardImageAsync(IFormFile? boardImage, string userId, string? existingImageUrl)
        {
            if (boardImage == null || boardImage.Length <= 0)
            {
                return existingImageUrl;
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(boardImage.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(fileExtension))
            {
                ModelState.AddModelError(nameof(BoardCreateViewModel.BoardImage), "Only image files are allowed (.jpg, .jpeg, .png, .gif, .webp).");
                return existingImageUrl;
            }

            if (boardImage.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError(nameof(BoardCreateViewModel.BoardImage), "Image file size must not exceed 5MB.");
                return existingImageUrl;
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "boards");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            if (!string.IsNullOrWhiteSpace(existingImageUrl))
            {
                var oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existingImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldImagePath))
                {
                    System.IO.File.Delete(oldImagePath);
                }
            }

            var uniqueFileName = $"{userId}_{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await boardImage.CopyToAsync(fileStream);
            }

            return $"/uploads/boards/{uniqueFileName}";
        }

        private async Task ApplyTagsToBoardAsync(Board board, List<string>? tags)
        {
            board.Tags.Clear();

            if (tags == null || !tags.Any())
            {
                return;
            }

            var validatedTags = new List<string>();

            foreach (var tag in tags)
            {
                var trimmedTag = tag?.Trim();
                if (string.IsNullOrWhiteSpace(trimmedTag))
                    continue;

                if (!IsValidTag(trimmedTag))
                {
                    ModelState.AddModelError(nameof(BoardCreateViewModel.Tags), $"Invalid tag '{trimmedTag}'. Tags must contain only letters and single hyphens (not at start or end).");
                    return;
                }

                var formattedTag = FormatTag(trimmedTag);
                if (!validatedTags.Contains(formattedTag, StringComparer.OrdinalIgnoreCase))
                {
                    validatedTags.Add(formattedTag);
                }
            }

            if (!validatedTags.Any())
            {
                return;
            }

            var existingTags = await _context.Tags
                .Where(t => validatedTags.Contains(t.Name))
                .ToListAsync();

            var existingNames = new HashSet<string>(existingTags.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);

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

            foreach (var tag in existingTags)
            {
                board.Tags.Add(tag);
            }
        }

        private static GroupManagement ParseGroupManagementOption(string? option)
        {
            return option switch
            {
                "allowOverbooking" => GroupManagement.AllowOverbooking,
                "keepOpenWhenFull" => GroupManagement.KeepOpenWhenFull,
                "increaseMax" => GroupManagement.AllowOverbooking,
                "manualIncrease" => GroupManagement.KeepOpenWhenFull,
                _ => GroupManagement.CloseOnFull
            };
        }

        private static string ToGroupManagementOptionValue(GroupManagement option)
        {
            return option switch
            {
                GroupManagement.AllowOverbooking => "allowOverbooking",
                GroupManagement.KeepOpenWhenFull => "keepOpenWhenFull",
                _ => "closeOnFull"
            };
        }

        private static void UpdateBoardStatusByCapacity(Board board, int occupiedSeats)
        {
            if (board.CurrentStatus is BoardStatus.Closed or BoardStatus.Cancelled or BoardStatus.Archived)
            {
                return;
            }

            if (board.GroupManagementOption == GroupManagement.CloseOnFull)
            {
                board.CurrentStatus = occupiedSeats >= board.MaxParticipants ? BoardStatus.Full : BoardStatus.Open;
                return;
            }

            if (board.CurrentStatus == BoardStatus.Full)
            {
                board.CurrentStatus = BoardStatus.Open;
            }
        }
    }
}
