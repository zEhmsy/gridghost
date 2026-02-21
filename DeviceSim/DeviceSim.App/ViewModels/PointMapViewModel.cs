using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Linq;

namespace DeviceSim.App.ViewModels;

public partial class PointMapViewModel : ViewModelBase
{
    private readonly PointsViewModel _pointsViewModel;

    [ObservableProperty]
    private string? _selectedDeviceId;

    public bool IsDeviceSelected => SelectedDeviceId != null;
    public bool IsNoDeviceSelected => SelectedDeviceId == null;

    partial void OnSelectedDeviceIdChanged(string? value)
    {
        OnPropertyChanged(nameof(IsDeviceSelected));
        OnPropertyChanged(nameof(IsNoDeviceSelected));
    }

    public ObservableCollection<PointViewModel> Points { get; } = new();

    public PointMapViewModel(PointsViewModel pointsViewModel)
    {
        _pointsViewModel = pointsViewModel;
        
        WeakReferenceMessenger.Default.Register<SelectDeviceMessage>(this, (r, m) =>
        {
            SelectedDeviceId = m.DeviceId;
            UpdatePoints();
        });
    }

    public void UpdatePoints()
    {
        Points.Clear();
        if (SelectedDeviceId == null) return;
        
        var group = _pointsViewModel.DeviceGroups.FirstOrDefault(g => g.DeviceId == SelectedDeviceId);
        if (group != null)
        {
            foreach (var p in group.Points)
            {
                Points.Add(p);
            }
        }
    }

    [RelayCommand]
    public void Refresh()
    {
        UpdatePoints();
    }
}
