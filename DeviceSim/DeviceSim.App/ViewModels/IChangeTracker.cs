using System;
using System.Threading.Tasks;

namespace DeviceSim.App.ViewModels;

public interface IChangeTracker
{
    bool IsDirty { get; }
    Task<bool> SaveChangesAsync();
    void DiscardChanges();
}
