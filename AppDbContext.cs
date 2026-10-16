using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>Code-first model. Table and column names follow the Part 1 SQL script.</summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<RaceResult> RaceResults => Set<RaceResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(x => x.RoleId);
            e.Property(x => x.RoleName).IsRequired().HasMaxLength(50);
            e.HasIndex(x => x.RoleName).IsUnique();
            e.HasData(
                new Role { RoleId = RoleNames.OrganiserRoleId, RoleName = RoleNames.Organiser },
                new Role { RoleId = RoleNames.ParticipantRoleId, RoleName = RoleNames.Participant });
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.UserId);
            e.Property(x => x.FullName).IsRequired().HasMaxLength(100);
            e.Property(x => x.Email).IsRequired().HasMaxLength(150);
            e.Property(x => x.PasswordHash).IsRequired().HasMaxLength(255);
            e.Property(x => x.CreatedAt).HasColumnType("datetime");
            e.HasIndex(x => x.Email).IsUnique();
            e.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.ToTable("Events", t =>
            {
                t.HasCheckConstraint("CK_Events_DistanceKM", "[DistanceKM] > 0");
            });
            e.HasKey(x => x.EventId);
            e.Property(x => x.Title).IsRequired().HasMaxLength(150);
            e.Property(x => x.Description);
            e.Property(x => x.EventDate).HasColumnType("datetime");
            e.Property(x => x.Location).IsRequired().HasMaxLength(150);
            e.Property(x => x.DistanceKM).HasColumnType("decimal(5,2)");
            e.Property(x => x.EventType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnType("datetime");
            e.HasOne(x => x.Organiser).WithMany(u => u.OrganisedEvents).HasForeignKey(x => x.OrganiserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("Categories", t =>
            {
                t.HasCheckConstraint("CK_Categories_DistanceKM", "[DistanceKM] > 0");
                t.HasCheckConstraint("CK_Categories_Fee", "[Fee] >= 0");
            });
            e.HasKey(x => x.CategoryId);
            e.Property(x => x.CategoryName).IsRequired().HasMaxLength(100);
            e.Property(x => x.DistanceKM).HasColumnType("decimal(5,2)");
            e.Property(x => x.Fee).HasColumnType("decimal(10,2)");
            e.HasOne(x => x.Event).WithMany(ev => ev.Categories).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolments", t =>
            {
                t.HasCheckConstraint("CK_Enrolments_PaymentStatus", "[PaymentStatus] IN ('Pending', 'Paid', 'Cancelled')");
            });
            e.HasKey(x => x.EnrolmentId);
            e.Property(x => x.EnrolmentDate).HasColumnType("datetime");
            e.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => new { x.ParticipantId, x.CategoryId }).IsUnique();
            e.HasOne(x => x.Participant).WithMany(u => u.Enrolments).HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany(c => c.Enrolments).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RaceResult>(e =>
        {
            e.ToTable("Results", t =>
            {
                t.HasCheckConstraint("CK_Results_FinishTimeSeconds", "[FinishTimeSeconds] > 0");
                t.HasCheckConstraint("CK_Results_Position", "[Position] > 0");
            });
            e.HasKey(x => x.ResultId);
            e.Property(x => x.RecordedAt).HasColumnType("datetime");
            e.HasIndex(x => x.EnrolmentId).IsUnique();
            e.HasOne(x => x.Enrolment).WithOne(en => en.Result).HasForeignKey<RaceResult>(x => x.EnrolmentId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
