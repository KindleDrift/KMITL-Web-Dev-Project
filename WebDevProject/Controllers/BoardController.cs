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
                .Include(b => b.ExternalParticipants)
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
                .Include(b => b.ExternalParticipants)
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
                    board.GroupManagementOption = GroupManagement.CloseOnFull;
                    break;
                case "increaseMax":
                    board.GroupManagementOption = GroupManagement.IncreaseMaxParticipantsOnFull;
                    break;
                case "manualIncrease":
                    board.GroupManagementOption = GroupManagement.ManualIncreaseMaxParticipants;
                    break;
                default:
                    board.GroupManagementOption = GroupManagement.CloseOnFull;
                    break;
            }

            switch (model.JoinPolicyOption)
            {
                case "fcfs":
                    board.JoinPolicy = BoardJoinPolicy.FirstComeFirstServe;
                    break;
                case "application":
                default:
                    board.JoinPolicy = BoardJoinPolicy.Application;
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

        public async Task<IActionResult> Details(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var board = await _context.Boards
                .Include(b => b.Author)
                .Include(b => b.Tags)
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.ExternalParticipants)
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
                ExternalParticipants = board.ExternalParticipants.OrderBy(e => e.AddedAt).ToList(),
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
                .Include(b => b.ExternalParticipants)
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

            if (board.Deadline <= DateTime.UtcNow)
            {
                TempData["Error"] = "The registration deadline has passed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (GetOccupiedSeatCount(board) >= board.MaxParticipants)
            {
                TempData["Error"] = "Board is full. Cannot accept more participants.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (board.JoinPolicy == BoardJoinPolicy.FirstComeFirstServe)
            {
                var occupiedBeforeAdd = GetOccupiedSeatCount(board);

                var participant = new BoardParticipant
                {
                    BoardId = id,
                    UserId = userId,
                    JoinedAt = DateTime.UtcNow
                };

                _context.BoardParticipants.Add(participant);
                UpdateBoardStatusByCapacity(board, occupiedBeforeAdd + 1);
                await _context.SaveChangesAsync();

                TempData["Success"] = "You joined this board successfully.";
                return RedirectToAction(nameof(Details), new { id });
            }

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
        public async Task<IActionResult> CancelMyApplication(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var board = await _context.Boards
                .Include(b => b.Participants)
                .Include(b => b.ExternalParticipants)
                .Include(b => b.Applicants)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (board == null)
            {
                return NotFound();
            }

            if (board.AuthorId == userId)
            {
                TempData["Error"] = "Board owner cannot use this action.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var applicant = board.Applicants.FirstOrDefault(a => a.UserId == userId);
            if (applicant != null)
            {
                _context.BoardApplicants.Remove(applicant);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Your application has been cancelled.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var participant = board.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant != null)
            {
                var occupiedBeforeRemoval = GetOccupiedSeatCount(board);

                _context.BoardParticipants.Remove(participant);

                var occupiedAfterRemoval = Math.Max(occupiedBeforeRemoval - 1, 0);
                UpdateBoardStatusByCapacity(board, occupiedAfterRemoval);

                await _context.SaveChangesAsync();

                TempData["Success"] = "You left the board successfully.";
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Error"] = "You have no active application or participation in this board.";
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
                .Include(b => b.ExternalParticipants)
                .Include(b => b.Applicants)
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

            // Check if board is full
            if (GetOccupiedSeatCount(board) >= board.MaxParticipants)
            {
                TempData["Error"] = "Board is full. Cannot approve more participants.";
                return RedirectToAction(nameof(Details), new { id = boardId });
            }

            var occupiedBeforeAdd = GetOccupiedSeatCount(board);

            _context.BoardApplicants.Remove(applicant);

            var participant = new BoardParticipant
            {
                BoardId = boardId,
                UserId = applicantId,
                JoinedAt = DateTime.UtcNow
            };

            _context.BoardParticipants.Add(participant);
            UpdateBoardStatusByCapacity(board, occupiedBeforeAdd + 1);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Applicant approved successfully.";
            return RedirectToAction(nameof(Details), new { id = boardId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DenyParticipant(int boardId, string participantId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var board = await _context.Boards
                .Include(b => b.Participants)
                .Include(b => b.ExternalParticipants)
                .Include(b => b.DeniedUsers)
                .FirstOrDefaultAsync(b => b.Id == boardId);

            if (board == null)
            {
                return NotFound();
            }

            if (board.AuthorId != userId)
            {
                return Forbid();
            }

            var participant = board.Participants.FirstOrDefault(p => p.UserId == participantId);
            if (participant == null)
            {
                TempData["Error"] = "Participant not found.";
                return RedirectToAction(nameof(Details), new { id = boardId });
            }

            var occupiedBeforeRemoval = GetOccupiedSeatCount(board);

            _context.BoardParticipants.Remove(participant);

            if (!board.DeniedUsers.Any(d => d.UserId == participantId))
            {
                _context.BoardDenied.Add(new BoardDenied
                {
                    BoardId = boardId,
                    UserId = participantId,
                    DeniedAt = DateTime.UtcNow
                });
            }

            var occupiedAfterRemoval = Math.Max(occupiedBeforeRemoval - 1, 0);
            UpdateBoardStatusByCapacity(board, occupiedAfterRemoval);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Participant removed and denied.";
            return RedirectToAction(nameof(Details), new { id = boardId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddExternalParticipant(int boardId, string externalName, string? externalNote)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var board = await _context.Boards
                .Include(b => b.Participants)
                .Include(b => b.ExternalParticipants)
                .FirstOrDefaultAsync(b => b.Id == boardId);

            if (board == null)
            {
                return NotFound();
            }

            if (board.AuthorId != userId)
            {
                return Forbid();
            }

            var normalizedName = externalName?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                TempData["Error"] = "External participant name is required.";
                return RedirectToAction(nameof(Details), new { id = boardId });
            }

            if (normalizedName.Length > 120)
            {
                TempData["Error"] = "External participant name is too long.";
                return RedirectToAction(nameof(Details), new { id = boardId });
            }

            if (GetOccupiedSeatCount(board) >= board.MaxParticipants)
            {
                TempData["Error"] = "Board is full. Cannot add external participant.";
                return RedirectToAction(nameof(Details), new { id = boardId });
            }

            var occupiedBeforeAdd = GetOccupiedSeatCount(board);

            var participant = new BoardExternalParticipant
            {
                BoardId = boardId,
                Name = normalizedName,
                Note = string.IsNullOrWhiteSpace(externalNote) ? null : externalNote.Trim(),
                AddedAt = DateTime.UtcNow
            };

            _context.BoardExternalParticipants.Add(participant);
            UpdateBoardStatusByCapacity(board, occupiedBeforeAdd + 1);

            await _context.SaveChangesAsync();

            TempData["Success"] = "External participant added.";
            return RedirectToAction(nameof(Details), new { id = boardId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveExternalParticipant(int boardId, int externalParticipantId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var board = await _context.Boards
                .Include(b => b.Participants)
                .Include(b => b.ExternalParticipants)
                .FirstOrDefaultAsync(b => b.Id == boardId);

            if (board == null)
            {
                return NotFound();
            }

            if (board.AuthorId != userId)
            {
                return Forbid();
            }

            var externalParticipant = board.ExternalParticipants.FirstOrDefault(e => e.Id == externalParticipantId);
            if (externalParticipant == null)
            {
                TempData["Error"] = "External participant not found.";
                return RedirectToAction(nameof(Details), new { id = boardId });
            }

            var occupiedBeforeRemoval = GetOccupiedSeatCount(board);

            _context.BoardExternalParticipants.Remove(externalParticipant);

            var occupiedAfterRemoval = Math.Max(occupiedBeforeRemoval - 1, 0);
            UpdateBoardStatusByCapacity(board, occupiedAfterRemoval);

            await _context.SaveChangesAsync();

            TempData["Success"] = "External participant removed.";
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

            if (board.AuthorId != userId)
            {
                return Forbid();
            }

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

        private static int GetOccupiedSeatCount(Board board)
        {
            return board.Participants.Count(p => p.UserId != board.AuthorId) + board.ExternalParticipants.Count;
        }

        private static void UpdateBoardStatusByCapacity(Board board, int occupiedSeats)
        {
            if (occupiedSeats >= board.MaxParticipants)
            {
                board.CurrentStatus = BoardStatus.Full;
                return;
            }

            if (board.CurrentStatus == BoardStatus.Full)
            {
                board.CurrentStatus = BoardStatus.Open;
            }
        }
    }
}
