using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceSim.Core.Interfaces;
using DeviceSim.Core.Models;
using Avalonia.Threading;

namespace DeviceSim.App.ViewModels;

public partial class LogsViewModel : ViewModelBase
{
    private readonly ILogSink _logSink;

    public ObservableCollection<LogEntry> Logs { get; } = new();

    public LogsViewModel(ILogSink logSink)
    {
        _logSink = logSink;
        _logSink.OnLog += OnLogReceived;
    }

    private void OnLogReceived(LogEntry entry)
    {
        Dispatcher.UIThread.Post(() => 
        {
            Logs.Insert(0, entry);
            if (Logs.Count > 1000) Logs.RemoveAt(Logs.Count - 1);
        });
    }
    
    [RelayCommand]
    public void Clear()
    {
        Logs.Clear();
    }
}
