using MacroUI.Services;
using System;
using System.Runtime.InteropServices;
using Xunit;

namespace MacroUI.Tests
{
    public class IpcServiceTests
    {
        [Fact]
        public void WM_COPYDATA_IsCorrectValue()
        {
            // Assert
            Assert.Equal(0x004A, (int)IpcService.WM_COPYDATA);
        }

        [Fact]
        public void COPYDATASTRUCT_HasCorrectLayout()
        {
            // Arrange
            var type = typeof(IpcService.COPYDATASTRUCT);
            
            // Act
            var isSequential = type.IsLayoutSequential;
            var fields = type.GetFields();

            // Assert
            Assert.True(isSequential, "COPYDATASTRUCT must have sequential layout for marshaling.");
            Assert.Equal("dwData", fields[0].Name);
            Assert.Equal("cbData", fields[1].Name);
            Assert.Equal("lpData", fields[2].Name);
        }
    }
}
