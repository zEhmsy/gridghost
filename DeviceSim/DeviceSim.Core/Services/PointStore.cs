using System.Collections.Concurrent;
using DeviceSim.Core.Interfaces;
using DeviceSim.Core.Models;

namespace DeviceSim.Core.Services;

public class PointStore : IPointStore
{
    // DeviceId -> (PointKey -> PointValue)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, PointValue>> _store = new();
    
    public event Action<string, string, PointValue>? OnPointChanged;

    public void InitializePoints(string deviceId, IEnumerable<PointDefinition> points)
    {
        var devicePoints = new ConcurrentDictionary<string, PointValue>();
        foreach (var point in points)
        {
            devicePoints.TryAdd(point.Key, new PointValue { Value = 0, Source = PointSource.Manual });
        }
        _store.AddOrUpdate(deviceId, devicePoints, (k, v) => devicePoints);
    }

    public void RemoveDevice(string deviceId)
    {
        _store.TryRemove(deviceId, out _);
    }

    public void SetValue(string deviceId, string pointKey, object value, PointSource source, string? displayValue = null)
    {
        if (_store.TryGetValue(deviceId, out var devicePoints))
        {
            devicePoints.AddOrUpdate(pointKey, 
                _ => new PointValue { Value = value, Source = source, LastUpdated = DateTime.UtcNow, DisplayValue = displayValue },
                (_, existing) => 
                {
                    existing.Value = value;
                    existing.Source = source;
                    existing.LastUpdated = DateTime.UtcNow;
                    existing.DisplayValue = displayValue;
                    return existing;
                });

            if (devicePoints.TryGetValue(pointKey, out var updatedPoint))
            {
                 OnPointChanged?.Invoke(deviceId, pointKey, updatedPoint);
            }
        }
    }

    public PointValue GetValue(string deviceId, string pointKey)
    {
        if (_store.TryGetValue(deviceId, out var devicePoints) && devicePoints.TryGetValue(pointKey, out var value))
        {
            return value;
        }
        return new PointValue(); // Default
    }

    public bool TryGetValue(string deviceId, string pointKey, out PointValue value)
    {
        value = null!;
        if (_store.TryGetValue(deviceId, out var devicePoints) && devicePoints.TryGetValue(pointKey, out var v))
        {
            value = v;
            return true;
        }
        return false;
    }
}
