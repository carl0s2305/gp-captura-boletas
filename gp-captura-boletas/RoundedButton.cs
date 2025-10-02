using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class RoundedButton : Button
{
    public int CornerRadius { get; set; } = 12;
    public Color BorderColor { get; set; } = Color.Transparent;
    public float BorderThickness { get; set; } = 1f;
    public Color HoverFillColor { get; set; } = Color.Empty;
    public Color PressedFillColor { get; set; } = Color.Empty;

    private bool _hover, _pressed;

    public RoundedButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.ResizeRedraw
               | ControlStyles.UserPaint
               | ControlStyles.Selectable, true);

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;   // respeta BackColor
        Cursor = Cursors.Hand;
        Padding = new Padding(12, 4, 12, 4);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.CompositingQuality = CompositingQuality.HighQuality;

        var rect = ClientRectangle;
        rect.Width -= 1; rect.Height -= 1; // para el borde
        using (var path = GetRoundRect(rect, CornerRadius))
        {
            // Back color según estado
            var fill = BackColor;
            if (_pressed && PressedFillColor != Color.Empty) fill = PressedFillColor;
            else if (_hover && HoverFillColor != Color.Empty) fill = HoverFillColor;

            using (var br = new SolidBrush(fill))
                e.Graphics.FillPath(br, path);

            if (BorderThickness > 0 && BorderColor.A > 0)
                using (var pen = new Pen(BorderColor, BorderThickness))
                    e.Graphics.DrawPath(pen, path);

            // Región para hit-test sin “serrucho”
            Region?.Dispose();
            Region = new Region(path);
        }

        // Texto centrado
        TextRenderer.DrawText(
            e.Graphics, Text, Font, ClientRectangle, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    protected override void OnMouseEnter(System.EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(System.EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs mevent) { _pressed = true; Invalidate(); base.OnMouseDown(mevent); }
    protected override void OnMouseUp(MouseEventArgs mevent) { _pressed = false; Invalidate(); base.OnMouseUp(mevent); }

    private static GraphicsPath GetRoundRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.StartFigure();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
