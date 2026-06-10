using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class ColumnWrapper : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public ColumnWrapper(string name)
        {
            Name = name;
        }

        public override string ToString() => Name;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
