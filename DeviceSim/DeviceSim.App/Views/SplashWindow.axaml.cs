using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DeviceSim.App.Views;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
