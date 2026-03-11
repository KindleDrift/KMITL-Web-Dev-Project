using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDevProject.Data;
using WebDevProject.Models;
using WebDevProject.Services;
using WebDevProject.Helpers;

namespace WebDevProject.Controllers
{
    [Authorize(Policy = "AdminOnly")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly NotificationsService _notificationsService;
        private readonly BoardService _boardService;
        private readonly ProfileImageService _profileImageService;
        private readonly IServiceProvider _serviceProvider;

        public AdminController(
            ApplicationDbContext context,
            UserManager<Users> userManager,
            NotificationsService notificationsService,
            BoardService boardService,
            ProfileImageService profileImageService,
            IServiceProvider serviceProvider)
        {
            _context = context;
            _userManager = userManager;
            _notificationsService = notificationsService;
            _boardService = boardService;
            _profileImageService = profileImageService;
            _serviceProvider = serviceProvider;
        }

        public ActionResult Index()
        {
            return View();
        }

        public async Task<ActionResult> Users()
        {
            var users = await _context.Users.ToListAsync();
            return View(users);
        }

        public async Task<ActionResult> Boards()
        {
            var boards = await _context.Boards
                .Include(b => b.Author)
                .Include(b => b.Tags)
                .ToListAsync();
            return View(boards);
        }

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

            var profileImageResult = await _profileImageService.SaveProfileImageAsync(ProfileImage, id, user.ProfilePictureUrl);
            user.ProfilePictureUrl = profileImageResult.ImageUrl;
            if (!profileImageResult.Success)
            {
                ModelState.AddModelError("ProfileImage", profileImageResult.ErrorMessage!);
                return View(model);
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditBoard(int id, [Bind("Id,Title,Description,MaxParticipants,Location,EventDate,Deadline,NotifyAuthorOnFull,GroupManagementOption,JoinPolicy,CurrentStatus")] Board model, IFormFile? BoardImage, List<string>? Tags, int? clientTimeZoneOffsetMinutes)
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

            var boardImageResult = await _boardService.SaveBoardImageAsync(BoardImage, board.AuthorId, board.ImageUrl);
            board.ImageUrl = boardImageResult.ImageUrl;
            if (!boardImageResult.Success)
            {
                ModelState.AddModelError("BoardImage", boardImageResult.ErrorMessage!);
                return View(model);
            }

            var tagsResult = await _boardService.ApplyTagsToBoardAsync(board, Tags);
            if (!tagsResult.Success)
            {
                ModelState.AddModelError("Tags", tagsResult.ErrorMessage!);
                return View(model);
            }

            var eventDateUtc = TimeZoneHelper.FromClientLocalToUtc(model.EventDate, clientTimeZoneOffsetMinutes);
            var deadlineUtc = TimeZoneHelper.FromClientLocalToUtc(model.Deadline, clientTimeZoneOffsetMinutes);

            board.Title = model.Title;
            board.Description = model.Description;
            board.MaxParticipants = model.MaxParticipants;
            board.Location = model.Location;
            board.EventDate = eventDateUtc;
            board.Deadline = deadlineUtc;
            board.NotifyAuthorOnFull = model.NotifyAuthorOnFull;
            board.GroupManagementOption = model.GroupManagementOption;
            board.JoinPolicy = model.JoinPolicy;
            board.CurrentStatus = model.CurrentStatus;

            var approvedApplicantIds = _boardService.AutoApproveApplicantsOnJoinPolicyChange(board, id, oldJoinPolicy);

            // Notify
            if (approvedApplicantIds.Any())
            {
                await _notificationsService.CreateNotificationsForMultipleUsersAsync(
                    approvedApplicantIds,
                    $"Accepted: {board.Title}",
                    "An admin changed the board settings and you have been automatically accepted.",
                    NotificationType.AdminAction,
                    boardId: id);
            }

            _boardService.RecalculateStatusAfterBoardSettingChanges(board, oldGroupManagement, oldMaxParticipants);

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

            if (model.CurrentStatus == BoardStatus.Cancelled)
            {
                var participantIds = board.Participants.Select(p => p.UserId).ToList();
                if (participantIds.Any())
                {
                    await _notificationsService.CreateNotificationsForMultipleUsersAsync(
                        participantIds,
                        $"Board Cancelled: {board.Title}",
                        "The board you were participating in has been cancelled by an administrator.",
                        NotificationType.IsRejected);
                }

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ClearDatabase()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Unauthorized();
                }

                var boards = await _context.Boards.Include(b => b.Tags).ToListAsync();
                _context.Boards.RemoveRange(boards);

                var notifications = await _context.Notifications.ToListAsync();
                _context.Notifications.RemoveRange(notifications);

                var tags = await _context.Tags.ToListAsync();
                _context.Tags.RemoveRange(tags);

                var participants = await _context.BoardParticipants.ToListAsync();
                _context.BoardParticipants.RemoveRange(participants);

                var externalParticipants = await _context.BoardExternalParticipants.ToListAsync();
                _context.BoardExternalParticipants.RemoveRange(externalParticipants);

                var applicants = await _context.BoardApplicants.ToListAsync();
                _context.BoardApplicants.RemoveRange(applicants);

                var deniedUsers = await _context.BoardDenied.ToListAsync();
                _context.BoardDenied.RemoveRange(deniedUsers);

                var usersToDelete = await _context.Users.Where(u => u.Id != currentUser.Id).ToListAsync();
                _context.Users.RemoveRange(usersToDelete);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Database cleared successfully. All data except admin user has been deleted.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error clearing database: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetAndReseed()
        {
            try
            {
                await _context.Database.EnsureDeletedAsync();

                await _context.Database.EnsureCreatedAsync();

                await _context.Database.MigrateAsync();

                await DbSeeder.SeedAsync(_serviceProvider);

                TempData["SuccessMessage"] = "Database reset and reseeded successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error resetting database: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
