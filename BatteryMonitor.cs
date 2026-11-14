using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace LiquidGlassMonitor;

public partial class MainWindow
{
    [StructLayout(LayoutKind.Sequential)]
    private struct BatteryPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(
        out BatteryPowerStatus status);

    private readonly DispatcherTimer batteryTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(500)
    };

    private void InitializeBatteryMonitor()
    {
        batteryTimer.Tick += (_, _) => UpdateBatteryMonitor();

        Loaded += (_, _) =>
        {
            UpdateBatteryMonitor();
            batteryTimer.Start();
        };

        Closed += (_, _) => batteryTimer.Stop();
    }

    private void UpdateBatteryMonitor()
    {
        if (!GetSystemPowerStatus(out var power) ||
            (power.BatteryFlag & 128) != 0)
        {
            BatteryText.Text = "-- %";
            BatterySourceText.Text = "Absente";
            BatteryFill.Width = 0;
            BatteryBolt.Visibility = System.Windows.Visibility.Collapsed;
            return;
        }

        BatteryText.Text = power.BatteryLifePercent <= 100
            ? power.BatteryLifePercent + " %"
            : "-- %";

        if (power.BatteryLifePercent <= 100)
        {
            BatteryFill.Width = 17.0 * Math.Clamp((double)power.BatteryLifePercent, 0, 100) / 100.0;
        }
        else
        {
            BatteryFill.Width = 0;
        }

        bool secteur = power.ACLineStatus == 1;

        BatterySourceText.Text = secteur
            ? Translate("Secteur", "AC power")
            : power.ACLineStatus == 0
                ? "Batterie"
                : "Inconnu";

        BatteryBolt.Visibility = secteur
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;

        BatteryBody.Stroke = new System.Windows.Media.SolidColorBrush(
            secteur
                ? System.Windows.Media.Color.FromRgb(145, 226, 183)
                : System.Windows.Media.Color.FromRgb(220, 229, 240)
        );
    }
}