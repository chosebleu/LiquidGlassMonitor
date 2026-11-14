using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace LiquidGlassMonitor;

internal sealed record MtpStorageInfo(
    string DeviceName,
    string StorageName,
    long TotalBytes,
    long FreeBytes
);

internal static class MtpStorageReader
{
    public static List<MtpStorageInfo> Read()
    {
        var result = new List<MtpStorageInfo>();

        Type? shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType == null)
            return result;

        object? shellObject = Activator.CreateInstance(shellType);
        if (shellObject == null)
            return result;

        try
        {
            dynamic shell = shellObject;
            dynamic computer = shell.NameSpace(17);

            if (computer == null)
                return result;

            foreach (dynamic device in computer.Items())
            {
                try
                {
                    string name = Convert.ToString(device.Name) ?? "";

                    // Les peripheriques MTP exposes par Windows.
                    string path = Convert.ToString(device.Path) ?? "";

                    if (!path.Contains("ms_comp_mtp", StringComparison.OrdinalIgnoreCase))
                        continue;

                    dynamic folder = device.GetFolder;

                    if (folder == null)
                        continue;

                    foreach (dynamic storage in folder.Items())
                    {
                        try
                        {
                            object totalValue =
                                storage.ExtendedProperty("System.Capacity");

                            object freeValue =
                                storage.ExtendedProperty("System.FreeSpace");

                            if (!TryReadBytes(totalValue, out long total) ||
                                !TryReadBytes(freeValue, out long free) ||
                                total <= 0)
                                continue;

                            string storageName =
                                Convert.ToString(storage.Name) ?? "Stockage";

                            result.Add(new MtpStorageInfo(
                                name,
                                storageName,
                                total,
                                Math.Clamp(free, 0, total)
                            ));
                        }
                        catch
                        {
                            // Un stockage inaccessible ne bloque pas les autres.
                        }
                    }
                }
                catch
                {
                    // Un appareil inaccessible ne bloque pas la lecture.
                }
            }
        }
        finally
        {
            if (Marshal.IsComObject(shellObject))
                Marshal.FinalReleaseComObject(shellObject);
        }

        return result;
    }

    private static bool TryReadBytes(object? value, out long bytes)
    {
        bytes = 0;

        if (value == null)
            return false;

        try
        {
            bytes = Convert.ToInt64(
                value,
                CultureInfo.InvariantCulture
            );

            return bytes >= 0;
        }
        catch
        {
            return false;
        }
    }
}