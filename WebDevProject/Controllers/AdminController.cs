using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDevProject.Data;
using WebDevProject.Models;
using WebDevProject.Services;

namespace WebDevProject.Controllers
{
    [Authorize(Policy = "AdminOnly")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly NotificationsService _notificationsService;

        public AdminController(ApplicationDbContext context, UserManager<Users> userManager, NotificationsService notificationsService)
        {
            _context = context;
            _userManager = userManager;
            _notificationsService = notificationsService;
        }

        public ActionResult Index()
        {
            return View();
        }

        // Admin/Users - View all users
        public async Task<ActionResult> Users()
        {
            var users = await _context.Users.ToListAsync();
            return View(users);
        }

        // Admin/Boards
        public async Task<ActionResult> Boards()
        {
            var boards = await _context.Boards
                .Include(b => b.Author)
                .Include(b => b.Tags)
                .ToListAsync();
            return View(boards);
        }

        // Admin/EditUser/{id} - GET: Show edit form
        public async Task<ActionResult> EditUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // Admin/EditUser/{id} - POST: Update user
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditUser(string id, [Bind("Id,DisplayName,DateOfBirth,UserGender,Bio,HasCompletedOnboarding")] Users model, IFormFile? ProfileImage)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Can't change display name to an existing one except changing capitalization
            if (await _context.Users.AnyAsync(u => u.NormalizedDisplayName == model.DisplayName.ToUpperInvariant() && u.Id != id))
            {
                ModelState.AddModelError("DisplayName", "Display name is already taken.");
                return View(model);
            }

            // Handle profile image upload
            if (ProfileImage != null && ProfileImage.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(ProfileImage.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("ProfileImage", "Only image files are allowed (.jpg, .jpeg, .png, .gif, .webp).");
                    return View(model);
                }

                if (ProfileImage.Length > 5 * 1024 * 1024) // 5MB limit
                {
                    ModelState.AddModelError("ProfileImage", "Image file size must not exceed 5MB.");
                    return View(model);
                }

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Delete old image if exists
                if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
                {
                    var oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ProfilePictureUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                }

                var uniqueFileName = $"{id}_{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await ProfileImage.CopyToAsync(fileStream);
                }

                user.ProfilePictureUrl = $"/uploads/profiles/{uniqueFileName}";
            }

            user.DisplayName = model.DisplayName;
            user.NormalizedDisplayName = model.DisplayName.ToUpperInvariant();
            user.DateOfBirth = model.DateOfBirth;
            user.UserGender = model.UserGender;
            user.Bio = model.Bio;
            user.HasCompletedOnboarding = model.HasCompletedOnboarding;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Users.AnyAsync(u => u.Id == id))
                {
                    return NotFound();
                }
                throw;
            }

            return RedirectToAction(nameof(Users));
        }

        // Admin/Ban/{id} - POST: Ban user
        [HttpPost]
        public async Task<ActionResult> Ban(string id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            
            // Prevent admins from banning themselves
            if (currentUser?.Id == id)
            {
                return BadRequest("You cannot ban yourself.");
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);

            _context.Update(user);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Users));
        }

        // Admin/Unban/{id} - POST: Unban user
        [HttpPost]
        public async Task<ActionResult> Unban(string id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            user.LockoutEnd = null;

            _context.Update(user);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Users));
        }

        // Admin/EditBoard/{id} - GET: Show edit form for board
        public async Task<ActionResult> EditBoard(int id)
        {
            var board = await _context.Boards
                .Include(b => b.Author)
                .Include(b => b.Tags)
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id);
            
            if (board == null)
            {
                return NotFound();
            }

            return View(board);
        }

        // Admin/EditBoard/{id} - POST: Update board
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditBoard(int id, [Bind("Id,Title,Description,MaxParticipants,Location,EventDate,Deadline,NotifyAuthorOnFull,GroupManagementOption,JoinPolicy,CurrentStatus")] Board model, IFormFile? BoardImage, List<string>? Tags)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            var board = await _context.Boards
                .Include(b => b.Tags)
                .Include(b => b.Participants)
                .Include(b => b.ExternalParticipants)
                .Include(b => b.Applicants)
                .FirstOrDefaultAsync(b => b.Id == id);
            
            if (board == null)
            {
                return NotFound();
            }

            var oldJoinPolicy = board.JoinPolicy;
            var oldGroupManagement = board.GroupManagementOption;
            var oldMaxParticipants = board.MaxParticipants;

            // Handle board image upload
            if (BoardImage != null && BoardImage.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(BoardImage.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("BoardImage", "Only image files are allowed (.jpg, .jpeg, .png, .gif, .webp).");
                    return View(model);
                }

                if (BoardImage.Length > 5 * 1024 * 1024) // 5MB limit
                {
                    ModelState.AddModelError("BoardImage", "Image file size must not exceed 5MB.");
                    return View(model);
                }

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "boards");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Delete old image if exists
                if (!string.IsNullOrEmpty(board.ImageUrl))
                {
                    var oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", board.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                }

                var uniqueFileName = $"{board.AuthorId}_{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await BoardImage.CopyToAsync(fileStream);
                }

                board.ImageUrl = $"/uploads/boards/{uniqueFileName}";
            }

            // Handle tags
            if (Tags != null && Tags.Any())
            {
                var validatedTags = new List<string>();
                
                foreach (var tag in Tags)
                {
                    var trimmedTag = tag?.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedTag))
                        continue;

                    // Validate and format tag
                    if (!IsValidTag(trimmedTag))
                    {
                        ModelState.AddModelError("Tags", $"Invalid tag '{trimmedTag}'. Tags must contain only letters and single hyphens (not at start or end).");
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

                    // Clear existing tags and add new ones
                    board.Tags.Clear();
                    foreach (var tag in existingTags)
                    {
                        board.Tags.Add(tag);
                    }
                }
                else
                {
                    board.Tags.Clear();
                }
            }
            else
            {
                // If no tags provided, clear existing tags
                board.Tags.Clear();
            }

            // Update only the allowed properties
            board.Title = model.Title;
            board.Description = model.Description;
            board.MaxParticipants = model.MaxParticipants;
            board.Location = model.Location;
            board.EventDate = model.EventDate;
            board.Deadline = model.Deadline;
            board.NotifyAuthorOnFull = model.NotifyAuthorOnFull;
            board.GroupManagementOption = model.GroupManagementOption;
            board.JoinPolicy = model.JoinPolicy;
            board.CurrentStatus = model.CurrentStatus;

            // Edge Case 1: Join Policy changed from Application to FirstComeFirstServe
            // Auto-approve all pending applicants
            if (oldJoinPolicy == BoardJoinPolicy.Application && board.JoinPolicy == BoardJoinPolicy.FirstComeFirstServe)
            {
                var applicantsToApprove = board.Applicants.ToList();
                var approvedApplicantIds = new List<string>();
                
                foreach (var applicant in applicantsToApprove)
                {
                    var currentOccupied = GetOccupiedSeatCount(board);
                    
                    // Only approve if there's space OR if AllowOverbooking is enabled
                    if (currentOccupied < board.MaxParticipants || board.GroupManagementOption == GroupManagement.AllowOverbooking)
                    {
                        _context.BoardApplicants.Remove(applicant);
                        
                        var participant = new BoardParticipant
                        {
                            BoardId = id,
                            UserId = applicant.UserId,
                            JoinedAt = DateTime.UtcNow
                        };
                        
                        _context.BoardParticipants.Add(participant);
                        approvedApplicantIds.Add(applicant.UserId);
                    }
                }
                
                // Notify all approved applicants
                if (approvedApplicantIds.Any())
                {
                    await _notificationsService.CreateNotificationsForMultipleUsersAsync(
                        approvedApplicantIds,
                        $"Accepted: {board.Title}",
                        "An admin changed the board settings and you have been automatically accepted.",
                        NotificationType.AdminAction);
                }
            }

            // Edge Case 2: GroupManagement changed from CloseOnFull to AllowOverbooking/KeepOpenWhenFull
            // If board is currently Full, reconsider the status
            if (oldGroupManagement == GroupManagement.CloseOnFull && 
                board.GroupManagementOption != GroupManagement.CloseOnFull &&
                board.CurrentStatus == BoardStatus.Full)
            {
                // Set back to Open since we're no longer using CloseOnFull
                board.CurrentStatus = BoardStatus.Open;
            }

            // Edge Case 3: GroupManagement changed to CloseOnFull from something else
            // If currently at or over capacity, set to Full
            if (oldGroupManagement != GroupManagement.CloseOnFull &&
                board.GroupManagementOption == GroupManagement.CloseOnFull)
            {
                var currentOccupied = GetOccupiedSeatCount(board);
                if (currentOccupied >= board.MaxParticipants && board.CurrentStatus == BoardStatus.Open)
                {
                    board.CurrentStatus = BoardStatus.Full;
                }
            }

            // Edge Case 4: MaxParticipants changed - recalculate status
            if (oldMaxParticipants != board.MaxParticipants)
            {
                var currentOccupied = GetOccupiedSeatCount(board);
                
                // If capacity increased and was Full, might need to reopen
                if (board.MaxParticipants > oldMaxParticipants && board.CurrentStatus == BoardStatus.Full)
                {
                    if (currentOccupied < board.MaxParticipants)
                    {
                        board.CurrentStatus = BoardStatus.Open;
                    }
                }
                // If capacity decreased and now over capacity
                else if (board.MaxParticipants < oldMaxParticipants && board.GroupManagementOption == GroupManagement.CloseOnFull)
                {
                    if (currentOccupied >= board.MaxParticipants && board.CurrentStatus == BoardStatus.Open)
                    {
                        board.CurrentStatus = BoardStatus.Full;
                    }
                }
            }

            // Final status update based on current capacity
            UpdateBoardStatusByCapacity(board, GetOccupiedSeatCount(board));

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Boards.AnyAsync(b => b.Id == id))
                {
                    return NotFound();
                }
                throw;
            }

            // Notify participants and author if board status changed significantly
            if (model.CurrentStatus == BoardStatus.Cancelled)
            {
                // Notify all participants
                var participantIds = board.Participants.Select(p => p.UserId).ToList();
                if (participantIds.Any())
                {
                    await _notificationsService.CreateNotificationsForMultipleUsersAsync(
                        participantIds,
                        $"Board Cancelled: {board.Title}",
                        "The board you were participating in has been cancelled by an administrator.",
                        NotificationType.IsRejected);
                }

                // Notify all applicants
                var applicantIds = board.Applicants.Select(a => a.UserId).ToList();
                if (applicantIds.Any())
                {
                    await _notificationsService.CreateNotificationsForMultipleUsersAsync(
                        applicantIds,
                        $"Board Cancelled: {board.Title}",
                        "The board you applied to has been cancelled by an administrator.",
                        NotificationType.IsRejected);
                }
            }

            return RedirectToAction(nameof(Boards));
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

        private static int GetOccupiedSeatCount(Board board)
        {
            return board.Participants.Count(p => p.UserId != board.AuthorId) + board.ExternalParticipants.Count;
        }

        private static void UpdateBoardStatusByCapacity(Board board, int occupiedSeats)
        {
            if (board.CurrentStatus is BoardStatus.Closed or BoardStatus.Cancelled or BoardStatus.Archived)
            {
                return;
            }

            if (occupiedSeats >= board.MaxParticipants)
            {
                if (board.GroupManagementOption == GroupManagement.CloseOnFull)
                {
                    board.CurrentStatus = BoardStatus.Full;
                }

                return;
            }

            if (board.CurrentStatus == BoardStatus.Full)
            {
                board.CurrentStatus = BoardStatus.Open;
            }
        }
    }
}
