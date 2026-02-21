using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceSim.Core.Models;
using DeviceSim.App.Validation;
using System.Text;

namespace DeviceSim.App.ViewModels;

public partial class TemplateEditorViewModel : ViewModelBase
{
    private readonly DeviceTemplate _template;
    
    [ObservableProperty]
    private string _id;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private ProtocolType _protocol;
    
    [ObservableProperty]
    private int _defaultPort;

    [ObservableProperty]
    private ObservableCollection<PointEditorViewModel> _points;

    [ObservableProperty]
    private string _validationErrors = string.Empty;

    [ObservableProperty]
    private bool _hasErrors;

    [ObservableProperty]
    private bool _isDirty;

    partial void OnNameChanged(string value) { IsDirty = true; Validate(); }
    partial void OnProtocolChanged(ProtocolType value) { IsDirty = true; Validate(); }
    partial void OnDefaultPortChanged(int value) { IsDirty = true; Validate(); }

    public TemplateEditorViewModel(DeviceTemplate template)
    {
        _template = template;
        _id = template.Id;
        _name = template.Name;
        _protocol = template.Protocol;
        _defaultPort = template.Network?.Port ?? 502;
        
        _points = new ObservableCollection<PointEditorViewModel>(
            template.Points.Select(p => new PointEditorViewModel(p))
        );

        foreach (var point in _points)
        {
            point.PropertyChanged += (s, e) => { IsDirty = true; Validate(); };
        }
        
        Validate();
        
        // Listen to changes in points? For now, we validate on Save or explicit check.
        // Ideally we subscribe to property changes.
    }

    public DeviceTemplate GetTemplate()
    {
        // sync back
        _template.Id = Id;
        _template.Name = Name;
        _template.Protocol = Protocol;
        _template.Network = new NetworkConfig { Port = DefaultPort };
        _template.Points = Points.Select(p => p.Point).ToList();
        return _template;
    }

    [RelayCommand]
    public void AddPoint()
    {
        var newPoint = new PointDefinition 
        { 
            Key = $"new_point_{Points.Count + 1}",
            Name = "New Point",
            Modbus = new ModbusPointConfig { Address = 0, Kind = "Holding" }
        };
        var vm = new PointEditorViewModel(newPoint);
        vm.PropertyChanged += (s, e) => { IsDirty = true; Validate(); };
        Points.Add(vm);
        IsDirty = true;
        Validate();
    }

    [RelayCommand]
    public void RemovePoint(PointEditorViewModel point)
    {
        if (point != null && Points.Contains(point))
        {
            Points.Remove(point);
            IsDirty = true;
            Validate();
        }
    }

    public bool Validate()
    {
        var tempTemplate = GetTemplate();
        var errors = ValidationHelper.ValidateTemplate(tempTemplate);
        
        if (errors.Any())
        {
            var sb = new StringBuilder();
            foreach (var err in errors) sb.AppendLine($"• {err}");
            ValidationErrors = sb.ToString();
            HasErrors = true;
            return false;
        }
        
        ValidationErrors = string.Empty;
        HasErrors = false;
        return true;
    }

    public static System.Collections.Generic.List<ProtocolType> ProtocolTypes { get; } = System.Enum.GetValues<ProtocolType>().ToList();

}
