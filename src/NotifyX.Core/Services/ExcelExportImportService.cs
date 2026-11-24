using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using CodexBlaze.PaymentReminder.Core.Data;
using CodexBlaze.PaymentReminder.Core.Models;
using Serilog;

namespace CodexBlaze.PaymentReminder.Core.Services;

public interface IExcelExportImportService
{
    Task ExportToExcelAsync(string filePath);
    Task<ImportResult> ImportFromExcelAsync(string filePath);
}

public record ImportResult(bool Success, int PurchasesImported, int InstallmentsImported, int PaymentsImported, List<string> Errors);

public class ExcelExportImportService : IExcelExportImportService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly InstallmentCalculationService _calculationService;

    public ExcelExportImportService(IDbContextFactory<AppDbContext> contextFactory, InstallmentCalculationService calculationService)
    {
        _contextFactory = contextFactory;
        _calculationService = calculationService;
    }

    public async Task ExportToExcelAsync(string filePath)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var purchases = await context.Purchases.Include(p => p.Provider).AsNoTracking().ToListAsync();
        var installments = await context.Installments.Include(i => i.Purchase).AsNoTracking().ToListAsync();
        var payments = await context.Payments.Include(p => p.Purchase).AsNoTracking().ToListAsync();
        var providers = await context.Providers.AsNoTracking().ToListAsync();
        var notifications = await context.NotificationHistories.AsNoTracking().ToListAsync();
        var settings = await context.AppSettings.AsNoTracking().ToListAsync();

        using var workbook = new XLWorkbook();

        // 1. Purchases Sheet
        var wsPurchases = workbook.Worksheets.Add("Purchases");
        string[] purchaseHeaders = { "ID", "Item Name", "Category", "Provider", "Purchase Date", "Total Amount (LKR)", "Initial Payment (LKR)", "Remaining Amount (LKR)", "Installments", "Status", "Notes" };
        for (int i = 0; i < purchaseHeaders.Length; i++)
        {
            wsPurchases.Cell(1, i + 1).Value = purchaseHeaders[i];
            wsPurchases.Cell(1, i + 1).Style.Font.Bold = true;
            wsPurchases.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1D2433");
            wsPurchases.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
        }

        int row = 2;
        foreach (var p in purchases)
        {
            wsPurchases.Cell(row, 1).Value = p.Id;
            wsPurchases.Cell(row, 2).Value = p.ItemName;
            wsPurchases.Cell(row, 3).Value = p.Category;
            wsPurchases.Cell(row, 4).Value = p.Provider?.Name ?? "N/A";
            wsPurchases.Cell(row, 5).Value = p.PurchaseDate.ToString("yyyy-MM-dd");
            wsPurchases.Cell(row, 6).Value = (double)p.TotalAmount;
            wsPurchases.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            wsPurchases.Cell(row, 7).Value = (double)p.InitialPayment;
            wsPurchases.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            wsPurchases.Cell(row, 8).Value = (double)p.RemainingAmount;
            wsPurchases.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
            wsPurchases.Cell(row, 9).Value = p.NumberOfInstallments;
            wsPurchases.Cell(row, 10).Value = p.Status.ToString();
            wsPurchases.Cell(row, 11).Value = p.Notes;
            row++;
        }
        wsPurchases.Columns().AdjustToContents();

        // 2. Installments Sheet
        var wsInstallments = workbook.Worksheets.Add("Installments");
        string[] instHeaders = { "ID", "Purchase ID", "Purchase Name", "Installment #", "Due Date", "Amount Due (LKR)", "Amount Paid (LKR)", "Remaining (LKR)", "Status", "Paid Date", "Notes" };
        for (int i = 0; i < instHeaders.Length; i++)
        {
            wsInstallments.Cell(1, i + 1).Value = instHeaders[i];
            wsInstallments.Cell(1, i + 1).Style.Font.Bold = true;
            wsInstallments.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1D2433");
            wsInstallments.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
        }

        row = 2;
        foreach (var inst in installments)
        {
            wsInstallments.Cell(row, 1).Value = inst.Id;
            wsInstallments.Cell(row, 2).Value = inst.PurchaseId;
            wsInstallments.Cell(row, 3).Value = inst.Purchase?.ItemName ?? "N/A";
            wsInstallments.Cell(row, 4).Value = inst.InstallmentNumber;
            wsInstallments.Cell(row, 5).Value = inst.DueDate.ToString("yyyy-MM-dd");
            wsInstallments.Cell(row, 6).Value = (double)inst.AmountDue;
            wsInstallments.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            wsInstallments.Cell(row, 7).Value = (double)inst.AmountPaid;
            wsInstallments.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            wsInstallments.Cell(row, 8).Value = (double)inst.RemainingAmount;
            wsInstallments.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
            wsInstallments.Cell(row, 9).Value = inst.Status.ToString();
            wsInstallments.Cell(row, 10).Value = inst.PaidDate?.ToString("yyyy-MM-dd") ?? "";
            wsInstallments.Cell(row, 11).Value = inst.Notes;
            row++;
        }
        wsInstallments.Columns().AdjustToContents();

        // 3. Payments Sheet
        var wsPayments = workbook.Worksheets.Add("Payments");
        string[] paymentHeaders = { "Payment ID", "Purchase ID", "Installment ID", "Provider", "Payment Date", "Amount Paid (LKR)", "Payment Method", "Notes" };
        for (int i = 0; i < paymentHeaders.Length; i++)
        {
            wsPayments.Cell(1, i + 1).Value = paymentHeaders[i];
            wsPayments.Cell(1, i + 1).Style.Font.Bold = true;
            wsPayments.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1D2433");
            wsPayments.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
        }

        row = 2;
        foreach (var pm in payments)
        {
            wsPayments.Cell(row, 1).Value = pm.Id;
            wsPayments.Cell(row, 2).Value = pm.PurchaseId;
            wsPayments.Cell(row, 3).Value = pm.InstallmentId ?? 0;
            wsPayments.Cell(row, 4).Value = pm.ProviderName;
            wsPayments.Cell(row, 5).Value = pm.PaymentDate.ToString("yyyy-MM-dd");
            wsPayments.Cell(row, 6).Value = (double)pm.AmountPaid;
            wsPayments.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            wsPayments.Cell(row, 7).Value = pm.PaymentMethod;
            wsPayments.Cell(row, 8).Value = pm.Notes;
            row++;
        }
        wsPayments.Columns().AdjustToContents();

        // 4. Providers Sheet
        var wsProviders = workbook.Worksheets.Add("Providers");
        wsProviders.Cell(1, 1).Value = "ID";
        wsProviders.Cell(1, 2).Value = "Name";
        wsProviders.Cell(1, 3).Value = "Description";
        wsProviders.Cell(1, 4).Value = "Brand Color";
        row = 2;
        foreach (var pr in providers)
        {
            wsProviders.Cell(row, 1).Value = pr.Id;
            wsProviders.Cell(row, 2).Value = pr.Name;
            wsProviders.Cell(row, 3).Value = pr.Description;
            wsProviders.Cell(row, 4).Value = pr.BrandColorHex;
            row++;
        }
        wsProviders.Columns().AdjustToContents();

        // 5. Notifications Sheet
        var wsNotifs = workbook.Worksheets.Add("Notifications");
        wsNotifs.Cell(1, 1).Value = "ID";
        wsNotifs.Cell(1, 2).Value = "Title";
        wsNotifs.Cell(1, 3).Value = "Message";
        wsNotifs.Cell(1, 4).Value = "Type";
        wsNotifs.Cell(1, 5).Value = "Sent Date";
        row = 2;
        foreach (var n in notifications)
        {
            wsNotifs.Cell(row, 1).Value = n.Id;
            wsNotifs.Cell(row, 2).Value = n.Title;
            wsNotifs.Cell(row, 3).Value = n.Message;
            wsNotifs.Cell(row, 4).Value = n.NotificationType;
            wsNotifs.Cell(row, 5).Value = n.SentAt.ToString("yyyy-MM-dd HH:mm");
            row++;
        }
        wsNotifs.Columns().AdjustToContents();

        // 6. Settings Sheet
        var wsSettings = workbook.Worksheets.Add("Settings");
        wsSettings.Cell(1, 1).Value = "Key";
        wsSettings.Cell(1, 2).Value = "Value";
        wsSettings.Cell(1, 3).Value = "Group";
        row = 2;
        foreach (var s in settings)
        {
            wsSettings.Cell(row, 1).Value = s.Key;
            wsSettings.Cell(row, 2).Value = s.Value;
            wsSettings.Cell(row, 3).Value = s.Group;
            row++;
        }
        wsSettings.Columns().AdjustToContents();

        workbook.SaveAs(filePath);
        Log.Information("Exported full system workbook to {FilePath}", filePath);
    }

    public async Task<ImportResult> ImportFromExcelAsync(string filePath)
    {
        var errors = new List<string>();
        int purchasesImported = 0;
        int installmentsImported = 0;
        int paymentsImported = 0;

        if (!File.Exists(filePath))
        {
            return new ImportResult(false, 0, 0, 0, new List<string> { "File not found." });
        }

        try
        {
            using var workbook = new XLWorkbook(filePath);
            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

            var providers = await context.Providers.ToListAsync();
            var providerMap = providers.ToDictionary(p => p.Name.ToLower().Trim(), p => p.Id);

            // Import Purchases
            var wsPurchases = workbook.Worksheets.FirstOrDefault(w => w.Name == "Purchases");
            var idMap = new Dictionary<int, int>(); // Old PurchaseId -> New PurchaseId

            if (wsPurchases != null)
            {
                var rows = wsPurchases.RangeUsed().RowsUsed().Skip(1);
                foreach (var r in rows)
                {
                    try
                    {
                        int oldId = r.Cell(1).GetValue<int>();
                        string itemName = r.Cell(2).GetString();
                        string category = r.Cell(3).GetString();
                        string providerName = r.Cell(4).GetString();
                        DateTime pDate = DateTime.Parse(r.Cell(5).GetString());
                        decimal total = (decimal)r.Cell(6).GetDouble();
                        decimal initial = (decimal)r.Cell(7).GetDouble();
                        decimal remaining = (decimal)r.Cell(8).GetDouble();
                        int numInst = r.Cell(9).GetValue<int>();
                        string statusStr = r.Cell(10).GetString();
                        string notes = r.Cell(11).GetString();

                        if (!providerMap.TryGetValue(providerName.ToLower().Trim(), out int pId))
                        {
                            var newProvider = new Provider { Name = providerName, BrandColorHex = "#7C5CFC", IsActive = true };
                            await context.Providers.AddAsync(newProvider);
                            await context.SaveChangesAsync();
                            pId = newProvider.Id;
                            providerMap[providerName.ToLower().Trim()] = pId;
                        }

                        Enum.TryParse<PurchaseStatus>(statusStr, out var status);

                        var purchase = new Purchase
                        {
                            ItemName = itemName,
                            Category = string.IsNullOrWhiteSpace(category) ? "General" : category,
                            ProviderId = pId,
                            PurchaseDate = pDate,
                            TotalAmount = total,
                            InitialPayment = initial,
                            RemainingAmount = remaining,
                            NumberOfInstallments = numInst,
                            Status = status,
                            Notes = notes,
                            CreatedAt = DateTime.UtcNow
                        };

                        await context.Purchases.AddAsync(purchase);
                        await context.SaveChangesAsync();
                        idMap[oldId] = purchase.Id;
                        purchasesImported++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Purchases row {r.RowNumber()}: {ex.Message}");
                    }
                }
            }

            // Import Installments
            var wsInstallments = workbook.Worksheets.FirstOrDefault(w => w.Name == "Installments");
            var instIdMap = new Dictionary<int, int>();

            if (wsInstallments != null)
            {
                var rows = wsInstallments.RangeUsed().RowsUsed().Skip(1);
                foreach (var r in rows)
                {
                    try
                    {
                        int oldInstId = r.Cell(1).GetValue<int>();
                        int oldPurchId = r.Cell(2).GetValue<int>();

                        if (!idMap.TryGetValue(oldPurchId, out int newPurchId))
                            continue;

                        int instNum = r.Cell(4).GetValue<int>();
                        DateTime dueDate = DateTime.Parse(r.Cell(5).GetString());
                        decimal due = (decimal)r.Cell(6).GetDouble();
                        decimal paid = (decimal)r.Cell(7).GetDouble();
                        decimal rem = (decimal)r.Cell(8).GetDouble();
                        string statusStr = r.Cell(9).GetString();
                        string paidDateStr = r.Cell(10).GetString();
                        DateTime? paidDate = string.IsNullOrWhiteSpace(paidDateStr) ? null : DateTime.Parse(paidDateStr);
                        string notes = r.Cell(11).GetString();

                        Enum.TryParse<PaymentStatus>(statusStr, out var status);

                        var inst = new Installment
                        {
                            PurchaseId = newPurchId,
                            InstallmentNumber = instNum,
                            DueDate = dueDate,
                            AmountDue = due,
                            AmountPaid = paid,
                            RemainingAmount = rem,
                            Status = status,
                            PaidDate = paidDate,
                            Notes = notes
                        };

                        await context.Installments.AddAsync(inst);
                        await context.SaveChangesAsync();
                        instIdMap[oldInstId] = inst.Id;
                        installmentsImported++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Installments row {r.RowNumber()}: {ex.Message}");
                    }
                }
            }

            // Import Payments
            var wsPayments = workbook.Worksheets.FirstOrDefault(w => w.Name == "Payments");
            if (wsPayments != null)
            {
                var rows = wsPayments.RangeUsed().RowsUsed().Skip(1);
                foreach (var r in rows)
                {
                    try
                    {
                        int oldPurchId = r.Cell(2).GetValue<int>();
                        int oldInstId = r.Cell(3).GetValue<int>();

                        if (!idMap.TryGetValue(oldPurchId, out int newPurchId))
                            continue;

                        int? newInstId = instIdMap.TryGetValue(oldInstId, out int mappedInstId) ? mappedInstId : null;

                        string provName = r.Cell(4).GetString();
                        DateTime payDate = DateTime.Parse(r.Cell(5).GetString());
                        decimal paid = (decimal)r.Cell(6).GetDouble();
                        string method = r.Cell(7).GetString();
                        string notes = r.Cell(8).GetString();

                        var pm = new Payment
                        {
                            PurchaseId = newPurchId,
                            InstallmentId = newInstId,
                            ProviderName = provName,
                            PaymentDate = payDate,
                            AmountPaid = paid,
                            PaymentMethod = method,
                            Notes = notes,
                            CreatedAt = DateTime.UtcNow
                        };

                        await context.Payments.AddAsync(pm);
                        paymentsImported++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Payments row {r.RowNumber()}: {ex.Message}");
                    }
                }
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            Log.Information("Excel import finished: {Purchases} purchases, {Installments} installments, {Payments} payments.",
                purchasesImported, installmentsImported, paymentsImported);

            return new ImportResult(errors.Count == 0, purchasesImported, installmentsImported, paymentsImported, errors);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to import Excel workbook.");
            return new ImportResult(false, purchasesImported, installmentsImported, paymentsImported, new List<string> { ex.Message });
        }
    }
}
