// File: src/PostgresCopy.Desktop/OverwriteAllDatabasesDialog.cs

namespace PostgresCopy.Desktop;

internal sealed class OverwriteAllDatabasesDialog : Form
{
    private readonly TextBox confirmationTextBox = new();
    private readonly Button confirmButton = new();

    public OverwriteAllDatabasesDialog(IReadOnlyList<string> databaseNames)
    {
        Text = "Confirm overwrite of destination databases";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(520, 420);
        Padding = new Padding(16);

        var message = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 100,
            Text = "The following destination databases will be DROPPED and recreated from origin. " +
                   "Any other active connections to these databases will be forcibly terminated. " +
                   "All tables, indexes, sequences, functions, views, triggers, and data in each one will be permanently deleted. There is no undo.",
        };

        var list = new ListBox { Dock = DockStyle.Fill };
        foreach (var name in databaseNames)
        {
            list.Items.Add(name);
        }

        var confirmLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Text = "Type OVERWRITE to continue:",
            Margin = new Padding(0, 8, 0, 4),
        };

        confirmationTextBox.Dock = DockStyle.Top;
        confirmationTextBox.TextChanged += (_, _) =>
            confirmButton.Enabled = string.Equals(confirmationTextBox.Text, "OVERWRITE", StringComparison.Ordinal);

        confirmButton.Text = "Overwrite";
        confirmButton.DialogResult = DialogResult.OK;
        confirmButton.Enabled = false;
        confirmButton.Dock = DockStyle.Right;

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Dock = DockStyle.Right,
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
        };
        buttonPanel.Controls.Add(confirmButton);
        buttonPanel.Controls.Add(cancelButton);

        Controls.Add(list);
        Controls.Add(confirmationTextBox);
        Controls.Add(confirmLabel);
        Controls.Add(message);
        Controls.Add(buttonPanel);

        AcceptButton = confirmButton;
        CancelButton = cancelButton;
    }
}
