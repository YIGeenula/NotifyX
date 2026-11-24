using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodexBlaze.PaymentReminder.Core.Models;
using CodexBlaze.PaymentReminder.Core.Services;
using System.Collections.ObjectModel;

namespace CodexBlaze.PaymentReminder.Wpf.ViewModels;

public partial class PaymentsViewModel : ObservableObject
{
    private readonly IPaymentService _paymentService;
    private readonly ICloudSyncService _cloudSyncService;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private PaymentStatus? _selectedStatusFilter;

    [ObservableProperty]
    private bool _isPayDialogOpen;

    [ObservableProperty]
    private Installment? _selectedInstallmentForPayment;

    [ObservableProperty]
    private decimal _payAmount;

    [ObservableProperty]
    private DateTime _paymentDate = DateTime.Today;

    [ObservableProperty]
    private string _paymentMethod = "Online / Debit Card";

    [ObservableProperty]
    private string _paymentNotes = string.Empty;

    public ObservableCollection<Installment> Installments { get; } = new();
    public ObservableCollection<Payment> PaymentHistory { get; } = new();

    public PaymentsViewModel(IPaymentService paymentService, ICloudSyncService cloudSyncService)
    {
        _paymentService = paymentService;
        _cloudSyncService = cloudSyncService;
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        await _paymentService.RefreshInstallmentStatusesAsync();

        Installments.Clear();
        var insts = await _paymentService.GetAllInstallmentsAsync(SelectedStatusFilter);
        foreach (var i in insts)
        {
            // Only show pending / active installments in the schedule table (RemainingAmount > 0 and Status != Paid)
            // Once paid, they are logged and tracked in the Payment Transaction History table below.
            if (i.RemainingAmount > 0 && i.Status != PaymentStatus.Paid)
            {
                Installments.Add(i);
            }
        }

        PaymentHistory.Clear();
        var payments = await _paymentService.GetAllPaymentsAsync(SearchQuery);
        foreach (var p in payments) PaymentHistory.Add(p);
    }

    [RelayCommand]
    public void OpenPayDialog(Installment? installment)
    {
        if (installment == null) return;
        SelectedInstallmentForPayment = installment;
        PayAmount = installment.RemainingAmount;
        PaymentDate = DateTime.Today;
        PaymentNotes = string.Empty;
        IsPayDialogOpen = true;
    }

    [RelayCommand]
    public void ClosePayDialog()
    {
        IsPayDialogOpen = false;
        SelectedInstallmentForPayment = null;
    }

    [RelayCommand]
    public async Task SubmitPaymentAsync()
    {
        if (SelectedInstallmentForPayment == null || PayAmount <= 0) return;

        int purchId = SelectedInstallmentForPayment.PurchaseId;
        string itemName = SelectedInstallmentForPayment.Purchase?.ItemName ?? "";
        decimal paidNow = PayAmount;

        await _paymentService.RecordPaymentAsync(
            SelectedInstallmentForPayment.Id,
            PayAmount,
            PaymentDate,
            PaymentMethod,
            PaymentNotes);

        // Real-time Cloud Payment sync
        _ = Task.Run(async () =>
        {
            await _cloudSyncService.RecordPaymentInCloudAsync(purchId, itemName, paidNow);
        });

        IsPayDialogOpen = false;
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task DeletePaymentRecordAsync(Payment? payment)
    {
        if (payment == null) return;
        bool deleted = await _paymentService.DeletePaymentAsync(payment.Id);
        if (deleted)
        {
            await LoadDataAsync();
        }
    }
}
