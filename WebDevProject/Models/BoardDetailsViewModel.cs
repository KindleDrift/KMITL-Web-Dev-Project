namespace WebDevProject.Models
{
    public class BoardDetailsViewModel
    {
        public Board Board { get; set; } = null!;
        public bool IsAuthor { get; set; }
        public ApplicationStatus UserApplicationStatus { get; set; }
        public List<BoardParticipant> Participants { get; set; } = [];
        public List<BoardExternalParticipant> ExternalParticipants { get; set; } = [];
        public List<BoardApplicant> Applicants { get; set; } = [];
        public List<BoardDenied> DeniedUsers { get; set; } = [];

        public int OccupiedSlots => Participants.Count + ExternalParticipants.Count;
    }

    public enum ApplicationStatus
    {
        NotApplied,
        Pending,
        Approved,
        Denied
    }
}
