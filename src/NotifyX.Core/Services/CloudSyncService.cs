using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodexBlaze.PaymentReminder.Core.Models;
using Serilog;

namespace CodexBlaze.PaymentReminder.Core.Services;

public interface ICloudSyncService
{
    Task<string> GetUserVaultIdAsync();
    Task SetUserVaultIdAsync(string vaultId);
    Task<string> GetSyncEndpointAsync();
    Task SetSyncEndpointAsync(string endpoint);
    Task<bool> SyncPurchaseAsync(Purchase purchase);
    Task<bool> UpdatePurchaseInCloudAsync(Purchase purchase);
    Task<bool> RecordPaymentInCloudAsync(int purchaseId, string itemName, decimal amountPaid);
    Task<bool> DeletePurchaseFromCloudAsync(int purchaseId, string itemName);
    Task<bool> SyncAllPurchasesAsync(List<Purchase> purchases);
    Task<int> PullFromCloudAsync();
}

public class CloudSyncService : ICloudSyncService
{
    private readonly ISettingsService _settingsService;
    private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<CodexBlaze.PaymentReminder.Core.Data.AppDbContext> _contextFactory;
    private readonly InstallmentCalculationService _calculationService;

    // Master Free Cloud Vault API (Powered by Google Apps Script - 100% Free, Zero Hosting Cost)
    public const string DefaultSyncUrl = "https://script.google.com/macros/s/AKfycbwtxeJo6Qm1IysNP_pGgUyAcCVL4mKS5JSVIkNLkiXF6Jk6nmfv9vs2OCVxjoBLVPyj/exec";

    private static readonly HttpClient _httpClient;

    static CloudSyncService()
    {
        _httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        })
        {
            Timeout = TimeSpan.FromSeconds(25)
        };
    }

    public CloudSyncService(
        ISettingsService settingsService,
        Microsoft.EntityFrameworkCore.IDbContextFactory<CodexBlaze.PaymentReminder.Core.Data.AppDbContext> contextFactory,
        InstallmentCalculationService calculationService)
    {
        _settingsService = settingsService;
        _contextFactory = contextFactory;
        _calculationService = calculationService;
    }

    public async Task<string> GetUserVaultIdAsync()
    {
        string vault = await _settingsService.GetSettingAsync("UserVaultId", "");
        if (string.IsNullOrWhiteSpace(vault))
        {
            // Auto-generate a secure random 8-character Vault ID (e.g. NX-A892F1)
            byte[] bytes = new byte[4];
            RandomNumberGenerator.Fill(bytes);
            string hex = Convert.ToHexString(bytes).ToUpper();
            vault = $"NX-{hex}";
            await _settingsService.SaveSettingAsync("UserVaultId", vault, "CloudVault");
            Log.Information("Generated new User Vault ID: {VaultId}", vault);
        }
        return vault;
    }

    public async Task SetUserVaultIdAsync(string vaultId)
    {
        string clean = string.IsNullOrWhiteSpace(vaultId) ? "" : vaultId.Trim().ToUpper();
        await _settingsService.SaveSettingAsync("UserVaultId", clean, "CloudVault");
    }

    public async Task<string> GetSyncEndpointAsync()
    {
        string url = await _settingsService.GetSettingAsync("GoogleSheetSyncUrl", DefaultSyncUrl);
        return string.IsNullOrWhiteSpace(url) ? DefaultSyncUrl : url;
    }

    public async Task SetSyncEndpointAsync(string endpoint)
    {
        await _settingsService.SaveSettingAsync("GoogleSheetSyncUrl", endpoint, "CloudSync");
    }

    public async Task<bool> SyncPurchaseAsync(Purchase purchase)
    {
        string endpoint = await GetSyncEndpointAsync();
        if (string.IsNullOrWhiteSpace(endpoint)) return false;

        string vaultId = await GetUserVaultIdAsync();

        try
        {
            var payload = new
            {
                action = "add_purchase",
                vaultId = vaultId,
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
                Log.Information("Successfully synced purchase {Id} to Vault {Vault}", purchase.Id, vaultId);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to sync purchase {Id} to Cloud Vault.", purchase.Id);
            return false;
        }
    }

    public async Task<bool> UpdatePurchaseInCloudAsync(Purchase purchase)
    {
        string endpoint = await GetSyncEndpointAsync();
        if (string.IsNullOrWhiteSpace(endpoint)) return false;

        string vaultId = await GetUserVaultIdAsync();

        try
        {
            var payload = new
            {
                action = "edit_purchase",
                vaultId = vaultId,
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
            Log.Error(ex, "Failed to update purchase in cloud vault {Id}", purchase.Id);
            return false;
        }
    }

    public async Task<bool> RecordPaymentInCloudAsync(int purchaseId, string itemName, decimal amountPaid)
    {
        string endpoint = await GetSyncEndpointAsync();
        if (string.IsNullOrWhiteSpace(endpoint)) return false;

        string vaultId = await GetUserVaultIdAsync();

        try
        {
            var payload = new
            {
                action = "record_payment",
                vaultId = vaultId,
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
            Log.Error(ex, "Failed to record payment in cloud vault for purchase {Id}", purchaseId);
            return false;
        }
    }

    public async Task<bool> DeletePurchaseFromCloudAsync(int purchaseId, string itemName)
    {
        string endpoint = await GetSyncEndpointAsync();
        if (string.IsNullOrWhiteSpace(endpoint)) return false;

        string vaultId = await GetUserVaultIdAsync();

        try
        {
            var payload = new
            {
                action = "delete_purchase",
                vaultId = vaultId,
                id = purchaseId,
                itemName = itemName
            };

            string json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to send delete request to Cloud Vault for purchase {Id}.", purchaseId);
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

        string vaultId = await GetUserVaultIdAsync();
        string requestUrl = $"{endpoint}?vault={Uri.EscapeDataString(vaultId)}";

        try
        {
            var response = await _httpClient.GetAsync(requestUrl);
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

            Log.Information("Pulled {Count} purchases from Vault into local SQLite.", imported);
            return imported;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to pull data from Cloud Vault.");
            return 0;
        }
    }
}
