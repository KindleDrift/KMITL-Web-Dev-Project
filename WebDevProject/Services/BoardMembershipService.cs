using Microsoft.EntityFrameworkCore;
using WebDevProject.Data;
using WebDevProject.Models;

namespace WebDevProject.Services
{
    public enum BoardWorkflowStatus
    {
        Success,
        NotFound,
        Forbid,
        Error
    }

    public record BoardWorkflowResult(BoardWorkflowStatus Status, string Message)
    {
        public static BoardWorkflowResult Success(string message) => new(BoardWorkflowStatus.Success, message);
        public static BoardWorkflowResult NotFound(string message) => new(BoardWorkflowStatus.NotFound, message);
        public static BoardWorkflowResult Forbid(string message) => new(BoardWorkflowStatus.Forbid, message);
        public static BoardWorkflowResult Error(string message) => new(BoardWorkflowStatus.Error, message);
    }

    public class BoardMembershipService
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationsService _notificationsService;
        private readonly BoardService _boardService;

        public BoardMembershipService(ApplicationDbContext context, NotificationsService notificationsService, BoardService boardService)
        {
            _context = context;
            _notificationsService = notificationsService;
            _boardService = boardService;
        }

        public async Task<BoardWorkflowResult> ApplyAsync(int boardId, string userId)
        {
            var board = await _context.Boards
                .Include(b => b.Participants)
                .Include(b => b.ExternalParticipants)
                .Include(b => b.Applicants)
                .Include(b => b.DeniedUsers)
                .FirstOrDefaultAsync(b => b.Id == boardId);

            if (board == null)
            {
                return BoardWorkflowResult.NotFound("Board not found.");
            }

            if (board.AuthorId == userId)
            {
                return BoardWorkflowResult.Error("You cannot apply to your own board.");
            }

            if (board.Participants.Any(p => p.UserId == userId))
            {
                return BoardWorkflowResult.Error("You are already a participant.");
            }

            if (board.Applicants.Any(a => a.UserId == userId))
            {
                return BoardWorkflowResult.Error("You have already applied.");
            }

            if (board.DeniedUsers.Any(d => d.UserId == userId))
            {
                return BoardWorkflowResult.Error("You have been denied access to this board.");
            }

            if (board.CurrentStatus != BoardStatus.Open)
            {
                return BoardWorkflowResult.Error("This board is not accepting applications.");
            }

            if (board.Deadline <= DateTimeOffset.UtcNow.UtcDateTime)
            {
                return BoardWorkflowResult.Error("The registration deadline has passed.");
            }

            var currentOccupied = _boardService.GetOccupiedSeatCount(board);
            if (currentOccupied >= board.MaxParticipants &&
                board.JoinPolicy == BoardJoinPolicy.FirstComeFirstServe &&
                board.GroupManagementOption != GroupManagement.AllowOverbooking)
            {
                return BoardWorkflowResult.Error("Board is full. Cannot accept more participants.");
            }

            if (board.JoinPolicy == BoardJoinPolicy.FirstComeFirstServe)
            {
                var participant = new BoardParticipant
                {
                    BoardId = boardId,
                    UserId = userId,
                    JoinedAt = DateTimeOffset.UtcNow.UtcDateTime
                };

                _context.BoardParticipants.Add(participant);
                
                var newOccupiedCount = currentOccupied + 1;
                var wasFull = currentOccupied >= board.MaxParticipants;
                var isNowFull = newOccupiedCount >= board.MaxParticipants;
                
                _boardService.UpdateBoardStatusByCapacity(board, newOccupiedCount);
                await _context.SaveChangesAsync();

                await _notificationsService.CreateNotificationAsync(
                    board.AuthorId,
                    $"New Participant: {board.Title}",
                    "A new user has joined your board.",
                    NotificationType.NewRequest,
                    boardId: boardId,
                    relatedUserId: userId);
                
                if (!wasFull && isNowFull && board.NotifyAuthorOnFull)
                {
                    await _notificationsService.CreateNotificationAsync(
                        board.AuthorId,
                        $"Board Full: {board.Title}",
                        $"Your board '{board.Title}' is now full!",
                        NotificationType.BoardFull,
                        boardId: boardId);
                }

                return BoardWorkflowResult.Success("You joined this board successfully.");
            }

            _context.BoardApplicants.Add(new BoardApplicant
            {
                BoardId = boardId,
                UserId = userId,
                AppliedAt = DateTimeOffset.UtcNow.UtcDateTime
            });

            await _context.SaveChangesAsync();

            await _notificationsService.CreateNotificationAsync(
                board.AuthorId,
                $"New Application: {board.Title}",
                "A new user has applied to join your board.",
                NotificationType.NewRequest,
                boardId: boardId,
                relatedUserId: userId);

            return BoardWorkflowResult.Success("Your application has been submitted successfully.");
        }

        public async Task<BoardWorkflowResult> CancelMyApplicationAsync(int boardId, string userId)
        {
            var board = await _context.Boards
                .Include(b => b.Participants)
                .Include(b => b.ExternalParticipants)
                .Include(b => b.Applicants)
                .FirstOrDefaultAsync(b => b.Id == boardId);

            if (board == null)
            {
                return BoardWorkflowResult.NotFound("Board not found.");
            }

            if (board.AuthorId == userId)
            {
                return BoardWorkflowResult.Error("Board owner cannot use this action.");
            }

            var applicant = board.Applicants.FirstOrDefault(a => a.UserId == userId);
            if (applicant != null)
            {
                _context.BoardApplicants.Remove(applicant);
                await _context.SaveChangesAsync();
                return BoardWorkflowResult.Success("Your application has been cancelled.");
            }

            var participant = board.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant != null)
            {
                var occupiedBeforeRemoval = _boardService.GetOccupiedSeatCount(board);
                _context.BoardParticipants.Remove(participant);
                _boardService.UpdateBoardStatusByCapacity(board, Math.Max(occupiedBeforeRemoval - 1, 0));
                await _context.SaveChangesAsync();
                return BoardWorkflowResult.Success("You left the board successfully.");
            }

            return BoardWorkflowResult.Error("You have no active application or participation in this board.");
        }

        public async Task<BoardWorkflowResult> ApproveApplicantAsync(int boardId, string applicantId, string userId)
        {
            var board = await _context.Boards
                .Include(b => b.Participants)
                .Include(b => b.ExternalParticipants)
                .Include(b => b.Applicants)
                .FirstOrDefaultAsync(b => b.Id == boardId);

            if (board == null)
            {
                return BoardWorkflowResult.NotFound("Board not found.");
            }

            if (board.AuthorId != userId)
            {
                return BoardWorkflowResult.Forbid("Forbidden.");
            }

            if (board.CurrentStatus == BoardStatus.Cancelled || board.CurrentStatus == BoardStatus.Archived)
            {
                return BoardWorkflowResult.Error("Cannot modify participants for cancelled or archived boards.");
            }

            var applicant = board.Applicants.FirstOrDefault(a => a.UserId == applicantId);
            if (applicant == null)
            {
                return BoardWorkflowResult.Error("Applicant not found.");
            }

            var occupiedBeforeAdd = _boardService.GetOccupiedSeatCount(board);
            if (occupiedBeforeAdd >= board.MaxParticipants && board.GroupManagementOption != GroupManagement.AllowOverbooking)
            {
                return BoardWorkflowResult.Error("Board is full. Cannot approve more participants.");
            }

            _context.BoardApplicants.Remove(applicant);
            _context.BoardParticipants.Add(new BoardParticipant
            {
                BoardId = boardId,
                UserId = applicantId,
                JoinedAt = DateTimeOffset.UtcNow.UtcDateTime
            });

            var newOccupiedCount = occupiedBeforeAdd + 1;
            var wasFull = occupiedBeforeAdd >= board.MaxParticipants;
            var isNowFull = newOccupiedCount >= board.MaxParticipants;
            
            _boardService.UpdateBoardStatusByCapacity(board, newOccupiedCount);
            await _context.SaveChangesAsync();

            await _notificationsService.CreateNotificationAsync(
                applicantId,
                $"Accepted: {board.Title}",
                $"Congratulations! You have been accepted to join '{board.Title}'.",
                NotificationType.IsAccepted,
                boardId: boardId);

            if (!wasFull && isNowFull && board.NotifyAuthorOnFull)
            {
                await _notificationsService.CreateNotificationAsync(
                    board.AuthorId,
                    $"Board Full: {board.Title}",
                    $"Your board '{board.Title}' is now full!",
                    NotificationType.BoardFull,
                    boardId: boardId);
            }

            return BoardWorkflowResult.Success("Applicant approved successfully.");
        }

        public async Task<BoardWorkflowResult> DenyParticipantAsync(int boardId, string participantId, string userId)
        {
            var board = await _context.Boards
                .Include(b => b.Participants)
                .Include(b => b.ExternalParticipants)
                .Include(b => b.DeniedUsers)
                .FirstOrDefaultAsync(b => b.Id == boardId);

            if (board == null)
            {
                return BoardWorkflowResult.NotFound("Board not found.");
            }

            if (board.AuthorId != userId)
            {
                return BoardWorkflowResult.Forbid("Forbidden.");
            }

            if (board.CurrentStatus == BoardStatus.Cancelled || board.CurrentStatus == BoardStatus.Archived)
            {
                return BoardWorkflowResult.Error("Cannot modify participants for cancelled or archived boards.");
            }

            var participant = board.Participants.FirstOrDefault(p => p.UserId == participantId);
            if (participant == null)
            {
                return BoardWorkflowResult.Error("Participant not found.");
            }

            var occupiedBeforeRemoval = _boardService.GetOccupiedSeatCount(board);
            _context.BoardParticipants.Remove(participant);

            if (!board.DeniedUsers.Any(d => d.UserId == participantId))
            {
                _context.BoardDenied.Add(new BoardDenied
                {
                    BoardId = boardId,
                    UserId = participantId,
                    DeniedAt = DateTimeOffset.UtcNow.UtcDateTime
                });
            }

            _boardService.UpdateBoardStatusByCapacity(board, Math.Max(occupiedBeforeRemoval - 1, 0));
            await _context.SaveChangesAsync();

            await _notificationsService.CreateNotificationAsync(
                participantId,
                $"Removed from Board: {board.Title}",
                $"You have been removed from '{board.Title}' and are no longer able to rejoin.",
                NotificationType.IsRejected,
                boardId: boardId);

            return BoardWorkflowResult.Success("Participant removed and denied.");
        }

        public async Task<BoardWorkflowResult> AddExternalParticipantAsync(int boardId, string userId, string externalName, string? externalNote)
        {
            var board = await _context.Boards
                .Include(b => b.Participants)
                .Include(b => b.ExternalParticipants)
                .FirstOrDefaultAsync(b => b.Id == boardId);

            if (board == null)
            {
                return BoardWorkflowResult.NotFound("Board not found.");
            }

            if (board.AuthorId != userId)
            {
                return BoardWorkflowResult.Forbid("Forbidden.");
            }

            if (board.CurrentStatus == BoardStatus.Cancelled || board.CurrentStatus == BoardStatus.Archived)
            {
                return BoardWorkflowResult.Error("Cannot add participants to cancelled or archived boards.");
            }

            var normalizedName = externalName?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return BoardWorkflowResult.Error("External participant name is required.");
            }

            if (normalizedName.Length > 120)
            {
                return BoardWorkflowResult.Error("External participant name is too long.");
            }

            var occupiedBeforeAdd = _boardService.GetOccupiedSeatCount(board);
            if (occupiedBeforeAdd >= board.MaxParticipants && board.GroupManagementOption != GroupManagement.AllowOverbooking)
            {
                return BoardWorkflowResult.Error("Board is full. Cannot add external participant.");
            }

            _context.BoardExternalParticipants.Add(new BoardExternalParticipant
            {
                BoardId = boardId,
                Name = normalizedName,
                Note = string.IsNullOrWhiteSpace(externalNote) ? null : externalNote.Trim(),
                AddedAt = DateTimeOffset.UtcNow.UtcDateTime
            });

            _boardService.UpdateBoardStatusByCapacity(board, occupiedBeforeAdd + 1);
            await _context.SaveChangesAsync();

            return BoardWorkflowResult.Success("External participant added.");
        }

        public async Task<BoardWorkflowResult> RemoveExternalParticipantAsync(int boardId, int externalParticipantId, string userId)
        {
            var board = await _context.Boards
                .Include(b => b.Participants)
                .Include(b => b.ExternalParticipants)
                .FirstOrDefaultAsync(b => b.Id == boardId);

            if (board == null)
            {
                return BoardWorkflowResult.NotFound("Board not found.");
            }

            if (board.AuthorId != userId)
            {
                return BoardWorkflowResult.Forbid("Forbidden.");
            }

            if (board.CurrentStatus == BoardStatus.Cancelled || board.CurrentStatus == BoardStatus.Archived)
            {
                return BoardWorkflowResult.Error("Cannot modify participants for cancelled or archived boards.");
            }

            var externalParticipant = board.ExternalParticipants.FirstOrDefault(e => e.Id == externalParticipantId);
            if (externalParticipant == null)
            {
                return BoardWorkflowResult.Error("External participant not found.");
            }

            var occupiedBeforeRemoval = _boardService.GetOccupiedSeatCount(board);
            _context.BoardExternalParticipants.Remove(externalParticipant);
            _boardService.UpdateBoardStatusByCapacity(board, Math.Max(occupiedBeforeRemoval - 1, 0));
            await _context.SaveChangesAsync();

            return BoardWorkflowResult.Success("External participant removed.");
        }

        public async Task<BoardWorkflowResult> DenyApplicantAsync(int boardId, string applicantId, string userId)
        {
            var board = await _context.Boards
                .Include(b => b.Applicants)
                .Include(b => b.DeniedUsers)
                .FirstOrDefaultAsync(b => b.Id == boardId);

            if (board == null)
            {
                return BoardWorkflowResult.NotFound("Board not found.");
            }

            if (board.AuthorId != userId)
            {
                return BoardWorkflowResult.Forbid("Forbidden.");
            }

            if (board.CurrentStatus == BoardStatus.Cancelled || board.CurrentStatus == BoardStatus.Archived)
            {
                return BoardWorkflowResult.Error("Cannot modify applicants for cancelled or archived boards.");
            }

            var applicant = board.Applicants.FirstOrDefault(a => a.UserId == applicantId);
            if (applicant == null)
            {
                return BoardWorkflowResult.Error("Applicant not found.");
            }

            _context.BoardApplicants.Remove(applicant);
            _context.BoardDenied.Add(new BoardDenied
            {
                BoardId = boardId,
                UserId = applicantId,
                DeniedAt = DateTimeOffset.UtcNow.UtcDateTime
            });

            await _context.SaveChangesAsync();

            await _notificationsService.CreateNotificationAsync(
                applicantId,
                $"Application Rejected: {board.Title}",
                $"Your application for '{board.Title}' has been rejected.",
                NotificationType.IsRejected,
                boardId: boardId);

            return BoardWorkflowResult.Success("Applicant denied.");
        }

        public async Task<BoardWorkflowResult> RemoveDenialAsync(int boardId, string deniedUserId, string userId)
        {
            var board = await _context.Boards
                .Include(b => b.DeniedUsers)
                .FirstOrDefaultAsync(b => b.Id == boardId);

            if (board == null)
            {
                return BoardWorkflowResult.NotFound("Board not found.");
            }

            if (board.AuthorId != userId)
            {
                return BoardWorkflowResult.Forbid("Forbidden.");
            }

            if (board.CurrentStatus == BoardStatus.Cancelled || board.CurrentStatus == BoardStatus.Archived)
            {
                return BoardWorkflowResult.Error("Cannot modify denied users for cancelled or archived boards.");
            }

            var deniedUser = board.DeniedUsers.FirstOrDefault(d => d.UserId == deniedUserId);
            if (deniedUser == null)
            {
                return BoardWorkflowResult.Error("Denied user not found.");
            }

            _context.BoardDenied.Remove(deniedUser);
            await _context.SaveChangesAsync();

            return BoardWorkflowResult.Success("User removed from deny list.");
        }

        public async Task<(BoardWorkflowResult Result, List<string> AffectedUserIds)> CancelBoardAsync(int boardId, string userId)
        {
            var board = await _context.Boards
                .Include(b => b.Participants)
                .Include(b => b.ExternalParticipants)
                .Include(b => b.Applicants)
                .FirstOrDefaultAsync(b => b.Id == boardId);

            if (board == null)
            {
                return (BoardWorkflowResult.NotFound("Board not found."), new List<string>());
            }

            if (board.AuthorId != userId)
            {
                return (BoardWorkflowResult.Forbid("Forbidden."), new List<string>());
            }

            if (board.CurrentStatus == BoardStatus.Cancelled)
            {
                return (BoardWorkflowResult.Error("Board is already cancelled."), new List<string>());
            }

            var affectedUserIds = new List<string>();
            
            affectedUserIds.AddRange(board.Participants.Where(p => p.UserId != board.AuthorId).Select(p => p.UserId));
            
            affectedUserIds.AddRange(board.Applicants.Select(a => a.UserId));
            
            var participantsToRemove = board.Participants.Where(p => p.UserId != board.AuthorId).ToList();
            _context.BoardParticipants.RemoveRange(participantsToRemove);
            
            _context.BoardExternalParticipants.RemoveRange(board.ExternalParticipants);
            
            foreach (var applicant in board.Applicants.ToList())
            {
                _context.BoardApplicants.Remove(applicant);
                _context.BoardDenied.Add(new BoardDenied
                {
                    BoardId = boardId,
                    UserId = applicant.UserId,
                    DeniedAt = DateTimeOffset.UtcNow.UtcDateTime
                });
            }

            board.CurrentStatus = BoardStatus.Cancelled;

            await _context.SaveChangesAsync();

            return (BoardWorkflowResult.Success("Board cancelled successfully."), affectedUserIds.Distinct().ToList());
        }
    }
}
