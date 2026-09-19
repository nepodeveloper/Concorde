namespace Concorde.Infrastructure.Persistence;

using Concorde.Domain.Common;
using Concorde.Domain.Orders;
using Microsoft.EntityFrameworkCore;

public class ConcordeDbContext : DbContext
{
    public ConcordeDbContext(DbContextOptions<ConcordeDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(order =>
        {
            order.ToTable("Orders");
            order.HasKey(o => o.Id);
            order.Property(o => o.Id).ValueGeneratedNever(); // domain generates the GUID

            order.Property(o => o.ExternalReference)
                .IsRequired()
                .HasMaxLength(Order.MaxExternalReferenceLength);

            // FR-02.6: database-enforced uniqueness; the domain stores the reference
            // normalized (upper-cased), so a plain unique index is case-insensitive in effect.
            order.HasIndex(o => o.ExternalReference)
                .IsUnique()
                .HasDatabaseName("UX_Orders_ExternalReference");

            order.Property(o => o.CustomerName)
                .IsRequired()
                .HasMaxLength(Order.MaxCustomerNameLength);

            order.Property(o => o.CustomerCode)
                .HasMaxLength(Order.MaxCustomerCodeLength);

            order.Property(o => o.Currency)
                .IsRequired()
                .HasMaxLength(3)
                .HasConversion(c => c.Code, code => new Currency(code));

            order.Property(o => o.Notes)
                .HasMaxLength(Order.MaxNotesLength);

            order.Property(o => o.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion<string>();

            order.Property(o => o.StatusReason)
                .HasMaxLength(Order.MaxStatusReasonLength);

            order.Property(o => o.Subtotal)
                .IsRequired()
                .HasConversion(m => m.Amount, amount => new Money(amount));

            order.Property(o => o.Total)
                .IsRequired()
                .HasConversion(m => m.Amount, amount => new Money(amount));

            order.Property(o => o.CreatedAtUtc).IsRequired();
            order.Property(o => o.UpdatedAtUtc).IsRequired();

            order.OwnsMany(o => o.Lines, line =>
            {
                line.ToTable("OrderLines");
                line.WithOwner().HasForeignKey("OrderId");
                line.HasKey(l => l.Id);
                line.Property(l => l.Id).ValueGeneratedNever();

                line.Property(l => l.Sku)
                    .IsRequired()
                    .HasMaxLength(OrderLine.MaxSkuLength);

                line.Property(l => l.Name)
                    .IsRequired()
                    .HasMaxLength(OrderLine.MaxNameLength);

                line.Property(l => l.Quantity).IsRequired();

                line.Property(l => l.UnitPrice)
                    .IsRequired()
                    .HasConversion(m => m.Amount, amount => new Money(amount));

                line.Property(l => l.LineTotal)
                    .IsRequired()
                    .HasConversion(m => m.Amount, amount => new Money(amount));
            });

            order.Navigation(o => o.Lines)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
