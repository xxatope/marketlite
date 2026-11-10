using Microsoft.EntityFrameworkCore;
using Payment.Api.Domain;

namespace Payment.Api.Data;

public sealed class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Hold> Holds => Set<Hold>();
    public DbSet<TransactionLedger> LedgerEntries => Set<TransactionLedger>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>(builder =>
        {
            builder.ToTable("accounts");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.Currency)
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(a => a.Balance)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(a => a.HeldBalance)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Ignore(a => a.AvailableBalance);

            builder.Property(a => a.CreatedAt)
                .IsRequired();

            if (Database.IsNpgsql())
            {
                builder.Property(a => a.Version).IsRowVersion();
            }

            builder.HasMany(a => a.Holds)
                .WithOne()
                .HasForeignKey(h => h.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Navigation(a => a.Holds)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(a => a.LedgerEntries)
                .WithOne()
                .HasForeignKey(l => l.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Navigation(a => a.LedgerEntries)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasIndex(a => new { a.UserId, a.Currency })
                .IsUnique();
        });

        modelBuilder.Entity<Hold>(builder =>
        {
            builder.ToTable("holds");
            builder.HasKey(h => h.Id);

            builder.Property(h => h.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(h => h.IdempotencyKey)
                .HasMaxLength(256)
                .IsRequired();

            builder.Property(h => h.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(h => h.CreatedAt)
                .IsRequired();

            builder.HasIndex(h => h.IdempotencyKey)
                .IsUnique();

            builder.HasIndex(h => h.OrderId);
            builder.HasIndex(h => h.AccountId);
        });

        modelBuilder.Entity<TransactionLedger>(builder =>
        {
            builder.ToTable("transaction_ledger");
            builder.HasKey(l => l.Id);

            builder.Property(l => l.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(l => l.BalanceAfter)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(l => l.Type)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(l => l.ReferenceId)
                .HasMaxLength(256);

            builder.Property(l => l.Description)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(l => l.CreatedAt)
                .IsRequired();

            builder.HasIndex(l => l.AccountId);
            builder.HasIndex(l => l.CreatedAt);
        });
    }
}
