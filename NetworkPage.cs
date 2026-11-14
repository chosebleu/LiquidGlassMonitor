using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace LiquidGlassMonitor;

public partial class MainWindow
{
    private readonly DispatcherTimer networkTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Queue<double> networkDownHistory = new();
    private readonly Queue<double> networkUpHistory = new();
    private string? networkAdapterId;
    private long networkLastReceived;
    private long networkLastSent;
    private DateTime networkLastSample;
    private DateTime networkLastPing = DateTime.MinValue;
    private bool networkPingBusy;

    private void InitializeNetworkPage()
    {
        networkTimer.Tick += (_, _) =>
        {
            if (!compactMode && carouselPage == 1) RefreshNetworkPage();
            else networkLastSample = DateTime.MinValue;
        };
        networkTimer.Start();
        Closed += (_, _) => networkTimer.Stop();
    }

    private void RefreshNetworkPage()
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .Select(n => new { Nic = n, Properties = n.GetIPProperties() })
                .Where(x => x.Properties.UnicastAddresses.Any(a => a.Address.AddressFamily == AddressFamily.InterNetwork))
                .OrderByDescending(x => x.Properties.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.Any.Equals(g.Address)))
                .ThenByDescending(x => x.Nic.Speed)
                .FirstOrDefault();

            if (nic == null)
            {
                networkAdapterId = null;
                networkLastSample = DateTime.MinValue;
                NetworkInterfaceText.Text = Translate("Aucune connexion", "No connection");
                NetworkIpText.Text = "—";
                NetworkDownText.Text = "—";
                NetworkUpText.Text = "—";
                NetworkPingText.Text = Translate("Hors ligne", "Offline");
                return;
            }

            var adapter = nic.Nic;
            NetworkInterfaceText.Text = adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                ? "Wi-Fi · " + adapter.Name : "Ethernet / " + adapter.Name;
            NetworkIpText.Text = nic.Properties.UnicastAddresses
                .First(a => a.Address.AddressFamily == AddressFamily.InterNetwork).Address.ToString();

            var stats = adapter.GetIPv4Statistics();
            DateTime now = DateTime.UtcNow;
            if (networkAdapterId != adapter.Id || networkLastSample == DateTime.MinValue ||
                stats.BytesReceived < networkLastReceived || stats.BytesSent < networkLastSent)
            {
                networkAdapterId = adapter.Id;
                networkLastReceived = stats.BytesReceived;
                networkLastSent = stats.BytesSent;
                networkLastSample = now;
                NetworkDownText.Text = Translate("0,00", "0.00");
                NetworkUpText.Text = Translate("0,00", "0.00");
                networkDownHistory.Clear();
                networkUpHistory.Clear();
            }
            else
            {
                double elapsed = Math.Max(0.1, (now - networkLastSample).TotalSeconds);
                double down = Math.Max(0, stats.BytesReceived - networkLastReceived) / elapsed / 1048576.0;
                double up = Math.Max(0, stats.BytesSent - networkLastSent) / elapsed / 1048576.0;
                NetworkDownText.Text = down.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo(currentLanguage == "en" ? "en-US" : "fr-FR"));
                NetworkUpText.Text = up.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo(currentLanguage == "en" ? "en-US" : "fr-FR"));
                networkDownHistory.Enqueue(down);
                networkUpHistory.Enqueue(up);
                while (networkDownHistory.Count > 60) networkDownHistory.Dequeue();
                while (networkUpHistory.Count > 60) networkUpHistory.Dequeue();
                networkLastReceived = stats.BytesReceived;
                networkLastSent = stats.BytesSent;
                networkLastSample = now;
            }
            RenderNetworkGraph();
            if (!networkPingBusy && now - networkLastPing > TimeSpan.FromSeconds(10))
            {
                networkLastPing = now;
                _ = RefreshNetworkPingAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Réseau : " + ex.Message);
            NetworkInterfaceText.Text = Translate("Mesure indisponible", "Measurement unavailable");
            networkLastSample = DateTime.MinValue;
        }
    }

    private void NetworkGraph_SizeChanged(object sender, SizeChangedEventArgs e) => RenderNetworkGraph();

    private void RenderNetworkGraph()
    {
        if (NetworkGraphCanvas == null) return;
        double width = NetworkGraphCanvas.ActualWidth;
        double height = NetworkGraphCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;
        double maximum = Math.Max(0.05, networkDownHistory.Concat(networkUpHistory).DefaultIfEmpty(0).Max() * 1.15);
        NetworkDownGraph.Points = NetworkPoints(networkDownHistory, width, height, maximum);
        NetworkUpGraph.Points = NetworkPoints(networkUpHistory, width, height, maximum);
    }

    private static PointCollection NetworkPoints(IEnumerable<double> samples, double width, double height, double maximum)
    {
        var points = new PointCollection();
        var values = samples.ToArray();
        for (int i = 0; i < values.Length; i++)
            points.Add(new Point(i * width / 59.0, height - Math.Min(1, values[i] / maximum) * (height - 4)));
        return points;
    }

    private async Task RefreshNetworkPingAsync()
    {
        networkPingBusy = true;
        try
        {
            using var ping = new Ping();
            var response = await ping.SendPingAsync("1.1.1.1", 1500);
            NetworkPingText.Text = response.Status == IPStatus.Success
                ? response.RoundtripTime + " ms" : Translate("Indisponible", "Unavailable");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Ping : " + ex.Message);
            NetworkPingText.Text = Translate("Indisponible", "Unavailable");
        }
        finally { networkPingBusy = false; }
    }
}
