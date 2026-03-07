using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Xml;
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

            // Build XML response
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = false,
                Encoding = Encoding.UTF8
            };

            using (var writer = XmlWriter.Create(sb, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Boards");

                foreach (var b in boards)
                {
                    var visibleParticipants = b.Participants.Where(p => p.UserId != b.AuthorId).ToList();
                    var participantCount = visibleParticipants.Count + b.ExternalParticipants.Count;
                    var spotsLeft = Math.Max(b.MaxParticipants - participantCount, 0);
                    var isOpenPastDeadline = b.CurrentStatus == BoardStatus.Open && b.Deadline <= DateTime.UtcNow;

                    writer.WriteStartElement("Board");

                    writer.WriteElementString("Id", b.Id.ToString());
                    writer.WriteElementString("Title", b.Title);
                    writer.WriteElementString("Description", b.Description);
                    writer.WriteElementString("ImageUrl", string.IsNullOrWhiteSpace(b.ImageUrl) ? "/images/default-board.png" : b.ImageUrl);
                    writer.WriteElementString("Status", b.CurrentStatus.ToString());
                    writer.WriteElementString("DisplayStatus", isOpenPastDeadline ? "Open (Deadline Passed)" : b.CurrentStatus.ToString());
                    
                    var statusClass = b.CurrentStatus switch
                    {
                        BoardStatus.Open => isOpenPastDeadline ? "status-closed" : "status-open",
                        BoardStatus.Full => "status-full",
                        BoardStatus.Closed => "status-closed",
                        BoardStatus.Cancelled => "status-cancelled",
                        BoardStatus.Archived => "status-archived",
                        _ => "status-open"
                    };
                    writer.WriteElementString("StatusClass", statusClass);

                    writer.WriteElementString("EventDate", b.EventDate.ToString("dd MMM yyyy"));
                    writer.WriteElementString("EventTime", b.EventDate.ToString("HH:mm"));
                    writer.WriteElementString("Deadline", b.Deadline.ToString("dd MMM yyyy, HH:mm"));
                    writer.WriteElementString("Location", b.Location);

                    // Tags
                    writer.WriteStartElement("Tags");
                    foreach (var tag in b.Tags)
                    {
                        writer.WriteElementString("Tag", tag.Name);
                    }
                    writer.WriteEndElement(); // Tags

                    writer.WriteElementString("JoinPolicy", b.JoinPolicy.ToString());
                    writer.WriteElementString("JoinPolicyDisplay", b.JoinPolicy == BoardJoinPolicy.FirstComeFirstServe ? "First Come First Serve" : "Application");
                    writer.WriteElementString("CurrentParticipants", participantCount.ToString());
                    writer.WriteElementString("MaxParticipants", b.MaxParticipants.ToString());
                    writer.WriteElementString("SpotsLeft", spotsLeft.ToString());

                    // Author
                    writer.WriteStartElement("Author");
                    writer.WriteElementString("DisplayName", b.Author?.DisplayName ?? "Unknown");
                    writer.WriteElementString("ProfilePictureUrl", b.Author?.ProfilePictureUrl ?? "/images/default-profile.png");
                    writer.WriteEndElement(); // Author

                    // Preview Participants
                    writer.WriteStartElement("PreviewParticipants");
                    var previewParticipants = visibleParticipants.Take(5);
                    foreach (var participant in previewParticipants)
                    {
                        writer.WriteStartElement("Participant");
                        writer.WriteElementString("ProfilePictureUrl", participant.User?.ProfilePictureUrl ?? "/images/default-profile.png");
                        writer.WriteElementString("DisplayName", participant.User?.DisplayName ?? "Participant");
                        writer.WriteEndElement(); // Participant
                    }
                    writer.WriteEndElement(); // PreviewParticipants

                    writer.WriteElementString("TotalVisibleParticipants", visibleParticipants.Count.ToString());

                    writer.WriteEndElement(); // Board
                }

                writer.WriteEndElement(); // Boards
                writer.WriteEndDocument();
            }

            return Content(sb.ToString(), "application/xml");
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
                GroupManagementOption = ParseGroupManagementOption(model.GroupManagementOption),
                JoinPolicy = ParseJoinPolicyOption(model.JoinPolicyOption)
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
                JoinPolicyOption = ToJoinPolicyOptionValue(board.JoinPolicy),
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
            board.GroupManagementOption = ParseGroupManagementOption(model.GroupManagementOption);
            board.JoinPolicy = ParseJoinPolicyOption(model.JoinPolicyOption);
            
            // Allow manual status override (unless automatic status will be applied)
            if (model.CurrentStatus.HasValue)
            {
                board.CurrentStatus = model.CurrentStatus.Value;
            }

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

            // Edge Case 1: Join Policy changed from Application to FirstComeFirstServe
            // Auto-approve all pending applicants
            if (oldJoinPolicy == BoardJoinPolicy.Application && board.JoinPolicy == BoardJoinPolicy.FirstComeFirstServe)
            {
                var applicantsToApprove = board.Applicants.ToList();
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

            var currentOccupied = GetOccupiedSeatCount(board);
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

            if (GetOccupiedSeatCount(board) >= board.MaxParticipants &&
                board.GroupManagementOption != GroupManagement.AllowOverbooking)
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

            if (GetOccupiedSeatCount(board) >= board.MaxParticipants &&
                board.GroupManagementOption != GroupManagement.AllowOverbooking)
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

        private static bool IsValidTag(string tag)
        {
            if (tag.StartsWith('-') || tag.EndsWith('-'))
                return false;

            if (tag.Any(char.IsDigit))
                return false;

            for (int i = 0; i < tag.Length; i++)
            {
                char c = tag[i];

                if (char.IsLetter(c))
                    continue;

                if (c == '-')
                {
                    if (i > 0 && tag[i - 1] == '-')
                        return false;
                    continue;
                }

                return false;
            }

            return true;
        }

        private static string FormatTag(string tag)
        {
            tag = tag.ToLowerInvariant();

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

        private static BoardJoinPolicy ParseJoinPolicyOption(string? option)
        {
            return option switch
            {
                "fcfs" => BoardJoinPolicy.FirstComeFirstServe,
                _ => BoardJoinPolicy.Application
            };
        }

        private static string ToJoinPolicyOptionValue(BoardJoinPolicy option)
        {
            return option switch
            {
                BoardJoinPolicy.FirstComeFirstServe => "fcfs",
                _ => "application"
            };
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
