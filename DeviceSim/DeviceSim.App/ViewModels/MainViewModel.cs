using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows.Input;
using System.Threading.Tasks;

namespace DeviceSim.App.ViewModels;

public record NavigationMessage(string ViewName);
public record SelectDeviceMessage(string? DeviceId);
public record ShowErrorDialogMessage(string Title, string Message);

public partial class MainViewModel : ViewModelBase
{
    private ViewModelBase _currentView;

    private readonly DeviceSim.Core.Services.ConfigurationService _configService;

    public ViewModelBase DevicesView { get; }
    public PointsViewModel PointsView { get; }
    public PointMapViewModel PointMapView { get; }
    public ViewModelBase TemplatesView { get; }
    public ViewModelBase LogsView { get; }

    [ObservableProperty]
    private bool _isSidebarCollapsed;

    partial void OnIsSidebarCollapsedChanged(bool value)
    {
        _configService.CurrentConfig.Ui.SidebarCollapsed = value;
        _configService.Save();
    }

    [RelayCommand]
    public void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
    }

    public ViewModelBase CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public ICommand NavigateCommand { get; }

    public MainViewModel(
        DevicesViewModel devicesView, 
        PointsViewModel pointsView, 
        TemplatesViewModel templatesView, 
        LogsViewModel logsView,
        DeviceSim.Core.Services.DeviceManager deviceManager,
        DeviceSim.Core.Services.ConfigurationService configService)
    {
        _configService = configService;
        IsSidebarCollapsed = _configService.CurrentConfig.Ui.SidebarCollapsed;

        DevicesView = devicesView;
        PointsView = pointsView;
        TemplatesView = templatesView;
        LogsView = logsView;
        
        // Create the wrapper VM
        PointMapView = new PointMapViewModel(pointsView);

        _currentView = DevicesView; // Default

        NavigateCommand = new RelayCommand<string>(Navigate);
        
        deviceManager.OnError += ShowError;
        
        WeakReferenceMessenger.Default.Register<NavigationMessage>(this, (r, m) =>
        {
            Navigate(m.ViewName);
        });
        
        WeakReferenceMessenger.Default.Register<ShowErrorDialogMessage>(this, (r, m) =>
        {
            ShowError(m.Title, m.Message);
        });
    }

    private void Navigate(string? viewName)
    {
        if (string.IsNullOrEmpty(viewName)) return;

        if (CurrentView is IChangeTracker tracker && tracker.IsDirty)
        {
            _targetViewName = viewName;
            IsConfirmNavigateVisible = true;
            return;
        }

        ExecuteNavigation(viewName);
    }

    private void ExecuteNavigation(string viewName)
    {
        if (viewName == "EXIT")
        {
            System.Environment.Exit(0);
            return;
        }
        
        switch (viewName)
        {
            case "Devices": CurrentView = DevicesView; break;
            case "Points": CurrentView = PointsView; break;
            case "PointMap": CurrentView = PointMapView; break;
            case "Templates": CurrentView = TemplatesView; break;
            case "Logs": CurrentView = LogsView; break;
        }
    }

    public void RequestClose()
    {
        if (CurrentView is IChangeTracker tracker && tracker.IsDirty)
        {
            _targetViewName = "EXIT";
            IsConfirmNavigateVisible = true;
        }
        else
        {
            System.Environment.Exit(0);
        }
    }

    private string? _targetViewName;

    [ObservableProperty]
    private bool _isConfirmNavigateVisible;

    [RelayCommand]
    public async Task ConfirmNavigateSaveAsync()
    {
        if (CurrentView is IChangeTracker tracker && tracker.IsDirty)
        {
            var success = await tracker.SaveChangesAsync();
            if (!success) {
                // If save failed, don't navigate yet, user might want to fix it
                IsConfirmNavigateVisible = false;
                return;
            }
        }
        IsConfirmNavigateVisible = false;
        if (_targetViewName != null) ExecuteNavigation(_targetViewName);
        _targetViewName = null;
    }

    [RelayCommand]
    public void ConfirmNavigateDiscard()
    {
        if (CurrentView is IChangeTracker tracker && tracker.IsDirty)
        {
            tracker.DiscardChanges();
        }
        IsConfirmNavigateVisible = false;
        if (_targetViewName != null) ExecuteNavigation(_targetViewName);
        _targetViewName = null;
    }

    [RelayCommand]
    public void ConfirmNavigateCancel()
    {
        IsConfirmNavigateVisible = false;
        _targetViewName = null;
    }

    [ObservableProperty]
    private bool _isErrorDialogVisible;

    [ObservableProperty]
    private string _errorDialogTitle = string.Empty;

    [ObservableProperty]
    private string _errorDialogMessage = string.Empty;

    [RelayCommand]
    public void CloseErrorDialog()
    {
        IsErrorDialogVisible = false;
    }

    public void ShowError(string title, string message)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => 
        {
            ErrorDialogTitle = title;
            ErrorDialogMessage = message;
            IsErrorDialogVisible = true;
        });
    }

    [RelayCommand]
    public void OpenDonation()
    {
        try
        {
            var url = "https://www.buymeacoffee.com/gturturro";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { /* ignored */ }
    }
}
