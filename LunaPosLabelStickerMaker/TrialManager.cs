using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace LunaPosLabelStickerMaker;

public class TrialStateRecord
{
    public string MachineId { get; set; } = "";
    public long FirstLaunchTicks { get; set; }
    public long LastHeartbeatTicks { get; set; }
    public int RunSeconds { get; set; }
    public bool IsExpired { get; set; }
    public string Signature { get; set; } = "";
}

public static class TrialManager
{
    private static readonly byte[] HmacKey = Encoding.UTF8.GetBytes("LunaPos-LabelStudio-Trial-Security-Key-2026!#$");
    private static readonly string MachineId = ComputeMachineId();
    private static System.Windows.Forms.Timer? _heartbeatTimer;
    private static bool _isExpired = false;
    private static DateTime _firstLaunchUtc;
    private static DateTime _lastHeartbeatUtc;
    private static int _runSeconds = 0;
    private static bool _initialized = false;

    public static event Action? OnTrialExpired;

    public static bool IsTrialActive => AppConfig.IsTrial && !_isExpired;
    public static bool IsTrialExpired => AppConfig.IsTrial && _isExpired;

    public static TimeSpan RemainingTime
    {
        get
        {
            if (!AppConfig.IsTrial) return TimeSpan.FromDays(999);
            if (_isExpired) return TimeSpan.Zero;

            var elapsedWallClock = DateTime.UtcNow - _firstLaunchUtc;
            var elapsedRuntime = TimeSpan.FromSeconds(_runSeconds);
            var maxElapsed = elapsedWallClock > elapsedRuntime ? elapsedWallClock : elapsedRuntime;
            var maxAllowed = TimeSpan.FromMinutes(AppConfig.TrialDurationMinutes);

            if (maxElapsed >= maxAllowed)
            {
                return TimeSpan.Zero;
            }

            return maxAllowed - maxElapsed;
        }
    }

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        if (!AppConfig.IsTrial) return;

        // Load states from all redundant locations
        var records = new List<TrialStateRecord>();
        TryAdd(records, ReadFromRegistry(@"Software\LunaPOS\LabelStudio\TrialInfo"));
        TryAdd(records, ReadFromRegistry(@"Software\Classes\CLSID\{D872E14A-5829-4A73-A3B9-247012F2C5B3}"));
        TryAdd(records, ReadFromFile(GetLocalFilePath()));
        TryAdd(records, ReadFromFile(GetProgramDataFilePath()));

        DateTime now = DateTime.UtcNow;

        if (records.Count == 0)
        {
            // First time running on this device!
            _firstLaunchUtc = now;
            _lastHeartbeatUtc = now;
            _runSeconds = 0;
            _isExpired = false;
            SaveAll();
        }
        else
        {
            // If any record says expired, or any record is tampered with, lock down!
            bool anyExpired = records.Any(r => r.IsExpired);
            bool anyInvalidSig = records.Any(r => !VerifySignature(r));

            // Find earliest first launch across all records
            long earliestTicks = records.Where(r => r.FirstLaunchTicks > 0).Select(r => r.FirstLaunchTicks).DefaultIfEmpty(now.Ticks).Min();
            _firstLaunchUtc = new DateTime(earliestTicks, DateTimeKind.Utc);

            // Find latest heartbeat recorded
            long latestHeartbeatTicks = records.Select(r => r.LastHeartbeatTicks).DefaultIfEmpty(now.Ticks).Max();
            _lastHeartbeatUtc = new DateTime(latestHeartbeatTicks, DateTimeKind.Utc);

            // Maximum accumulated run seconds recorded
            _runSeconds = records.Select(r => r.RunSeconds).DefaultIfEmpty(0).Max();

            // Clock rollback check: If current time is more than 2 minutes in the past relative to last heartbeat
            bool clockRolledBack = now < (_lastHeartbeatUtc - TimeSpan.FromMinutes(2));

            // Check if 30 minutes expired either by wall clock or by cumulative run time
            bool wallClockExpired = (now - _firstLaunchUtc) >= TimeSpan.FromMinutes(AppConfig.TrialDurationMinutes);
            bool runTimeExpired = _runSeconds >= (AppConfig.TrialDurationMinutes * 60);

            if (anyExpired || anyInvalidSig || clockRolledBack || wallClockExpired || runTimeExpired)
            {
                _isExpired = true;
                SaveAll();
            }
            else
            {
                _lastHeartbeatUtc = now;
                SaveAll();
            }
        }

        // Setup background heartbeat timer (ticks every 5 seconds)
        _heartbeatTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _heartbeatTimer.Tick += (s, e) => OnHeartbeat();
        _heartbeatTimer.Start();
    }

    private static void OnHeartbeat()
    {
        if (!AppConfig.IsTrial || _isExpired) return;

        DateTime now = DateTime.UtcNow;
        _runSeconds += 5;

        // Anti-tamper clock check
        if (now < (_lastHeartbeatUtc - TimeSpan.FromMinutes(2)))
        {
            ExpireNow();
            return;
        }

        _lastHeartbeatUtc = now;

        if ((now - _firstLaunchUtc) >= TimeSpan.FromMinutes(AppConfig.TrialDurationMinutes) ||
            _runSeconds >= (AppConfig.TrialDurationMinutes * 60))
        {
            ExpireNow();
            return;
        }

        SaveAll();
    }

    public static void ExpireNow()
    {
        _isExpired = true;
        SaveAll();
        _heartbeatTimer?.Stop();
        OnTrialExpired?.Invoke();
    }

    public static bool CheckAccessAndShowDialog(System.Windows.Forms.IWin32Window? parent = null)
    {
        if (!AppConfig.IsTrial) return true;

        if (_isExpired)
        {
            using var dlg = new TrialExpiredForm();
            dlg.ShowDialog(parent);
            return false;
        }

        return true;
    }

    private static void SaveAll()
    {
        var record = new TrialStateRecord
        {
            MachineId = MachineId,
            FirstLaunchTicks = _firstLaunchUtc.Ticks,
            LastHeartbeatTicks = _lastHeartbeatUtc.Ticks,
            RunSeconds = _runSeconds,
            IsExpired = _isExpired
        };
        record.Signature = ComputeSignature(record);

        WriteToRegistry(@"Software\LunaPOS\LabelStudio\TrialInfo", record);
        WriteToRegistry(@"Software\Classes\CLSID\{D872E14A-5829-4A73-A3B9-247012F2C5B3}", record);
        WriteToFile(GetLocalFilePath(), record);
        WriteToFile(GetProgramDataFilePath(), record);
    }

    private static string ComputeMachineId()
    {
        try
        {
            string raw = $"{Environment.MachineName}|{Environment.UserName}|{Environment.ProcessorCount}|{Environment.OSVersion.VersionString}";
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash)[..16];
        }
        catch
        {
            return "DEFAULT-MACHINE";
        }
    }

    private static string ComputeSignature(TrialStateRecord rec)
    {
        string payload = $"{rec.MachineId}:{rec.FirstLaunchTicks}:{rec.LastHeartbeatTicks}:{rec.RunSeconds}:{rec.IsExpired}";
        using var hmac = new HMACSHA256(HmacKey);
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    private static bool VerifySignature(TrialStateRecord rec)
    {
        if (string.IsNullOrEmpty(rec.Signature)) return false;
        string expected = ComputeSignature(rec);
        return string.Equals(expected, rec.Signature, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryAdd(List<TrialStateRecord> list, TrialStateRecord? rec)
    {
        if (rec != null) list.Add(rec);
    }

    private static TrialStateRecord? ReadFromRegistry(string subKey)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(subKey);
            if (key == null) return null;
            string? json = key.GetValue("Data") as string;
            if (string.IsNullOrEmpty(json)) return null;
            return JsonSerializer.Deserialize<TrialStateRecord>(json);
        }
        catch
        {
            return null;
        }
    }

    private static void WriteToRegistry(string subKey, TrialStateRecord rec)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(subKey);
            if (key != null)
            {
                string json = JsonSerializer.Serialize(rec);
                key.SetValue("Data", json);
            }
        }
        catch { }
    }

    private static string GetLocalFilePath()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LunaPOS");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, ".sys_trial.dat");
    }

    private static string GetProgramDataFilePath()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LunaPOS");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, ".lic_state.sys");
    }

    private static TrialStateRecord? ReadFromFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            string json = File.ReadAllText(path);
            if (string.IsNullOrEmpty(json)) return null;
            return JsonSerializer.Deserialize<TrialStateRecord>(json);
        }
        catch
        {
            return null;
        }
    }

    private static void WriteToFile(string path, TrialStateRecord rec)
    {
        try
        {
            string json = JsonSerializer.Serialize(rec);
            File.WriteAllText(path, json);
            File.SetAttributes(path, FileAttributes.Hidden | FileAttributes.System);
        }
        catch { }
    }
}
