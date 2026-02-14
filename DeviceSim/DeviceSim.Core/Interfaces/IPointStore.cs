using DeviceSim.Core.Models;

namespace DeviceSim.Core.Interfaces;

public interface IPointStore
{
    // Basic CRUD
    void SetValue(string deviceId, string pointKey, object value, PointSource source, string? displayValue = null);
    PointValue GetValue(string deviceId, string pointKey);
    bool TryGetValue(string deviceId, string pointKey, out PointValue value);
    
    // Bulk operations
    void InitializePoints(string deviceId, IEnumerable<PointDefinition> points);
    void RemoveDevice(string deviceId);
    
    // Events
    event Action<string, string, PointValue> OnPointChanged; // deviceId, pointKey, newValue
}
