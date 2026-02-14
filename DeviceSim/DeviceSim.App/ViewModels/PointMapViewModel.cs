using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DeviceSim.App.ViewModels;

public partial class PointMapViewModel : ViewModelBase
{
    private readonly PointsViewModel _pointsViewModel;

    public ObservableCollection<PointViewModel> Points => _pointsViewModel.Points;

    public PointMapViewModel(PointsViewModel pointsViewModel)
    {
        _pointsViewModel = pointsViewModel;
    }

    [RelayCommand]
    public void Refresh()
    {
        // Delegate to the main points view model if needed, or just relying on binding
        // If PointsViewModel has a refresh, call it.
    }
}
