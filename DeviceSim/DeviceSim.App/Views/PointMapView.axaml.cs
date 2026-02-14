using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DeviceSim.App.Views;

public partial class PointMapView : UserControl
{
    public PointMapView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
