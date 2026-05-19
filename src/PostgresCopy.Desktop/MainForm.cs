// File: src/PostgresCopy.Desktop/MainForm.cs

using Npgsql;
using PostgresCopy.Cli;
using PostgresCopy.Config;
using PostgresCopy.Database;
using PostgresCopy.Migration;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PostgresCopy.Desktop;

public sealed class MainForm : Form
{
    private static readonly Color WindowBackColor = SystemColors.Control;
    private static readonly Color SurfaceBackColor = SystemColors.Window;
    private static readonly Color BorderColor = Color.FromArgb(213, 221, 232);
    private static readonly Color MutedTextColor = Color.FromArgb(90, 103, 121);
    private static readonly Color PrimaryTextColor = Color.FromArgb(24, 34, 48);
    private static readonly Color AccentColor = Color.FromArgb(32, 125, 92);
    private static readonly Color LogBackColor = Color.FromArgb(12, 18, 30);
    private static readonly Color LogForeColor = Color.FromArgb(221, 231, 242);
    private static readonly Color LogMutedColor = Color.FromArgb(143, 158, 181);
    private static readonly Color LogStepColor = Color.FromArgb(125, 211, 252);
    private static readonly Color LogSuccessColor = Color.FromArgb(134, 239, 172);
    private static readonly Color LogWarningColor = Color.FromArgb(251, 191, 36);
    private static readonly Color LogErrorColor = Color.FromArgb(252, 129, 129);
    private static readonly Color LogWorkColor = Color.FromArgb(147, 197, 253);
    private static readonly Color LogDataColor = Color.FromArgb(216, 180, 254);
    private static readonly Color LogHelpColor = Color.FromArgb(203, 213, 225);
    private const string RepositoryUrl = "https://github.com/soulwax/PGCopy";

    // Connection tab
    private readonly TextBox originTextBox = new();
    private readonly TextBox destinationTextBox = new();
    private readonly TextBox schemaTextBox = new();
    private readonly TextBox tablesTextBox = new();
    private readonly CheckBox dryRunCheckBox = new();
    private readonly CheckBox verifyCheckBox = new();
    private readonly CheckBox truncateCheckBox = new();
    private readonly CheckBox createSchemaCheckBox = new();

    // Database Peek tab
    private readonly TextBox peekDatabaseTextBox = new();
    private readonly Button peekButton = new();

    // Preflight tab
    private readonly Button preflightButton = new();

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
    private readonly Button saveLogButton = new();
    private readonly RichTextBox logTextBox = new();
    private readonly Panel logScrollTrack = new();
    private readonly Panel logScrollThumb = new();
    private readonly Button logFontDecreaseButton = new();
    private readonly Button logFontIncreaseButton = new();
    private readonly Label statusLabel = new();
    private readonly ToolTip helpToolTip = new();

    private const int MaxLogOperations = 6;
    private const float MinLogFontSize = 8f;
    private const float MaxLogFontSize = 16f;
    private readonly List<LogOperation> logOperations = [];
    private LogOperation? activeLogOperation;
    private CancellationTokenSource? activeRun;
    private string? runningStatusOverride;
    private bool syncingConnectionText;
    private bool draggingLogScrollThumb;
    private int logScrollDragStartY;
    private int logScrollDragStartTop;
    private float logFontSize = 10f;

    private const int EmLineScroll = 0x00B6;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    public MainForm()
    {
        Text = "PostgresCopy";
        UseExecutableIcon();
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        BackColor = WindowBackColor;
        ForeColor = PrimaryTextColor;
        MinimumSize = new Size(860, 620);
        Size = new Size(1120, 860);
        StartPosition = FormStartPosition.CenterScreen;

        helpToolTip.AutoPopDelay = 12000;
        helpToolTip.InitialDelay = 550;
        helpToolTip.ReshowDelay = 150;
        helpToolTip.ShowAlways = true;

        BuildLayout();
        UpdateRunState();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(24, 20, 24, 18),
            BackColor = WindowBackColor,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleBar = BuildHeader();

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(0, 160),
            Margin = Padding.Empty,
        };
        StyleTabs(tabs);

        var connectionTab = CreateTabPage("Connection");
        connectionTab.Controls.Add(BuildInputPanel());
        tabs.TabPages.Add(connectionTab);

        var preflightTab = CreateTabPage("Preflight");
        preflightTab.Controls.Add(BuildPreflightPanel());
        tabs.TabPages.Add(preflightTab);

        var peekTab = CreateTabPage("Peek into Database");
        peekTab.Controls.Add(BuildPeekPanel());
        tabs.TabPages.Add(peekTab);

        var sshTab = CreateTabPage("SSH Tunnel");
        sshTab.Controls.Add(BuildSshPanel());
        tabs.TabPages.Add(sshTab);

        var workspace = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            FixedPanel = FixedPanel.None,
            SplitterWidth = 7,
            Panel1MinSize = 160,
            Panel2MinSize = 120,
            Margin = Padding.Empty,
            BackColor = WindowBackColor,
        };
        workspace.Panel1.Controls.Add(tabs);
        workspace.Panel2.Controls.Add(BuildLogBox());
        workspace.Resize += (_, _) => SetWorkspaceSplit(workspace);

        root.Controls.Add(titleBar, 0, 0);
        root.Controls.Add(workspace, 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);

        Controls.Add(root);
        Shown += (_, _) => SetWorkspaceSplit(workspace);
    }

    private static void SetWorkspaceSplit(SplitContainer workspace)
    {
        var available = workspace.ClientSize.Height;
        if (available <= 0)
            return;

        if (available <= workspace.Panel1MinSize + workspace.Panel2MinSize)
        {
            var compactDistance = Math.Max(
                1,
                Math.Min(workspace.Panel1MinSize, available - workspace.Panel2MinSize));

            if (compactDistance > 0 && compactDistance < available)
                workspace.SplitterDistance = compactDistance;

            return;
        }

        var desiredUpper = Math.Clamp(
            (int)Math.Round(available * 0.54),
            workspace.Panel1MinSize,
            available - workspace.Panel2MinSize);

        if (Math.Abs(workspace.SplitterDistance - desiredUpper) > 24)
            workspace.SplitterDistance = desiredUpper;
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 16),
            BackColor = WindowBackColor,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var logo = new LogoPanel
        {
            Size = new Size(156, 156),
            Margin = new Padding(0, 0, 22, 0),
        };

        var titleStack = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0, 34, 0, 0),
            BackColor = WindowBackColor,
        };

        var title = new Label
        {
            Text = "PostgresCopy",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 22, FontStyle.Bold),
            ForeColor = PrimaryTextColor,
            Margin = new Padding(0, 0, 0, 2),
        };

        var subtitle = new Label
        {
            Text = "Copy, inspect, and tunnel PostgreSQL databases.",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 10.5f, FontStyle.Regular),
            ForeColor = MutedTextColor,
            Margin = new Padding(1, 0, 0, 0),
        };

        var privacyNote = BuildPrivacyNote();

        titleStack.Controls.Add(title, 0, 0);
        titleStack.Controls.Add(subtitle, 0, 1);
        titleStack.Controls.Add(privacyNote, 0, 2);

        header.Controls.Add(logo, 0, 0);
        header.Controls.Add(titleStack, 1, 0);
        return header;
    }

    private Control BuildPrivacyNote()
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = WindowBackColor,
            Margin = new Padding(1, 12, 0, 0),
            Padding = Padding.Empty,
        };

        var note = new Label
        {
            Text = "This software is proudly free and open source. See: ",
            AutoSize = true,
            ForeColor = MutedTextColor,
            Font = new Font(Font.FontFamily, 9.5f, FontStyle.Regular),
            Margin = new Padding(0, 2, 8, 0),
        };

        var link = new LinkLabel
        {
            Text = "GitHub",
            AutoSize = true,
            LinkColor = AccentColor,
            ActiveLinkColor = Color.FromArgb(23, 90, 67),
            VisitedLinkColor = AccentColor,
            Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold),
            Margin = new Padding(0, 2, 0, 0),
        };
        link.Links.Add(0, link.Text.Length, RepositoryUrl);
        link.LinkClicked += (_, eventArgs) =>
        {
            if (eventArgs.Link?.LinkData is string url)
                OpenUrl(url);
        };

        panel.Controls.Add(note);
        panel.Controls.Add(link);
        return panel;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Opening the repository is a convenience; the app should keep running if Windows blocks it.
        }
    }

    private void UseExecutableIcon()
    {
        try
        {
            var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (icon is not null)
                Icon = icon;
        }
        catch
        {
            // The app icon is polish only; keep the form usable if Windows cannot extract it.
        }
    }

    private static TabPage CreateTabPage(string text)
    {
        return new TabPage(text)
        {
            Padding = new Padding(14),
            AutoScroll = true,
            BackColor = SurfaceBackColor,
            ForeColor = PrimaryTextColor,
        };
    }

    private void StyleTabs(TabControl tabs)
    {
        tabs.Appearance = TabAppearance.Normal;
        tabs.DrawMode = TabDrawMode.Normal;
        tabs.SizeMode = TabSizeMode.Normal;
        tabs.Padding = new Point(12, 4);
        tabs.Font = new Font(Font.FontFamily, 9.5f, FontStyle.Regular);
    }

    // ── Connection tab ─────────────────────────────────────────────────────────

    private Control BuildInputPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            BackColor = SurfaceBackColor,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        originTextBox.AutoSize = false;
        originTextBox.Height = 34;
        originTextBox.ScrollBars = ScrollBars.None;
        originTextBox.PlaceholderText = "postgres://user:password@localhost:5432/source";
        StyleTextBox(originTextBox);

        destinationTextBox.AutoSize = false;
        destinationTextBox.Height = 34;
        destinationTextBox.ScrollBars = ScrollBars.None;
        destinationTextBox.PlaceholderText = "postgres://user:password@localhost:5433/target";
        StyleTextBox(destinationTextBox);
        originTextBox.TextChanged += (_, _) => SyncConnectionText(originTextBox, sshOriginTextBox);
        destinationTextBox.TextChanged += (_, _) => SyncConnectionText(destinationTextBox, sshDestinationTextBox);

        schemaTextBox.Text = "public";
        schemaTextBox.PlaceholderText = "public";
        StyleTextBox(schemaTextBox);

        tablesTextBox.PlaceholderText = "optional: users,orders,products";
        StyleTextBox(tablesTextBox);

        AddRow(panel, "Origin URL", originTextBox,
            "The source PostgreSQL database. PostgresCopy reads schema and rows from here and never modifies it.");
        AddRow(panel, "Destination URL", destinationTextBox,
            "The target PostgreSQL database. Copy operations write here, so double-check this is not the same database as origin.");
        AddRow(panel, "Schema", schemaTextBox,
            "The PostgreSQL schema to copy. Most databases use public unless your tables live in a named schema.");
        AddRow(panel, "Tables", tablesTextBox,
            "Optional comma-separated table list. Leave empty to copy every base table in the selected schema.");
        AddRow(panel, "Options", BuildOptionsPanel());

        return panel;
    }

    private Control BuildOptionsPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            BackColor = SurfaceBackColor,
            Padding = new Padding(0, 2, 0, 0),
        };

        dryRunCheckBox.Text = "Dry run";
        dryRunCheckBox.Checked = true;
        dryRunCheckBox.AutoSize = true;
        StyleCheckBox(dryRunCheckBox);
        dryRunCheckBox.CheckedChanged += (_, _) => UpdateRunState();
        SetHelp(dryRunCheckBox,
            "Runs every validation and shows counts without copying or deleting rows. Start here when using a new database pair.");

        verifyCheckBox.Text = "Verify counts";
        verifyCheckBox.Checked = true;
        verifyCheckBox.AutoSize = true;
        StyleCheckBox(verifyCheckBox);
        SetHelp(verifyCheckBox,
            "After a real copy, compare row counts between origin and destination. This is a quick sanity check, not a full checksum.");

        truncateCheckBox.Text = "Truncate destination";
        truncateCheckBox.AutoSize = true;
        StyleCheckBox(truncateCheckBox);
        truncateCheckBox.CheckedChanged += (_, _) => UpdateRunState();
        SetHelp(truncateCheckBox,
            "Before a real copy, delete all rows from the planned destination tables so the destination becomes a fresh copy. Origin is not changed. You will still get a warning before anything is deleted.");

        createSchemaCheckBox.Text = "Create schema (requires pg_dump)";
        createSchemaCheckBox.AutoSize = true;
        StyleCheckBox(createSchemaCheckBox);
        SetHelp(createSchemaCheckBox,
            "Copies table definitions, indexes, sequences, and constraints from origin to destination before data checks. Requires pg_dump and psql on PATH.");

        panel.Controls.Add(dryRunCheckBox);
        panel.Controls.Add(verifyCheckBox);
        panel.Controls.Add(truncateCheckBox);
        panel.Controls.Add(createSchemaCheckBox);
        return panel;
    }

    // ── Peek tab ───────────────────────────────────────────────────────────────

    private Control BuildPeekPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            BackColor = SurfaceBackColor,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        peekDatabaseTextBox.AutoSize = false;
        peekDatabaseTextBox.Height = 34;
        peekDatabaseTextBox.ScrollBars = ScrollBars.None;
        peekDatabaseTextBox.PlaceholderText = "postgres://user:password@localhost:5432[/database]";
        StyleTextBox(peekDatabaseTextBox);

        peekButton.Text = "Peek database";
        peekButton.AutoSize = true;
        StyleButton(peekButton, ButtonTone.Primary);
        peekButton.Click += PeekButton_Click;
        SetHelp(peekButton,
            "Connect to the database URL and write a database or table summary into the operations log.");

        var actionPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = SurfaceBackColor,
        };
        actionPanel.Controls.Add(peekButton);

        var note = new Label
        {
            Text = "Omit the database name to list databases on the server. Include a database name to list user tables and row counts.",
            AutoSize = false,
            Dock = DockStyle.Fill,
            Height = 28,
            ForeColor = MutedTextColor,
            Margin = new Padding(0, 4, 0, 0),
        };

        AddRow(panel, "Database URL", peekDatabaseTextBox,
            "A PostgreSQL URL or Npgsql connection string. Without a database name, PostgresCopy lists visible databases. With a database name, it lists tables and row counts.");
        AddRow(panel, "Action", actionPanel);

        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(note, 1, row);

        return panel;
    }

    private async void PeekButton_Click(object? sender, EventArgs eventArgs)
    {
        BeginLogOperation("Database peek");
        runningStatusOverride = "Peeking into database...";
        activeRun = new CancellationTokenSource();
        SetRunning(true);
        string? finalStatus = null;

        try
        {
            AppendLog("Connecting to PostgreSQL...");

            var result = await new DatabasePeekInspector().PeekAsync(
                peekDatabaseTextBox.Text.Trim(),
                activeRun.Token);

            AppendLog($"Connected: {result.RedactedConnectionString}");
            AppendDatabasePeekResult(result);

            finalStatus = "Database peek complete.";
            statusLabel.Text = finalStatus;
        }
        catch (ValidationException ex)
        {
            AppendLog(FormatValidationError(ex.Message));
            finalStatus = "Validation failed.";
            statusLabel.Text = finalStatus;
        }
        catch (PostgresException ex)
        {
            AppendLog(FormatPostgresError(ex));
            finalStatus = "PostgreSQL error.";
            statusLabel.Text = finalStatus;
        }
        catch (NpgsqlException ex)
        {
            AppendLog(FormatNpgsqlError(ex.Message));
            finalStatus = "Connection failed.";
            statusLabel.Text = finalStatus;
        }
        catch (OperationCanceledException)
        {
            AppendLog("Database peek cancelled.");
            finalStatus = "Cancelled.";
            statusLabel.Text = finalStatus;
        }
        catch (Exception ex)
        {
            AppendLog(FormatUnexpectedError(ex.Message));
            finalStatus = "Database peek failed.";
            statusLabel.Text = finalStatus;
        }
        finally
        {
            activeRun?.Dispose();
            activeRun = null;
            runningStatusOverride = null;
            SetRunning(false);
            if (finalStatus is not null)
                statusLabel.Text = finalStatus;
        }
    }

    private void AppendDatabasePeekResult(DatabasePeekResult result)
    {
        if (!result.HasDatabase)
        {
            AppendLog("No database specified. Listing visible databases.");
            AppendLog($"Databases: {result.Databases.Count}");

            if (result.Databases.Count == 0)
            {
                AppendLog("  (none visible to this user)");
                return;
            }

            foreach (var database in result.Databases)
                AppendLog($"  {database}");

            return;
        }

        AppendLog($"Database: {result.DatabaseName}");
        AppendLog($"Tables: {result.Tables.Count}");

        if (result.Tables.Count == 0)
        {
            AppendLog("  (no user tables found)");
            return;
        }

        var tableWidth = Math.Max(
            "Table".Length,
            result.Tables.Max(table => $"{table.Schema}.{table.Name}".Length));
        var countWidth = Math.Max(
            "Rows".Length,
            result.Tables.Max(table => table.RowCount.ToString("N0").Length));

        AppendLog($"  {"Table".PadRight(tableWidth)}  {"Rows".PadLeft(countWidth)}");
        AppendLog($"  {new string('-', tableWidth)}  {new string('-', countWidth)}");

        foreach (var table in result.Tables)
        {
            var tableName = $"{table.Schema}.{table.Name}";
            AppendLog($"  {tableName.PadRight(tableWidth)}  {table.RowCount.ToString("N0").PadLeft(countWidth)}");
        }
    }

    // ── Preflight tab ──────────────────────────────────────────────────────────

    private Control BuildPreflightPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            BackColor = SurfaceBackColor,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        preflightButton.Text = "Check environment";
        preflightButton.AutoSize = true;
        StyleButton(preflightButton, ButtonTone.Primary);
        preflightButton.Click += PreflightButton_Click;
        SetHelp(preflightButton,
            "Check local tools used by optional workflows: pg_dump, psql, Docker, and SSH config auto-population.");

        var actionPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = SurfaceBackColor,
        };
        actionPanel.Controls.Add(preflightButton);

        var note = new Label
        {
            Text = "Preflight does not connect to your databases. It only checks local tools and configuration that can affect schema copy, integration tests, or SSH convenience.",
            AutoSize = false,
            Dock = DockStyle.Fill,
            Height = 42,
            ForeColor = MutedTextColor,
            Margin = new Padding(0, 4, 0, 0),
        };

        AddRow(panel, "Environment", actionPanel);
        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(note, 1, row);

        return panel;
    }

    private async void PreflightButton_Click(object? sender, EventArgs eventArgs)
    {
        BeginLogOperation("Environment preflight");
        runningStatusOverride = "Checking local environment...";
        activeRun = new CancellationTokenSource();
        SetRunning(true);
        string? finalStatus = null;

        try
        {
            AppendLog("Checking local tools and optional configuration...");
            var results = await EnvironmentPreflightChecker.CheckAsync(activeRun.Token);
            foreach (var result in results)
            {
                AppendLog($"{(result.Passed ? "[ok]" : "[warn]")} {result.Name}: {result.Detail}");
            }

            finalStatus = results.All(result => result.Passed)
                ? "Preflight passed."
                : "Preflight complete with warnings.";
            statusLabel.Text = finalStatus;
        }
        catch (OperationCanceledException)
        {
            AppendLog("Environment preflight cancelled.");
            finalStatus = "Cancelled.";
            statusLabel.Text = finalStatus;
        }
        catch (Exception ex)
        {
            AppendLog(FormatUnexpectedError(ex.Message));
            finalStatus = "Preflight failed.";
            statusLabel.Text = finalStatus;
        }
        finally
        {
            activeRun?.Dispose();
            activeRun = null;
            runningStatusOverride = null;
            SetRunning(false);
            if (finalStatus is not null)
                statusLabel.Text = finalStatus;
        }
    }

    // ── SSH Tunnel tab ──────────────────────────────────────────────────────────

    private Control BuildSshPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            BackColor = SurfaceBackColor,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        sshOriginTextBox.AutoSize = false;
        sshOriginTextBox.Height = 34;
        sshOriginTextBox.ScrollBars = ScrollBars.None;
        sshOriginTextBox.PlaceholderText = "postgres://user:password@localhost:5432/source";
        StyleTextBox(sshOriginTextBox);
        sshOriginTextBox.TextChanged += (_, _) => SyncConnectionText(sshOriginTextBox, originTextBox);

        sshDestinationTextBox.AutoSize = false;
        sshDestinationTextBox.Height = 34;
        sshDestinationTextBox.ScrollBars = ScrollBars.None;
        sshDestinationTextBox.PlaceholderText = "postgres://user:password@localhost:5433/target";
        StyleTextBox(sshDestinationTextBox);
        sshDestinationTextBox.TextChanged += (_, _) => SyncConnectionText(sshDestinationTextBox, destinationTextBox);

        // ~/.ssh/config host selector
        var sshConfigEntries = SshConfigReader.Read();
        sshConfigHostCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        sshConfigHostCombo.Width = 260;
        StyleComboBox(sshConfigHostCombo);
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
        StyleCheckBox(sshForOriginCheckBox);
        sshForDestCheckBox.Text = "Destination";
        sshForDestCheckBox.AutoSize = true;
        StyleCheckBox(sshForDestCheckBox);
        applyPanel.Controls.Add(sshForOriginCheckBox);
        applyPanel.Controls.Add(sshForDestCheckBox);
        AddRow(panel, "Tunnel for", applyPanel);
        AddRow(panel, "Origin URL", sshOriginTextBox);
        AddRow(panel, "Destination URL", sshDestinationTextBox);

        // SSH host / port
        sshPortTextBox.Text = "22";
        sshPortTextBox.Width = 60;
        StyleTextBox(sshPortTextBox);
        var hostPortPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = SurfaceBackColor };
        sshHostTextBox.PlaceholderText = "ssh.example.com";
        sshHostTextBox.Width = 260;
        StyleTextBox(sshHostTextBox);
        var portLabel = new Label { Text = "Port", AutoSize = true, Margin = new Padding(8, 4, 4, 0) };
        hostPortPanel.Controls.Add(sshHostTextBox);
        hostPortPanel.Controls.Add(portLabel);
        hostPortPanel.Controls.Add(sshPortTextBox);
        AddRow(panel, "SSH host", hostPortPanel);

        // Username
        sshUserTextBox.PlaceholderText = "username";
        StyleTextBox(sshUserTextBox);
        AddRow(panel, "Username", sshUserTextBox);

        // Auth type
        sshAuthCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        sshAuthCombo.Items.AddRange(["Password", "Private key file"]);
        sshAuthCombo.SelectedIndex = 0;
        sshAuthCombo.Width = 160;
        StyleComboBox(sshAuthCombo);
        sshAuthCombo.SelectedIndexChanged += (_, _) => UpdateSshAuthVisibility();
        AddRow(panel, "Auth", sshAuthCombo);

        // Password panel
        sshPasswordTextBox.PlaceholderText = "SSH password";
        sshPasswordTextBox.UseSystemPasswordChar = true;
        sshPasswordTextBox.Dock = DockStyle.Fill;
        StyleTextBox(sshPasswordTextBox);
        sshPasswordPanel.Dock = DockStyle.Fill;
        sshPasswordPanel.AutoSize = true;
        sshPasswordPanel.BackColor = SurfaceBackColor;
        sshPasswordPanel.Controls.Add(sshPasswordTextBox);
        AddRow(panel, "Password", sshPasswordPanel);

        // Key file panel
        sshKeyPathTextBox.PlaceholderText = "Path to private key file (.pem, .ppk, OpenSSH)";
        StyleTextBox(sshKeyPathTextBox);
        sshKeyBrowseButton.Text = "Browse…";
        sshKeyBrowseButton.AutoSize = true;
        StyleButton(sshKeyBrowseButton, ButtonTone.Secondary);
        sshKeyBrowseButton.Click += SshKeyBrowse_Click;
        sshKeyPassphraseTextBox.PlaceholderText = "Passphrase (optional)";
        sshKeyPassphraseTextBox.UseSystemPasswordChar = true;
        StyleTextBox(sshKeyPassphraseTextBox);

        var keyLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, BackColor = SurfaceBackColor };
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
        sshKeyPanel.BackColor = SurfaceBackColor;
        sshKeyPanel.Controls.Add(keyLayout);
        sshKeyPanel.Visible = false;
        AddRow(panel, "Key file", sshKeyPanel);

        // Remote postgres location
        sshRemoteHostTextBox.Text = "localhost";
        sshRemotePortTextBox.Text = "5432";
        sshRemotePortTextBox.Width = 60;
        StyleTextBox(sshRemoteHostTextBox);
        StyleTextBox(sshRemotePortTextBox);
        var remotePanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = SurfaceBackColor };
        sshRemoteHostTextBox.Width = 200;
        var remotePortLabel = new Label { Text = "Port", AutoSize = true, Margin = new Padding(8, 4, 4, 0) };
        remotePanel.Controls.Add(sshRemoteHostTextBox);
        remotePanel.Controls.Add(remotePortLabel);
        remotePanel.Controls.Add(sshRemotePortTextBox);
        AddRow(panel, "Remote host", remotePanel);

        testSshButton.Text = "Test tunnel";
        testSshButton.AutoSize = true;
        StyleButton(testSshButton, ButtonTone.Primary);
        testSshButton.Click += TestSshButton_Click;
        var testPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = SurfaceBackColor,
        };
        testPanel.Controls.Add(testSshButton);
        AddRow(panel, "Connection", testPanel);

        var note = new Label
        {
            Text = "Remote host/port = where PostgreSQL is visible from the SSH server. Test tunnel checks the selected database URL(s) through SSH.",
            AutoSize = false,
            Dock = DockStyle.Fill,
            Height = 34,
            ForeColor = MutedTextColor,
            Margin = new Padding(0, 4, 0, 0),
        };
        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(note, 1, row);

        return panel;
    }

    private async void TestSshButton_Click(object? sender, EventArgs eventArgs)
    {
        BeginLogOperation("SSH tunnel test");
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
            AppendLog(FormatValidationError(ex.Message));
            finalStatus = "SSH tunnel test failed.";
            statusLabel.Text = finalStatus;
        }
        catch (PostgresException ex)
        {
            AppendLog(FormatPostgresError(ex));
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
            AppendLog(FormatUnexpectedError(ex.Message));
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
        var logShell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(14, 12, 14, 14),
            Margin = new Padding(0, 0, 0, 0),
            BackColor = LogBackColor,
        };
        logShell.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        logShell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var logHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = LogBackColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        logHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        logHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var logTitle = new Label
        {
            Text = "Operations log",
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(176, 190, 208),
            Margin = Padding.Empty,
        };

        var logFontButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = LogBackColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };

        ConfigureLogFontButton(logFontDecreaseButton, "A-");
        ConfigureLogFontButton(logFontIncreaseButton, "A+");
        logFontDecreaseButton.Click += (_, _) => ChangeLogFontSize(-1f);
        logFontIncreaseButton.Click += (_, _) => ChangeLogFontSize(1f);
        SetHelp(logFontDecreaseButton, "Decrease operations log font size.");
        SetHelp(logFontIncreaseButton, "Increase operations log font size.");
        logFontButtons.Controls.Add(logFontDecreaseButton);
        logFontButtons.Controls.Add(logFontIncreaseButton);

        logHeader.Controls.Add(logTitle, 0, 0);
        logHeader.Controls.Add(logFontButtons, 1, 0);

        var logBody = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = LogBackColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        logBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        logBody.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 8));

        logTextBox.Dock = DockStyle.Fill;
        logTextBox.ReadOnly = true;
        logTextBox.ScrollBars = RichTextBoxScrollBars.None;
        logTextBox.WordWrap = true;
        logTextBox.DetectUrls = false;
        logTextBox.HideSelection = false;
        logTextBox.BorderStyle = BorderStyle.None;
        logTextBox.Font = new Font("Consolas", logFontSize);
        logTextBox.BackColor = LogBackColor;
        logTextBox.ForeColor = LogForeColor;
        logTextBox.Margin = new Padding(0, 0, 8, 0);
        logTextBox.VScroll += (_, _) => UpdateLogScrollThumb();
        logTextBox.TextChanged += (_, _) => UpdateLogScrollThumb();
        logTextBox.Resize += (_, _) => UpdateLogScrollThumb();
        logTextBox.MouseWheel += (_, _) => BeginInvoke((Action)UpdateLogScrollThumb);
        SetHelp(logTextBox,
            "Operations log. It keeps the latest six dry runs, copies, SSH tests, and database peeks. Text wraps instead of overflowing horizontally.");

        logScrollTrack.Dock = DockStyle.Fill;
        logScrollTrack.Width = 8;
        logScrollTrack.BackColor = Color.FromArgb(19, 28, 43);
        logScrollTrack.Margin = Padding.Empty;
        logScrollTrack.Padding = Padding.Empty;
        logScrollTrack.Cursor = Cursors.Hand;
        logScrollTrack.Visible = false;
        logScrollTrack.MouseDown += LogScrollTrack_MouseDown;
        logScrollTrack.Resize += (_, _) => UpdateLogScrollThumb();

        logScrollThumb.Width = 8;
        logScrollThumb.Height = 32;
        logScrollThumb.Left = 0;
        logScrollThumb.Top = 0;
        logScrollThumb.Visible = false;
        logScrollThumb.BackColor = Color.FromArgb(86, 101, 124);
        logScrollThumb.Cursor = Cursors.Hand;
        logScrollThumb.MouseDown += LogScrollThumb_MouseDown;
        logScrollThumb.MouseMove += LogScrollThumb_MouseMove;
        logScrollThumb.MouseUp += LogScrollThumb_MouseUp;
        logScrollTrack.Controls.Add(logScrollThumb);

        logBody.Controls.Add(logTextBox, 0, 0);
        logBody.Controls.Add(logScrollTrack, 1, 0);

        logShell.Controls.Add(logHeader, 0, 0);
        logShell.Controls.Add(logBody, 0, 1);
        UpdateLogFontButtonState();
        return logShell;
    }

    private void ConfigureLogFontButton(Button button, string text)
    {
        button.Text = text;
        button.AutoSize = false;
        button.Size = new Size(34, 23);
        button.Margin = new Padding(4, 0, 0, 0);
        button.Padding = Padding.Empty;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(60, 74, 96);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(35, 47, 66);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(48, 64, 88);
        button.BackColor = Color.FromArgb(20, 29, 44);
        button.ForeColor = LogForeColor;
        button.Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold);
        button.UseVisualStyleBackColor = false;
        button.Cursor = Cursors.Hand;
    }

    private void ChangeLogFontSize(float delta)
    {
        logFontSize = Math.Clamp(logFontSize + delta, MinLogFontSize, MaxLogFontSize);
        logTextBox.Font = new Font("Consolas", logFontSize);
        UpdateLogFontButtonState();
        UpdateLogScrollThumb();
    }

    private void UpdateLogFontButtonState()
    {
        logFontDecreaseButton.Enabled = logFontSize > MinLogFontSize;
        logFontIncreaseButton.Enabled = logFontSize < MaxLogFontSize;
        logFontDecreaseButton.Cursor = logFontDecreaseButton.Enabled ? Cursors.Hand : Cursors.Default;
        logFontIncreaseButton.Cursor = logFontIncreaseButton.Enabled ? Cursors.Hand : Cursors.Default;
    }

    private Control BuildFooter()
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            ColumnCount = 2,
            AutoSize = true,
            Padding = new Padding(0, 14, 0, 0),
            BackColor = WindowBackColor,
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        statusLabel.Text = "Ready. Start with a dry run.";
        statusLabel.AutoSize = true;
        statusLabel.Anchor = AnchorStyles.Left;
        statusLabel.ForeColor = MutedTextColor;
        statusLabel.Font = new Font(Font.FontFamily, 9.5f, FontStyle.Regular);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = WindowBackColor,
        };

        clearLogButton.Text = "Clear log";
        clearLogButton.AutoSize = true;
        StyleButton(clearLogButton, ButtonTone.Secondary);
        clearLogButton.Click += (_, _) => ClearLogHistory();
        SetHelp(clearLogButton, "Clear all currently visible operation history.");

        saveLogButton.Text = "Save log";
        saveLogButton.AutoSize = true;
        StyleButton(saveLogButton, ButtonTone.Secondary);
        saveLogButton.Click += SaveLogButton_Click;
        SetHelp(saveLogButton, "Save the visible redacted operations log to a text file.");

        cancelButton.Text = "Cancel";
        cancelButton.AutoSize = true;
        cancelButton.Enabled = false;
        StyleButton(cancelButton, ButtonTone.Danger);
        cancelButton.Click += (_, _) => activeRun?.Cancel();
        SetHelp(cancelButton, "Ask the active run to stop at the next safe cancellation point.");

        runButton.AutoSize = true;
        StyleButton(runButton, ButtonTone.Primary);
        runButton.Click += RunButton_Click;
        SetHelp(runButton, "Start the current mode. Dry run previews; copy writes to the destination database.");

        buttons.Controls.Add(clearLogButton);
        buttons.Controls.Add(saveLogButton);
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(runButton);

        footer.Controls.Add(statusLabel, 0, 0);
        footer.Controls.Add(buttons, 1, 0);
        return footer;
    }

    // ── Run logic ───────────────────────────────────────────────────────────────

    private async void RunButton_Click(object? sender, EventArgs eventArgs)
    {
        BeginLogOperation(dryRunCheckBox.Checked ? "Dry run" : "Copy");
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
            if (!ConfirmTruncateIfNeeded(settings))
            {
                logger.Info("Copy cancelled before destination truncation.");
                statusLabel.Text = "Copy cancelled.";
                return;
            }

            await new MigrationRunner(logger).RunAsync(
                settings,
                destructiveActionsConfirmed: true,
                activeRun.Token);

            statusLabel.Text = settings.DryRun ? "Dry run complete." : "Copy complete.";
        }
        catch (ValidationException ex)
        {
            logger.Error(FormatValidationError(ex.Message));
            statusLabel.Text = "Validation failed.";
        }
        catch (MigrationTableException ex)
        {
            logger.Error(FormatMigrationTableError(ex.Message));
            logger.Error($"Copied before failure: {ex.TablesCopiedBeforeFailure} table(s), {ex.RowsCopiedBeforeFailure} row(s).");
            statusLabel.Text = "Migration failed.";
        }
        catch (VerificationException ex)
        {
            logger.Error(FormatVerificationError(ex.Message));
            statusLabel.Text = "Verification failed.";
        }
        catch (PostgresException ex)
        {
            logger.Error(FormatPostgresError(ex));
            statusLabel.Text = "PostgreSQL error.";
        }
        catch (NpgsqlException ex)
        {
            logger.Error(FormatNpgsqlError(ex.Message));
            statusLabel.Text = "Connection failed.";
        }
        catch (OperationCanceledException)
        {
            logger.Error("Migration cancelled.");
            statusLabel.Text = "Cancelled.";
        }
        catch (Exception ex)
        {
            logger.Error(FormatUnexpectedError(ex.Message));
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
            createSchemaCheckBox.Checked,
            false,
            false);

        return MigrationSettingsValidator.Validate(options);
    }

    private bool ConfirmTruncateIfNeeded(MigrationSettings settings)
    {
        if (settings.DryRun || !settings.TruncateDestination)
            return true;

        var message =
            "Truncate destination will delete all rows from the planned destination tables before copying." +
            Environment.NewLine + Environment.NewLine +
            "Origin is not changed. Destination data removed by this step cannot be restored by PostgresCopy." +
            Environment.NewLine + Environment.NewLine +
            "Use this only when the destination should become a fresh copy of the origin. If you are unsure, choose No and run a dry run first.";

        var result = MessageBox.Show(
            this,
            message,
            "Confirm destination truncation",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        return result == DialogResult.Yes;
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

        var operation = activeLogOperation;
        if (operation is null)
        {
            BeginLogOperation("Operation");
            operation = activeLogOperation
                ?? throw new InvalidOperationException("Log operation was not initialized.");
        }

        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        operation.Lines.Add(line);
        AppendLogLineToTextBox(line);
        logTextBox.ScrollToCaret();
        UpdateLogScrollThumb();
    }

    private void AppendLogLineToTextBox(string line)
    {
        logTextBox.SelectionStart = logTextBox.TextLength;
        logTextBox.SelectionLength = 0;
        logTextBox.SelectionColor = GetLogLineColor(line);
        logTextBox.AppendText(line + Environment.NewLine);
        logTextBox.SelectionColor = LogForeColor;
        logTextBox.SelectionStart = logTextBox.TextLength;
    }

    private void BeginLogOperation(string title)
    {
        activeLogOperation = new LogOperation(title);
        logOperations.Add(activeLogOperation);
        while (logOperations.Count > MaxLogOperations)
            logOperations.RemoveAt(0);

        RenderLogHistory();
        AppendLog($"--- {title} started ---");
    }

    private void ClearLogHistory()
    {
        logOperations.Clear();
        activeLogOperation = null;
        logTextBox.Clear();
        UpdateLogScrollThumb();
    }

    private void SaveLogButton_Click(object? sender, EventArgs eventArgs)
    {
        if (string.IsNullOrWhiteSpace(logTextBox.Text))
        {
            MessageBox.Show(
                this,
                "There is no operations log to save yet.",
                "Save log",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Save operations log",
            FileName = $"PostgresCopy-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            Filter = "Text files (*.txt)|*.txt|Markdown files (*.md)|*.md|All files (*.*)|*.*",
            DefaultExt = "txt",
            AddExtension = true,
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            File.WriteAllText(dialog.FileName, logTextBox.Text);
            statusLabel.Text = "Operations log saved.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Could not save the operations log.{Environment.NewLine}{ex.Message}",
                "Save log failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void RenderLogHistory()
    {
        logTextBox.Clear();
        foreach (var operation in logOperations)
        {
            foreach (var line in operation.Lines)
                AppendLogLineToTextBox(line);
        }

        UpdateLogScrollThumb();
    }

    private static Color GetLogLineColor(string line)
    {
        var message = StripTimestamp(line).TrimStart();

        if (message.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Failed ", StringComparison.OrdinalIgnoreCase)
            || message.Contains(" failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Validation failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("PostgreSQL returned an error", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Could not complete", StringComparison.OrdinalIgnoreCase))
        {
            return LogErrorColor;
        }

        if (message.StartsWith("[warn]", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cancelled", StringComparison.OrdinalIgnoreCase)
            || message.Contains("warning", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Truncate destination", StringComparison.OrdinalIgnoreCase)
            || message.Contains("will be truncated", StringComparison.OrdinalIgnoreCase))
        {
            return LogWarningColor;
        }

        if (message.StartsWith("OK", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("[ok]", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Copied ", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Connected:", StringComparison.OrdinalIgnoreCase)
            || message.Contains(" established", StringComparison.OrdinalIgnoreCase)
            || message.Contains(" passed", StringComparison.OrdinalIgnoreCase)
            || message.Contains(" complete", StringComparison.OrdinalIgnoreCase))
        {
            return LogSuccessColor;
        }

        if (message.StartsWith("==", StringComparison.Ordinal)
            || message.StartsWith("---", StringComparison.Ordinal)
            || message.StartsWith("Plan:", StringComparison.OrdinalIgnoreCase))
        {
            return LogStepColor;
        }

        if (message.StartsWith("Copying ", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Checking ", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Connecting ", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Running ", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Discovering ", StringComparison.OrdinalIgnoreCase))
        {
            return LogWorkColor;
        }

        if (message.StartsWith("Origin:", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Destination:", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Database:", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Databases:", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Tables:", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Schema:", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Mode:", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("  -", StringComparison.Ordinal)
            || message.StartsWith("  ", StringComparison.Ordinal))
        {
            return LogDataColor;
        }

        if (message.StartsWith("What happened:", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("How to resolve:", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("What to try:", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Why PostgresCopy stopped:", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("SQLSTATE:", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("Note:", StringComparison.OrdinalIgnoreCase))
        {
            return LogHelpColor;
        }

        return LogForeColor;
    }

    private static string StripTimestamp(string line)
    {
        return line.Length > 11
            && line[0] == '['
            && line[3] == ':'
            && line[6] == ':'
            && line[9] == ']'
            && line[10] == ' '
            ? line[11..]
            : line;
    }

    private void LogScrollTrack_MouseDown(object? sender, MouseEventArgs e)
    {
        if (logScrollThumb.Bounds.Contains(e.Location))
            return;

        ScrollLogToThumbTop(e.Y - logScrollThumb.Height / 2);
    }

    private void LogScrollThumb_MouseDown(object? sender, MouseEventArgs e)
    {
        draggingLogScrollThumb = true;
        logScrollDragStartY = logScrollThumb.PointToScreen(e.Location).Y;
        logScrollDragStartTop = logScrollThumb.Top;
        logScrollThumb.Capture = true;
    }

    private void LogScrollThumb_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!draggingLogScrollThumb)
            return;

        var currentY = logScrollThumb.PointToScreen(e.Location).Y;
        ScrollLogToThumbTop(logScrollDragStartTop + currentY - logScrollDragStartY);
    }

    private void LogScrollThumb_MouseUp(object? sender, MouseEventArgs e)
    {
        draggingLogScrollThumb = false;
        logScrollThumb.Capture = false;
    }

    private void UpdateLogScrollThumb()
    {
        if (!logTextBox.IsHandleCreated || logScrollTrack.Height <= 0)
            return;

        var totalLines = GetLogVisualLineCount();
        var visibleLines = GetLogVisibleLineCount();
        var maxFirstLine = Math.Max(0, totalLines - visibleLines);

        if (maxFirstLine == 0)
        {
            logScrollTrack.Visible = false;
            logScrollThumb.Visible = false;
            return;
        }

        var firstLine = Math.Clamp(GetLogFirstVisibleLine(), 0, maxFirstLine);
        var trackHeight = logScrollTrack.ClientSize.Height;
        var thumbHeight = Math.Clamp(
            (int)Math.Round(trackHeight * (visibleLines / (double)totalLines)),
            28,
            trackHeight);
        var travel = Math.Max(1, trackHeight - thumbHeight);
        var thumbTop = (int)Math.Round(travel * (firstLine / (double)maxFirstLine));

        logScrollTrack.Visible = true;
        logScrollThumb.Visible = true;
        logScrollThumb.SetBounds(0, Math.Clamp(thumbTop, 0, travel), logScrollTrack.Width, thumbHeight);
    }

    private void ScrollLogToThumbTop(int thumbTop)
    {
        var totalLines = GetLogVisualLineCount();
        var visibleLines = GetLogVisibleLineCount();
        var maxFirstLine = Math.Max(0, totalLines - visibleLines);
        if (maxFirstLine == 0)
            return;

        var travel = Math.Max(1, logScrollTrack.ClientSize.Height - logScrollThumb.Height);
        var clampedTop = Math.Clamp(thumbTop, 0, travel);
        var targetLine = (int)Math.Round(maxFirstLine * (clampedTop / (double)travel));
        ScrollLogToFirstVisibleLine(targetLine);
    }

    private void ScrollLogToFirstVisibleLine(int targetLine)
    {
        if (!logTextBox.IsHandleCreated)
            return;

        var currentLine = GetLogFirstVisibleLine();
        var delta = targetLine - currentLine;
        if (delta != 0)
            SendMessage(logTextBox.Handle, EmLineScroll, IntPtr.Zero, new IntPtr(delta));

        UpdateLogScrollThumb();
    }

    private int GetLogFirstVisibleLine()
    {
        var charIndex = logTextBox.GetCharIndexFromPosition(new Point(1, 1));
        return logTextBox.GetLineFromCharIndex(charIndex);
    }

    private int GetLogVisualLineCount()
    {
        if (logTextBox.TextLength == 0)
            return 1;

        return logTextBox.GetLineFromCharIndex(logTextBox.TextLength - 1) + 1;
    }

    private int GetLogVisibleLineCount()
    {
        return Math.Max(1, logTextBox.ClientSize.Height / Math.Max(1, logTextBox.Font.Height));
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
        createSchemaCheckBox.Enabled = !running;

        peekDatabaseTextBox.Enabled = !running;
        peekButton.Enabled = !running;
        preflightButton.Enabled = !running;

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
        saveLogButton.Enabled = !running;
        runButton.Enabled = !running;
        cancelButton.Enabled = running;
        RefreshButtonStyles();

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
        runButton.Text = dryRunCheckBox.Checked ? "Run dry run" : "Run copy";
        runButton.Enabled = activeRun is null;
        ApplyButtonEnabledState(runButton, ButtonTone.Primary);

        if (activeRun is null)
        {
            if (dryRunCheckBox.Checked && truncateCheckBox.Checked)
                statusLabel.Text = "Ready. Dry run will preview truncation without deleting data.";
            else if (truncateCheckBox.Checked)
                statusLabel.Text = "Ready to copy. You will confirm truncation before rows are deleted.";
            else
                statusLabel.Text = dryRunCheckBox.Checked ? "Ready. Start with a dry run." : "Ready to copy.";
        }
    }

    private void RefreshButtonStyles()
    {
        ApplyButtonEnabledState(runButton, ButtonTone.Primary);
        ApplyButtonEnabledState(peekButton, ButtonTone.Primary);
        ApplyButtonEnabledState(preflightButton, ButtonTone.Primary);
        ApplyButtonEnabledState(testSshButton, ButtonTone.Primary);
        ApplyButtonEnabledState(sshKeyBrowseButton, ButtonTone.Secondary);
        ApplyButtonEnabledState(clearLogButton, ButtonTone.Secondary);
        ApplyButtonEnabledState(saveLogButton, ButtonTone.Secondary);
        ApplyButtonEnabledState(cancelButton, ButtonTone.Danger);
    }

    private enum ButtonTone
    {
        Primary,
        Secondary,
        Danger,
    }

    private void StyleTextBox(TextBox textBox)
    {
        textBox.BorderStyle = BorderStyle.None;
        textBox.BackColor = Color.FromArgb(246, 249, 252);
        textBox.ForeColor = PrimaryTextColor;
        textBox.Font = new Font(Font.FontFamily, 9.5f, FontStyle.Regular);
        textBox.Margin = new Padding(0, 6, 0, 8);
    }

    private void StyleComboBox(ComboBox comboBox)
    {
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.BackColor = Color.FromArgb(250, 252, 254);
        comboBox.ForeColor = PrimaryTextColor;
        comboBox.Font = new Font(Font.FontFamily, 9.5f, FontStyle.Regular);
        comboBox.Margin = new Padding(0, 5, 0, 7);
    }

    private void StyleCheckBox(CheckBox checkBox)
    {
        checkBox.ForeColor = PrimaryTextColor;
        checkBox.Font = new Font(Font.FontFamily, 9.5f, FontStyle.Regular);
        checkBox.Margin = new Padding(0, 3, 18, 7);
        checkBox.FlatStyle = FlatStyle.System;
    }

    private void StyleButton(Button button, ButtonTone tone)
    {
        button.Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold);
        button.MinimumSize = new Size(110, 36);
        button.Padding = new Padding(12, 2, 12, 2);
        button.Margin = new Padding(8, 0, 0, 0);
        button.FlatStyle = FlatStyle.System;
        button.UseVisualStyleBackColor = true;
        button.BackColor = SystemColors.Control;
        button.ForeColor = SystemColors.ControlText;
        button.Cursor = Cursors.Hand;
        ApplyButtonEnabledState(button, tone);
    }

    private void ApplyButtonEnabledState(Button button, ButtonTone tone)
    {
        button.BackColor = SystemColors.Control;
        button.ForeColor = button.Enabled ? SystemColors.ControlText : SystemColors.GrayText;
        button.UseVisualStyleBackColor = true;
        button.Font = new Font(
            Font.FontFamily,
            9.5f,
            tone == ButtonTone.Primary ? FontStyle.Bold : FontStyle.Regular);
        button.Cursor = button.Enabled ? Cursors.Hand : Cursors.Default;
    }

    private void AddRow(TableLayoutPanel panel, string labelText, Control control, string? helpText = null)
    {
        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(54, 65, 82),
            Margin = new Padding(0, 10, 18, 10),
        };

        control.Dock = DockStyle.Fill;
        if (control.Margin == Padding.Empty)
            control.Margin = new Padding(0, 5, 0, 7);

        if (!string.IsNullOrWhiteSpace(helpText))
        {
            SetHelp(label, helpText);
            SetHelp(control, helpText);
        }

        panel.Controls.Add(label, 0, row);
        panel.Controls.Add(control, 1, row);
    }

    private void SetHelp(Control control, string text)
    {
        helpToolTip.SetToolTip(control, text);
    }

    private static string FormatValidationError(string message)
    {
        return "Validation failed before copying any data." + Environment.NewLine
            + message + Environment.NewLine
            + "What to try: check the highlighted fields, confirm the origin and destination point to the intended databases, then run a dry run again.";
    }

    private static string FormatMigrationTableError(string message)
    {
        return "A table copy failed after the migration had already started." + Environment.NewLine
            + message + Environment.NewLine
            + "What to try: inspect the named table on both databases for constraints, triggers, permissions, or type differences, then rerun from a fresh dry run.";
    }

    private static string FormatVerificationError(string message)
    {
        return "The copy finished, but row-count verification did not match." + Environment.NewLine
            + message + Environment.NewLine
            + "What to try: do not trust the destination yet. Check for triggers, concurrent writes, failed tables above, or a destination database that was modified during the copy.";
    }

    private static string FormatPostgresError(PostgresException ex)
    {
        var hint = ex.SqlState switch
        {
            "28P01" => "Check the username and password in the connection string.",
            "3D000" => "Check the database name in the connection string.",
            "42501" => "The connected user does not have enough permission for this operation. Grant the needed schema/table privileges or use a role with copy rights.",
            "42P01" => "A table PostgreSQL expected was not found. Check the schema name, table filter, and whether Create schema was run against the intended destination.",
            "42703" => "A column PostgreSQL expected was not found. Check whether origin and destination schemas were generated from the same version.",
            _ => "Check the database server message above, then confirm host, port, database name, SSL mode, and user permissions."
        };

        return $"PostgreSQL returned an error: {ex.MessageText}" + Environment.NewLine
            + $"SQLSTATE: {ex.SqlState}" + Environment.NewLine
            + $"What to try: {hint}";
    }

    private static string FormatNpgsqlError(string message)
    {
        return "Could not complete the PostgreSQL connection or network operation." + Environment.NewLine
            + message + Environment.NewLine
            + "What to try: check host, port, SSL mode, VPN/SSH tunnel status, firewall rules, and whether the database accepts direct connections from this machine.";
    }

    private static string FormatUnexpectedError(string message)
    {
        return "PostgresCopy hit an unexpected error before it could finish." + Environment.NewLine
            + message + Environment.NewLine
            + "What to try: review the last successful log step, run a dry run if possible, and share this log if the problem repeats.";
    }

    private sealed class LogOperation(string title)
    {
        public string Title { get; } = title;
        public List<string> Lines { get; } = [];
    }
}
