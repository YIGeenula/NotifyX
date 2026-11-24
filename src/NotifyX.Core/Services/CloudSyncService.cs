using System.Net.Http;
using System.Text;
using System.Text.Json;
using CodexBlaze.PaymentReminder.Core.Models;
using Serilog;

namespace CodexBlaze.PaymentReminder.Core.Services;

public interface ICloudSyncService
{
    Task<bool> SyncPurchaseAsync(Purchase purchase);
    Task<bool> UpdatePurchaseInCloudAsync(Purchase purchase);
    Task<bool> RecordPaymentInCloudAsync(int purchaseId, string itemName, decimal amountPaid);
    Task<bool> DeletePurchaseFromCloudAsync(int purchaseId, string itemName);
    Task<bool> SyncAllPurchasesAsync(List<Purchase> purchases);
    Task<int> PullFromCloudAsync();
    Task<string> GetSyncEndpointAsync();
    Task SetSyncEndpointAsync(string endpoint);
}

public class CloudSyncService : ICloudSyncService
{
    private readonly ISettingsService _settingsService;
    private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<CodexBlaze.PaymentReminder.Core.Data.AppDbContext> _contextFactory;
    private readonly InstallmentCalculationService _calculationService;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(20) };

    public const string DefaultSyncUrl = "";

    public CloudSyncService(
        ISettingsService settingsService,
        Microsoft.EntityFrameworkCore.IDbContextFactory<CodexBlaze.PaymentReminder.Core.Data.AppDbContext> contextFactory,
        InstallmentCalculationService calculationService)
    {
        _settingsService = settingsService;
        _contextFactory = contextFactory;
        _calculationService = calculationService;
    }

    public async Task<string> GetSyncEndpointAsync()
    {
        return await _settingsService.GetSettingAsync("GoogleSheetSyncUrl", DefaultSyncUrl);
    }

    public async Task SetSyncEndpointAsync(string endpoint)
    {
        await _settingsService.SaveSettingAsync("GoogleSheetSyncUrl", endpoint, "CloudSync");
    }

    public async Task<bool> SyncPurchaseAsync(Purchase purchase)
    {
        string endpoint = await GetSyncEndpointAsync();
        if (string.IsNullOrWhiteSpace(endpoint)) return false;

        try
        {
            var payload = new
            {
                action = "add_purchase",
                id = purchase.Id,
                itemName = purchase.ItemName,
                provider = purchase.Provider?.Name ?? "Provider",
                totalAmount = purchase.TotalAmount,
                remainingAmount = purchase.RemainingAmount,
                installments = purchase.NumberOfInstallments,
                status = purchase.Status.ToString(),
                purchaseDate = purchase.PurchaseDate.ToString("yyyy-MM-dd")
            };

            string json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);
            if (response.IsSuccessStatusCode)
            {
                Log.Information("Successfully synced purchase {Id} to Google Sheet & Cloud API.", purchase.Id);
                return true;
            }
            else
            {
                Log.Warning("Cloud sync responded with code: {StatusCode}", response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to sync purchase {Id} to Google Sheet.", purchase.Id);
            return false;
        }
    }

    public async Task<bool> UpdatePurchaseInCloudAsync(Purchase purchase)
    {
        string endpoint = await GetSyncEndpointAsync();
        if (string.IsNullOrWhiteSpace(endpoint)) return false;

        try
        {
            var payload = new
            {
                action = "edit_purchase",
                id = purchase.Id,
                itemName = purchase.ItemName,
                provider = purchase.Provider?.Name ?? "Provider",
                totalAmount = purchase.TotalAmount,
                remainingAmount = purchase.RemainingAmount,
                installments = purchase.NumberOfInstallments,
                status = purchase.Status.ToString()
            };

            string json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update purchase in cloud {Id}", purchase.Id);
            return false;
        }
    }

    public async Task<bool> RecordPaymentInCloudAsync(int purchaseId, string itemName, decimal amountPaid)
    {
        string endpoint = await GetSyncEndpointAsync();
        if (string.IsNullOrWhiteSpace(endpoint)) return false;

        try
        {
            var payload = new
            {
                action = "record_payment",
                purchaseId = purchaseId,
                itemName = itemName,
                amountPaid = amountPaid
            };

            string json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to record payment in cloud for purchase {Id}", purchaseId);
            return false;
        }
    }

    public async Task<bool> DeletePurchaseFromCloudAsync(int purchaseId, string itemName)
    {
        string endpoint = await GetSyncEndpointAsync();
        if (string.IsNullOrWhiteSpace(endpoint)) return false;

        try
        {
            var payload = new
            {
                action = "delete_purchase",
                id = purchaseId,
                itemName = itemName
            };

            string json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);
            if (response.IsSuccessStatusCode)
            {
                Log.Information("Successfully sent delete request for purchase {Id} ('{Name}') to Google Sheet.", purchaseId, itemName);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to send delete request to Google Sheet for purchase {Id}.", purchaseId);
            return false;
        }
    }

    public async Task<bool> SyncAllPurchasesAsync(List<Purchase> purchases)
    {
        bool allOk = true;
        foreach (var p in purchases)
        {
            bool ok = await SyncPurchaseAsync(p);
            if (!ok) allOk = false;
        }
        return allOk;
    }

    public async Task<int> PullFromCloudAsync()
    {
        string endpoint = await GetSyncEndpointAsync();
        if (string.IsNullOrWhiteSpace(endpoint)) return 0;

        try
        {
            var response = await _httpClient.GetAsync(endpoint);
            if (!response.IsSuccessStatusCode) return 0;

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() <= 1)
                return 0;

            await using var context = await _contextFactory.CreateDbContextAsync();
            var providers = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(context.Providers);
            var providerMap = providers.ToDictionary(p => p.Name.ToLower().Trim(), p => p.Id);

            int imported = 0;
            var rows = root.EnumerateArray().Skip(1); // skip headers

            foreach (var row in rows)
            {
                if (row.GetArrayLength() < 7) continue;

                string itemName = row[1].ToString();
                string providerName = row[2].ToString();
                decimal total = decimal.TryParse(row[3].ToString(), out var t) ? t : 0m;
                decimal remaining = decimal.TryParse(row[4].ToString(), out var r) ? r : 0m;
                int installments = int.TryParse(row[5].ToString(), out var inst) ? inst : 3;
                string statusStr = row[6].ToString();
                DateTime pDate = DateTime.TryParse(row[7].ToString(), out var d) ? d : DateTime.Today;

                if (string.IsNullOrWhiteSpace(itemName)) continue;

                // Check if already exists locally
                bool exists = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
                    context.Purchases,
                    p => p.ItemName.ToLower() == itemName.ToLower() && p.TotalAmount == total);

                if (!exists)
                {
                    if (!providerMap.TryGetValue(providerName.ToLower().Trim(), out int provId))
                    {
                        var newProv = new Provider { Name = providerName, BrandColorHex = "#7C5CFC", IsActive = true };
                        await context.Providers.AddAsync(newProv);
                        await context.SaveChangesAsync();
                        provId = newProv.Id;
                        providerMap[providerName.ToLower().Trim()] = provId;
                    }

                    Enum.TryParse<PurchaseStatus>(statusStr, true, out var pStatus);

                    var purchase = new Purchase
                    {
                        ItemName = itemName,
                        ProviderId = provId,
                        TotalAmount = total,
                        RemainingAmount = remaining,
                        InitialPayment = Math.Max(0m, total - remaining),
                        NumberOfInstallments = installments,
                        Status = pStatus,
                        PurchaseDate = pDate,
                        Category = "Imported",
                        CreatedAt = DateTime.UtcNow
                    };

                    await context.Purchases.AddAsync(purchase);
                    await context.SaveChangesAsync();

                    // Auto generate installment rows for local database consistency
                    var schedule = _calculationService.GenerateSchedule(
                        remaining > 0 ? remaining : total,
                        installments,
                        pDate.AddMonths(1),
                        ScheduleInterval.Monthly);

                    foreach (var s in schedule)
                    {
                        await context.Installments.AddAsync(new Installment
                        {
                            PurchaseId = purchase.Id,
                            InstallmentNumber = s.InstallmentNumber,
                            AmountDue = s.AmountDue,
                            RemainingAmount = remaining <= 0 ? 0m : s.AmountDue,
                            AmountPaid = remaining <= 0 ? s.AmountDue : 0m,
                            DueDate = s.DueDate,
                            Status = remaining <= 0 ? PaymentStatus.Paid : PaymentStatus.Upcoming
                        });
                    }

                    await context.SaveChangesAsync();
                    imported++;
                }
            }

            Log.Information("Pulled {Count} purchases from Google Sheet into local SQLite.", imported);
            return imported;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to pull data from Google Sheet.");
            return 0;
        }
    }
}
