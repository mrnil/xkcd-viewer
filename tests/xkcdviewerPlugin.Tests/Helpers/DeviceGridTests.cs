namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;

    using Xunit;

    public class DeviceGridTests
    {
        [Theory]
        [InlineData(DeviceType.Loupedeck70, 3)]
        [InlineData(DeviceType.Loupedeck50, 5)]
        [InlineData(DeviceType.Loupedeck20, 4)]
        public void GetColumns_DeviceSeenOnHardware_ReturnsItsWidth(DeviceType deviceType, Int32 expected)
        {
            Assert.True(DeviceGrid.IsKnown(deviceType));
            Assert.Equal(expected, DeviceGrid.GetColumns(deviceType));
        }

        [Theory]
        [InlineData(DeviceType.Loupedeck30)]
        [InlineData(DeviceType.Loupedeck71)]
        [InlineData((DeviceType)0)]
        public void GetColumns_UnknownDevice_FallsBackToThreeColumns(DeviceType deviceType)
        {
            Assert.False(DeviceGrid.IsKnown(deviceType));
            Assert.Equal(DeviceGrid.FallbackColumns, DeviceGrid.GetColumns(deviceType));
            Assert.Equal(3, DeviceGrid.FallbackColumns);
        }
    }
}
