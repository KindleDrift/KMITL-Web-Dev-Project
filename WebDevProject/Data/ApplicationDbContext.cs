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

        public DbSet<BoardExternalParticipant> BoardExternalParticipants => Set<BoardExternalParticipant>();

        public DbSet<BoardApplicant> BoardApplicants => Set<BoardApplicant>();

        public DbSet<BoardDenied> BoardDenied => Set<BoardDenied>();

        public DbSet<Tag> Tags => Set<Tag>();

        public DbSet<Notification> Notifications => Set<Notification>();

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

            builder.Entity<BoardExternalParticipant>()
                .HasKey(be => be.Id);

            builder.Entity<BoardExternalParticipant>()
                .HasOne(be => be.Board)
                .WithMany(b => b.ExternalParticipants)
                .HasForeignKey(be => be.BoardId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<BoardApplicant>()
                .HasKey(ba => new { ba.BoardId, ba.UserId });

            builder.Entity<BoardApplicant>()
                .HasOne(ba => ba.Board)
                .WithMany(b => b.Applicants)
                .HasForeignKey(ba => ba.BoardId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<BoardApplicant>()
                .HasOne(ba => ba.User)
                .WithMany(u => u.BoardApplications)
                .HasForeignKey(ba => ba.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<BoardDenied>()
                .HasKey(bd => new { bd.BoardId, bd.UserId });

            builder.Entity<BoardDenied>()
                .HasOne(bd => bd.Board)
                .WithMany(b => b.DeniedUsers)
                .HasForeignKey(bd => bd.BoardId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<BoardDenied>()
                .HasOne(bd => bd.User)
                .WithMany(u => u.BoardDenials)
                .HasForeignKey(bd => bd.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Board>()
                .HasMany(b => b.Tags)
                .WithMany(t => t.Boards);

            builder.Entity<Tag>()
                .HasIndex(t => t.Name)
                .IsUnique();
        }
    }
}
