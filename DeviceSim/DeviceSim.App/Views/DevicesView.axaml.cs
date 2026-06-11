using Avalonia.Controls;
using Avalonia.Input;
using DeviceSim.App.ViewModels;

namespace DeviceSim.App.Views;

public partial class DevicesView : UserControl
{
    public DevicesView()
    {
        InitializeComponent();
    }

    private void OnNameDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is TextBlock textBlock && textBlock.DataContext is DeviceInstanceViewModel vm)
        {
            vm.OpenPointMapCommand.Execute(null);
        }
    }
}
