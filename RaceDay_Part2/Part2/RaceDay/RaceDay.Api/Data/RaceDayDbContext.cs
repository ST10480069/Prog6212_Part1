using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// EF Core code-first model. Table, column, key and constraint names follow the
/// Part 1 SQL script (singular table names, UQ_/FK_/CK_ constraint names).
/// </summary>
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Role>(e =>
        {
            e.ToTable("Role", t => t.HasCheckConstraint("CK_Role_RoleName",
                "[RoleName] IN ('Organiser', 'Participant')"));
            e.Property(r => r.RoleName).HasMaxLength(20).IsRequired();
            e.HasIndex(r => r.RoleName).IsUnique().HasDatabaseName("UQ_Role_RoleName");
            // The two roles are fixed, so they are seeded by the migration.
            e.HasData(
                new Role { RoleId = 1, RoleName = RoleNames.Organiser },
                new Role { RoleId = 2, RoleName = RoleNames.Participant });
        });

        mb.Entity<AppUser>(e =>
        {
            e.ToTable("User");
            e.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(150).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
            e.Property(u => u.PhoneNumber).HasMaxLength(20);
            e.Property(u => u.CreatedAt).HasDefaultValueSql("SYSDATETIME()");
            e.HasIndex(u => u.Email).IsUnique().HasDatabaseName("UQ_User_Email");
            e.HasOne(u => u.Role).WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .HasConstraintName("FK_User_Role")
                .OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Event>(e =>
        {
            e.ToTable("Event", t =>
            {
                t.HasCheckConstraint("CK_Event_Status",
                    "[Status] IN ('Draft', 'Published', 'Cancelled', 'Completed')");
                t.HasCheckConstraint("CK_Event_EventType",
                    "[EventType] IN ('Run', 'Walk', 'Cycle')");
                t.HasCheckConstraint("CK_Event_DistanceKm", "[DistanceKm] > 0");
            });
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Location).HasMaxLength(200).IsRequired();
            e.Property(x => x.DistanceKm).HasPrecision(5, 2);
            e.Property(x => x.EventType).HasMaxLength(10).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(EventStatuses.Published);
            e.HasOne(x => x.Organiser).WithMany(u => u.OrganisedEvents)
                .HasForeignKey(x => x.OrganiserId)
                .HasConstraintName("FK_Event_Organiser")
                .OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Category>(e =>
        {
            e.ToTable("Category", t =>
            {
                t.HasCheckConstraint("CK_Category_CategoryType",
                    "[CategoryType] IN ('Distance', 'Age')");
                t.HasCheckConstraint("CK_Category_Details",
                    "([CategoryType] = 'Distance' AND [DistanceKm] IS NOT NULL) " +
                    "OR ([CategoryType] = 'Age' AND [MinAge] IS NOT NULL)");
            });
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
            e.Property(c => c.CategoryType).HasMaxLength(10).IsRequired();
            e.Property(c => c.DistanceKm).HasPrecision(5, 2);
            e.Property(c => c.Price).HasPrecision(8, 2).HasDefaultValue(0m);
            e.Property(c => c.MaxParticipants).HasDefaultValue(100);
            e.HasIndex(c => new { c.EventId, c.Name }).IsUnique().HasDatabaseName("UQ_Category_EventName");
            e.HasOne(c => c.Event).WithMany(x => x.Categories)
                .HasForeignKey(c => c.EventId)
                .HasConstraintName("FK_Category_Event")
                .OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolment", t => t.HasCheckConstraint("CK_Enrolment_Status",
                "[Status] IN ('Confirmed', 'Cancelled')"));
            e.Property(x => x.EnrolmentDate).HasDefaultValueSql("SYSDATETIME()");
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(EnrolmentStatuses.Confirmed);
            e.HasIndex(x => new { x.ParticipantId, x.CategoryId }).IsUnique()
                .HasDatabaseName("UQ_Enrolment_ParticipantCategory");
            e.HasIndex(x => new { x.CategoryId, x.BibNumber }).IsUnique()
                .HasDatabaseName("UQ_Enrolment_CategoryBib");
            e.HasOne(x => x.Participant).WithMany(u => u.Enrolments)
                .HasForeignKey(x => x.ParticipantId)
                .HasConstraintName("FK_Enrolment_Participant")
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.CategoryId)
                .HasConstraintName("FK_Enrolment_Category")
                .OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Result>(e =>
        {
            e.ToTable("Result", t => t.HasCheckConstraint("CK_Result_Status",
                "[Status] IN ('Finished', 'DNF', 'DQ')"));
            e.Property(r => r.Status).HasMaxLength(20).HasDefaultValue(ResultStatuses.Finished);
            // Unique index = one optional result per enrolment (1 : 0..1)
            e.HasIndex(r => r.EnrolmentId).IsUnique().HasDatabaseName("UQ_Result_Enrolment");
            e.HasOne(r => r.Enrolment).WithOne(x => x.Result)
                .HasForeignKey<Result>(r => r.EnrolmentId)
                .HasConstraintName("FK_Result_Enrolment")
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
