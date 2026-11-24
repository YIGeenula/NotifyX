namespace CodexBlaze.PaymentReminder.Core.Models;

public class Purchase
{
    public int Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    
    public int ProviderId { get; set; }
    public Provider? Provider { get; set; }

    public DateTime PurchaseDate { get; set; } = DateTime.Today;
    public decimal TotalAmount { get; set; }
    public decimal InitialPayment { get; set; }
    public decimal RemainingAmount { get; set; }
    public int NumberOfInstallments { get; set; }
    public string Notes { get; set; } = string.Empty;

    public string? ImagePath { get; set; }
    public string? ReceiptAttachmentPath { get; set; }

    public PurchaseStatus Status { get; set; } = PurchaseStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Installment> Installments { get; set; } = new List<Installment>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
