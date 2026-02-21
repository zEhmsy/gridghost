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
using System.Threading.Tasks;

namespace DeviceSim.App;

public partial class App : Application
{
    public new static App Current => (App)Application.Current!;
    public IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        // Remove Avalonia data validation check 
        BindingPlugins.DataValidators.RemoveAt(0);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var splashVm = new SplashWindowViewModel();
            var splash = new SplashWindow { DataContext = splashVm };
            desktop.MainWindow = splash;
            splash.Show();

            // Background initialization
            var initTask = Task.Run(() => 
            {
                var services = new ServiceCollection();
                ConfigureServices(services);
                Services = services.BuildServiceProvider();
                
                // Force load device manager to trigger background repository loading
                _ = Services.GetRequiredService<DeviceManager>();
            });

            // Wait for animation or timeout (3s)
            // The VM already invokes AnimationCompleted when 48 frames are done.
            var tcs = new TaskCompletionSource<bool>();
            splashVm.AnimationCompleted += () => tcs.TrySetResult(true);
            
            // Timeout after 3.5s just in case
            var delayTask = Task.Delay(3500);

            await Task.WhenAny(tcs.Task, delayTask);
            await initTask;

            // Transition to MainWindow
            var mainVm = Services.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow { DataContext = mainVm };
            
            desktop.MainWindow = mainWindow;
            mainWindow.Show();
            splash.Close();
            splashVm.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<ILogSink, LogService>();
        services.AddSingleton<ConfigurationService>();
        services.AddSingleton<IPointStore, PointStore>();

        // Template Path (Cross-platform)
        var templatePath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "GridGhost", "Templates");
        
        // Ensure templates exist (seed from app install dir if empty)
        if (!System.IO.Directory.Exists(templatePath))
        {
            System.IO.Directory.CreateDirectory(templatePath);
        }

        var appTemplates = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Templates");
        if (System.IO.Directory.Exists(appTemplates) && !System.Linq.Enumerable.Any(System.IO.Directory.EnumerateFiles(templatePath, "*.json")))
        {
            foreach (var file in System.IO.Directory.EnumerateFiles(appTemplates, "*.json"))
            {
                var dest = System.IO.Path.Combine(templatePath, System.IO.Path.GetFileName(file));
                try 
                {
                    System.IO.File.Copy(file, dest, true);
                }
                catch { /* ignore copy errors */ }
            }
        }
        
        services.AddSingleton<DeviceSim.Core.Services.TemplateRepository>(s => new DeviceSim.Core.Services.TemplateRepository(templatePath)); 
        services.AddSingleton<DeviceSim.App.Services.TemplateRepository>(s => new DeviceSim.App.Services.TemplateRepository(templatePath));
        
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
