using CodexBlaze.PaymentReminder.Core.Data;
using CodexBlaze.PaymentReminder.Core.Models;
using CodexBlaze.PaymentReminder.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodexBlaze.PaymentReminder.Tests;

public class DatabaseAndServiceIntegrationTests
{
    private class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public TestDbContextFactory(DbContextOptions<AppDbContext> options) => _options = options;
        public AppDbContext CreateDbContext() => new(_options);
        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AppDbContext(_options));
    }

    [Fact]
    public async Task CreatePurchase_WithInstallments_PersistsAndCalculatesProperly()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var factory = new TestDbContextFactory(options);
        var calcService = new InstallmentCalculationService();
        var init = new DatabaseInitializer(factory);
        await init.InitializeAsync();

        var purchService = new PurchaseService(factory, calcService);
        var providers = await purchService.GetProvidersAsync();
        Assert.NotEmpty(providers);

        var purchase = new Purchase
        {
            ItemName = "Sony WH-1000XM5",
            Category = "Audio",
            ProviderId = providers.First().Id,
            TotalAmount = 75000m,
            InitialPayment = 15000m,
            PurchaseDate = DateTime.Today
        };

        var schedule = calcService.GenerateSchedule(60000m, 3, DateTime.Today.AddMonths(1), ScheduleInterval.Monthly);
        var installments = schedule.Select(s => new Installment
        {
            InstallmentNumber = s.InstallmentNumber,
            AmountDue = s.AmountDue,
            RemainingAmount = s.AmountDue,
            DueDate = s.DueDate
        }).ToList();

        var created = await purchService.CreatePurchaseAsync(purchase, installments);
        Assert.True(created.Id > 0);
        Assert.Equal(60000m, created.RemainingAmount);
        Assert.Equal(PurchaseStatus.Active, created.Status);

        // Verify retrieval
        var retrieved = await purchService.GetPurchaseByIdAsync(created.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(3, retrieved.Installments.Count);
        Assert.Single(retrieved.Payments); // Downpayment logged
        Assert.Equal(15000m, retrieved.Payments.First().AmountPaid);
    }

    [Fact]
    public async Task RecordPayment_PartialAndFull_UpdatesInstallmentAndPurchaseBalances()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var factory = new TestDbContextFactory(options);
        var calcService = new InstallmentCalculationService();
        var init = new DatabaseInitializer(factory);
        await init.InitializeAsync();

        var purchService = new PurchaseService(factory, calcService);
        var payService = new PaymentService(factory, calcService);
        var providers = await purchService.GetProvidersAsync();

        var purchase = new Purchase
        {
            ItemName = "Mechanical Keyboard",
            Category = "Accessories",
            ProviderId = providers.First().Id,
            TotalAmount = 20000m,
            InitialPayment = 0m,
            PurchaseDate = DateTime.Today
        };

        var installments = new List<Installment>
        {
            new() { InstallmentNumber = 1, AmountDue = 10000m, DueDate = DateTime.Today.AddDays(7) },
            new() { InstallmentNumber = 2, AmountDue = 10000m, DueDate = DateTime.Today.AddDays(37) }
        };

        var created = await purchService.CreatePurchaseAsync(purchase, installments);
        var firstInst = created.Installments.First();

        // 1. Partial payment: Pay 4000
        await payService.RecordPaymentAsync(firstInst.Id, 4000m, DateTime.Today, "Card", "Partial 1");
        var instAfterPartial = (await payService.GetAllInstallmentsAsync()).First(i => i.Id == firstInst.Id);
        Assert.Equal(4000m, instAfterPartial.AmountPaid);
        Assert.Equal(6000m, instAfterPartial.RemainingAmount);
        Assert.Equal(PaymentStatus.PartiallyPaid, instAfterPartial.Status);

        // 2. Pay remaining 6000 on first installment
        await payService.RecordPaymentAsync(firstInst.Id, 6000m, DateTime.Today, "Card", "Partial 2");
        var instAfterFull = (await payService.GetAllInstallmentsAsync()).First(i => i.Id == firstInst.Id);
        Assert.Equal(10000m, instAfterFull.AmountPaid);
        Assert.Equal(0m, instAfterFull.RemainingAmount);
        Assert.Equal(PaymentStatus.Paid, instAfterFull.Status);

        // 3. Settle second installment (10000) -> Purchase should now complete
        var secondInst = (await payService.GetAllInstallmentsAsync()).First(i => i.InstallmentNumber == 2);
        await payService.RecordPaymentAsync(secondInst.Id, 10000m, DateTime.Today, "Card", "Full Inst 2");

        var completedPurch = await purchService.GetPurchaseByIdAsync(created.Id);
        Assert.NotNull(completedPurch);
        Assert.Equal(0m, completedPurch.RemainingAmount);
        Assert.Equal(PurchaseStatus.Completed, completedPurch.Status);
    }
}
