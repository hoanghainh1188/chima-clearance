using B2.Domain.Audit;
using B2.Domain.DailyHeaders;
using B2.Domain.Declarations;
using B2.Domain.MasterData;
using B2.Domain.Users;
using B2.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace B2.Infrastructure.Persistence;

public sealed class B2DbContext : DbContext, IUnitOfWork
{
    public B2DbContext(DbContextOptions<B2DbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Processor> Processors => Set<Processor>();
    public DbSet<Yard> Yards => Set<Yard>();
    public DbSet<Declaration> Declarations => Set<Declaration>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<DailyHeader> DailyHeaders => Set<DailyHeader>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshTokenRecord> RefreshTokens => Set<RefreshTokenRecord>();
    public DbSet<IdempotencyKeyRecord> IdempotencyKeys => Set<IdempotencyKeyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).HasMaxLength(50).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(60).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<Processor>(e =>
        {
            e.ToTable("Processors");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FullName).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Yard>(e =>
        {
            e.ToTable("Yards");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Declaration>(e =>
        {
            e.ToTable("Declarations");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DeclarationNumber).IsUnique();
            e.HasIndex(x => x.BusinessDate);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ProcessorId);
            e.HasIndex(x => x.YardId);
            e.Property(x => x.DeclarationNumber).HasMaxLength(12).IsRequired();
            e.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
            e.Property(x => x.GoodsName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.CancellationReason).HasMaxLength(500);
            e.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
            e.HasMany(x => x.Vehicles)
                .WithOne(v => v.Declaration)
                .HasForeignKey(v => v.DeclarationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Vehicle>(e =>
        {
            e.ToTable("Vehicles");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DeclarationId);
            e.HasIndex(x => x.PlateLast4);
            e.HasIndex(x => x.ExitMarked);
            e.HasIndex(x => x.EntryTime);
            e.HasIndex(x => new { x.PlateNumber, x.EntryTime }).HasDatabaseName("IX_Vehicle_PlateEntry");
            e.Property(x => x.PlateNumber).HasMaxLength(15).IsRequired();
            e.Property(x => x.PlateLast4)
                .HasMaxLength(4)
                .HasComputedColumnSql("substr(\"PlateNumber\", -4)", stored: true);
            e.Property(x => x.TrailerOrContainerNumber).HasMaxLength(20);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        });

        modelBuilder.Entity<DailyHeader>(e =>
        {
            e.ToTable("DailyHeaders");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Date).IsUnique();
            e.Property(x => x.OnDutyOfficerName).HasMaxLength(200);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EntityType, x.EntityId, x.At }).HasDatabaseName("IX_AuditLog_EntityIdAt");
            e.HasIndex(x => new { x.ActorUserId, x.At }).HasDatabaseName("IX_AuditLog_ActorAt");
            e.Property(x => x.EntityType).HasMaxLength(50).IsRequired();
            e.Property(x => x.ChangesJson).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.CorrelationId).HasMaxLength(64);
            e.Property(x => x.Action).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<RefreshTokenRecord>(e =>
        {
            e.ToTable("RefreshTokens");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenHash);
            e.HasIndex(x => x.UserId);
            e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<IdempotencyKeyRecord>(e =>
        {
            e.ToTable("IdempotencyKeys");
            e.HasKey(x => new { x.Key, x.RequestHash });
            e.Property(x => x.Key).HasMaxLength(128);
            e.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.ResponseBody).IsRequired();
            e.Property(x => x.ResponseContentType).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.CreatedAt);
        });
    }

}
