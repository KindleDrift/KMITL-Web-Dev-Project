using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDevProject.Data;
using WebDevProject.Models;

namespace WebDevProject.Controllers
{
    [Authorize(Policy = "AdminOnly")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Users> _userManager;

        public AdminController(ApplicationDbContext context, UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
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
        public ActionResult Boards()
        {
            return View();
        }

        // Admin/Edit/{id} - GET: Show edit form
        public async Task<ActionResult> Edit(string id)
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

        // Admin/Edit/{id} - POST: Update user
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(string id, [Bind("Id,DisplayName,DateOfBirth,UserGender,Bio,HasCompletedOnboarding")] Users model)
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
    }
}
