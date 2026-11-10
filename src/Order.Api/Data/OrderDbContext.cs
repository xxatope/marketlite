using Microsoft.EntityFrameworkCore;
using Order.Api.Domain;

namespace Order.Api.Data;

public sealed class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }

    public DbSet<Domain.Order> Orders => Set<Domain.Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Domain.Order>(builder =>
        {
            builder.ToTable("orders");
            builder.HasKey(o => o.Id);

            builder.Property(o => o.Currency)
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(o => o.TotalAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(o => o.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(o => o.CancellationReason)
                .HasMaxLength(500);

            builder.Property(o => o.CreatedAt)
                .IsRequired();

            if (Database.IsNpgsql())
            {
                builder.Property(o => o.Version).IsRowVersion();
            }

            builder.HasMany(o => o.Items)
                .WithOne()
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(o => o.Items)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasIndex(o => o.CustomerId);
            builder.HasIndex(o => o.CreatedAt);
        });

        modelBuilder.Entity<OrderItem>(builder =>
        {
            builder.ToTable("order_items");
            builder.HasKey(i => i.Id);

            builder.Property(i => i.ProductName)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(i => i.UnitPrice)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(i => i.Quantity)
                .IsRequired();

            builder.Ignore(i => i.Subtotal);

            builder.HasIndex(i => i.OrderId);
            builder.HasIndex(i => i.ProductId);
        });
    }
}
