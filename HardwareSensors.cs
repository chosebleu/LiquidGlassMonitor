using System;
using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace LiquidGlassMonitor;

public sealed class HardwareSensors
{
    private readonly Computer computer;
    private bool available;

    public HardwareSensors()
    {
        computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true
        };

        try
        {
            computer.Open();
            available = true;
        }
        catch
        {
            available = false;
        }
    }

    public double? ReadGpu()
    {
        if (!available)
            return null;

        try
        {
            foreach (var hardware in computer.Hardware)
            {
                if (hardware.HardwareType != HardwareType.GpuIntel &&
                    hardware.HardwareType != HardwareType.GpuNvidia &&
                    hardware.HardwareType != HardwareType.GpuAmd)
                    continue;

                hardware.Update();

                var sensors = hardware.Sensors
                    .Where(s => s.SensorType == SensorType.Load &&
                                s.Value.HasValue)
                    .ToList();

                var preferred = sensors.FirstOrDefault(s =>
                    s.Name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase));

                preferred ??= sensors.FirstOrDefault(s =>
                    s.Name.Equals("D3D 3D", StringComparison.OrdinalIgnoreCase));

                if (preferred != null)
                    return Math.Clamp(preferred.Value!.Value, 0, 100);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    public void Close()
    {
        if (!available)
            return;

        try
        {
            computer.Close();
        }
        catch
        {
            // Ne pas interrompre la fermeture du widget.
        }

        available = false;
    }
}