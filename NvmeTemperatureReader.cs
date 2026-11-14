using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace LiquidGlassMonitor;

internal static class NvmeTemperatureReader
{
    private const uint QueryProperty = 0x002D1400;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string name, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle handle, uint control,
        byte[] input, uint inputLength,
        byte[] output, uint outputLength,
        out uint returned, IntPtr overlapped);

    private static uint U32(byte[] b, int offset) =>
        BitConverter.ToUInt32(b, offset);

    private static void W32(byte[] b, int offset, uint value) =>
        BitConverter.GetBytes(value).CopyTo(b, offset);

    public static int? Read(int diskNumber)
    {
        try
        {
            using var disk = CreateFile(
                @"\\.\PhysicalDrive" + diskNumber,
                0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);

            if (disk.IsInvalid)
                return null;

            byte[] buffer = new byte[4096];

            // STORAGE_PROPERTY_QUERY + STORAGE_PROTOCOL_SPECIFIC_DATA
            W32(buffer, 0, 50);
            W32(buffer, 4, 0);
            W32(buffer, 8, 3);
            W32(buffer, 12, 2);
            W32(buffer, 16, 2);
            W32(buffer, 20, 0);
            W32(buffer, 24, 40);
            W32(buffer, 28, 512);

            if (!DeviceIoControl(
                disk, QueryProperty,
                buffer, (uint)buffer.Length,
                buffer, (uint)buffer.Length,
                out uint returned, IntPtr.Zero))
                return null;

            if (returned < 48 || U32(buffer, 0) != 48 ||
                U32(buffer, 4) != 48)
                return null;

            uint offset = U32(buffer, 24);
            uint length = U32(buffer, 28);

            if (offset < 40 || length < 512)
                return null;

            ulong start = 8UL + offset;

            if (start + length > returned ||
                start + length > (ulong)buffer.Length)
                return null;

            int p = (int)start;
            int kelvin = buffer[p + 1] | (buffer[p + 2] << 8);

            if (kelvin < 200 || kelvin > 500)
                return null;

            return kelvin - 273;
        }
        catch
        {
            return null;
        }
    }
}