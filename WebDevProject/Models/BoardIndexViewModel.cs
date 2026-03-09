namespace WebDevProject.Models
{
    public class BoardIndexViewModel
    {
        public IReadOnlyList<Board> ActiveBoards { get; init; } = [];

        public IReadOnlyList<Board> ParticipatingBoards { get; init; } = [];

        public IReadOnlyList<Board> ApplyingBoards { get; init; } = [];
    }
}
