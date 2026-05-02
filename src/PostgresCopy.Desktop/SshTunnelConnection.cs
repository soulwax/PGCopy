using Npgsql;
using Renci.SshNet;

namespace PostgresCopy.Desktop;

public sealed class SshTunnelConnection : IDisposable
{
    private readonly SshClient _client;
    private readonly ForwardedPortLocal? _originPort;
    private readonly ForwardedPortLocal? _destPort;

    public string? PatchedOrigin { get; }
    public string? PatchedDest { get; }

    private SshTunnelConnection(
        SshClient client,
        ForwardedPortLocal? originPort,
        ForwardedPortLocal? destPort,
        string? patchedOrigin,
        string? patchedDest)
    {
        _client = client;
        _originPort = originPort;
        _destPort = destPort;
        PatchedOrigin = patchedOrigin;
        PatchedDest = patchedDest;
    }

    public static async Task<SshTunnelConnection> StartAsync(
        SshTunnelConfig config,
        string originConnectionString,
        string destConnectionString,
        CancellationToken ct)
    {
        var connectionInfo = BuildConnectionInfo(config);
        var client = new SshClient(connectionInfo);

        await Task.Run(() => client.Connect(), ct);

        ForwardedPortLocal? originPort = null;
        ForwardedPortLocal? destPort = null;
        string? patchedOrigin = null;
        string? patchedDest = null;

        if (config.EnableForOrigin)
        {
            originPort = new ForwardedPortLocal("127.0.0.1", 0u, config.RemoteHost, (uint)config.RemotePort);
            client.AddForwardedPort(originPort);
            originPort.Start();
            patchedOrigin = PatchConnectionString(originConnectionString, "127.0.0.1", (int)originPort.BoundPort);
        }

        if (config.EnableForDestination)
        {
            destPort = new ForwardedPortLocal("127.0.0.1", 0u, config.RemoteHost, (uint)config.RemotePort);
            client.AddForwardedPort(destPort);
            destPort.Start();
            patchedDest = PatchConnectionString(destConnectionString, "127.0.0.1", (int)destPort.BoundPort);
        }

        return new SshTunnelConnection(client, originPort, destPort, patchedOrigin, patchedDest);
    }

    private static ConnectionInfo BuildConnectionInfo(SshTunnelConfig config)
    {
        AuthenticationMethod auth = config.AuthType switch
        {
            SshAuthType.PrivateKey when !string.IsNullOrWhiteSpace(config.KeyFilePath) =>
                new PrivateKeyAuthenticationMethod(
                    config.Username,
                    string.IsNullOrEmpty(config.KeyPassphrase)
                        ? new PrivateKeyFile(config.KeyFilePath)
                        : new PrivateKeyFile(config.KeyFilePath, config.KeyPassphrase)),
            _ =>
                new PasswordAuthenticationMethod(config.Username, config.Password ?? string.Empty),
        };

        return new ConnectionInfo(config.Host, config.Port, config.Username, auth);
    }

    private static string PatchConnectionString(string connectionString, string host, int port)
    {
        var b = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Host = host,
            Port = port,
        };
        return b.ConnectionString;
    }

    public void Dispose()
    {
        _originPort?.Stop();
        _originPort?.Dispose();
        _destPort?.Stop();
        _destPort?.Dispose();
        _client.Disconnect();
        _client.Dispose();
    }
}
