using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebDevProject.Models;

namespace WebDevProject.Data
{
    public class ApplicationDbContext : IdentityDbContext<Users>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Board> Boards => Set<Board>();

        public DbSet<BoardParticipant> BoardParticipants => Set<BoardParticipant>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Board>()
                .HasOne(b => b.Author)
                .WithMany(u => u.AuthoredBoards)
                .HasForeignKey(b => b.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<BoardParticipant>()
                .HasKey(bp => new { bp.BoardId, bp.UserId });

            builder.Entity<BoardParticipant>()
                .HasOne(bp => bp.Board)
                .WithMany(b => b.Participants)
                .HasForeignKey(bp => bp.BoardId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<BoardParticipant>()
                .HasOne(bp => bp.User)
                .WithMany(u => u.BoardParticipations)
                .HasForeignKey(bp => bp.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
