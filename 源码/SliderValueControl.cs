using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Drawing.Text;
using System.Windows.Forms;

namespace QingMonitor {
    // A single, buffered drawing surface keeps the track and its numeric readout in sync.
    public sealed class SliderValueControl : Control {
        private decimal minimum, maximum = 100M, increment = 1M, value;
        private int decimalPlaces;
        private bool dragging, hot;
        private string unitText = "";
        private string[] stepLabels;
        private Color accentColor = GlassTheme.Accent;

        public event EventHandler ValueChanged;

        public SliderValueControl() {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = GlassTheme.Surface;
            ForeColor = GlassTheme.Text;
            Size = new Size(340, 38);
            MinimumSize = new Size(180, 36);
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.Slider;
        }

        [DefaultValue(typeof(decimal), "0")]
        public decimal Minimum {
            get { return minimum; }
            set { minimum = value; if(maximum < minimum) maximum = minimum; SetValue(this.value); Invalidate(); }
        }
        [DefaultValue(typeof(decimal), "100")]
        public decimal Maximum {
            get { return maximum; }
            set { maximum = value; if(minimum > maximum) minimum = maximum; SetValue(this.value); Invalidate(); }
        }
        [DefaultValue(typeof(decimal), "1")]
        public decimal Increment {
            get { return increment; }
            set { if(value <= 0M) throw new ArgumentOutOfRangeException("value"); increment = value; SetValue(this.value); }
        }
        [DefaultValue(typeof(decimal), "0")]
        public decimal Value { get { return value; } set { SetValue(value); } }
        [DefaultValue(0)]
        public int DecimalPlaces {
            get { return decimalPlaces; }
            set { if(value < 0 || value > 4) throw new ArgumentOutOfRangeException("value"); decimalPlaces = value; Invalidate(); }
        }
        [DefaultValue("")]
        public string UnitText { get { return unitText; } set { unitText = value ?? ""; Invalidate(); } }
        public Color AccentColor { get { return accentColor; } set { accentColor = value; Invalidate(); } }
        [DefaultValue(null)]
        public string[] StepLabels {
            get { return stepLabels == null ? null : (string[])stepLabels.Clone(); }
            set {
                stepLabels = value == null ? null : (string[])value.Clone();
                Invalidate();
                if(IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            }
        }

        private void SetValue(decimal requested) {
            decimal next = Math.Max(minimum, Math.Min(maximum, requested));
            next = Math.Max(minimum, Math.Min(maximum, minimum + Math.Round((next - minimum) / increment, 0, MidpointRounding.AwayFromZero) * increment));
            if(value == next) return;
            value = next;
            Invalidate();
            if(IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            var handler = ValueChanged;
            if(handler != null) handler(this, EventArgs.Empty);
        }

        private string FormatValue(decimal number) {
            if(stepLabels != null && number == Decimal.Truncate(number) && number >= 0 && number < stepLabels.Length) {
                string label = stepLabels[(int)number];
                if(label != null) return label;
            }
            return number.ToString("F" + decimalPlaces, CultureInfo.CurrentCulture) + unitText;
        }
        private float DpiScale { get { return DeviceDpi / 96F; } }

        // Reserve the full range's readout width so the thumb never shifts when digit counts change.
        private RectangleF TrackBounds {
            get {
                int readout = Math.Max(TextRenderer.MeasureText(FormatValue(minimum), Font).Width,
                    TextRenderer.MeasureText(FormatValue(maximum), Font).Width);
                if(stepLabels != null) foreach(string label in stepLabels)
                    if(label != null) readout = Math.Max(readout,TextRenderer.MeasureText(label,Font).Width);
                float padding = 12F * DpiScale;
                float end = Math.Max(padding + 1F, Width - readout - 24F * DpiScale);
                return new RectangleF(padding, Height / 2F - 3F * DpiScale, end - padding, 6F * DpiScale);
            }
        }

        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF track = TrackBounds;
            float cy = track.Top + track.Height / 2F;
            float ratio = maximum == minimum ? 0F : (float)((value - minimum) / (maximum - minimum));
            float thumbX = track.Left + ratio * track.Width;
            Color fill = Enabled ? accentColor : Color.FromArgb(142, 153, 172);
            using(var pen = new Pen(Color.FromArgb(55, 83, 95, 115), track.Height)) {
                pen.StartCap = pen.EndCap = LineCap.Round;
                g.DrawLine(pen, track.Left, cy, track.Right, cy);
            }
            if(thumbX > track.Left) using(var pen = new Pen(fill, track.Height)) {
                pen.StartCap = pen.EndCap = LineCap.Round;
                g.DrawLine(pen, track.Left, cy, thumbX, cy);
            }
            if(stepLabels != null && maximum > minimum) using(var marks = new Pen(Color.FromArgb(90,GlassTheme.Muted),DpiScale)) {
                for(int index=0;index<stepLabels.Length;index++) {
                    if(index < minimum || index > maximum || stepLabels[index] == null) continue;
                    float x = track.Left+(float)((index-minimum)/(maximum-minimum))*track.Width;
                    g.DrawLine(marks,x,cy+10F*DpiScale,x,cy+12F*DpiScale);
                }
            }
            float radius = (hot || dragging ? 8F : 7F) * DpiScale;
            using(var shadow = new SolidBrush(Color.FromArgb(25, 40, 53, 76)))
                g.FillEllipse(shadow, thumbX - radius - DpiScale, cy - radius + DpiScale, radius * 2F + 2F * DpiScale, radius * 2F + 2F * DpiScale);
            using(var brush = new SolidBrush(Enabled ? Color.White : Color.FromArgb(225, 230, 239)))
                g.FillEllipse(brush, thumbX - radius, cy - radius, radius * 2F, radius * 2F);
            using(var pen = new Pen(fill, 2F * DpiScale))
                g.DrawEllipse(pen, thumbX - radius + DpiScale, cy - radius + DpiScale, (radius - DpiScale) * 2F, (radius - DpiScale) * 2F);
            if(Focused && ShowFocusCues) {
                var focus = Rectangle.Round(new RectangleF(thumbX - radius - 3F * DpiScale, cy - radius - 3F * DpiScale, (radius + 3F * DpiScale) * 2F, (radius + 3F * DpiScale) * 2F));
                ControlPaint.DrawFocusRectangle(g, focus, ForeColor, BackColor);
            }
            var readoutBounds = new Rectangle((int)(track.Right + 14F * DpiScale), 0, Math.Max(1, Width - (int)(track.Right + 14F * DpiScale) - (int)(3F * DpiScale)), Height);
            using(var brush = new SolidBrush(Enabled ? ForeColor : GlassTheme.Muted))
            using(var format = new StringFormat(StringFormat.GenericDefault)) {
                format.Alignment = StringAlignment.Far; format.LineAlignment = StringAlignment.Center;
                format.FormatFlags = StringFormatFlags.NoWrap;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.DrawString(FormatValue(value),Font,brush,readoutBounds,format);
            }
        }

        private void SetFromMouse(int x) {
            RectangleF track = TrackBounds;
            decimal ratio = (decimal)Math.Max(0F, Math.Min(1F, (x - track.Left) / track.Width));
            SetValue(minimum + (maximum - minimum) * ratio);
        }
        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);
            if(!Enabled || e.Button != MouseButtons.Left) return;
            Focus();
            dragging = true;
            Capture = true;
            SetFromMouse(e.X);
        }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if(dragging) SetFromMouse(e.X); }
        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e);
            if(e.Button != MouseButtons.Left || !dragging) return;
            SetFromMouse(e.X);
            dragging = false;
            Capture = false;
            Invalidate();
        }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if(!Capture) { dragging = false; Invalidate(); } }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { if(!Enabled) Capture = false; base.OnEnabledChanged(e); Invalidate(); }
        protected override bool IsInputKey(Keys keyData) {
            switch(keyData & Keys.KeyCode) {
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down: case Keys.Home: case Keys.End: return true;
            }
            return base.IsInputKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);
            if(!Enabled) return;
            switch(e.KeyCode) {
                case Keys.Left: case Keys.Down: SetValue(value - increment); break;
                case Keys.Right: case Keys.Up: SetValue(value + increment); break;
                case Keys.Home: SetValue(minimum); break;
                case Keys.End: SetValue(maximum); break;
                default: return;
            }
            e.Handled = true; e.SuppressKeyPress = true;
        }

        protected override AccessibleObject CreateAccessibilityInstance() { return new SliderAccessibleObject(this); }
        private sealed class SliderAccessibleObject : ControlAccessibleObject {
            private readonly SliderValueControl slider;
            public SliderAccessibleObject(SliderValueControl owner) : base(owner) { slider = owner; }
            public override string Value {
                get { return slider.FormatValue(slider.Value); }
                set {
                    if(slider.stepLabels != null) {
                        int index = Array.IndexOf(slider.stepLabels,value);
                        if(index >= 0) { slider.Value = index; return; }
                    }
                    decimal next; if(decimal.TryParse(value, out next)) slider.Value = next;
                }
            }
        }
    }
}
