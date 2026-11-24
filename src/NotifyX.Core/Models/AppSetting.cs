namespace CodexBlaze.PaymentReminder.Core.Models;

public class AppSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Group { get; set; } = "General";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
