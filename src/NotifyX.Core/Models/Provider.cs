namespace CodexBlaze.PaymentReminder.Core.Models;

public class Provider
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string BrandColorHex { get; set; } = "#7C5CFC";
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();

    public override string ToString() => Name;
}
