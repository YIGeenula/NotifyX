using Microsoft.EntityFrameworkCore;
using CodexBlaze.PaymentReminder.Core.Models;

namespace CodexBlaze.PaymentReminder.Core.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<Installment> Installments => Set<Installment>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<NotificationHistory> NotificationHistories => Set<NotificationHistory>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Provider configuration
        modelBuilder.Entity<Provider>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.BrandColorHex).HasMaxLength(20);
        });

        // Purchase configuration
        modelBuilder.Entity<Purchase>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ItemName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.InitialPayment).HasPrecision(18, 2);
            entity.Property(e => e.RemainingAmount).HasPrecision(18, 2);

            entity.HasOne(e => e.Provider)
                  .WithMany(p => p.Purchases)
                  .HasForeignKey(e => e.ProviderId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Installments)
                  .WithOne(i => i.Purchase)
                  .HasForeignKey(i => i.PurchaseId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Payments)
                  .WithOne(p => p.Purchase)
                  .HasForeignKey(p => p.PurchaseId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.PurchaseDate);
            entity.HasIndex(e => e.Status);
        });

        // Installment configuration
        modelBuilder.Entity<Installment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AmountDue).HasPrecision(18, 2);
            entity.Property(e => e.AmountPaid).HasPrecision(18, 2);
            entity.Property(e => e.RemainingAmount).HasPrecision(18, 2);

            entity.HasMany(e => e.Payments)
                  .WithOne(p => p.Installment)
                  .HasForeignKey(p => p.InstallmentId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.DueDate);
            entity.HasIndex(e => e.Status);
        });

        // Payment configuration
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AmountPaid).HasPrecision(18, 2);
            entity.Property(e => e.PaymentMethod).HasMaxLength(100);
            entity.Property(e => e.ProviderName).HasMaxLength(100);
            entity.HasIndex(e => e.PaymentDate);
        });

        // Notification History
        modelBuilder.Entity<NotificationHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.NotificationType).HasMaxLength(50);
            entity.HasIndex(e => e.SentAt);
        });

        // AppSetting
        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Value).IsRequired();
            entity.Property(e => e.Group).HasMaxLength(50);
        });
    }
}
