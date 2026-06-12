using System.Collections.Generic;
using System.Linq;
using MacroUI;
using MacroUI.ViewModels;
using Xunit;

namespace MacroUI.Tests
{
    public class MacroTreeViewModelTests
    {
        [Fact]
        public void AddCategoryCommand_AddsCategoryToRoot_WhenNothingSelected()
        {
            // Arrange
            var vm = new MacroTreeViewModel();
            vm.SelectedNode = null;

            // Act
            vm.AddCategoryCommand.Execute(null);

            // Assert
            Assert.Single(vm.RootNodes);
            Assert.Equal("New Category", vm.RootNodes[0].Name);
        }

        [Fact]
        public void AddMacroCommand_AddsMacroUnderCategory_WhenCategorySelected()
        {
            // Arrange
            var vm = new MacroTreeViewModel();
            var categoryMacro = new MacroNode { Name = "Cat1", Children = new Dictionary<string, MacroNode>() };
            var categoryNode = new TreeNodeViewModel("Cat1", categoryMacro, null);
            vm.RootNodes.Add(categoryNode);
            vm.SelectedNode = categoryNode;

            // Act
            vm.AddMacroCommand.Execute(null);

            // Assert
            Assert.Single(categoryNode.Children);
            var addedMacro = categoryNode.Children[0];
            Assert.Equal("New Macro", addedMacro.Name);
            Assert.True(categoryNode.IsExpanded);
        }

        [Fact]
        public void DeleteNodeCommand_RemovesSelectedNode()
        {
            // Arrange
            var vm = new MacroTreeViewModel();
            var macro = new MacroNode { Name = "Test" };
            var node = new TreeNodeViewModel("Test", macro, null);
            vm.RootNodes.Add(node);
            vm.SelectedNode = node;

            // Act
            vm.DeleteNodeCommand.Execute(null);

            // Assert
            Assert.Empty(vm.RootNodes);
        }

        [Fact]
        public void UndoCommand_RevertsAddOperation()
        {
            // Arrange
            var vm = new MacroTreeViewModel();
            vm.AddCategoryCommand.Execute(null);
            Assert.Single(vm.RootNodes); // Just to verify it was added

            // Act
            vm.UndoCommand.Execute(null);

            // Assert
            Assert.Empty(vm.RootNodes);
        }
    }
}
