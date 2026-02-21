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
            devicePoints.TryAdd(point.Key, new PointValue { 
                Value = point.Type == "bool" ? false : 0.0, 
                ExpectedType = point.Type, 
                Source = PointSource.Manual 
            });
        }
        _store.AddOrUpdate(deviceId, devicePoints, (k, v) => devicePoints);
    }

    public void RemoveDevice(string deviceId)
    {
        _store.TryRemove(deviceId, out _);
    }

    public void SetValue(string deviceId, string pointKey, object value, PointSource source, string? displayValue = null)
    {
        if (_store.TryGetValue(deviceId, out var devicePoints) && devicePoints.TryGetValue(pointKey, out var existing))
        {
            // Debug Logs for Diagnosis 
            if (pointKey == "H10" || pointKey == "C96" || pointKey == "supply_fan_speed" || pointKey == "fan_status")
            {
                System.Diagnostics.Debug.WriteLine($"[STORE-DEBUG] Update {pointKey} | Expected: {existing.ExpectedType} | IncomingType: {value?.GetType().Name} | Value: {value} | Source: {source}");
            }

            // Strongly typed guard clause for numeric vs boolean
            if (existing.ExpectedType != "bool" && value is bool)
            {
                System.Diagnostics.Debug.WriteLine($"[STORE-ERROR] App/MVVM attempted to assign true/false bool to numeric point {pointKey}! Source: {source}. Blocked.");
                return; 
            }

            if (existing.ExpectedType == "bool" && !(value is bool))
            {
                if (value is IConvertible) value = Convert.ToBoolean(value);
            }

            devicePoints.AddOrUpdate(pointKey, 
                _ => new PointValue { Value = value, ExpectedType = existing.ExpectedType, Source = source, LastUpdated = DateTime.UtcNow, DisplayValue = displayValue, OverrideStatus = existing.OverrideStatus },
                (_, pt) => 
                {
                    pt.Value = value;
                    pt.Source = source;
                    pt.LastUpdated = DateTime.UtcNow;
                    pt.DisplayValue = displayValue;
                    return pt;
                });

            if (devicePoints.TryGetValue(pointKey, out var updatedPoint))
            {
                 OnPointChanged?.Invoke(deviceId, pointKey, updatedPoint);
            }
        }
    }

    public void UpdateOverrideStatus(string deviceId, string pointKey, string? status)
    {
        if (_store.TryGetValue(deviceId, out var devicePoints) && devicePoints.TryGetValue(pointKey, out var existing))
        {
            existing.OverrideStatus = status;
            OnPointChanged?.Invoke(deviceId, pointKey, existing);
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
