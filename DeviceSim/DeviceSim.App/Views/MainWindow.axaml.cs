using Avalonia.Controls;
using DeviceSim.App.ViewModels;

namespace DeviceSim.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.CurrentView is IChangeTracker tracker && tracker.IsDirty)
        {
            e.Cancel = true;
            vm.RequestClose();
        }
    }
}
