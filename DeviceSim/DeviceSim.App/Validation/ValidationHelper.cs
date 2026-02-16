using System;
using System.Collections.Generic;
using System.Linq;
using DeviceSim.Core.Models;

namespace DeviceSim.App.Validation;

public static class ValidationHelper
{
    public static List<string> ValidateTemplate(DeviceTemplate template)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            errors.Add("Template Name is required.");
        }

        // Validate Points
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Track Modbus addresses to detect collisions
        // Key format: "{Kind}:{Address}" or "{Kind}:{Address}:Bit{BitStart}-{BitLength}"
        var modbusAddresses = new Dictionary<string, List<PointDefinition>>();

        foreach (var point in template.Points)
        {
            // 1. Unique Key
            if (string.IsNullOrWhiteSpace(point.Key))
            {
                errors.Add($"Point has empty Key.");
            }
            else
            {
                if (keys.Contains(point.Key))
                    errors.Add($"Duplicate Key: '{point.Key}'");
                keys.Add(point.Key);
            }

            // 2. Modbus Validations
            if (point.Modbus != null)
            {
                var m = point.Modbus;
                
                // Address range check (basic)
                if (m.Address < 0 || m.Address > 65535)
                    errors.Add($"Point '{point.Key}': Invalid Address {m.Address}");

                // BitField checks
                if (m.BitField != null)
                {
                    if (m.BitField.StartBit < 0 || m.BitField.StartBit > 15)
                        errors.Add($"Point '{point.Key}': BitStart {m.BitField.StartBit} out of range (0-15)");
                    if (m.BitField.BitLength < 1 || m.BitField.BitLength > 16)
                        errors.Add($"Point '{point.Key}': BitLength {m.BitField.BitLength} out of range (1-16)");
                    if (m.BitField.StartBit + m.BitField.BitLength > 16)
                        errors.Add($"Point '{point.Key}': Bit range exceeds 16 bits");
                }

                // Check collisions
                // Normalized check: 
                // Simple Register: "Holding:10"
                // BitField: "Holding:10" -> Requires checking bit overlap
                
                string addrKey = $"{m.Kind}:{m.Address}";
                
                if (!modbusAddresses.ContainsKey(addrKey))
                {
                    modbusAddresses[addrKey] = new List<PointDefinition>();
                }
                
                var existingPoints = modbusAddresses[addrKey];
                
                foreach (var existing in existingPoints)
                {
                    // If either is a full register (no BitField), it's a collision
                    bool currentIsFull = m.BitField == null;
                    bool existingIsFull = existing.Modbus?.BitField == null;

                    if (currentIsFull || existingIsFull)
                    {
                         errors.Add($"Address Collision: '{point.Key}' overlaps with '{existing.Key}' at {m.Kind} {m.Address}");
                    }
                    else
                    {
                        // Both are bitfields, check overlap
                        var b1 = m.BitField!;
                        var b2 = existing.Modbus!.BitField!;
                        
                        int end1 = b1.StartBit + b1.BitLength;
                        int end2 = b2.StartBit + b2.BitLength;

                        // Overlap logic: Start1 < End2 && Start2 < End1
                        if (b1.StartBit < end2 && b2.StartBit < end1)
                        {
                             errors.Add($"Bit Overlap: '{point.Key}' overlaps with '{existing.Key}' at {m.Kind} {m.Address} (Bits {b1.StartBit}-{end1-1} vs {b2.StartBit}-{end2-1})");
                        }
                    }
                }
                
                modbusAddresses[addrKey].Add(point);
            }
            
            // 3. Scaling
            if (point.Modbus?.Scale == 0)
            {
                errors.Add($"Point '{point.Key}': Scale cannot be 0.");
            }
        }

        return errors;
    }
}
