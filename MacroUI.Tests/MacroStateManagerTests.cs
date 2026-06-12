using System;
using System.Collections.Generic;
using MacroUI;
using MacroUI.Services;
using Moq;
using Xunit;

namespace MacroUI.Tests
{
    public class MacroStateManagerTests
    {
        private MacroNode CreateTestTree()
        {
            var node1 = new MacroNode { Name = "Node1", Action = "send:1" };
            var node2 = new MacroNode { Name = "Node2", Action = "send:2" };
            var subCategory = new MacroNode
            {
                Name = "SubCat",
                Children = new Dictionary<string, MacroNode>
                {
                    { "child1", new MacroNode { Name = "Child1", Action = "run:notepad.exe" } }
                }
            };

            return new MacroNode
            {
                Name = "Root",
                Children = new Dictionary<string, MacroNode>
                {
                    { "n1", node1 },
                    { "n2", node2 },
                    { "sub", subCategory }
                }
            };
        }

        [Fact]
        public void HandleCommand_Show_ResetsStateAndFiresEvents()
        {
            // Arrange
            var root = CreateTestTree();
            var manager = new MacroStateManager(root);
            
            bool visibilityChanged = false;
            bool menuChanged = false;
            manager.OnVisibilityChanged += () => visibilityChanged = true;
            manager.OnMenuChanged += () => menuChanged = true;

            // Act
            manager.HandleCommand("SHOW");

            // Assert
            Assert.True(visibilityChanged);
            Assert.True(menuChanged);
            Assert.True(manager.IsVisible);
            Assert.Equal(0, manager.SelectedIndex);
            Assert.Equal(root, manager.CurrentNode);
        }

        [Fact]
        public void HandleCommand_Hide_SetsInvisibleAndFiresEvent()
        {
            // Arrange
            var manager = new MacroStateManager(CreateTestTree());
            manager.HandleCommand("SHOW");
            
            bool visibilityChanged = false;
            manager.OnVisibilityChanged += () => visibilityChanged = true;

            // Act
            manager.HandleCommand("HIDE");

            // Assert
            Assert.True(visibilityChanged);
            Assert.False(manager.IsVisible);
        }

        [Fact]
        public void HandleCommand_Next_IncrementsIndexProperly()
        {
            // Arrange
            var root = CreateTestTree();
            var manager = new MacroStateManager(root);
            manager.HandleCommand("SHOW");

            // Act
            manager.HandleCommand("NEXT");

            // Assert
            Assert.Equal(1, manager.SelectedIndex);

            manager.HandleCommand("NEXT");
            Assert.Equal(2, manager.SelectedIndex);

            manager.HandleCommand("NEXT");
            Assert.Equal(3, manager.SelectedIndex); // 3 items + 1 back button

            // Wrap around
            manager.HandleCommand("NEXT");
            Assert.Equal(0, manager.SelectedIndex);
        }

        [Fact]
        public void HandleCommand_Prev_DecrementsIndexProperly()
        {
            // Arrange
            var root = CreateTestTree();
            var manager = new MacroStateManager(root);
            manager.HandleCommand("SHOW"); // Index 0

            // Act
            manager.HandleCommand("PREV"); // Should wrap to 3 (3 children + 1 back = 4 total)

            // Assert
            Assert.Equal(3, manager.SelectedIndex);

            manager.HandleCommand("PREV");
            Assert.Equal(2, manager.SelectedIndex);
        }

        [Fact]
        public void HandleCommand_SelectAction_FiresExecutionAndHides()
        {
            // Arrange
            var root = CreateTestTree();
            var manager = new MacroStateManager(root);
            manager.HandleCommand("SHOW");

            string executedAction = null;
            manager.OnExecuteAction += (action) => executedAction = action;

            // Act - Select first item (n1 -> "send:1")
            manager.HandleCommand("SELECT");

            // Assert
            Assert.Equal("send:1", executedAction);
            Assert.False(manager.IsVisible);
        }

        [Fact]
        public void HandleCommand_SelectCategory_DrillsDown()
        {
            // Arrange
            var root = CreateTestTree();
            var manager = new MacroStateManager(root);
            manager.HandleCommand("SHOW");

            manager.HandleCommand("NEXT");
            manager.HandleCommand("NEXT"); // Select "sub"

            // Act
            manager.HandleCommand("SELECT");

            // Assert
            Assert.Equal("SubCat", manager.CurrentNode.Name);
            Assert.Equal(0, manager.SelectedIndex);
            Assert.True(manager.IsVisible);
        }

        [Fact]
        public void HandleCommand_Back_GoesUpHierarchy()
        {
            // Arrange
            var root = CreateTestTree();
            var manager = new MacroStateManager(root);
            manager.HandleCommand("SHOW");
            manager.HandleCommand("NEXT");
            manager.HandleCommand("NEXT");
            manager.HandleCommand("SELECT"); // Inside SubCat
            
            Assert.Equal("SubCat", manager.CurrentNode.Name);

            // Act
            manager.HandleCommand("BACK");

            // Assert
            Assert.Equal("Root", manager.CurrentNode.Name);
            Assert.Equal(0, manager.SelectedIndex);
        }

        [Fact]
        public void HandleCommand_NullRootNode_HandlesGracefully()
        {
            // Arrange
            var manager = new MacroStateManager(null);
            
            // Act
            manager.HandleCommand("SHOW");
            manager.HandleCommand("NEXT"); // Should not throw

            // Assert
            Assert.Equal("Root", manager.CurrentNode.Name);
            Assert.Empty(manager.CurrentNode.Children);
        }
        [Fact]
        public void HandleCommand_Next_StartsTimer()
        {
            // Arrange
            var mockTimer = new Mock<MacroUI.Services.ITimer>();
            var manager = new MacroStateManager(CreateTestTree(), mockTimer.Object);
            manager.HandleCommand("SHOW");

            // Act
            manager.HandleCommand("NEXT");

            // Assert
            mockTimer.Verify(t => t.Start(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public void TimerTick_TriggersSelectAction()
        {
            // Arrange
            var mockTimer = new Mock<MacroUI.Services.ITimer>();
            var manager = new MacroStateManager(CreateTestTree(), mockTimer.Object);
            manager.HandleCommand("SHOW");
            manager.HandleCommand("NEXT"); // Move to first item ("Node1")

            string executedAction = null;
            manager.OnExecuteAction += (action) => executedAction = action;

            // Act
            mockTimer.Raise(t => t.OnTick += null);

            // Assert
            Assert.Equal("send:2", executedAction);
            Assert.False(manager.IsVisible);
        }

        [Fact]
        public void HandleCommand_Hide_StopsTimer()
        {
            // Arrange
            var mockTimer = new Mock<MacroUI.Services.ITimer>();
            var manager = new MacroStateManager(CreateTestTree(), mockTimer.Object);
            manager.HandleCommand("SHOW");
            manager.HandleCommand("NEXT");

            // Act
            manager.HandleCommand("HIDE");

            // Assert
            mockTimer.Verify(t => t.Stop(), Times.AtLeastOnce);
        }
    }
}
