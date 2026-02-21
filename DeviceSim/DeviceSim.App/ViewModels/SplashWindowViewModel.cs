using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DeviceSim.App.ViewModels;

public partial class SplashWindowViewModel : ObservableObject, IDisposable
{
    private const int FrameCount = 48;
    private const int TargetFps = 16;
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(1000.0 / TargetFps);

    private readonly List<Bitmap> _frames = new();
    private DispatcherTimer? _timer;
    private int _currentFrameIndex = 0;

    [ObservableProperty]
    private Bitmap? _currentFrame;

    public event Action? AnimationCompleted;

    public SplashWindowViewModel()
    {
        LoadFrames();
        if (_frames.Any())
        {
            CurrentFrame = _frames[0];
            StartAnimation();
        }
    }

    private void LoadFrames()
    {
        try
        {
            for (int i = 1; i <= FrameCount; i++)
            {
                var uri = new Uri($"avares://DeviceSim.App/Assets/splash_frames/ezgif-frame-{i:D3}.png");
                if (AssetLoader.Exists(uri))
                {
                    using var stream = AssetLoader.Open(uri);
                    _frames.Add(new Bitmap(stream));
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading splash frames: {ex.Message}");
        }

        // Fallback: load static logo if frames missing
        if (!_frames.Any())
        {
            LoadFallback();
        }
    }

    private void LoadFallback()
    {
         try
         {
             var uri = new Uri("avares://DeviceSim.App/Assets/Icons/gridghost_icon.ico");
             if (AssetLoader.Exists(uri))
             {
                 using var stream = AssetLoader.Open(uri);
                 _frames.Add(new Bitmap(stream));
             }
         }
         catch { /* ignore */ }
    }

    private void StartAnimation()
    {
        _timer = new DispatcherTimer
        {
            Interval = FrameInterval
        };
        _timer.Tick += (s, e) => {
            _currentFrameIndex++;
            if (_currentFrameIndex >= _frames.Count)
            {
                _timer.Stop();
                AnimationCompleted?.Invoke();
                return;
            }
            CurrentFrame = _frames[_currentFrameIndex];
        };
        _timer.Start();
    }

    public void Dispose()
    {
        _timer?.Stop();
        foreach (var frame in _frames)
        {
            frame.Dispose();
        }
        _frames.Clear();
    }
}
