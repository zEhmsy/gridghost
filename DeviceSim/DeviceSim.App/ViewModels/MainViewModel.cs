using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace DeviceSim.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private ViewModelBase _currentView;

    public ViewModelBase DevicesView { get; }
    public PointsViewModel PointsView { get; }
    public PointMapViewModel PointMapView { get; }
    public ViewModelBase TemplatesView { get; }
    public ViewModelBase LogsView { get; }

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
        LogsViewModel logsView)
    {
        DevicesView = devicesView;
        PointsView = pointsView;
        TemplatesView = templatesView;
        LogsView = logsView;
        
        // Create the wrapper VM
        PointMapView = new PointMapViewModel(pointsView);

        _currentView = DevicesView; // Default

        NavigateCommand = new RelayCommand<string>(Navigate);
    }

    private void Navigate(string? viewName)
    {
        switch (viewName)
        {
            case "Devices": CurrentView = DevicesView; break;
            case "Points": CurrentView = PointsView; break;
            case "PointMap": CurrentView = PointMapView; break;
            case "Templates": CurrentView = TemplatesView; break;
            case "Logs": CurrentView = LogsView; break;
        }
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
