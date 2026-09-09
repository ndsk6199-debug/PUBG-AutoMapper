using System.IO;
using System.Net.Sockets;
using System.Text;

namespace PubgAutoMapper.Services;

public sealed class ControlBridge : IDisposable
{
    private const int AgentPort = 27184;
    private readonly AdbService _adb;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public ControlBridge(AdbService adb) => _adb = adb;

    public async Task ConnectAsync(string serial, CancellationToken ct = default)
    {
        // Desktop is the TCP client, agent is the TCP server on Android.
        await _adb.RunPublicAsync($"-s \"{serial}\" forward --remove tcp:{AgentPort}", ct);
        await _adb.RunPublicAsync($"-s \"{serial}\" forward tcp:{AgentPort} tcp:{AgentPort}", ct);
        _client = new TcpClient();
        await _client.ConnectAsync("127.0.0.1", AgentPort, ct);
        var stream = _client.GetStream();
        _reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        _writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
        var response = await SendAsync("PING", ct);
        if (response != "OK") throw new InvalidOperationException("Android Input Agent did not respond.");
    }

    public Task<string> TapAsync(double x, double y, CancellationToken ct = default) => SendAsync($"TAP {x:0.####} {y:0.####}", ct);
    public Task<string> SwipeAsync(double x1, double y1, double x2, double y2, int durationMs = 120, CancellationToken ct = default)
        => SendAsync($"SWIPE {x1:0.####} {y1:0.####} {x2:0.####} {y2:0.####} {durationMs}", ct);

    private async Task<string> SendAsync(string command, CancellationToken ct)
    {
        if (_writer is null || _reader is null) throw new InvalidOperationException("Control bridge is not connected.");
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(command.AsMemory(), ct).ConfigureAwait(false);
            return (await _reader.ReadLineAsync(ct).ConfigureAwait(false))?.Trim() ?? "ERR";
        }
        finally { _sendLock.Release(); }
    }

    public void Dispose()
    {
        try { _client?.Dispose(); } catch { }
        _sendLock.Dispose();
    }
}
