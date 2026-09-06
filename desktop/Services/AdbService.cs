using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace PubgAutoMapper.Services;

public sealed record AdbDevice(string Serial, string State, string Model);

public sealed class AdbService
{
    public string FindAdb()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "tools", "adb", "adb.exe");
        return File.Exists(local) ? local : "adb.exe";
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
        var psi = new ProcessStartInfo(FindAdb(), args)
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("adb.exe not found. Install Android Platform Tools or put adb.exe in tools/adb.");
        var output = await p.StandardOutput.ReadToEndAsync(ct);
        var error = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        return (p.ExitCode, output, error);
    }

    private static string Q(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
