namespace GameProbe.TestTarget;

public sealed class TestTargetForm : Form
{
    public TestTargetForm()
    {
        Text = "GameProbe TestTarget — P0 skeleton";
        ClientSize = new Size(800, 500);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(32, 36, 44);

        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = "TestTarget\n\nP2 sẽ thêm lưới màu và log mouse message tại đây.",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14f, FontStyle.Regular),
            ForeColor = Color.FromArgb(190, 198, 210)
        };

        Controls.Add(label);
    }
}
