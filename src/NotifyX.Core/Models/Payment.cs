namespace CodexBlaze.PaymentReminder.Core.Models;

public class Payment
{
    public int Id { get; set; }
    public int PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    public int? InstallmentId { get; set; }
    public Installment? Installment { get; set; }

    public string ProviderName { get; set; } = string.Empty;
    public decimal AmountPaid { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.Today;
    public string PaymentMethod { get; set; } = "Online / Debit Card";
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
