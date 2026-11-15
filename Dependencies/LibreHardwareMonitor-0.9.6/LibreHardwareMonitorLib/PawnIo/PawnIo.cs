using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using PInvoke = Windows.Win32.PInvoke;

namespace LibreHardwareMonitor.PawnIo;

public class PawnIo
{
    private const uint DEVICE_TYPE = 41394u << 16;
    private const int FN_NAME_LENGTH = 32;
    private const uint IOCTL_PIO_EXECUTE_FN = 0x841 << 2;
    private const uint IOCTL_PIO_LOAD_BINARY = 0x821 << 2;

    private readonly SafeFileHandle _handle;

    static PawnIo()
    {
        using RegistryKey subKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");

        if (Version.TryParse(subKey?.GetValue("DisplayVersion") as string, out Version version))
        {
            Version = version;
        }
        else
        {
            using RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

            using RegistryKey subKeyWow64 = registryKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");

            if (Version.TryParse(subKeyWow64?.GetValue("DisplayVersion") as string, out version))
            {
                Version = version;
            }
        }
    }

    private PawnIo(SafeFileHandle handle) => _handle = handle;

    /// <summary>
    /// Gets a value indicating whether PawnIO is installed on the system.
    /// </summary>
    public static bool IsInstalled => Version is not null;

    /// <summary>
    /// Retrieves the version information for the installed PawnIO.
    /// </summary>
    public static Version Version { get; }

    /// <summary>
    /// Gets a value indicating whether the underlying handle is currently valid and open.
    /// </summary>
    public bool IsLoaded => _handle is
    {
        IsInvalid: false,
        IsClosed: false
    };

    internal static unsafe PawnIo LoadModuleFromResource(Assembly assembly, string resourceName)
    {
        byte[] bin;
        string overrideDirectory = Environment.GetEnvironmentVariable("LGM_PAWNIO_OVERRIDE_DIR");

        if (!string.IsNullOrWhiteSpace(overrideDirectory))
        {
            const string resourcePrefix = "LibreHardwareMonitor.Resources.PawnIo.";

            string[] allowedModules =
            {
                "AMDFamily0F.bin",
                "AMDFamily10.bin",
                "AMDFamily17.bin",
                "IntelMSR.bin",
                "IsaBridgeEC.bin",
                "LpcACPIEC.bin",
                "LpcCrOSEC.bin",
                "LpcIO.bin",
                "RyzenSMU.bin",
                "SmbusI801.bin",
                "SmbusNCT6793.bin",
                "SmbusPIIX4.bin"
            };

            if (!resourceName.StartsWith(resourcePrefix, StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected PawnIO resource.");

            string fileName = resourceName.Substring(resourcePrefix.Length);

            if (Array.IndexOf(allowedModules, fileName) < 0)
                throw new InvalidOperationException("Unauthorized PawnIO module.");

            string rootPath = Path.GetFullPath(overrideDirectory);
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, fileName));

            if (!fullPath.StartsWith(rootPath.TrimEnd(Path.DirectorySeparatorChar) +
                                     Path.DirectorySeparatorChar,
                                     StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid PawnIO module path.");

            if (!File.Exists(fullPath))
                throw new FileNotFoundException("External PawnIO module not found.", fullPath);

            bin = File.ReadAllBytes(fullPath);

            if (bin.Length == 0)
                throw new InvalidDataException("External PawnIO module is empty.");
        }
        else
        {
            using Stream stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException("Embedded PawnIO module not found.");

            using MemoryStream memory = new();
            stream.CopyTo(memory);
            bin = memory.ToArray();
        }

        SafeFileHandle handle = PInvoke.CreateFile(@"\\?\GLOBALROOT\Device\PawnIO",
                                                   (uint)FileAccess.ReadWrite,
                                                   FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
                                                   null,
                                                   FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                                                   FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL,
                                                   null);

        if (handle.IsInvalid)
        {
            handle.Dispose();
            return new PawnIo(null);
        }

        try
        {
            fixed (byte* pIn = bin)
            {
                if (PInvoke.DeviceIoControl((HANDLE)handle.DangerousGetHandle(),
                                            (uint)ControlCode.LoadBinary,
                                            pIn,
                                            (uint)bin.Length,
                                            null,
                                            0u,
                                            null,
                                            null))
                {
                    return new PawnIo(handle);
                }
            }

            handle.Dispose();
            return new PawnIo(null);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }
    public void Close()
    {
        if (IsLoaded)
            _handle.Close();
    }

    public unsafe long[] Execute(string name, long[] input, int outLength)
    {
        if (IsLoaded)
        {
            byte[] output = new byte[outLength * sizeof(long)];
            byte[] totalInput = new byte[(input.Length * sizeof(long)) + FN_NAME_LENGTH];
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(name), 0, totalInput, 0, Math.Min(FN_NAME_LENGTH - 1, name.Length));
            Buffer.BlockCopy(input, 0, totalInput, FN_NAME_LENGTH, input.Length * sizeof(long));

            uint read = 0;

            fixed (byte* pIn = totalInput, pOut = output)
            {
                if (PInvoke.DeviceIoControl((HANDLE)_handle.DangerousGetHandle(), (uint)ControlCode.Execute, pIn, (uint)totalInput.Length, pOut, (uint)output.Length, &read, null))
                {
                    long[] outp = new long[read / sizeof(long)];
                    Buffer.BlockCopy(output, 0, outp, 0, (int)read);
                    return outp;
                }
            }
        }

        return new long[outLength];
    }

    public unsafe int ExecuteHr(string name, long[] inBuffer, uint inSize, long[] outBuffer, uint outSize, out uint returnSize)
    {
        if (inBuffer.Length < inSize)
            throw new ArgumentOutOfRangeException(nameof(inSize));

        if (outBuffer.Length < outSize)
            throw new ArgumentOutOfRangeException(nameof(outSize));

        if (!IsLoaded)
        {
            returnSize = 0;
            return 0;
        }

        uint read = 0;

        byte[] output = new byte[outSize * sizeof(long)];
        byte[] totalInput = new byte[(inSize * sizeof(long)) + FN_NAME_LENGTH];
        Buffer.BlockCopy(Encoding.ASCII.GetBytes(name), 0, totalInput, 0, Math.Min(FN_NAME_LENGTH - 1, name.Length));
        Buffer.BlockCopy(inBuffer, 0, totalInput, FN_NAME_LENGTH, inBuffer.Length * sizeof(long));

        fixed (byte* pIn = totalInput, pOut = output)
        {
            if (PInvoke.DeviceIoControl((HANDLE)_handle.DangerousGetHandle(), (uint)ControlCode.Execute, pIn, (uint)totalInput.Length, pOut, (uint)output.Length, &read, null))
            {
                Buffer.BlockCopy(output, 0, outBuffer, 0, Math.Min((int)read, outBuffer.Length * sizeof(long)));
                returnSize = read / sizeof(long);
                return 0;
            }
        }

        returnSize = 0;
        return PInvoke.HRESULT_FROM_WIN32((WIN32_ERROR)Marshal.GetLastWin32Error());
    }

    private enum ControlCode : uint
    {
        LoadBinary = DEVICE_TYPE | IOCTL_PIO_LOAD_BINARY,
        Execute = DEVICE_TYPE | IOCTL_PIO_EXECUTE_FN
    }
}
