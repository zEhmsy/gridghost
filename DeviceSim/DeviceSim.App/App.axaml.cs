using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DeviceSim.App.ViewModels;
using DeviceSim.App.Views;
using DeviceSim.Core.Interfaces;
using DeviceSim.Core.Services;
using DeviceSim.Protocols.Bacnet;
using DeviceSim.Protocols.Modbus;
using Microsoft.Extensions.DependencyInjection;
using System;
using Avalonia.Data.Core.Plugins;

namespace DeviceSim.App;

public partial class App : Application
{
    public new static App Current => (App)Application.Current!;
    public IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Remove Avalonia data validation check 
        BindingPlugins.DataValidators.RemoveAt(0);

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = Services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm
            };
        }

        // Global scheduler start removed. Simulation is now per-device via DeviceManager.

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<ILogSink, LogService>();
        services.AddSingleton<ConfigurationService>();
        services.AddSingleton<IPointStore, PointStore>();
        services.AddSingleton<TemplateRepository>(s => new TemplateRepository(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Templates"))); // Path: bin/Debug/net8.0/Templates
        
        // Adapters
        services.AddSingleton<IProtocolAdapter, ModbusAdapter>();
        services.AddSingleton<IProtocolAdapter, BacnetAdapter>();

        // DeviceManager (depends on adapters)
        services.AddSingleton<DeviceManager>();
        
        // Simulation Scheduler
        services.AddSingleton<SimulationScheduler>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<DevicesViewModel>();
        services.AddTransient<PointsViewModel>();
        services.AddTransient<TemplatesViewModel>();
        services.AddTransient<LogsViewModel>();
    }
}
