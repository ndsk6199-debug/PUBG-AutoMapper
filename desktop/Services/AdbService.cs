using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PubgAutoMapper.Services;

public sealed record AdbDevice(string Serial, string State, string Model);

public sealed class AdbService
{
    public string FindAdb()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "tools", "adb", "adb.exe"),
            @"C:\platform-tools\adb.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "platform-tools", "adb.exe")
        };

        var local = candidates.FirstOrDefault(File.Exists);
        if (!string.IsNullOrWhiteSpace(local)) return local;

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var exe = Path.Combine(dir.Trim(), "adb.exe");
            if (File.Exists(exe)) return exe;
        }

        throw new FileNotFoundException(
            "adb.exe was not found. Checked bundled tools/adb, C:\\platform-tools, user platform-tools, and PATH.");
    }

    public async Task<IReadOnlyList<AdbDevice>> GetDevicesAsync(CancellationToken ct = default)
    {
        var r = await RunAsync("devices -l", ct);
        if (r.ExitCode != 0) throw new InvalidOperationException(r.StdErr.Trim());
        var list = new List<AdbDevice>();
        foreach (var raw in r.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 2 || p[0].Equals("List", StringComparison.OrdinalIgnoreCase)) continue;
            var model = p.FirstOrDefault(x => x.StartsWith("model:"))?.Substring(6) ?? "";
            list.Add(new AdbDevice(p[0], p[1], model));
        }
        return list;
    }

    public async Task<(int Width, int Height)> GetResolutionAsync(string serial, CancellationToken ct = default)
    {
        var r = await RunAsync($"-s {Q(serial)} shell wm size", ct);
        var m = Regex.Match(r.StdOut, @"(\d+)x(\d+)");
        if (!m.Success) throw new InvalidOperationException("Could not read Android screen size.");
        return (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
    }

    public async Task<byte[]> CapturePngAsync(string serial, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(FindAdb(), $"-s {Q(serial)} exec-out screencap -p")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start adb.exe.");
        using var ms = new MemoryStream();
        await p.StandardOutput.BaseStream.CopyToAsync(ms, ct);
        var err = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0) throw new InvalidOperationException(err.Trim());
        return ms.ToArray();
    }

    public async Task RunPublicAsync(string args, CancellationToken ct = default)
    {
        var r = await RunAsync(args, ct);
        if (r.ExitCode != 0) throw new InvalidOperationException(r.StdErr.Trim());
    }

    private async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(string args, CancellationToken ct)
    {
        var adb = FindAdb();
        var psi = new ProcessStartInfo(adb, args)
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start adb.exe at {adb}.");
        var output = await p.StandardOutput.ReadToEndAsync(ct);
        var error = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        return (p.ExitCode, output, error);
    }

    private static string Q(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
