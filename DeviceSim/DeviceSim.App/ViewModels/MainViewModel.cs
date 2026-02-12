using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace DeviceSim.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private ViewModelBase _currentView;

    public DevicesViewModel DevicesView { get; }
    public PointsViewModel PointsView { get; }
    public TemplatesViewModel TemplatesView { get; }
    public LogsViewModel LogsView { get; }

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

        _currentView = DevicesView; // Default

        NavigateCommand = new RelayCommand<string>(Navigate);
    }

    private void Navigate(string? viewName)
    {
        switch (viewName)
        {
            case "Devices": CurrentView = DevicesView; break;
            case "Points": CurrentView = PointsView; break;
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
