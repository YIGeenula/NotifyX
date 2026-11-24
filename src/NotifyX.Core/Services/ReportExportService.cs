using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using CodexBlaze.PaymentReminder.Core.Data;
using CodexBlaze.PaymentReminder.Core.Models;
using Serilog;

namespace CodexBlaze.PaymentReminder.Core.Services;

public interface IReportExportService
{
    Task ExportFilteredExcelReportAsync(string filePath, DateTime? fromDate, DateTime? toDate, int? providerId, string periodLabel);
    Task ExportFilteredPdfReportAsync(string filePath, DateTime? fromDate, DateTime? toDate, int? providerId, string periodLabel);
}

public class ReportExportService : IReportExportService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IDashboardService _dashboardService;

    static ReportExportService()
    {
        // QuestPDF Community License for non-commercial / community use
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ReportExportService(IDbContextFactory<AppDbContext> contextFactory, IDashboardService dashboardService)
    {
        _contextFactory = contextFactory;
        _dashboardService = dashboardService;
    }

    public async Task ExportFilteredExcelReportAsync(string filePath, DateTime? fromDate, DateTime? toDate, int? providerId, string periodLabel)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var stats = await _dashboardService.GetDashboardStatsAsync(fromDate, toDate, providerId);

        var purchaseQuery = context.Purchases.Include(p => p.Provider).Include(p => p.Installments).AsNoTracking().AsQueryable();
        var paymentQuery = context.Payments.Include(p => p.Purchase).AsNoTracking().AsQueryable();

        if (providerId.HasValue && providerId.Value > 0)
        {
            purchaseQuery = purchaseQuery.Where(p => p.ProviderId == providerId.Value);
            paymentQuery = paymentQuery.Where(p => p.Purchase != null && p.Purchase.ProviderId == providerId.Value);
        }

        if (fromDate.HasValue)
        {
            purchaseQuery = purchaseQuery.Where(p => p.PurchaseDate >= fromDate.Value.Date);
            paymentQuery = paymentQuery.Where(p => p.PaymentDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            purchaseQuery = purchaseQuery.Where(p => p.PurchaseDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));
            paymentQuery = paymentQuery.Where(p => p.PaymentDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));
        }

        var purchases = await purchaseQuery.OrderByDescending(p => p.PurchaseDate).ToListAsync();
        var payments = await paymentQuery.OrderByDescending(p => p.PaymentDate).ToListAsync();

        using var workbook = new XLWorkbook();

        // 1. Executive Summary Sheet
        var wsSummary = workbook.Worksheets.Add("Executive Summary");
        wsSummary.Range("A1:E1").Merge().Value = "⚡ NotifyX - Payment Reminder Financial Report";
        wsSummary.Range("A1:E1").Style.Font.Bold = true;
        wsSummary.Range("A1:E1").Style.Font.FontSize = 16;
        wsSummary.Range("A1:E1").Style.Font.FontColor = XLColor.White;
        wsSummary.Range("A1:E1").Style.Fill.BackgroundColor = XLColor.FromHtml("#7C5CFC");
        wsSummary.Range("A1:E1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        wsSummary.Cell("A2").Value = $"Reporting Period: {periodLabel}";
        wsSummary.Cell("A2").Style.Font.Italic = true;
        wsSummary.Cell("A3").Value = $"Generated On: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

        wsSummary.Cell("A5").Value = "Metric";
        wsSummary.Cell("B5").Value = "Value (LKR)";
        wsSummary.Range("A5:B5").Style.Font.Bold = true;
        wsSummary.Range("A5:B5").Style.Fill.BackgroundColor = XLColor.FromHtml("#1D2433");
        wsSummary.Range("A5:B5").Style.Font.FontColor = XLColor.White;

        wsSummary.Cell("A6").Value = "Total Purchases Committed";
        wsSummary.Cell("B6").Value = stats.TotalPurchaseValue;
        wsSummary.Cell("B6").Style.NumberFormat.Format = "#,##0.00";

        wsSummary.Cell("A7").Value = "Total Paid / Settled";
        wsSummary.Cell("B7").Value = stats.TotalPaidAmount;
        wsSummary.Cell("B7").Style.NumberFormat.Format = "#,##0.00";
        wsSummary.Cell("B7").Style.Font.FontColor = XLColor.FromHtml("#16A34A");

        wsSummary.Cell("A8").Value = "Remaining Outstanding Balance";
        wsSummary.Cell("B8").Value = stats.TotalOutstandingBalance;
        wsSummary.Cell("B8").Style.NumberFormat.Format = "#,##0.00";
        wsSummary.Cell("B8").Style.Font.FontColor = XLColor.FromHtml("#7C5CFC");

        wsSummary.Cell("A9").Value = "Active Purchases Count";
        wsSummary.Cell("B9").Value = stats.ActivePurchasesCount;

        wsSummary.Cell("A10").Value = "Completed Purchases Count";
        wsSummary.Cell("B10").Value = stats.CompletedPurchasesCount;

        wsSummary.Cell("A11").Value = "Upcoming Due Installments";
        wsSummary.Cell("B11").Value = stats.UpcomingInstallmentsCount;

        wsSummary.Cell("A12").Value = "Overdue Installments";
        wsSummary.Cell("B12").Value = stats.OverdueInstallmentsCount;
        wsSummary.Cell("B12").Style.Font.FontColor = XLColor.FromHtml("#DC2626");

        wsSummary.Columns().AdjustToContents();

        // 2. Purchases Sheet
        var wsPurchases = workbook.Worksheets.Add("Purchases");
        string[] pHeaders = { "ID", "Item Name", "Provider", "Category", "Purchase Date", "Total Amount (LKR)", "Initial Downpayment", "Remaining Amount", "Installments", "Status" };
        for (int c = 0; c < pHeaders.Length; c++)
        {
            var cell = wsPurchases.Cell(1, c + 1);
            cell.Value = pHeaders[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#21C8B7");
        }

        int pRow = 2;
        foreach (var p in purchases)
        {
            wsPurchases.Cell(pRow, 1).Value = p.Id;
            wsPurchases.Cell(pRow, 2).Value = p.ItemName;
            wsPurchases.Cell(pRow, 3).Value = p.Provider?.Name ?? "Provider";
            wsPurchases.Cell(pRow, 4).Value = p.Category;
            wsPurchases.Cell(pRow, 5).Value = p.PurchaseDate.ToString("yyyy-MM-dd");
            wsPurchases.Cell(pRow, 6).Value = p.TotalAmount;
            wsPurchases.Cell(pRow, 6).Style.NumberFormat.Format = "#,##0.00";
            wsPurchases.Cell(pRow, 7).Value = p.InitialPayment;
            wsPurchases.Cell(pRow, 7).Style.NumberFormat.Format = "#,##0.00";
            wsPurchases.Cell(pRow, 8).Value = p.RemainingAmount;
            wsPurchases.Cell(pRow, 8).Style.NumberFormat.Format = "#,##0.00";
            wsPurchases.Cell(pRow, 9).Value = p.NumberOfInstallments;
            wsPurchases.Cell(pRow, 10).Value = p.Status.ToString();
            pRow++;
        }
        wsPurchases.Columns().AdjustToContents();

        // 3. Payments Sheet
        var wsPayments = workbook.Worksheets.Add("Payments");
        string[] payHeaders = { "Payment ID", "Purchase Item", "Provider", "Payment Date", "Amount Paid (LKR)", "Method", "Notes" };
        for (int c = 0; c < payHeaders.Length; c++)
        {
            var cell = wsPayments.Cell(1, c + 1);
            cell.Value = payHeaders[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#7C5CFC");
        }

        int payRow = 2;
        foreach (var pay in payments)
        {
            wsPayments.Cell(payRow, 1).Value = pay.Id;
            wsPayments.Cell(payRow, 2).Value = pay.Purchase?.ItemName ?? "-";
            wsPayments.Cell(payRow, 3).Value = pay.ProviderName;
            wsPayments.Cell(payRow, 4).Value = pay.PaymentDate.ToString("yyyy-MM-dd");
            wsPayments.Cell(payRow, 5).Value = pay.AmountPaid;
            wsPayments.Cell(payRow, 5).Style.NumberFormat.Format = "#,##0.00";
            wsPayments.Cell(payRow, 6).Value = pay.PaymentMethod;
            wsPayments.Cell(payRow, 7).Value = pay.Notes;
            payRow++;
        }
        wsPayments.Columns().AdjustToContents();

        workbook.SaveAs(filePath);
        Log.Information("Generated Excel Financial Report at {FilePath}", filePath);
    }

    public async Task ExportFilteredPdfReportAsync(string filePath, DateTime? fromDate, DateTime? toDate, int? providerId, string periodLabel)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var stats = await _dashboardService.GetDashboardStatsAsync(fromDate, toDate, providerId);

        var purchaseQuery = context.Purchases.Include(p => p.Provider).Include(p => p.Installments).AsNoTracking().AsQueryable();
        var paymentQuery = context.Payments.Include(p => p.Purchase).AsNoTracking().AsQueryable();

        if (providerId.HasValue && providerId.Value > 0)
        {
            purchaseQuery = purchaseQuery.Where(p => p.ProviderId == providerId.Value);
            paymentQuery = paymentQuery.Where(p => p.Purchase != null && p.Purchase.ProviderId == providerId.Value);
        }

        if (fromDate.HasValue)
        {
            purchaseQuery = purchaseQuery.Where(p => p.PurchaseDate >= fromDate.Value.Date);
            paymentQuery = paymentQuery.Where(p => p.PaymentDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            purchaseQuery = purchaseQuery.Where(p => p.PurchaseDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));
            paymentQuery = paymentQuery.Where(p => p.PaymentDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));
        }

        var purchases = await purchaseQuery.OrderByDescending(p => p.PurchaseDate).Take(25).ToListAsync();
        var payments = await paymentQuery.OrderByDescending(p => p.PaymentDate).Take(25).ToListAsync();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI"));

                // Header
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Row(r =>
                            {
                                r.AutoItem().Text("Notify").FontSize(22).Bold().FontColor("#1A202C");
                                r.AutoItem().Text("X").FontSize(24).ExtraBold().FontColor("#7C5CFC");
                            });
                            titleCol.Item().Text("PERSONAL PAYMENT REMINDER & BNPL TRACKER").FontSize(9).Bold().FontColor("#718096");
                        });

                        row.RelativeItem().AlignRight().Column(metaCol =>
                        {
                            metaCol.Item().Text("FINANCIAL STATEMENT").FontSize(14).Bold().FontColor("#7C5CFC");
                            metaCol.Item().Text($"Period: {periodLabel}").FontSize(9).FontColor("#4A5568");
                            metaCol.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(8).FontColor("#A0AEC0");
                        });
                    });

                    col.Item().PaddingVertical(8).LineHorizontal(1.5f).LineColor("#E2E8F0");
                });

                // Content
                page.Content().PaddingVertical(10).Column(col =>
                {
                    // KPI Highlight Boxes
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor("#E2E8F0").Background("#F7FAFC").Padding(10).Column(c =>
                        {
                            c.Item().Text("COMMITTED").FontSize(8).Bold().FontColor("#718096");
                            c.Item().Text($"LKR {stats.TotalPurchaseValue:N2}").FontSize(13).Bold().FontColor("#1A202C");
                            c.Item().Text($"{stats.ActivePurchasesCount} Active purchases").FontSize(8).FontColor("#A0AEC0");
                        });

                        row.Spacing(10);

                        row.RelativeItem().Border(1).BorderColor("#E2E8F0").Background("#F0FDF4").Padding(10).Column(c =>
                        {
                            c.Item().Text("TOTAL SETTLED").FontSize(8).Bold().FontColor("#16A34A");
                            c.Item().Text($"LKR {stats.TotalPaidAmount:N2}").FontSize(13).Bold().FontColor("#16A34A");
                            c.Item().Text($"{stats.CompletedPurchasesCount} Completed").FontSize(8).FontColor("#A0AEC0");
                        });

                        row.Spacing(10);

                        row.RelativeItem().Border(1).BorderColor("#E2E8F0").Background("#FAF5FF").Padding(10).Column(c =>
                        {
                            c.Item().Text("OUTSTANDING").FontSize(8).Bold().FontColor("#7C5CFC");
                            c.Item().Text($"LKR {stats.TotalOutstandingBalance:N2}").FontSize(13).Bold().FontColor("#7C5CFC");
                            c.Item().Text($"{stats.UpcomingInstallmentsCount} Due installments").FontSize(8).FontColor("#A0AEC0");
                        });
                    });

                    col.Item().PaddingTop(16).Text("Recent Purchases & Installments").FontSize(12).Bold().FontColor("#1A202C");

                    // Purchases Table
                    col.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2.5f);
                            columns.RelativeColumn(2.5f);
                            columns.RelativeColumn(1.8f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background("#7C5CFC").Padding(5).Text("Item").Bold().FontColor(Colors.White).FontSize(9);
                            header.Cell().Background("#7C5CFC").Padding(5).Text("Provider").Bold().FontColor(Colors.White).FontSize(9);
                            header.Cell().Background("#7C5CFC").Padding(5).Text("Date").Bold().FontColor(Colors.White).FontSize(9);
                            header.Cell().Background("#7C5CFC").Padding(5).AlignRight().Text("Total").Bold().FontColor(Colors.White).FontSize(9);
                            header.Cell().Background("#7C5CFC").Padding(5).AlignRight().Text("Remaining").Bold().FontColor(Colors.White).FontSize(9);
                            header.Cell().Background("#7C5CFC").Padding(5).AlignCenter().Text("Status").Bold().FontColor(Colors.White).FontSize(9);
                        });

                        foreach (var p in purchases)
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor("#EDF2F7").Padding(5).Text(p.ItemName).FontSize(8).Medium();
                            table.Cell().BorderBottom(0.5f).BorderColor("#EDF2F7").Padding(5).Text(p.Provider?.Name ?? "-").FontSize(8);
                            table.Cell().BorderBottom(0.5f).BorderColor("#EDF2F7").Padding(5).Text(p.PurchaseDate.ToString("yyyy-MM-dd")).FontSize(8);
                            table.Cell().BorderBottom(0.5f).BorderColor("#EDF2F7").Padding(5).AlignRight().Text($"LKR {p.TotalAmount:N2}").FontSize(8);
                            table.Cell().BorderBottom(0.5f).BorderColor("#EDF2F7").Padding(5).AlignRight().Text($"LKR {p.RemainingAmount:N2}").FontSize(8).Bold().FontColor("#7C5CFC");
                            table.Cell().BorderBottom(0.5f).BorderColor("#EDF2F7").Padding(5).AlignCenter().Text(p.Status.ToString()).FontSize(8).FontColor(p.Status == PurchaseStatus.Completed ? "#16A34A" : "#2563EB");
                        }
                    });

                    // Payments Table
                    if (payments.Count > 0)
                    {
                        col.Item().PaddingTop(16).Text("Recent Settled Payments").FontSize(12).Bold().FontColor("#1A202C");

                        col.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background("#21C8B7").Padding(5).Text("Item").Bold().FontColor(Colors.White).FontSize(9);
                                header.Cell().Background("#21C8B7").Padding(5).Text("Provider").Bold().FontColor(Colors.White).FontSize(9);
                                header.Cell().Background("#21C8B7").Padding(5).Text("Paid Date").Bold().FontColor(Colors.White).FontSize(9);
                                header.Cell().Background("#21C8B7").Padding(5).AlignRight().Text("Amount Paid").Bold().FontColor(Colors.White).FontSize(9);
                                header.Cell().Background("#21C8B7").Padding(5).Text("Method").Bold().FontColor(Colors.White).FontSize(9);
                            });

                            foreach (var pay in payments)
                            {
                                table.Cell().BorderBottom(0.5f).BorderColor("#EDF2F7").Padding(5).Text(pay.Purchase?.ItemName ?? "-").FontSize(8).Medium();
                                table.Cell().BorderBottom(0.5f).BorderColor("#EDF2F7").Padding(5).Text(pay.ProviderName).FontSize(8);
                                table.Cell().BorderBottom(0.5f).BorderColor("#EDF2F7").Padding(5).Text(pay.PaymentDate.ToString("yyyy-MM-dd")).FontSize(8);
                                table.Cell().BorderBottom(0.5f).BorderColor("#EDF2F7").Padding(5).AlignRight().Text($"LKR {pay.AmountPaid:N2}").FontSize(8).Bold().FontColor("#16A34A");
                                table.Cell().BorderBottom(0.5f).BorderColor("#EDF2F7").Padding(5).Text(pay.PaymentMethod).FontSize(8);
                            }
                        });
                    }
                });

                // Footer
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("Generated securely by NotifyX Desktop Application").FontSize(8).FontColor("#A0AEC0");
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
                });
            });
        }).GeneratePdf(filePath);

        Log.Information("Generated PDF Financial Report at {FilePath}", filePath);
    }
}
