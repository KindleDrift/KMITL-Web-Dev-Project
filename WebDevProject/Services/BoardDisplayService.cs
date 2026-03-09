using WebDevProject.Models;

namespace WebDevProject.Services
{
    public class BoardDisplayService
    {
        public bool IsOpenPastDeadline(Board board)
        {
            return board.CurrentStatus == BoardStatus.Open && board.Deadline <= DateTimeOffset.UtcNow.UtcDateTime;
        }

        public string GetBoardStatusClass(Board board)
        {
            var isOpenPastDeadline = IsOpenPastDeadline(board);
            return board.CurrentStatus switch
            {
                BoardStatus.Open => isOpenPastDeadline ? "status-closed" : "status-open",
                BoardStatus.Full => "status-full",
                BoardStatus.Closed => "status-closed",
                BoardStatus.Cancelled => "status-cancelled",
                BoardStatus.Archived => "status-archived",
                _ => "status-open"
            };
        }

        public string GetBoardDisplayStatus(Board board)
        {
            var isOpenPastDeadline = IsOpenPastDeadline(board);
            return isOpenPastDeadline ? "Open (Deadline Passed)" : board.CurrentStatus.ToString();
        }

        public int GetVisibleParticipantCount(Board board)
        {
            var visibleParticipants = board.Participants?.Where(p => p.UserId != board.AuthorId).ToList() ?? new List<BoardParticipant>();
            var externalCount = board.ExternalParticipants?.Count ?? 0;
            return visibleParticipants.Count + externalCount;
        }

        public int GetSpotsLeft(Board board)
        {
            var participantCount = GetVisibleParticipantCount(board);
            return Math.Max(board.MaxParticipants - participantCount, 0);
        }

        public List<BoardParticipant> GetVisibleParticipants(Board board)
        {
            return board.Participants?.Where(p => p.UserId != board.AuthorId).ToList() ?? new List<BoardParticipant>();
        }
    }
}
