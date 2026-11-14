using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

internal static class SafeUsbEject
{
    internal readonly record struct Result(bool Success, string Message);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode,
        EntryPoint = "CM_Locate_DevNodeW")]
    private static extern int LocateDevice(
        out uint deviceNode,
        string deviceId,
        uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode,
        EntryPoint = "CM_Request_Device_EjectW")]
    private static extern int RequestEject(
        uint deviceNode,
        out int vetoType,
        StringBuilder vetoName,
        int vetoNameLength,
        uint flags);

    [DllImport("cfgmgr32.dll", EntryPoint = "CM_Get_Parent")]
    private static extern int GetParent(
        out uint parentNode,
        uint deviceNode,
        uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode,
        EntryPoint = "CM_Get_Device_IDW")]
    private static extern int GetDeviceId(
        uint deviceNode,
        StringBuilder buffer,
        int bufferLength,
        uint flags);
    internal static Task<Result> EjectAsync(string drivePath)
    {
        return Task.Run(() => Eject(drivePath));
    }

    private static Result Eject(string drivePath)
    {
        try
        {
            string letter = drivePath.TrimEnd('\\').ToUpperInvariant();

            if (letter.Length != 2 ||
                letter[1] != ':' ||
                letter[0] < 'A' ||
                letter[0] > 'Z')
            {
                return new Result(false, "Lettre de volume invalide.");
            }

            // Identifier le disque physique associé à la lettre.
            // La commande est en lecture seule.
            string script = @"
$letter = '" + letter + @"'
Get-CimInstance Win32_DiskDrive | ForEach-Object {
    $disk = $_
    if ($disk.InterfaceType -ne 'USB' -or
        $disk.PNPDeviceID -notlike 'USBSTOR\*') {
        return
    }

    Get-CimAssociatedInstance -InputObject $disk `
        -Association Win32_DiskDriveToDiskPartition |
        ForEach-Object {
            Get-CimAssociatedInstance -InputObject $_ `
                -Association Win32_LogicalDiskToPartition |
                Where-Object { $_.DeviceID -eq $letter } |
                ForEach-Object { $disk.PNPDeviceID }
        }
}
";

            var start = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-EncodedCommand");
            start.ArgumentList.Add(
                Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));

            using var process = Process.Start(start);

            if (process == null)
                return new Result(false, "Impossible d'interroger Windows.");

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(15000))
            {
                process.Kill();
                process.WaitForExit();
                return new Result(false, "La recherche du périphérique a expiré.");
            }

            string output = outputTask.GetAwaiter().GetResult().Trim();
            string error = errorTask.GetAwaiter().GetResult();

            if (process.ExitCode != 0)
                return new Result(false, "Identification impossible : " + error);

            string[] ids = output.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);

            if (ids.Length != 1 ||
                !ids[0].StartsWith(
                    "USBSTOR\\",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new Result(
                    false,
                    "Windows n'a pas identifié un périphérique USB unique pour " +
                    letter + ". Aucun retrait effectué.");
            }

            int locateResult = LocateDevice(out uint node, ids[0], 0);

            if (locateResult != 0)
                return new Result(
                    false,
                    "Windows n'a pas retrouvé le périphérique (code " +
                    locateResult + ").");

            // Le disque USBSTOR est un enfant du peripherique USB.
            // Demander le retrait au parent physique immediat.
            int parentResult = GetParent(
                out uint parentNode,
                node,
                0);

            if (parentResult != 0)
                return new Result(
                    false,
                    "Impossible d'identifier le parent USB. Code : " +
                    parentResult);

            // Protection : ne pas demander le retrait d'un hub ou
            // d'un controleur USB partage.
            var parentId = new StringBuilder(512);

            int idResult = GetDeviceId(
                parentNode,
                parentId,
                parentId.Capacity,
                0);

            if (idResult != 0 ||
                !parentId.ToString().StartsWith(
                    "USB\\VID_",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new Result(
                    false,
                    "Le parent n'est pas un peripherique USB attendu. " +
                    "Aucun retrait effectue.");
            }

            var vetoName = new StringBuilder(512);

            int result = RequestEject(
                parentNode,
                out int veto,
                vetoName,
                vetoName.Capacity,
                0);

            if (result == 0)
                return new Result(
                    true,
                    "Windows a accepté l'éjection du périphérique " +
                    letter + ". Vous pouvez le retirer.");

            string detail = vetoName.ToString();

            return new Result(
                false,
                "Windows a refusé l'éjection de " + letter +
                ". Fermez les fichiers et applications utilisant le disque, " +
                "puis réessayez." +
                Environment.NewLine +
                "Code : " + result + " / veto : " + veto +
                (string.IsNullOrWhiteSpace(detail)
                    ? ""
                    : Environment.NewLine + detail));
        }
        catch (Exception ex)
        {
            return new Result(
                false,
                "Aucun retrait effectué. " + ex.Message);
        }
    }
}