using System;
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

        public Guid Id { get; set; }
        public bool IsFolder { get; set; }
        public string Icon { get; set; } = "📁";
        public object? Entity { get; set; }
        public EntityFolder? FolderModel { get; set; }
        public EntityTreeNodeViewModel? ParentNode { get; set; }
        public ObservableCollection<EntityTreeNodeViewModel> Children { get; } = new();

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
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
                Name = name,
                IsFolder = false,
                Icon = icon,
                Entity = entity,
                ParentNode = parent
            };
            return node;
        }
    }
}
