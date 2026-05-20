// File: tests/PostgresCopy.Tests/SshConfigReaderTests.cs

using PostgresCopy.Config;

namespace PostgresCopy.Tests;

public sealed class SshConfigReaderTests
{
    [Fact]
    public void Parse_returns_empty_for_empty_input()
    {
        var result = SshConfigReader.Parse([]);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_returns_empty_for_comments_and_blank_lines_only()
    {
        var result = SshConfigReader.Parse(["# this is a comment", "", "  "]);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_reads_basic_host_entry()
    {
        string[] lines =
        [
            "Host myserver",
            "    HostName 192.168.1.10",
            "    User deploy",
            "    Port 2222",
        ];

        var result = SshConfigReader.Parse(lines);

        var entry = Assert.Single(result);
        Assert.Equal("myserver", entry.Alias);
        Assert.Equal("192.168.1.10", entry.HostName);
        Assert.Equal("deploy", entry.User);
        Assert.Equal(2222, entry.Port);
    }

    [Fact]
    public void Parse_uses_alias_as_hostname_when_hostname_not_set()
    {
        string[] lines = ["Host jumpbox", "    User admin"];

        var result = SshConfigReader.Parse(lines);

        var entry = Assert.Single(result);
        Assert.Equal("jumpbox", entry.HostName);
    }

    [Fact]
    public void Parse_defaults_port_to_22()
    {
        string[] lines = ["Host server", "    HostName example.com"];

        var result = SshConfigReader.Parse(lines);

        Assert.Equal(22, result[0].Port);
    }

    [Fact]
    public void Parse_reads_identity_file()
    {
        string[] lines =
        [
            "Host prod",
            "    HostName prod.example.com",
            "    IdentityFile /home/user/.ssh/id_rsa",
        ];

        var result = SshConfigReader.Parse(lines);

        Assert.Equal("/home/user/.ssh/id_rsa", result[0].IdentityFile);
    }

    [Fact]
    public void Parse_reads_multiple_host_entries()
    {
        string[] lines =
        [
            "Host alpha",
            "    HostName alpha.example.com",
            "",
            "Host beta",
            "    HostName beta.example.com",
            "    Port 2222",
        ];

        var result = SshConfigReader.Parse(lines);

        Assert.Equal(2, result.Count);
        Assert.Equal("alpha", result[0].Alias);
        Assert.Equal("beta", result[1].Alias);
        Assert.Equal(2222, result[1].Port);
    }

    [Fact]
    public void Parse_skips_wildcard_host_entries()
    {
        string[] lines =
        [
            "Host *",
            "    ServerAliveInterval 60",
            "",
            "Host *.internal",
            "    User ops",
            "",
            "Host real-server",
            "    HostName real.example.com",
        ];

        var result = SshConfigReader.Parse(lines);

        var entry = Assert.Single(result);
        Assert.Equal("real-server", entry.Alias);
    }

    [Fact]
    public void Parse_ignores_comment_lines_within_blocks()
    {
        string[] lines =
        [
            "# production jump host",
            "Host prod",
            "    # connect via bastion",
            "    HostName prod.example.com",
            "    User ubuntu",
        ];

        var result = SshConfigReader.Parse(lines);

        var entry = Assert.Single(result);
        Assert.Equal("ubuntu", entry.User);
    }

    [Fact]
    public void Parse_is_case_insensitive_for_keys()
    {
        string[] lines =
        [
            "HOST mybox",
            "    HOSTNAME mybox.internal",
            "    USER root",
            "    PORT 22",
            "    IDENTITYFILE ~/.ssh/id_ed25519",
        ];

        var result = SshConfigReader.Parse(lines);

        var entry = Assert.Single(result);
        Assert.Equal("mybox.internal", entry.HostName);
        Assert.Equal("root", entry.User);
    }

    [Fact]
    public void Parse_ignores_invalid_port_values()
    {
        string[] lines =
        [
            "Host server",
            "    Port notanumber",
        ];

        var result = SshConfigReader.Parse(lines);

        Assert.Equal(22, result[0].Port);
    }

    [Fact]
    public void ExpandPath_returns_null_for_null_input()
    {
        Assert.Null(SshConfigReader.ExpandPath(null));
    }

    [Fact]
    public void ExpandPath_expands_tilde_slash_prefix()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expanded = SshConfigReader.ExpandPath("~/.ssh/id_rsa");

        Assert.Equal(Path.Combine(home, ".ssh/id_rsa"), expanded);
    }

    [Fact]
    public void ExpandPath_expands_tilde_backslash_prefix()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expanded = SshConfigReader.ExpandPath(@"~\.ssh\id_rsa");

        Assert.Equal(Path.Combine(home, @".ssh\id_rsa"), expanded);
    }

    [Fact]
    public void ExpandPath_leaves_absolute_paths_unchanged()
    {
        var path = "/etc/ssh/id_rsa";
        Assert.Equal(path, SshConfigReader.ExpandPath(path));
    }

    [Fact]
    public void Parse_expands_tilde_in_identity_file()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] lines =
        [
            "Host bastion",
            "    HostName bastion.example.com",
            "    IdentityFile ~/.ssh/bastion_key",
        ];

        var result = SshConfigReader.Parse(lines);

        Assert.Equal(Path.Combine(home, ".ssh/bastion_key"), result[0].IdentityFile);
    }

    [Fact]
    public void Parse_uses_first_token_only_when_host_has_multiple_patterns()
    {
        string[] lines =
        [
            "Host server1 server2",
            "    HostName server1.example.com",
        ];

        var result = SshConfigReader.Parse(lines);

        var entry = Assert.Single(result);
        Assert.Equal("server1", entry.Alias);
    }
}
