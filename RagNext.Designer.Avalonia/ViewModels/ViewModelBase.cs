using CommunityToolkit.Mvvm.ComponentModel;

namespace RagNext.Designer.Avalonia.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    public new void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
    }
}
