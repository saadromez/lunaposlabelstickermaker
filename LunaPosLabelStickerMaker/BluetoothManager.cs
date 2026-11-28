using System.Diagnostics;
using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace LunaPosLabelStickerMaker;

public class BluetoothPortInfo
{
    public string PortName { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsBluetooth { get; set; } = false;

    public string DisplayText => IsBluetooth
        ? $"📶 {PortName} - {Description} [Bluetooth]"
        : $"🔌 {PortName} - {Description}";

    public override string ToString() => DisplayText;
}

public static class BluetoothManager
{
    public static List<BluetoothPortInfo> GetAvailablePorts()
    {
        var result = new List<BluetoothPortInfo>();
        var seenPorts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Query WMI for rich PNP descriptions
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, DeviceID, Caption FROM Win32_PnPEntity WHERE Name LIKE '%(COM%)'");
            foreach (ManagementObject obj in searcher.Get())
            {
                string name = obj["Name"]?.ToString() ?? "";
                string deviceId = obj["DeviceID"]?.ToString() ?? "";

                var match = Regex.Match(name, @"\((COM\d+)\)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string comPort = match.Groups[1].Value.ToUpperInvariant();
                    string desc = name.Replace($"({comPort})", "").Trim();
                    if (string.IsNullOrWhiteSpace(desc)) desc = "Serial Device";

                    bool isBt = name.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) ||
                               deviceId.Contains("BTHENUM", StringComparison.OrdinalIgnoreCase) ||
                               deviceId.Contains("BTH", StringComparison.OrdinalIgnoreCase);

                    result.Add(new BluetoothPortInfo
                    {
                        PortName = comPort,
                        Description = desc,
                        IsBluetooth = isBt
                    });
                    seenPorts.Add(comPort);
                }
            }
        }
        catch
        {
            // Fallback gracefully if WMI is restricted
        }

        // 2. Query Registry SERIALCOMM as fast secondary detector
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM");
            if (key != null)
            {
                foreach (string valName in key.GetValueNames())
                {
                    string? port = key.GetValue(valName)?.ToString()?.Trim().ToUpperInvariant();
                    if (!string.IsNullOrEmpty(port) && !seenPorts.Contains(port))
                    {
                        bool isBt = valName.Contains("Bth", StringComparison.OrdinalIgnoreCase) ||
                                   valName.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase);
                        string desc = isBt ? "Standard Serial over Bluetooth link" : "Serial Port";

                        result.Add(new BluetoothPortInfo
                        {
                            PortName = port,
                            Description = desc,
                            IsBluetooth = isBt
                        });
                        seenPorts.Add(port);
                    }
                    else if (!string.IsNullOrEmpty(port) && seenPorts.Contains(port))
                    {
                        // If registry says BthModem, ensure isBt is marked true
                        if (valName.Contains("Bth", StringComparison.OrdinalIgnoreCase))
                        {
                            var existing = result.FirstOrDefault(p => string.Equals(p.PortName, port, StringComparison.OrdinalIgnoreCase));
                            if (existing != null) existing.IsBluetooth = true;
                        }
                    }
                }
            }
        }
        catch { }

        // 3. Fallback to SerialPort.GetPortNames() for any remaining ports
        try
        {
            foreach (string p in SerialPort.GetPortNames())
            {
                string up = p.Trim().ToUpperInvariant();
                if (!seenPorts.Contains(up))
                {
                    result.Add(new BluetoothPortInfo
                    {
                        PortName = up,
                        Description = "Communications Port",
                        IsBluetooth = false
                    });
                    seenPorts.Add(up);
                }
            }
        }
        catch { }

        // Sort Bluetooth ports first, then alphabetically by COM number
        return result.OrderByDescending(p => p.IsBluetooth)
                     .ThenBy(p => ExtractComNumber(p.PortName))
                     .ToList();
    }

    private static int ExtractComNumber(string portName)
    {
        var m = Regex.Match(portName, @"\d+");
        return m.Success && int.TryParse(m.Value, out int n) ? n : 999;
    }

    public static void OpenBluetoothSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Could not open Windows Bluetooth Settings: {ex.Message}");
        }
    }

    public static void SendRawBytes(string portName, int baudRate, byte[] data)
    {
        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("No COM / Bluetooth port specified.");

        if (baudRate <= 0) baudRate = 9600;

        using var port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 4000,
            WriteTimeout = 10000,
            DtrEnable = true, // Required for many Bluetooth SPP modules
            RtsEnable = true,
            Handshake = Handshake.None
        };

        try
        {
            port.Open();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to connect to Bluetooth / COM port '{portName}': {ex.Message}\n\nPlease verify that the printer is turned on, paired in Windows, and within Bluetooth range.", ex);
        }

        try
        {
            // Send in chunks of 1024 bytes to prevent buffer overflow on micro Bluetooth chips
            const int chunkSize = 1024;
            int offset = 0;
            while (offset < data.Length)
            {
                int count = Math.Min(chunkSize, data.Length - offset);
                port.Write(data, offset, count);
                offset += count;
                if (offset < data.Length)
                {
                    Thread.Sleep(15);
                }
            }

            // Wait briefly for buffer to flush
            int waited = 0;
            while (port.BytesToWrite > 0 && waited < 5000)
            {
                Thread.Sleep(50);
                waited += 50;
            }
            Thread.Sleep(200);
        }
        finally
        {
            if (port.IsOpen)
            {
                port.Close();
            }
        }
    }
}
