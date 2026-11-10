using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<Hold> Holds => Set<Hold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SchoolClass>(e =>
        {
            e.ToTable("classes", t => t.HasCheckConstraint("ck_classes_total_seats_positive", "total_seats > 0"));
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("id");
            e.Property(c => c.Name).HasColumnName("name").IsRequired();
            e.Property(c => c.TotalSeats).HasColumnName("total_seats");
            e.Property(c => c.CreatedAt).HasColumnName("created_at");
            e.HasIndex(c => c.CreatedAt).HasDatabaseName("ix_classes_created_at");
        });

        modelBuilder.Entity<Hold>(e =>
        {
            e.ToTable("holds", t => t.HasCheckConstraint(
                "ck_holds_status", "status IN ('HELD','CONFIRMED','EXPIRED','CANCELLED')"));
            e.HasKey(h => h.Id);
            e.Property(h => h.Id).HasColumnName("id");
            e.Property(h => h.ClassId).HasColumnName("class_id").IsRequired();
            e.Property(h => h.StudentId).HasColumnName("student_id").HasMaxLength(17).IsRequired();
            e.Property(h => h.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            e.Property(h => h.CreatedAt).HasColumnName("created_at");
            e.Property(h => h.ExpiresAt).HasColumnName("expires_at");
            e.Property(h => h.ConfirmedAt).HasColumnName("confirmed_at");
            e.Property(h => h.CancelledAt).HasColumnName("cancelled_at");
            e.Property(h => h.UpdatedAt).HasColumnName("updated_at");

            e.HasOne<SchoolClass>().WithMany().HasForeignKey(h => h.ClassId)
                .HasConstraintName("fk_holds_classes_class_id").OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(h => new { h.ClassId, h.Status, h.ExpiresAt })
                .HasDatabaseName("ix_holds_class_status_expires");
            e.HasIndex(h => new { h.StudentId, h.CreatedAt })
                .IsDescending(false, true)
                .HasDatabaseName("ix_holds_student_created");
            // Database-level safety net for "one active application per student per class".
            e.HasIndex(h => new { h.ClassId, h.StudentId })
                .IsUnique()
                .HasFilter("status IN ('HELD','CONFIRMED')")
                .HasDatabaseName("ux_holds_one_active_per_student_class");
        });
    }
}

/// <summary>Lets <c>dotnet ef migrations add</c> build the context without starting the web host.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=school_admission;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }
}
