using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebDevProject.Data;
using WebDevProject.Filters;
using WebDevProject.Models;
using WebDevProject.Services;

namespace WebDevProject.Controllers
{
    [RequireOnboarding]
    public class BoardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationsService _notificationsService;
        private readonly BoardService _boardService;

        public BoardController(ApplicationDbContext context, NotificationsService notificationsService, BoardService boardService)
        {
            _context = context;
            _notificationsService = notificationsService;
            _boardService = boardService;
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
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SearchBoards(string searchName, string tags, DateTime? eventDateFrom, DateTime? eventDateTo, string statuses, string joinPolicies)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var boardQuery = _context.Boards
                .AsNoTracking()
                .Include(b => b.Author)
                .Include(b => b.Participants)
                    .ThenInclude(bp => bp.User)
                .Include(b => b.ExternalParticipants)
                .Include(b => b.Tags)
                .Where(b => b.CurrentStatus != BoardStatus.Archived);

            // Exclude user's own posts if logged in
            if (!string.IsNullOrWhiteSpace(userId))
            {
                boardQuery = boardQuery.Where(b => b.AuthorId != userId);
            }

            // Filter by name
            if (!string.IsNullOrWhiteSpace(searchName))
            {
                boardQuery = boardQuery.Where(b => EF.Functions.Like(b.Title, $"%{searchName}%"));
            }

            // Filter by tags
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

            // Filter by event date range
            if (eventDateFrom.HasValue)
            {
                boardQuery = boardQuery.Where(b => b.EventDate >= eventDateFrom.Value);
            }

            if (eventDateTo.HasValue)
            {
                // Add one day to include boards on the "to" date
                var eventDateToInclusive = eventDateTo.Value.AddDays(1);
                boardQuery = boardQuery.Where(b => b.EventDate < eventDateToInclusive);
            }

            // Filter by status
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

            // Filter by join policy
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

            var xml = _boardService.BuildBoardsXml(boards);
            return Content(xml, "application/xml");
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

            var oldJoinPolicy = board.JoinPolicy;
            var oldGroupManagement = board.GroupManagementOption;
            var oldMaxParticipants = board.MaxParticipants;

            board.Title = model.Title;
            board.Description = model.Description;
            board.Location = model.Location;
            board.EventDate = model.EventDate;
            board.Deadline = model.Deadline;
            board.MaxParticipants = model.MaxParticipants;
            board.NotifyAuthorOnFull = model.NotifyAuthorOnFull;
            board.GroupManagementOption = _boardService.ParseGroupManagementOption(model.GroupManagementOption);
            board.JoinPolicy = _boardService.ParseJoinPolicyOption(model.JoinPolicyOption);
            
            // Allow manual status override (unless automatic status will be applied)
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

            // Edge Case 1: Join Policy changed from Application to FirstComeFirstServe
            // Auto-approve all pending applicants
            if (oldJoinPolicy == BoardJoinPolicy.Application && board.JoinPolicy == BoardJoinPolicy.FirstComeFirstServe)
            {
                var applicantsToApprove = board.Applicants.ToList();
                foreach (var applicant in applicantsToApprove)
                {
                    var currentOccupied = _boardService.GetOccupiedSeatCount(board);
                    
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
                    }
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
                var currentOccupied = _boardService.GetOccupiedSeatCount(board);
                if (currentOccupied >= board.MaxParticipants && board.CurrentStatus == BoardStatus.Open)
                {
                    board.CurrentStatus = BoardStatus.Full;
                }
            }

            // Edge Case 4: MaxParticipants changed - recalculate status
            if (oldMaxParticipants != board.MaxParticipants)
            {
                var currentOccupied = _boardService.GetOccupiedSeatCount(board);
                
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
            _boardService.UpdateBoardStatusByCapacity(board, _boardService.GetOccupiedSeatCount(board));
            
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

            if (board.AuthorId == userId)
            {
                TempData["Error"] = "You cannot apply to your own board.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (board.Participants.Any(p => p.UserId == userId))
            {
                TempData["Error"] = "You are already a participant.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (board.Applicants.Any(a => a.UserId == userId))
            {
                TempData["Error"] = "You have already applied.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (board.DeniedUsers.Any(d => d.UserId == userId))
            {
                TempData["Error"] = "You have been denied access to this board.";
                return RedirectToAction(nameof(Details), new { id });
            }

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

            var currentOccupied = _boardService.GetOccupiedSeatCount(board);
            if (currentOccupied >= board.MaxParticipants)
            {
                if (board.JoinPolicy == BoardJoinPolicy.FirstComeFirstServe &&
                    board.GroupManagementOption != GroupManagement.AllowOverbooking)
                {
                    TempData["Error"] = "Board is full. Cannot accept more participants.";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            if (board.JoinPolicy == BoardJoinPolicy.FirstComeFirstServe)
            {
                var occupiedBeforeAdd = _boardService.GetOccupiedSeatCount(board);

                var participant = new BoardParticipant
                {
                    BoardId = id,
                    UserId = userId,
                    JoinedAt = DateTime.UtcNow
                };

                _context.BoardParticipants.Add(participant);
                _boardService.UpdateBoardStatusByCapacity(board, occupiedBeforeAdd + 1);
                await _context.SaveChangesAsync();

                // Notify the board author about the new participant
                await _notificationsService.CreateNotificationAsync(
                    board.AuthorId,
                    $"New Participant: {board.Title}",
                    $"A new user has joined your board.",
                    NotificationType.NewRequest,
                    boardId: id,
                    relatedUserId: userId);

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

            // Notify the board author about the new application
            await _notificationsService.CreateNotificationAsync(
                board.AuthorId,
                $"New Application: {board.Title}",
                $"A new user has applied to join your board.",
                NotificationType.NewRequest,
                boardId: id,
                relatedUserId: userId);

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
                var occupiedBeforeRemoval = _boardService.GetOccupiedSeatCount(board);

                _context.BoardParticipants.Remove(participant);

                var occupiedAfterRemoval = Math.Max(occupiedBeforeRemoval - 1, 0);
                _boardService.UpdateBoardStatusByCapacity(board, occupiedAfterRemoval);

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

            if (_boardService.GetOccupiedSeatCount(board) >= board.MaxParticipants &&
                board.GroupManagementOption != GroupManagement.AllowOverbooking)
            {
                TempData["Error"] = "Board is full. Cannot approve more participants.";
                return RedirectToAction(nameof(Details), new { id = boardId });
            }

            var occupiedBeforeAdd = _boardService.GetOccupiedSeatCount(board);

            _context.BoardApplicants.Remove(applicant);

            var participant = new BoardParticipant
            {
                BoardId = boardId,
                UserId = applicantId,
                JoinedAt = DateTime.UtcNow
            };

            _context.BoardParticipants.Add(participant);
            _boardService.UpdateBoardStatusByCapacity(board, occupiedBeforeAdd + 1);

            await _context.SaveChangesAsync();

            // Notify the applicant that they have been accepted
            await _notificationsService.CreateNotificationAsync(
                applicantId,
                $"Accepted: {board.Title}",
                $"Congratulations! You have been accepted to join '{board.Title}'.",
                NotificationType.IsAccepted,
                boardId: boardId);

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

            var occupiedBeforeRemoval = _boardService.GetOccupiedSeatCount(board);

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
            _boardService.UpdateBoardStatusByCapacity(board, occupiedAfterRemoval);

            await _context.SaveChangesAsync();

            // Notify the user they were removed and denied
            await _notificationsService.CreateNotificationAsync(
                participantId,
                $"Removed from Board: {board.Title}",
                $"You have been removed from '{board.Title}' and are no longer able to rejoin.",
                NotificationType.IsRejected,
                boardId: boardId);

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

            if (_boardService.GetOccupiedSeatCount(board) >= board.MaxParticipants &&
                board.GroupManagementOption != GroupManagement.AllowOverbooking)
            {
                TempData["Error"] = "Board is full. Cannot add external participant.";
                return RedirectToAction(nameof(Details), new { id = boardId });
            }

            var occupiedBeforeAdd = _boardService.GetOccupiedSeatCount(board);

            var participant = new BoardExternalParticipant
            {
                BoardId = boardId,
                Name = normalizedName,
                Note = string.IsNullOrWhiteSpace(externalNote) ? null : externalNote.Trim(),
                AddedAt = DateTime.UtcNow
            };

            _context.BoardExternalParticipants.Add(participant);
            _boardService.UpdateBoardStatusByCapacity(board, occupiedBeforeAdd + 1);

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

            var occupiedBeforeRemoval = _boardService.GetOccupiedSeatCount(board);

            _context.BoardExternalParticipants.Remove(externalParticipant);

            var occupiedAfterRemoval = Math.Max(occupiedBeforeRemoval - 1, 0);
            _boardService.UpdateBoardStatusByCapacity(board, occupiedAfterRemoval);

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

            // Notify the applicant they were rejected
            await _notificationsService.CreateNotificationAsync(
                applicantId,
                $"Application Rejected: {board.Title}",
                $"Your application for '{board.Title}' has been rejected.",
                NotificationType.IsRejected,
                boardId: boardId);

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
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

            // Optional: Check if event date has passed
            if (board.EventDate > DateTime.UtcNow)
            {
                TempData["Error"] = "You can only archive boards after the event date has passed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            board.CurrentStatus = BoardStatus.Archived;
            _context.Boards.Update(board);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Board has been archived successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
