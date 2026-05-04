// File: src/PostgresCopy.Desktop/SshTunnelConfig.cs

namespace PostgresCopy.Desktop;

public enum SshAuthType { Password, PrivateKey }

public sealed record SshTunnelConfig(
    bool EnableForOrigin,
    bool EnableForDestination,
    string Host,
    int Port,
    string Username,
    SshAuthType AuthType,
    string? Password,
    string? KeyFilePath,
    string? KeyPassphrase,
    string RemoteHost,
    int RemotePort);
