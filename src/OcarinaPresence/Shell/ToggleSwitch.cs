using System.Drawing.Drawing2D;

namespace OcarinaPresence;

public sealed class ToggleSwitch : CheckBox
{
    public static readonly Color OnColor = Color.FromArgb(35, 165, 90);
    public static readonly Color OffColor = Color.FromArgb(128, 132, 142);
    const int Frame = 15;
    const float Duration = 160f;

    readonly System.Windows.Forms.Timer _timer = new() { Interval = Frame };
    float _position;

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = false;
        Cursor = Cursors.Hand;
        _timer.Tick += (_, _) => Step();
        Fit();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Fit();
    }

    void Fit()
    {
        var height = Font.Height + 12;
        Size = new Size(height * 5 / 2, height);
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        base.OnCheckedChanged(e);
        if (IsHandleCreated && Visible)
        {
            _timer.Start();
            return;
        }
        _position = Checked ? 1 : 0;
        Invalidate();
    }

    void Step()
    {
        var target = Checked ? 1f : 0f;
        var step = Frame / Duration;
        _position = Math.Abs(target - _position) <= step ? target : _position + Math.Sign(target - _position) * step;
        if (_position == target)
            _timer.Stop();
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _timer.Dispose();
        base.Dispose(disposing);
    }

    static Color Blend(Color from, Color to, float amount) => Color.FromArgb(
        (int)(from.A + (to.A - from.A) * amount),
        (int)(from.R + (to.R - from.R) * amount),
        (int)(from.G + (to.G - from.G) * amount),
        (int)(from.B + (to.B - from.B) * amount));

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? SystemColors.Control);

        var eased = _position * _position * (3 - 2 * _position);
        var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        var diameter = bounds.Height;
        using var track = new GraphicsPath();
        track.AddArc(bounds.Left, bounds.Top, diameter, diameter, 90, 180);
        track.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 180);
        track.CloseFigure();
        using (var fill = new SolidBrush(Blend(OffColor, OnColor, eased)))
            g.FillPath(fill, track);
        if (Focused && ShowFocusCues)
        {
            using var focus = new Pen(SystemColors.Highlight, 2);
            g.DrawPath(focus, track);
        }

        var inset = 3f;
        var knob = diameter - inset * 2;
        var travel = bounds.Width - knob - inset * 2;
        g.FillEllipse(Brushes.White, bounds.Left + inset + travel * eased, bounds.Top + inset, knob, knob);

        using var font = new Font(Font, FontStyle.Bold);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var labelWidth = bounds.Width - knob - inset - diameter / 3;
        using (var on = new SolidBrush(Color.FromArgb((int)(255 * eased), Color.White)))
            g.DrawString("ON", font, on, new RectangleF(bounds.Left + diameter / 3, bounds.Top, labelWidth, bounds.Height), format);
        using (var off = new SolidBrush(Color.FromArgb((int)(255 * (1 - eased)), Color.White)))
            g.DrawString("OFF", font, off, new RectangleF(bounds.Left + knob + inset, bounds.Top, labelWidth, bounds.Height), format);
    }
}
