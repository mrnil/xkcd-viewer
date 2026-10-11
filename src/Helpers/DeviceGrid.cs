namespace Loupedeck.xkcdviewerPlugin
{
    using System;
    using System.Collections.Generic;

    // Grid widths observed on hardware (docs/ai/product.md, "Hardware facts"). The SDK has no API for the number
    // of keys on a folder page, so a device that isn't listed here is assumed to be the keypad's 3x3.
    public static class DeviceGrid
    {
        public const Int32 FallbackColumns = 3;

        private static readonly Dictionary<DeviceType, Int32> _columns = new Dictionary<DeviceType, Int32>
        {
            [DeviceType.Loupedeck70] = 3,   // MX Creative Keypad
            [DeviceType.Loupedeck50] = 5,   // Loupedeck Live S
            [DeviceType.Loupedeck20] = 4,   // Loupedeck CT
        };

        public static Boolean IsKnown(DeviceType deviceType) => _columns.ContainsKey(deviceType);

        public static Int32 GetColumns(DeviceType deviceType) =>
            _columns.TryGetValue(deviceType, out var columns) ? columns : FallbackColumns;
    }
}
