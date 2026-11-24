using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodexBlaze.PaymentReminder.Core.Models;
using CodexBlaze.PaymentReminder.Core.Services;
using System.Collections.ObjectModel;

namespace CodexBlaze.PaymentReminder.Wpf.ViewModels;

public partial class PurchasesViewModel : ObservableObject
{
    private readonly IPurchaseService _purchaseService;
    private readonly InstallmentCalculationService _calculationService;
    private readonly ICloudSyncService _cloudSyncService;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private Provider? _selectedProviderFilter;

    [ObservableProperty]
    private PurchaseStatus? _selectedStatusFilter;

    [ObservableProperty]
    private Purchase? _selectedPurchase;

    [ObservableProperty]
    private bool _isCreateDialogOpen;

    [ObservableProperty]
    private bool _isEditDialogOpen;

    [ObservableProperty]
    private Purchase? _editingPurchase;

    // Create / Edit Form Fields
    [ObservableProperty]
    private string _newItemName = string.Empty;

    [ObservableProperty]
    private string _newDescription = string.Empty;

    [ObservableProperty]
    private string _newCategory = "Electronics";

    [ObservableProperty]
    private Provider? _newProvider;

    [ObservableProperty]
    private DateTime _newPurchaseDate = DateTime.Today;

    [ObservableProperty]
    private decimal _newTotalAmount = 30000m;

    [ObservableProperty]
    private decimal _newInitialPayment = 0m;

    [ObservableProperty]
    private int _newInstallmentCount = 3;

    [ObservableProperty]
    private ScheduleInterval _newInterval = ScheduleInterval.Monthly;

    [ObservableProperty]
    private DateTime _newFirstDueDate = DateTime.Today.AddMonths(1);

    [ObservableProperty]
    private string _newNotes = string.Empty;

    public ObservableCollection<Purchase> Purchases { get; } = new();
    public ObservableCollection<Provider> Providers { get; } = new();
    public ObservableCollection<Installment> PreviewInstallments { get; } = new();

    public PurchasesViewModel(IPurchaseService purchaseService, InstallmentCalculationService calculationService, ICloudSyncService cloudSyncService)
    {
        _purchaseService = purchaseService;
        _calculationService = calculationService;
        _cloudSyncService = cloudSyncService;
    }

    [RelayCommand]
    public async Task LoadPurchasesAsync()
    {
        Providers.Clear();
        var provs = await _purchaseService.GetProvidersAsync();
        foreach (var p in provs) Providers.Add(p);

        if (NewProvider == null && Providers.Count > 0)
        {
            NewProvider = Providers.First();
        }

        Purchases.Clear();
        var list = await _purchaseService.GetAllPurchasesAsync(SearchQuery, SelectedProviderFilter?.Id, SelectedStatusFilter);
        foreach (var p in list) Purchases.Add(p);

        GeneratePreview();
    }

    [RelayCommand]
    partial void OnNewTotalAmountChanged(decimal value)
    {
        RecalculateBnplInstallments();
    }

    partial void OnNewInstallmentCountChanged(int value)
    {
        RecalculateBnplInstallments();
    }

    partial void OnNewFirstDueDateChanged(DateTime value)
    {
        GeneratePreview();
    }

    partial void OnNewIntervalChanged(ScheduleInterval value)
    {
        GeneratePreview();
    }

    private void RecalculateBnplInstallments()
    {
        if (NewTotalAmount > 0 && NewInstallmentCount > 0)
        {
            // Buy Now, Pay Later standard logic:
            // e.g. 999 with 3 installments -> 1st installment (333) paid upfront on purchase date.
            // Remaining 2 installments (666) are scheduled for future due dates.
            decimal singleShare = decimal.Round(NewTotalAmount / NewInstallmentCount, 2, MidpointRounding.AwayFromZero);
            NewInitialPayment = singleShare;
        }
        else
        {
            NewInitialPayment = 0m;
        }
        GeneratePreview();
    }

    public void GeneratePreview()
    {
        PreviewInstallments.Clear();
        decimal financed = Math.Max(0m, NewTotalAmount - NewInitialPayment);
        int remainingInstallmentCount = Math.Max(0, NewInstallmentCount - 1);
        if (financed <= 0 || remainingInstallmentCount <= 0) return;

        var schedule = _calculationService.GenerateSchedule(financed, remainingInstallmentCount, NewFirstDueDate, NewInterval);
        foreach (var item in schedule)
        {
            PreviewInstallments.Add(new Installment
            {
                InstallmentNumber = item.InstallmentNumber + 1, // Numbered starting from #2 since #1 is paid at purchase
                AmountDue = item.AmountDue,
                RemainingAmount = item.AmountDue,
                DueDate = item.DueDate,
                Status = PaymentStatus.Upcoming
            });
        }
    }

    [RelayCommand]
    public void OpenCreateDialog()
    {
        NewItemName = string.Empty;
        NewDescription = string.Empty;
        NewTotalAmount = 30000m;
        NewInstallmentCount = 3;
        NewPurchaseDate = DateTime.Today;
        NewFirstDueDate = DateTime.Today.AddMonths(1);
        RecalculateBnplInstallments();
        IsCreateDialogOpen = true;
    }

    [RelayCommand]
    public void OpenEditDialog(Purchase? purchase)
    {
        if (purchase == null) return;
        EditingPurchase = purchase;
        NewItemName = purchase.ItemName;
        NewDescription = purchase.Description;
        NewCategory = purchase.Category;
        NewProvider = Providers.FirstOrDefault(p => p.Id == purchase.ProviderId) ?? Providers.FirstOrDefault();
        NewTotalAmount = purchase.TotalAmount;
        NewPurchaseDate = purchase.PurchaseDate;
        NewNotes = purchase.Notes;
        IsEditDialogOpen = true;
    }

    [RelayCommand]
    public void CloseEditDialog()
    {
        IsEditDialogOpen = false;
        EditingPurchase = null;
    }

    [RelayCommand]
    public async Task UpdateExistingPurchaseAsync()
    {
        if (EditingPurchase == null || string.IsNullOrWhiteSpace(NewItemName)) return;

        EditingPurchase.ItemName = NewItemName.Trim();
        EditingPurchase.Description = NewDescription.Trim();
        EditingPurchase.Category = NewCategory.Trim();
        if (NewProvider != null)
        {
            EditingPurchase.ProviderId = NewProvider.Id;
            EditingPurchase.Provider = NewProvider;
        }
        EditingPurchase.PurchaseDate = NewPurchaseDate;
        EditingPurchase.Notes = NewNotes.Trim();

        await _purchaseService.UpdatePurchaseAsync(EditingPurchase);

        // Real-time Cloud update
        _ = Task.Run(async () =>
        {
            await _cloudSyncService.UpdatePurchaseInCloudAsync(EditingPurchase);
        });

        IsEditDialogOpen = false;
        EditingPurchase = null;
        await LoadPurchasesAsync();
    }

    [RelayCommand]
    public void CloseCreateDialog()
    {
        IsCreateDialogOpen = false;
    }

    [RelayCommand]
    public async Task SavePurchaseAsync()
    {
        if (string.IsNullOrWhiteSpace(NewItemName)) return;
        if (NewProvider == null) return;
        if (NewTotalAmount <= 0) return;

        GeneratePreview();

        var purchase = new Purchase
        {
            ItemName = NewItemName.Trim(),
            Description = NewDescription.Trim(),
            Category = string.IsNullOrWhiteSpace(NewCategory) ? "General" : NewCategory.Trim(),
            ProviderId = NewProvider.Id,
            PurchaseDate = NewPurchaseDate,
            TotalAmount = NewTotalAmount,
            InitialPayment = NewInitialPayment,
            RemainingAmount = Math.Max(0m, NewTotalAmount - NewInitialPayment),
            NumberOfInstallments = NewInstallmentCount,
            Notes = NewNotes.Trim(),
            Status = PurchaseStatus.Active
        };

        var installments = PreviewInstallments.ToList();
        var created = await _purchaseService.CreatePurchaseAsync(purchase, installments);
        created.Provider = NewProvider;

        // Auto sync to Google Sheets & Webhook asynchronously
        _ = Task.Run(async () =>
        {
            await _cloudSyncService.SyncPurchaseAsync(created);
        });

        IsCreateDialogOpen = false;
        await LoadPurchasesAsync();
    }

    [RelayCommand]
    public async Task DeletePurchaseAsync(Purchase? purchase)
    {
        if (purchase == null) return;
        int pId = purchase.Id;
        string pName = purchase.ItemName;

        bool deleted = await _purchaseService.DeletePurchaseAsync(pId);
        if (deleted)
        {
            Purchases.Remove(purchase);
            if (SelectedPurchase?.Id == pId)
            {
                SelectedPurchase = null;
            }

            // Real-time delete from Google Sheet as well
            _ = Task.Run(async () =>
            {
                await _cloudSyncService.DeletePurchaseFromCloudAsync(pId, pName);
            });
        }
    }
}
