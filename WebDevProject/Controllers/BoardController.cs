using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDevProject.Data;
using WebDevProject.Models;

namespace WebDevProject.Controllers
{
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
                    .ThenInclude(bp => bp.User);

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
                ? []
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
        
        public IActionResult Search()
        {
            var boardQuery = _context.Boards
                .AsNoTracking()
                .Include(b => b.Author)
                .Include(b => b.Participants);
                
            var existingBoards = boardQuery
                .Where(b => b.CurrentStatus != BoardStatus.Archived)
                .OrderByDescending(b => b.CreatedAt)
                .ToList();

            return View(existingBoards);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Board board)
        {
            if (!ModelState.IsValid)
            {
                return View(board);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Unauthorized();
            }

            board.AuthorId = userId;
            board.CreatedAt = DateTime.UtcNow;
            board.CurrentStatus = BoardStatus.Open;

            _context.Boards.Add(board);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
        
        public IActionResult BoardInfo()
        {
            return View();
        }
    }
}
