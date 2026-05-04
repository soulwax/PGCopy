// File: src/PostgresCopy.Desktop/MainForm.cs

using Npgsql;
using PostgresCopy.Cli;
using PostgresCopy.Config;
using PostgresCopy.Migration;

namespace PostgresCopy.Desktop;

public sealed class MainForm : Form
{
    // Connection tab
    private readonly TextBox originTextBox = new();
    private readonly TextBox destinationTextBox = new();
    private readonly TextBox schemaTextBox = new();
    private readonly TextBox tablesTextBox = new();
    private readonly CheckBox dryRunCheckBox = new();
    private readonly CheckBox verifyCheckBox = new();
    private readonly CheckBox truncateCheckBox = new();
    private readonly CheckBox createSchemaCheckBox = new();
    private readonly TextBox truncateConfirmationTextBox = new();

    // SSH Tunnel tab
    private readonly ComboBox sshConfigHostCombo = new();
    private readonly CheckBox sshForOriginCheckBox = new();
    private readonly CheckBox sshForDestCheckBox = new();
    private readonly TextBox sshOriginTextBox = new();
    private readonly TextBox sshDestinationTextBox = new();
    private readonly TextBox sshHostTextBox = new();
    private readonly TextBox sshPortTextBox = new();
    private readonly TextBox sshUserTextBox = new();
    private readonly ComboBox sshAuthCombo = new();
    private readonly TextBox sshPasswordTextBox = new();
    private readonly Panel sshPasswordPanel = new();
    private readonly TextBox sshKeyPathTextBox = new();
    private readonly Button sshKeyBrowseButton = new();
    private readonly TextBox sshKeyPassphraseTextBox = new();
    private readonly Panel sshKeyPanel = new();
    private readonly TextBox sshRemoteHostTextBox = new();
    private readonly TextBox sshRemotePortTextBox = new();
    private readonly Button testSshButton = new();

    // Footer
    private readonly Button runButton = new();
    private readonly Button cancelButton = new();
    private readonly Button clearLogButton = new();
    private readonly TextBox logTextBox = new();
    private readonly Label statusLabel = new();

    private CancellationTokenSource? activeRun;
    private string? runningStatusOverride;
    private bool syncingConnectionText;

    public MainForm()
    {
        Text = "PostgresCopy";
        MinimumSize = new Size(960, 780);
        StartPosition = FormStartPosition.CenterScreen;

        BuildLayout();
        UpdateRunState();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleBar = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 12),
        };

        var logo = new LogoPanel
        {
            Size = new Size(128, 128),
            Margin = new Padding(0, 0, 18, 0),
        };

        var title = new Label
        {
            Text = "PostgresCopy",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 18, FontStyle.Bold),
            Margin = new Padding(0, 31, 0, 0),
        };

        titleBar.Controls.Add(logo);
        titleBar.Controls.Add(title);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(0, 330),
        };

        var connectionTab = new TabPage("Connection") { Padding = new Padding(8), AutoScroll = true };
        connectionTab.Controls.Add(BuildInputPanel());
        tabs.TabPages.Add(connectionTab);

        var sshTab = new TabPage("SSH Tunnel") { Padding = new Padding(8), AutoScroll = true };
        sshTab.Controls.Add(BuildSshPanel());
        tabs.TabPages.Add(sshTab);

        root.Controls.Add(titleBar, 0, 0);
        root.Controls.Add(tabs, 0, 1);
        root.Controls.Add(BuildLogBox(), 0, 2);
        root.Controls.Add(BuildFooter(), 0, 3);

        Controls.Add(root);
    }

    // ── Connection tab ─────────────────────────────────────────────────────────

    private Control BuildInputPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        originTextBox.Multiline = true;
        originTextBox.Height = 58;
        originTextBox.ScrollBars = ScrollBars.Vertical;
        originTextBox.PlaceholderText = "postgres://user:password@localhost:5432/source";

        destinationTextBox.Multiline = true;
        destinationTextBox.Height = 58;
        destinationTextBox.ScrollBars = ScrollBars.Vertical;
        destinationTextBox.PlaceholderText = "postgres://user:password@localhost:5433/target";
        originTextBox.TextChanged += (_, _) => SyncConnectionText(originTextBox, sshOriginTextBox);
        destinationTextBox.TextChanged += (_, _) => SyncConnectionText(destinationTextBox, sshDestinationTextBox);

        schemaTextBox.Text = "public";
        schemaTextBox.PlaceholderText = "public";

        tablesTextBox.PlaceholderText = "optional: users,orders,products";

        truncateConfirmationTextBox.PlaceholderText = "Type TRUNCATE when destination truncation is checked";
        truncateConfirmationTextBox.Enabled = false;
        truncateConfirmationTextBox.TextChanged += (_, _) => UpdateRunState();

        AddRow(panel, "Origin URL", originTextBox);
        AddRow(panel, "Destination URL", destinationTextBox);
        AddRow(panel, "Schema", schemaTextBox);
        AddRow(panel, "Tables", tablesTextBox);
        AddRow(panel, "Options", BuildOptionsPanel());
        AddRow(panel, "Confirm", truncateConfirmationTextBox);

        return panel;
    }

    private Control BuildOptionsPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
        };

        dryRunCheckBox.Text = "Dry run";
        dryRunCheckBox.Checked = true;
        dryRunCheckBox.AutoSize = true;
        dryRunCheckBox.CheckedChanged += (_, _) => UpdateRunState();

        verifyCheckBox.Text = "Verify counts";
        verifyCheckBox.Checked = true;
        verifyCheckBox.AutoSize = true;

        truncateCheckBox.Text = "Truncate destination";
        truncateCheckBox.AutoSize = true;
        truncateCheckBox.CheckedChanged += (_, _) =>
        {
            truncateConfirmationTextBox.Enabled = truncateCheckBox.Checked;
            if (!truncateCheckBox.Checked)
                truncateConfirmationTextBox.Clear();
            UpdateRunState();
        };

        createSchemaCheckBox.Text = "Create schema (requires pg_dump)";
        createSchemaCheckBox.AutoSize = true;

        panel.Controls.Add(dryRunCheckBox);
        panel.Controls.Add(verifyCheckBox);
        panel.Controls.Add(truncateCheckBox);
        panel.Controls.Add(createSchemaCheckBox);
        return panel;
    }

    // ── SSH Tunnel tab ──────────────────────────────────────────────────────────

    private Control BuildSshPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        sshOriginTextBox.Multiline = true;
        sshOriginTextBox.Height = 58;
        sshOriginTextBox.ScrollBars = ScrollBars.Vertical;
        sshOriginTextBox.PlaceholderText = "postgres://user:password@localhost:5432/source";
        sshOriginTextBox.TextChanged += (_, _) => SyncConnectionText(sshOriginTextBox, originTextBox);

        sshDestinationTextBox.Multiline = true;
        sshDestinationTextBox.Height = 58;
        sshDestinationTextBox.ScrollBars = ScrollBars.Vertical;
        sshDestinationTextBox.PlaceholderText = "postgres://user:password@localhost:5433/target";
        sshDestinationTextBox.TextChanged += (_, _) => SyncConnectionText(sshDestinationTextBox, destinationTextBox);

        // ~/.ssh/config host selector
        var sshConfigEntries = SshConfigReader.Read();
        sshConfigHostCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        sshConfigHostCombo.Width = 260;
        if (sshConfigEntries.Count > 0)
        {
            sshConfigHostCombo.Items.Add("(select to pre-fill)");
            foreach (var entry in sshConfigEntries)
                sshConfigHostCombo.Items.Add(entry);
            sshConfigHostCombo.DisplayMember = nameof(SshConfigEntry.Alias);
            sshConfigHostCombo.SelectedIndex = 0;
            sshConfigHostCombo.SelectedIndexChanged += (_, _) =>
            {
                if (sshConfigHostCombo.SelectedItem is not SshConfigEntry entry) return;
                sshHostTextBox.Text = entry.HostName;
                sshPortTextBox.Text = entry.Port.ToString();
                if (entry.User is not null) sshUserTextBox.Text = entry.User;
                if (entry.IdentityFile is not null)
                {
                    sshKeyPathTextBox.Text = entry.IdentityFile;
                    sshAuthCombo.SelectedIndex = 1;
                    UpdateSshAuthVisibility();
                }
            };
            AddRow(panel, "~/.ssh/config", sshConfigHostCombo);
        }

        // Apply to
        var applyPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        sshForOriginCheckBox.Text = "Origin";
        sshForOriginCheckBox.AutoSize = true;
        sshForDestCheckBox.Text = "Destination";
        sshForDestCheckBox.AutoSize = true;
        applyPanel.Controls.Add(sshForOriginCheckBox);
        applyPanel.Controls.Add(sshForDestCheckBox);
        AddRow(panel, "Tunnel for", applyPanel);
        AddRow(panel, "Origin URL", sshOriginTextBox);
        AddRow(panel, "Destination URL", sshDestinationTextBox);

        // SSH host / port
        sshPortTextBox.Text = "22";
        sshPortTextBox.Width = 60;
        var hostPortPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        sshHostTextBox.PlaceholderText = "ssh.example.com";
        sshHostTextBox.Width = 260;
        var portLabel = new Label { Text = "Port", AutoSize = true, Margin = new Padding(8, 4, 4, 0) };
        hostPortPanel.Controls.Add(sshHostTextBox);
        hostPortPanel.Controls.Add(portLabel);
        hostPortPanel.Controls.Add(sshPortTextBox);
        AddRow(panel, "SSH host", hostPortPanel);

        // Username
        sshUserTextBox.PlaceholderText = "username";
        AddRow(panel, "Username", sshUserTextBox);

        // Auth type
        sshAuthCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        sshAuthCombo.Items.AddRange(["Password", "Private key file"]);
        sshAuthCombo.SelectedIndex = 0;
        sshAuthCombo.Width = 160;
        sshAuthCombo.SelectedIndexChanged += (_, _) => UpdateSshAuthVisibility();
        AddRow(panel, "Auth", sshAuthCombo);

        // Password panel
        sshPasswordTextBox.PlaceholderText = "SSH password";
        sshPasswordTextBox.UseSystemPasswordChar = true;
        sshPasswordTextBox.Dock = DockStyle.Fill;
        sshPasswordPanel.Dock = DockStyle.Fill;
        sshPasswordPanel.AutoSize = true;
        sshPasswordPanel.Controls.Add(sshPasswordTextBox);
        AddRow(panel, "Password", sshPasswordPanel);

        // Key file panel
        sshKeyPathTextBox.PlaceholderText = "Path to private key file (.pem, .ppk, OpenSSH)";
        sshKeyBrowseButton.Text = "Browse…";
        sshKeyBrowseButton.AutoSize = true;
        sshKeyBrowseButton.Click += SshKeyBrowse_Click;
        sshKeyPassphraseTextBox.PlaceholderText = "Passphrase (optional)";
        sshKeyPassphraseTextBox.UseSystemPasswordChar = true;

        var keyLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        keyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        keyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sshKeyPathTextBox.Dock = DockStyle.Fill;
        sshKeyBrowseButton.Dock = DockStyle.Fill;
        keyLayout.Controls.Add(sshKeyPathTextBox, 0, 0);
        keyLayout.Controls.Add(sshKeyBrowseButton, 1, 0);
        keyLayout.Controls.Add(sshKeyPassphraseTextBox, 0, 1);
        keyLayout.SetColumnSpan(sshKeyPassphraseTextBox, 2);

        sshKeyPanel.Dock = DockStyle.Fill;
        sshKeyPanel.AutoSize = true;
        sshKeyPanel.Controls.Add(keyLayout);
        sshKeyPanel.Visible = false;
        AddRow(panel, "Key file", sshKeyPanel);

        // Remote postgres location
        sshRemoteHostTextBox.Text = "localhost";
        sshRemotePortTextBox.Text = "5432";
        sshRemotePortTextBox.Width = 60;
        var remotePanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        sshRemoteHostTextBox.Width = 200;
        var remotePortLabel = new Label { Text = "Port", AutoSize = true, Margin = new Padding(8, 4, 4, 0) };
        remotePanel.Controls.Add(sshRemoteHostTextBox);
        remotePanel.Controls.Add(remotePortLabel);
        remotePanel.Controls.Add(sshRemotePortTextBox);
        AddRow(panel, "Remote host", remotePanel);

        testSshButton.Text = "Test tunnel";
        testSshButton.AutoSize = true;
        testSshButton.Click += TestSshButton_Click;
        var testPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        testPanel.Controls.Add(testSshButton);
        AddRow(panel, "Connection", testPanel);

        var note = new Label
        {
            Text = "Remote host/port = where PostgreSQL is visible from the SSH server. Test tunnel checks the selected database URL(s) through SSH.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 4, 0, 0),
        };
        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(note, 1, row);

        return panel;
    }

    private async void TestSshButton_Click(object? sender, EventArgs eventArgs)
    {
        logTextBox.Clear();
        runningStatusOverride = "Testing SSH tunnel...";
        activeRun = new CancellationTokenSource();
        SetRunning(true);
        SshTunnelConnection? tunnel = null;
        string? finalStatus = null;

        try
        {
            if (!SshEnabled)
                throw new ValidationException("Select Origin, Destination, or both under Tunnel for before testing.");

            var originText = sshOriginTextBox.Text.Trim();
            var destText = sshDestinationTextBox.Text.Trim();

            if (sshForOriginCheckBox.Checked && string.IsNullOrWhiteSpace(originText))
                throw new ValidationException("Origin URL is required to test the origin tunnel.");
            if (sshForDestCheckBox.Checked && string.IsNullOrWhiteSpace(destText))
                throw new ValidationException("Destination URL is required to test the destination tunnel.");

            var sshConfig = BuildSshConfig();
            AppendLog($"Connecting SSH tunnel to {sshConfig.Host}:{sshConfig.Port}...");

            tunnel = await SshTunnelConnection.StartAsync(sshConfig, originText, destText, activeRun.Token);
            AppendLog("SSH tunnel established.");

            if (tunnel.PatchedOrigin != null)
            {
                AppendLog("Checking origin database through SSH tunnel...");
                await TestDatabaseConnectionAsync(tunnel.PatchedOrigin, activeRun.Token);
                AppendLog("Origin database connection passed.");
            }

            if (tunnel.PatchedDest != null)
            {
                AppendLog("Checking destination database through SSH tunnel...");
                await TestDatabaseConnectionAsync(tunnel.PatchedDest, activeRun.Token);
                AppendLog("Destination database connection passed.");
            }

            finalStatus = "SSH tunnel test passed.";
            statusLabel.Text = finalStatus;
            AppendLog("SSH tunnel test passed.");
        }
        catch (ValidationException ex)
        {
            AppendLog($"Error: {ex.Message}");
            finalStatus = "SSH tunnel test failed.";
            statusLabel.Text = finalStatus;
        }
        catch (PostgresException ex)
        {
            AppendLog($"PostgreSQL error: {ex.MessageText}");
            finalStatus = "SSH tunnel test failed.";
            statusLabel.Text = finalStatus;
        }
        catch (OperationCanceledException)
        {
            AppendLog("SSH tunnel test cancelled.");
            finalStatus = "Cancelled.";
            statusLabel.Text = finalStatus;
        }
        catch (Exception ex)
        {
            AppendLog($"Error: {ex.Message}");
            finalStatus = "SSH tunnel test failed.";
            statusLabel.Text = finalStatus;
        }
        finally
        {
            tunnel?.Dispose();
            activeRun?.Dispose();
            activeRun = null;
            runningStatusOverride = null;
            SetRunning(false);
            if (finalStatus is not null)
                statusLabel.Text = finalStatus;
        }
    }

    private static async Task TestDatabaseConnectionAsync(string connectionString, CancellationToken cancellationToken)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Timeout = 10,
            CommandTimeout = 10,
        };

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("select 1", connection);
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private void SyncConnectionText(TextBox source, TextBox target)
    {
        if (syncingConnectionText || target.Text == source.Text)
            return;

        syncingConnectionText = true;
        try
        {
            target.Text = source.Text;
        }
        finally
        {
            syncingConnectionText = false;
        }
    }

    private void UpdateSshAuthVisibility()
    {
        var useKey = sshAuthCombo.SelectedIndex == 1;
        sshPasswordPanel.Visible = !useKey;
        sshKeyPanel.Visible = useKey;
    }

    private void SshKeyBrowse_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Select SSH private key",
            Filter = "Key files (*.pem;*.ppk;*)|*.pem;*.ppk;*|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            sshKeyPathTextBox.Text = dlg.FileName;
    }

    private bool SshEnabled =>
        sshForOriginCheckBox.Checked || sshForDestCheckBox.Checked;

    private SshTunnelConfig BuildSshConfig()
    {
        if (!int.TryParse(sshPortTextBox.Text, out var sshPort) || sshPort <= 0)
            throw new ValidationException("SSH port must be a positive integer.");
        if (!int.TryParse(sshRemotePortTextBox.Text, out var remotePort) || remotePort <= 0)
            throw new ValidationException("Remote PostgreSQL port must be a positive integer.");
        if (string.IsNullOrWhiteSpace(sshHostTextBox.Text))
            throw new ValidationException("SSH host is required when SSH tunnel is enabled.");
        if (string.IsNullOrWhiteSpace(sshUserTextBox.Text))
            throw new ValidationException("SSH username is required.");

        var authType = sshAuthCombo.SelectedIndex == 1 ? SshAuthType.PrivateKey : SshAuthType.Password;

        return new SshTunnelConfig(
            sshForOriginCheckBox.Checked,
            sshForDestCheckBox.Checked,
            sshHostTextBox.Text.Trim(),
            sshPort,
            sshUserTextBox.Text.Trim(),
            authType,
            authType == SshAuthType.Password ? sshPasswordTextBox.Text : null,
            authType == SshAuthType.PrivateKey ? sshKeyPathTextBox.Text.Trim() : null,
            authType == SshAuthType.PrivateKey ? sshKeyPassphraseTextBox.Text : null,
            string.IsNullOrWhiteSpace(sshRemoteHostTextBox.Text) ? "localhost" : sshRemoteHostTextBox.Text.Trim(),
            remotePort);
    }

    // ── Log + footer ────────────────────────────────────────────────────────────

    private Control BuildLogBox()
    {
        logTextBox.Dock = DockStyle.Fill;
        logTextBox.Multiline = true;
        logTextBox.ReadOnly = true;
        logTextBox.ScrollBars = ScrollBars.Vertical;
        logTextBox.Font = new Font(FontFamily.GenericMonospace, 10);
        logTextBox.BackColor = Color.White;
        return logTextBox;
    }

    private Control BuildFooter()
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            ColumnCount = 2,
            AutoSize = true,
            Padding = new Padding(0, 12, 0, 0),
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        statusLabel.Text = "Ready. Start with a dry run.";
        statusLabel.AutoSize = true;
        statusLabel.Anchor = AnchorStyles.Left;

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };

        clearLogButton.Text = "Clear log";
        clearLogButton.AutoSize = true;
        clearLogButton.Click += (_, _) => logTextBox.Clear();

        cancelButton.Text = "Cancel";
        cancelButton.AutoSize = true;
        cancelButton.Enabled = false;
        cancelButton.Click += (_, _) => activeRun?.Cancel();

        runButton.AutoSize = true;
        runButton.Click += RunButton_Click;

        buttons.Controls.Add(clearLogButton);
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(runButton);

        footer.Controls.Add(statusLabel, 0, 0);
        footer.Controls.Add(buttons, 1, 0);
        return footer;
    }

    // ── Run logic ───────────────────────────────────────────────────────────────

    private async void RunButton_Click(object? sender, EventArgs eventArgs)
    {
        logTextBox.Clear();
        SetRunning(true);

        activeRun = new CancellationTokenSource();
        var logger = new UiMigrationLogger(AppendLog);
        SshTunnelConnection? tunnel = null;

        try
        {
            var originText = originTextBox.Text.Trim();
            var destText = destinationTextBox.Text.Trim();

            if (SshEnabled)
            {
                var sshConfig = BuildSshConfig();
                AppendLog($"Connecting SSH tunnel to {sshConfig.Host}:{sshConfig.Port}…");
                tunnel = await SshTunnelConnection.StartAsync(sshConfig, originText, destText, activeRun.Token);

                if (tunnel.PatchedOrigin != null)
                {
                    originText = tunnel.PatchedOrigin;
                    AppendLog("SSH tunnel active for origin.");
                }

                if (tunnel.PatchedDest != null)
                {
                    destText = tunnel.PatchedDest;
                    AppendLog("SSH tunnel active for destination.");
                }
            }

            var settings = BuildSettings(originText, destText);
            await new MigrationRunner(logger).RunAsync(
                settings,
                destructiveActionsConfirmed: true,
                activeRun.Token);

            statusLabel.Text = settings.DryRun ? "Dry run complete." : "Copy complete.";
        }
        catch (ValidationException ex)
        {
            logger.Error(ex.Message);
            statusLabel.Text = "Validation failed.";
        }
        catch (MigrationTableException ex)
        {
            logger.Error(ex.Message);
            logger.Error($"Copied before failure: {ex.TablesCopiedBeforeFailure} table(s), {ex.RowsCopiedBeforeFailure} row(s).");
            statusLabel.Text = "Migration failed.";
        }
        catch (VerificationException ex)
        {
            logger.Error(ex.Message);
            statusLabel.Text = "Verification failed.";
        }
        catch (PostgresException ex)
        {
            logger.Error($"PostgreSQL error: {ex.MessageText}");
            statusLabel.Text = "PostgreSQL error.";
        }
        catch (OperationCanceledException)
        {
            logger.Error("Migration cancelled.");
            statusLabel.Text = "Cancelled.";
        }
        catch (Exception ex)
        {
            logger.Error(ex.Message);
            statusLabel.Text = "Failed.";
        }
        finally
        {
            tunnel?.Dispose();
            activeRun.Dispose();
            activeRun = null;
            SetRunning(false);
        }
    }

    private MigrationSettings BuildSettings(string? originOverride = null, string? destOverride = null)
    {
        if (truncateCheckBox.Checked
            && !string.Equals(truncateConfirmationTextBox.Text, "TRUNCATE", StringComparison.Ordinal))
        {
            throw new ValidationException("Type TRUNCATE to confirm destination truncation.");
        }

        var options = new CliOptions(
            originOverride ?? originTextBox.Text.Trim(),
            destOverride ?? destinationTextBox.Text.Trim(),
            string.IsNullOrWhiteSpace(schemaTextBox.Text) ? "public" : schemaTextBox.Text.Trim(),
            ParseTables(tablesTextBox.Text),
            dryRunCheckBox.Checked,
            truncateCheckBox.Checked,
            verifyCheckBox.Checked,
            false,
            true,
            CliOptionsParser.DefaultBatchSize,
            createSchemaCheckBox.Checked);

        return MigrationSettingsValidator.Validate(options);
    }

    private static IReadOnlyList<string> ParseTables(string tables)
    {
        if (string.IsNullOrWhiteSpace(tables))
            return [];

        return tables
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private void AppendLog(string message)
    {
        if (IsDisposed) return;

        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(message));
            return;
        }

        logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void SetRunning(bool running)
    {
        originTextBox.Enabled = !running;
        destinationTextBox.Enabled = !running;
        schemaTextBox.Enabled = !running;
        tablesTextBox.Enabled = !running;
        dryRunCheckBox.Enabled = !running;
        verifyCheckBox.Enabled = !running;
        truncateCheckBox.Enabled = !running;
        truncateConfirmationTextBox.Enabled = !running && truncateCheckBox.Checked;
        createSchemaCheckBox.Enabled = !running;

        sshConfigHostCombo.Enabled = !running;
        sshForOriginCheckBox.Enabled = !running;
        sshForDestCheckBox.Enabled = !running;
        sshOriginTextBox.Enabled = !running;
        sshDestinationTextBox.Enabled = !running;
        sshHostTextBox.Enabled = !running;
        sshPortTextBox.Enabled = !running;
        sshUserTextBox.Enabled = !running;
        sshAuthCombo.Enabled = !running;
        sshPasswordTextBox.Enabled = !running;
        sshKeyPathTextBox.Enabled = !running;
        sshKeyBrowseButton.Enabled = !running;
        sshKeyPassphraseTextBox.Enabled = !running;
        sshRemoteHostTextBox.Enabled = !running;
        sshRemotePortTextBox.Enabled = !running;
        testSshButton.Enabled = !running;

        clearLogButton.Enabled = !running;
        runButton.Enabled = !running;
        cancelButton.Enabled = running;

        if (running)
        {
            statusLabel.Text = runningStatusOverride
                ?? (dryRunCheckBox.Checked ? "Running dry run..." : "Running copy...");
        }
        else
        {
            UpdateRunState();
        }
    }

    private void UpdateRunState()
    {
        var destructiveReady = !truncateCheckBox.Checked
            || string.Equals(truncateConfirmationTextBox.Text, "TRUNCATE", StringComparison.Ordinal);

        runButton.Text = dryRunCheckBox.Checked ? "Run dry run" : "Run copy";
        runButton.Enabled = activeRun is null && destructiveReady;

        if (!destructiveReady)
            statusLabel.Text = "Type TRUNCATE to enable destination truncation.";
        else if (activeRun is null)
            statusLabel.Text = dryRunCheckBox.Checked ? "Ready. Start with a dry run." : "Ready to copy.";
    }

    private static void AddRow(TableLayoutPanel panel, string labelText, Control control)
    {
        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 12, 8),
        };

        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 4, 0, 4);

        panel.Controls.Add(label, 0, row);
        panel.Controls.Add(control, 1, row);
    }
}
