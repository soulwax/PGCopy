using Npgsql;
using PostgresCopy.Cli;
using PostgresCopy.Config;
using PostgresCopy.Migration;

namespace PostgresCopy.Desktop;

public sealed class MainForm : Form
{
    private readonly TextBox originTextBox = new();
    private readonly TextBox destinationTextBox = new();
    private readonly TextBox schemaTextBox = new();
    private readonly TextBox tablesTextBox = new();
    private readonly CheckBox dryRunCheckBox = new();
    private readonly CheckBox verifyCheckBox = new();
    private readonly CheckBox truncateCheckBox = new();
    private readonly CheckBox createSchemaCheckBox = new();
    private readonly TextBox truncateConfirmationTextBox = new();
    private readonly Button runButton = new();
    private readonly Button cancelButton = new();
    private readonly Button clearLogButton = new();
    private readonly TextBox logTextBox = new();
    private readonly Label statusLabel = new();

    private CancellationTokenSource? activeRun;

    public MainForm()
    {
        Text = "PostgresCopy";
        MinimumSize = new Size(920, 720);
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
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "PostgresCopy",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 18, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12),
        };

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(BuildInputPanel(), 0, 1);
        root.Controls.Add(BuildLogBox(), 0, 2);
        root.Controls.Add(BuildFooter(), 0, 3);

        Controls.Add(root);
    }

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

        schemaTextBox.Text = "public";
        schemaTextBox.PlaceholderText = "public";

        tablesTextBox.PlaceholderText = "optional: users,orders,products";

        AddRow(panel, "Origin URL", originTextBox);
        AddRow(panel, "Destination URL", destinationTextBox);
        AddRow(panel, "Schema", schemaTextBox);
        AddRow(panel, "Tables", tablesTextBox);
        AddRow(panel, "Options", BuildOptionsPanel());
        AddRow(panel, "Confirm", truncateConfirmationTextBox);

        truncateConfirmationTextBox.PlaceholderText = "Type TRUNCATE when destination truncation is checked";
        truncateConfirmationTextBox.Enabled = false;
        truncateConfirmationTextBox.TextChanged += (_, _) => UpdateRunState();

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
            {
                truncateConfirmationTextBox.Clear();
            }

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

    private async void RunButton_Click(object? sender, EventArgs eventArgs)
    {
        logTextBox.Clear();
        SetRunning(true);

        activeRun = new CancellationTokenSource();
        var logger = new UiMigrationLogger(AppendLog);

        try
        {
            var settings = BuildSettings();
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
            activeRun.Dispose();
            activeRun = null;
            SetRunning(false);
        }
    }

    private MigrationSettings BuildSettings()
    {
        if (truncateCheckBox.Checked
            && !string.Equals(truncateConfirmationTextBox.Text, "TRUNCATE", StringComparison.Ordinal))
        {
            throw new ValidationException("Type TRUNCATE to confirm destination truncation.");
        }

        var options = new CliOptions(
            originTextBox.Text.Trim(),
            destinationTextBox.Text.Trim(),
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
        {
            return [];
        }

        return tables
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private void AppendLog(string message)
    {
        if (IsDisposed)
        {
            return;
        }

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
        clearLogButton.Enabled = !running;
        runButton.Enabled = !running;
        cancelButton.Enabled = running;

        if (running)
        {
            statusLabel.Text = dryRunCheckBox.Checked ? "Running dry run..." : "Running copy...";
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
        {
            statusLabel.Text = "Type TRUNCATE to enable destination truncation.";
        }
        else if (activeRun is null)
        {
            statusLabel.Text = dryRunCheckBox.Checked
                ? "Ready. Start with a dry run."
                : "Ready to copy.";
        }
    }
}
