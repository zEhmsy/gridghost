using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceSim.Core.Models;
using DeviceSim.Core.Services;
using Avalonia.Threading;

namespace DeviceSim.App.ViewModels;

public partial class DevicesViewModel : ViewModelBase
{
    private readonly DeviceManager _deviceManager;
    private readonly ConfigurationService _configService;
    private bool _isLoadingExportSettings;

    public ObservableCollection<DeviceInstanceViewModel> Devices { get; } = new();

    [ObservableProperty]
    private string _niagaraExportHost = "127.0.0.1";

    [ObservableProperty]
    private string _niagaraExportStatus = "";

    public DevicesViewModel(DeviceManager deviceManager, ConfigurationService configService)
    {
        _deviceManager = deviceManager;
        _configService = configService;
        _deviceManager.OnDeviceUpdated += OnDeviceUpdated;
        
        LoadExportSettings();
        LoadDevices();
    }

    private void LoadExportSettings()
    {
        _isLoadingExportSettings = true;
        var export = _configService.CurrentConfig.NiagaraExport;
        NiagaraExportHost = ShouldUseDetectedHost(export.ExportHost)
            ? DetectPreferredLocalIp()
            : export.ExportHost.Trim();
        NiagaraExportStatus = "Choose Export to build a Niagara manifest";
        _isLoadingExportSettings = false;
    }

    partial void OnNiagaraExportHostChanged(string value) => SaveExportSettings();

    private void SaveExportSettings()
    {
        if (_isLoadingExportSettings) return;

        var export = _configService.CurrentConfig.NiagaraExport;
        export.ExportHost = string.IsNullOrWhiteSpace(NiagaraExportHost)
            ? DetectPreferredLocalIp()
            : NiagaraExportHost.Trim();

        _configService.Save();
        NiagaraExportStatus = "Export host saved";
    }

    private void LoadDevices()
    {
        Devices.Clear();
        foreach (var d in _deviceManager.GetAll())
        {
            Devices.Add(new DeviceInstanceViewModel(d, _deviceManager));
        }
    }

    private void OnDeviceUpdated(DeviceInstance instance)
    {
        Dispatcher.UIThread.Post(() => 
        {
            var vm = Devices.FirstOrDefault(d => d.Id == instance.Id);
            if (vm != null)
            {
                vm.UpdateFromModel();
            }
            else
            {
                Devices.Add(new DeviceInstanceViewModel(instance, _deviceManager));
                System.Diagnostics.Debug.WriteLine($"[DevicesViewModel] Added device. Total Devices in view: {Devices.Count}");
            }
        });
    }

    [RelayCommand]
    public async Task Remove(DeviceInstanceViewModel vm)
    {
        await _deviceManager.RemoveInstanceAsync(vm.Id);
        Devices.Remove(vm);
    }

    [RelayCommand]
    public async Task ExportNiagaraManifest()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return;
            }

            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Niagara Manifest",
                SuggestedFileName = "niagara-manifest.json",
                DefaultExtension = "json",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Niagara Manifest JSON")
                    {
                        Patterns = new[] { "*.json" },
                        MimeTypes = new[] { "application/json" }
                    }
                }
            });

            if (file == null) return;

            var host = string.IsNullOrWhiteSpace(NiagaraExportHost)
                ? DetectPreferredLocalIp()
                : NiagaraExportHost.Trim();
            NiagaraExportHost = host;

            var path = file.Path.LocalPath;
            var result = _configService.ExportNiagaraManifest(path, host);
            NiagaraExportStatus = $"Exported {result.DeviceCount} devices, {result.PointCount} points";
        }
        catch (System.Exception ex)
        {
            NiagaraExportStatus = $"Export failed: {ex.Message}";
        }
    }

    private static bool ShouldUseDetectedHost(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
               || value.Trim() == "127.0.0.1"
               || value.Trim().Equals("localhost", System.StringComparison.OrdinalIgnoreCase);
    }

    private static string DetectPreferredLocalIp()
    {
        try
        {
            var addresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                .Select(a => a.ToString())
                .ToList();

            return addresses.FirstOrDefault(IsPrivateIpv4)
                   ?? addresses.FirstOrDefault()
                   ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    private static bool IsPrivateIpv4(string ip)
    {
        return ip.StartsWith("192.168.")
               || ip.StartsWith("10.")
               || (ip.StartsWith("172.") && int.TryParse(ip.Split('.')[1], out var second) && second >= 16 && second <= 31);
    }
}
