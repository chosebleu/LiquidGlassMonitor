using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace LiquidGlassMonitor;

internal static class PortableDeviceDetector
{
    private static readonly Guid WpdClass =
        new("eec5ad98-8080-425f-922a-dabf3de3f69a");

    private const uint DIGCF_PRESENT = 0x00000002;
    private const uint SPDRP_DEVICEDESC = 0x00000000;
    private const uint SPDRP_FRIENDLYNAME = 0x0000000C;

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
        string? enumerator,
        IntPtr hwndParent,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(
        IntPtr deviceInfoSet,
        uint memberIndex,
        ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceRegistryProperty(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData,
        uint property,
        out uint propertyRegDataType,
        StringBuilder propertyBuffer,
        uint propertyBufferSize,
        out uint requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiGetDeviceInstanceId(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData,
        StringBuilder deviceInstanceId,
        uint deviceInstanceIdSize,
        out uint requiredSize);

    [DllImport("setupapi.dll")]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    public static IReadOnlyList<string> GetConnectedDevices()
    {
        var result = new List<string>();
        Guid deviceClass = WpdClass;

        IntPtr handle = SetupDiGetClassDevs(
            ref deviceClass, null, IntPtr.Zero, DIGCF_PRESENT);

        if (handle == new IntPtr(-1))
            return result;

        try
        {
            for (uint index = 0; ; index++)
            {
                var info = new SP_DEVINFO_DATA
                {
                    cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>()
                };

                if (!SetupDiEnumDeviceInfo(handle, index, ref info))
                    break;

                var id = new StringBuilder(1024);

                if (!SetupDiGetDeviceInstanceId(
                    handle, ref info, id, (uint)id.Capacity, out _))
                    continue;

                // Ne retenir que les peripheriques WPD relies par USB.
                if (!id.ToString().StartsWith(
                    @"USB\", StringComparison.OrdinalIgnoreCase))
                    continue;

                var name = new StringBuilder(1024);

                bool found = SetupDiGetDeviceRegistryProperty(
                    handle, ref info, SPDRP_FRIENDLYNAME,
                    out _, name, (uint)(name.Capacity * 2), out _);

                if (!found)
                {
                    found = SetupDiGetDeviceRegistryProperty(
                        handle, ref info, SPDRP_DEVICEDESC,
                        out _, name, (uint)(name.Capacity * 2), out _);
                }

                if (found && !string.IsNullOrWhiteSpace(name.ToString()))
                    result.Add(name.ToString());
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(handle);
        }

        return result;
    }
}