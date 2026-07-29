using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RagNext.Designer.Avalonia.ViewModels;
using RagNext.Models;

namespace RagNext.Designer.Avalonia.Services
{
    public static class EntityTreeHelper
    {
        public static ObservableCollection<EntityTreeNodeViewModel> BuildTreeNodes<TEntity>(
            IEnumerable<TEntity> entities,
            EntityCategoryTree categoryTree,
            Func<TEntity, Guid> idSelector,
            Func<TEntity, string> nameSelector,
            string defaultIcon) where TEntity : class
        {
            var roots = new ObservableCollection<EntityTreeNodeViewModel>();
            var entityMap = entities.ToDictionary(e => idSelector(e), e => e);
            var assignedIds = new HashSet<Guid>();

            // Recursively build folder nodes
            void BuildFolder(EntityFolder folderModel, ObservableCollection<EntityTreeNodeViewModel> parentChildren, EntityTreeNodeViewModel? parentNode)
            {
                var folderNode = EntityTreeNodeViewModel.CreateFolderNode(folderModel, parentNode);
                parentChildren.Add(folderNode);

                // Add subfolders first
                foreach (var childFolder in folderModel.Children)
                {
                    BuildFolder(childFolder, folderNode.Children, folderNode);
                }

                // Add entity leaves inside this folder
                foreach (var id in folderModel.EntityIds.ToList())
                {
                    if (entityMap.TryGetValue(id, out var entity))
                    {
                        var leafNode = EntityTreeNodeViewModel.CreateEntityNode(entity, id, nameSelector(entity), defaultIcon, folderNode);
                        folderNode.Children.Add(leafNode);
                        assignedIds.Add(id);
                    }
                }
            }

            // Build top-level root folders
            foreach (var rootFolder in categoryTree.Roots)
            {
                BuildFolder(rootFolder, roots, null);
            }

            // Unassigned entities stay at top-level root
            foreach (var entity in entities)
            {
                var id = idSelector(entity);
                if (!assignedIds.Contains(id))
                {
                    var leafNode = EntityTreeNodeViewModel.CreateEntityNode(entity, id, nameSelector(entity), defaultIcon, null);
                    roots.Add(leafNode);
                }
            }

            return roots;
        }

        public static void AddFolder(EntityCategoryTree categoryTree, EntityTreeNodeViewModel? selectedNode, string folderName = "New Folder")
        {
            var newFolder = new EntityFolder { Id = Guid.NewGuid(), Name = folderName };
            if (selectedNode != null && selectedNode.IsFolder && selectedNode.FolderModel != null)
            {
                selectedNode.FolderModel.Children.Add(newFolder);
            }
            else
            {
                categoryTree.Roots.Add(newFolder);
            }
        }

        public static void RemoveFolder(EntityCategoryTree categoryTree, EntityFolder folderModel)
        {
            // Remove from top-level roots
            if (categoryTree.Roots.Contains(folderModel))
            {
                categoryTree.Roots.Remove(folderModel);
                return;
            }

            // Search recursively in parent folders
            bool RemoveFromParent(IEnumerable<EntityFolder> folders)
            {
                foreach (var parent in folders)
                {
                    if (parent.Children.Contains(folderModel))
                    {
                        parent.Children.Remove(folderModel);
                        return true;
                    }
                    if (RemoveFromParent(parent.Children)) return true;
                }
                return false;
            }

            RemoveFromParent(categoryTree.Roots);
        }

        public static void MoveEntityToFolder(EntityCategoryTree categoryTree, Guid entityId, EntityFolder? targetFolder)
        {
            // Remove entityId from any existing folder
            void RemoveEntityFromFolders(IEnumerable<EntityFolder> folders)
            {
                foreach (var f in folders)
                {
                    f.EntityIds.Remove(entityId);
                    RemoveEntityFromFolders(f.Children);
                }
            }

            RemoveEntityFromFolders(categoryTree.Roots);

            // Add to target folder if specified (null means root level)
            if (targetFolder != null)
            {
                if (!targetFolder.EntityIds.Contains(entityId))
                {
                    targetFolder.EntityIds.Add(entityId);
                }
            }
        }
    }
}
