using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class StringWrapper : INotifyPropertyChanged
    {
        private string _value = string.Empty;
        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged();
                }
            }
        }

        public StringWrapper(string val)
        {
            Value = val;
        }

        public override string ToString() => Value;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
