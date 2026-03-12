using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebDevProject.Data;
using WebDevProject.Filters;
using WebDevProject.Helpers;
using WebDevProject.Models;
using WebDevProject.Services;

namespace WebDevProject.Controllers
{
    [Authorize]
    [RequireOnboarding]
    public class BoardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly BoardService _boardService;
        private readonly BoardMembershipService _boardMembershipService;

        public BoardController(
            ApplicationDbContext context,
            BoardService boardService,
            BoardMembershipService boardMembershipService)
        {
            _context = context;
            _boardService = boardService;
            _boardMembershipService = boardMembershipService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var boardQuery = _context.Boards
                .AsNoTracking()
                .AsSplitQuery()
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

            var applyingBoards = string.IsNullOrWhiteSpace(userId)
                ? new List<Board>()
                : await boardQuery
                    .Where(b => b.AuthorId != userId && b.Applicants.Any(a => a.UserId == userId))
                    .OrderBy(b => b.EventDate)
                    .ToListAsync();

            var model = new BoardIndexViewModel
            {
                ActiveBoards = activeBoards,
                ParticipatingBoards = participatingBoards,
                ApplyingBoards = applyingBoards
            };

            return View(model);
        }

        [AllowAnonymous]
        public IActionResult Search(string name)
        {
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> SearchBoards(string searchName, string tags, DateTime? eventDateFrom, DateTime? eventDateTo, string statuses, string joinPolicies, int? clientTimeZoneOffsetMinutes)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var boardQuery = _context.Boards
                .AsNoTracking()
                .AsSplitQuery()
                .Include(b => b.Author)
                .Include(b => b.Participants)
                    .ThenInclude(bp => bp.User)
                .Include(b => b.ExternalParticipants)
                .Include(b => b.Tags)
                .Where(b => b.CurrentStatus != BoardStatus.Archived);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                boardQuery = boardQuery.Where(b => b.AuthorId != userId);
            }

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                boardQuery = boardQuery.Where(b => EF.Functions.Like(b.Title, $"%{searchName}%"));
            }

            if (!string.IsNullOrWhiteSpace(tags))
            {
                var tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();

                if (tagList.Any())
                {
                    boardQuery = boardQuery.Where(b => b.Tags.Any(t => tagList.Contains(t.Name)));
                }
            }

            if (eventDateFrom.HasValue)
            {
                var eventDateFromUtc = TimeZoneHelper.FromClientLocalToUtc(eventDateFrom.Value.Date, clientTimeZoneOffsetMinutes);
                boardQuery = boardQuery.Where(b => b.EventDate >= eventDateFromUtc);
            }

            if (eventDateTo.HasValue)
            {
                var eventDateToInclusiveUtc = TimeZoneHelper.FromClientLocalToUtc(eventDateTo.Value.Date.AddDays(1), clientTimeZoneOffsetMinutes);
                boardQuery = boardQuery.Where(b => b.EventDate < eventDateToInclusiveUtc);
            }

            if (!string.IsNullOrWhiteSpace(statuses))
            {
                var statusList = statuses.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                if (statusList.Any())
                {
                    var parsedStatuses = new List<BoardStatus>();
                    foreach (var status in statusList)
                    {
                        if (Enum.TryParse<BoardStatus>(status, true, out var parsed))
                        {
                            parsedStatuses.Add(parsed);
                        }
                    }

                    if (parsedStatuses.Any())
                    {
                        boardQuery = boardQuery.Where(b => parsedStatuses.Contains(b.CurrentStatus));
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(joinPolicies))
            {
                var policyList = joinPolicies.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();

                if (policyList.Any())
                {
                    var parsedPolicies = new List<BoardJoinPolicy>();
                    foreach (var policy in policyList)
                    {
                        if (Enum.TryParse<BoardJoinPolicy>(policy, true, out var parsed))
                        {
                            parsedPolicies.Add(parsed);
                        }
                    }

                    if (parsedPolicies.Any())
                    {
                        boardQuery = boardQuery.Where(b => parsedPolicies.Contains(b.JoinPolicy));
                    }
                }
            }

            var boards = await boardQuery
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            var dtos = _boardService.GetBoardSearchDtos(boards);
            return Json(dtos);
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

            var eventDateUtc = TimeZoneHelper.FromClientLocalToUtc(model.EventDate, model.ClientTimeZoneOffsetMinutes);
            var deadlineUtc = TimeZoneHelper.FromClientLocalToUtc(model.Deadline, model.ClientTimeZoneOffsetMinutes);

            if (deadlineUtc > eventDateUtc)
            {
                ModelState.AddModelError(nameof(model.Deadline), "Deadline must be before the event date.");
                return View(model);
            }

            if (deadlineUtc < TimeZoneHelper.UtcNow.UtcDateTime)
            {
                ModelState.AddModelError(nameof(model.Deadline), "Deadline must be in the future.");
                return View(model);
            }

            var board = new Board
            {
                Title = model.Title,
                Description = model.Description,
                Location = model.Location,
                EventDate = eventDateUtc,
                Deadline = deadlineUtc,
                MaxParticipants = model.MaxParticipants,
                AuthorId = userId,
                NotifyAuthorOnFull = model.NotifyAuthorOnFull,
                CurrentStatus = BoardStatus.Open,
                CreatedAt = TimeZoneHelper.UtcNow.UtcDateTime,
                GroupManagementOption = _boardService.ParseGroupManagementOption(model.GroupManagementOption),
                JoinPolicy = _boardService.ParseJoinPolicyOption(model.JoinPolicyOption)
            };

            var imageResult = await _boardService.SaveBoardImageAsync(model.BoardImage, userId, null);
            board.ImageUrl = imageResult.ImageUrl;
            if (!imageResult.Success)
            {
                ModelState.AddModelError(nameof(BoardCreateViewModel.BoardImage), imageResult.ErrorMessage!);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var tagsResult = await _boardService.ApplyTagsToBoardAsync(board, model.Tags);
            if (!tagsResult.Success)
            {
                ModelState.AddModelError(nameof(BoardCreateViewModel.Tags), tagsResult.ErrorMessage!);
            }

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
                GroupManagementOption = _boardService.ToGroupManagementOptionValue(board.GroupManagementOption),
                JoinPolicyOption = _boardService.ToJoinPolicyOptionValue(board.JoinPolicy),
                Tags = board.Tags.Select(t => t.Name).ToList(),
                CurrentStatus = board.CurrentStatus
            };

            ViewBag.BoardId = board.Id;
            ViewBag.CurrentImageUrl = board.ImageUrl;
            ViewBag.IsCancelledOrArchived = board.CurrentStatus == BoardStatus.Cancelled || board.CurrentStatus == BoardStatus.Archived;
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
                .Include(b => b.ExternalParticipants)
                .Include(b => b.Applicants)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (board == null)
            {
                return NotFound();
            }

            if (board.AuthorId != userId)
            {
                return Forbid();
            }

            var isCancelledOrArchived = board.CurrentStatus == BoardStatus.Cancelled || board.CurrentStatus == BoardStatus.Archived;
            if (isCancelledOrArchived)
            {
                if (model.CurrentStatus.HasValue && model.CurrentStatus.Value != board.CurrentStatus)
                {
                    board.CurrentStatus = model.CurrentStatus.Value;
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Board status updated successfully.";
                    return RedirectToAction(nameof(Details), new { id = board.Id });
                }
                else
                {
                    TempData["Error"] = "This board is cancelled or archived. Only status changes are allowed in edit mode.";
                    ViewBag.BoardId = board.Id;
                    ViewBag.CurrentImageUrl = board.ImageUrl;
                    ViewBag.IsCancelledOrArchived = true;
                    return View(model);
                }
            }

            if (!ModelState.IsValid)
            {
                ViewBag.BoardId = board.Id;
                ViewBag.CurrentImageUrl = board.ImageUrl;
                return View(model);
            }

            var eventDateUtc = TimeZoneHelper.FromClientLocalToUtc(model.EventDate, model.ClientTimeZoneOffsetMinutes);
            var deadlineUtc = TimeZoneHelper.FromClientLocalToUtc(model.Deadline, model.ClientTimeZoneOffsetMinutes);

            if (deadlineUtc > eventDateUtc)
            {
                ModelState.AddModelError(nameof(model.Deadline), "Deadline must be before the event date.");
                ViewBag.BoardId = board.Id;
                ViewBag.CurrentImageUrl = board.ImageUrl;
                return View(model);
            }

            var oldJoinPolicy = board.JoinPolicy;
            var oldGroupManagement = board.GroupManagementOption;
            var oldMaxParticipants = board.MaxParticipants;

            board.Title = model.Title;
            board.Description = model.Description;
            board.Location = model.Location;
            board.EventDate = eventDateUtc;
            board.Deadline = deadlineUtc;
            board.MaxParticipants = model.MaxParticipants;
            board.NotifyAuthorOnFull = model.NotifyAuthorOnFull;
            board.GroupManagementOption = _boardService.ParseGroupManagementOption(model.GroupManagementOption);
            board.JoinPolicy = _boardService.ParseJoinPolicyOption(model.JoinPolicyOption);
            
            if (model.CurrentStatus.HasValue)
            {
                board.CurrentStatus = model.CurrentStatus.Value;
            }

            var editImageResult = await _boardService.SaveBoardImageAsync(model.BoardImage, userId, board.ImageUrl);
            board.ImageUrl = editImageResult.ImageUrl;
            if (!editImageResult.Success)
            {
                ModelState.AddModelError(nameof(BoardCreateViewModel.BoardImage), editImageResult.ErrorMessage!);
            }

            if (!ModelState.IsValid)
            {
                ViewBag.BoardId = board.Id;
                ViewBag.CurrentImageUrl = board.ImageUrl;
                return View(model);
            }

            var editTagsResult = await _boardService.ApplyTagsToBoardAsync(board, model.Tags);
            if (!editTagsResult.Success)
            {
                ModelState.AddModelError(nameof(BoardCreateViewModel.Tags), editTagsResult.ErrorMessage!);
            }

            if (!ModelState.IsValid)
            {
                ViewBag.BoardId = board.Id;
                ViewBag.CurrentImageUrl = board.ImageUrl;
                return View(model);
            }

            _boardService.AutoApproveApplicantsOnJoinPolicyChange(board, id, oldJoinPolicy);

            _boardService.RecalculateStatusAfterBoardSettingChanges(board, oldGroupManagement, oldMaxParticipants);
            
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
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var result = await _boardMembershipService.ApplyAsync(id, userId);
            return ToDetailsResult(result, id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelMyApplication(int id)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var result = await _boardMembershipService.CancelMyApplicationAsync(id, userId);
            return ToDetailsResult(result, id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveApplicant(int boardId, string applicantId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var result = await _boardMembershipService.ApproveApplicantAsync(boardId, applicantId, userId);
            return ToDetailsResult(result, boardId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DenyParticipant(int boardId, string participantId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var result = await _boardMembershipService.DenyParticipantAsync(boardId, participantId, userId);
            return ToDetailsResult(result, boardId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddExternalParticipant(int boardId, string externalName, string? externalNote)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var result = await _boardMembershipService.AddExternalParticipantAsync(boardId, userId, externalName, externalNote);
            return ToDetailsResult(result, boardId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveExternalParticipant(int boardId, int externalParticipantId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var result = await _boardMembershipService.RemoveExternalParticipantAsync(boardId, externalParticipantId, userId);
            return ToDetailsResult(result, boardId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DenyApplicant(int boardId, string applicantId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var result = await _boardMembershipService.DenyApplicantAsync(boardId, applicantId, userId);
            return ToDetailsResult(result, boardId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveDenial(int boardId, string deniedUserId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var result = await _boardMembershipService.RemoveDenialAsync(boardId, deniedUserId, userId);
            return ToDetailsResult(result, boardId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var board = await _context.Boards
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

            if (board.CurrentStatus == BoardStatus.Archived)
            {
                TempData["Error"] = "Board is already archived.";
                return RedirectToAction(nameof(Index));
            }

            if (board.CurrentStatus != BoardStatus.Cancelled && board.EventDate > TimeZoneHelper.UtcNow.UtcDateTime)
            {
                TempData["Error"] = "You can only archive boards after the event date has passed or if the board has been cancelled.";
                return RedirectToAction(nameof(Details), new { id });
            }

            board.CurrentStatus = BoardStatus.Archived;
            _context.Boards.Update(board);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Board has been archived successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var (result, affectedUserIds) = await _boardMembershipService.CancelBoardAsync(id, userId);

            if (result.Status == BoardWorkflowStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == BoardWorkflowStatus.Forbid)
            {
                return Forbid();
            }

            if (result.Status == BoardWorkflowStatus.Error)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction(nameof(Details), new { id });
            }

            var board = await _context.Boards
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id);

            if (board != null && affectedUserIds.Any())
            {
                var notificationsService = HttpContext.RequestServices.GetRequiredService<NotificationsService>();
                await notificationsService.CreateNotificationsForMultipleUsersAsync(
                    affectedUserIds,
                    $"Board Cancelled: {board.Title}",
                    $"The board '{board.Title}' has been cancelled by the author.",
                    NotificationType.IsRejected,
                    boardId: id);
            }

            TempData["Success"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        private bool TryGetCurrentUserId(out string userId)
        {
            userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(userId);
        }

        private IActionResult ToDetailsResult(BoardWorkflowResult result, int boardId)
        {
            if (result.Status == BoardWorkflowStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == BoardWorkflowStatus.Forbid)
            {
                return Forbid();
            }

            TempData[result.Status == BoardWorkflowStatus.Success ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Details), new { id = boardId });
        }
    }
}
