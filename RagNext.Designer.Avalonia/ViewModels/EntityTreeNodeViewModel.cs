using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using RagNext.Models;
using RagsCore.Models;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class EntityTreeNodeViewModel : ViewModelBase
    {
        private string _name = string.Empty;
        private bool _isExpanded = true;
        private bool _isSelected;
        private object? _entity;

        public Guid Id { get; set; }
        public bool IsFolder { get; set; }
        public string Icon { get; set; } = "📁";

        public object? Entity
        {
            get => _entity;
            set
            {
                if (_entity is INotifyPropertyChanged oldNpc)
                {
                    oldNpc.PropertyChanged -= OnEntityPropertyChanged;
                }

                _entity = value;

                if (_entity is INotifyPropertyChanged newNpc)
                {
                    newNpc.PropertyChanged += OnEntityPropertyChanged;
                }
            }
        }

        public EntityFolder? FolderModel { get; set; }
        public EntityTreeNodeViewModel? ParentNode { get; set; }
        public ObservableCollection<EntityTreeNodeViewModel> Children { get; } = new();

        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                {
                    if (FolderModel != null && FolderModel.Name != value)
                    {
                        FolderModel.Name = value;
                    }

                    if (Entity is Room r && r.Name != value) r.Name = value;
                    else if (Entity is GameObject g && g.Name != value) g.Name = value;
                    else if (Entity is Character c && c.Name != value) c.Name = value;
                    else if (Entity is GlobalFunction f && f.Name != value) f.Name = value;
                    else if (Entity is GameVariable v && v.Name != value) v.Name = value;
                    else if (Entity is GameTimer t && t.Name != value) t.Name = value;
                }
            }
        }

        private void OnEntityPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Name" || string.IsNullOrEmpty(e.PropertyName))
            {
                string? entityName = null;
                if (sender is Room r) entityName = r.Name;
                else if (sender is GameObject g) entityName = g.Name;
                else if (sender is Character c) entityName = c.Name;
                else if (sender is GlobalFunction f) entityName = f.Name;
                else if (sender is GameVariable v) entityName = v.Name;
                else if (sender is GameTimer t) entityName = t.Name;

                if (entityName != null && _name != entityName)
                {
                    Name = entityName;
                }
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value))
                {
                    if (FolderModel != null)
                    {
                        FolderModel.IsExpanded = value;
                    }
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public static EntityTreeNodeViewModel CreateFolderNode(EntityFolder folder, EntityTreeNodeViewModel? parent = null)
        {
            var node = new EntityTreeNodeViewModel
            {
                Id = folder.Id,
                Name = folder.Name,
                IsFolder = true,
                Icon = "📁",
                IsExpanded = folder.IsExpanded,
                FolderModel = folder,
                ParentNode = parent
            };
            return node;
        }

        public static EntityTreeNodeViewModel CreateEntityNode(object entity, Guid id, string name, string icon, EntityTreeNodeViewModel? parent = null)
        {
            var node = new EntityTreeNodeViewModel
            {
                Id = id,
                Entity = entity,
                Name = name,
                IsFolder = false,
                Icon = icon,
                ParentNode = parent
            };
            return node;
        }
    }
}
