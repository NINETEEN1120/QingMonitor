using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace QingMonitor {
    // Keep the standard WinForms controls and their keyboard/accessibility behavior.
    internal static class GlassTheme {
        internal static readonly Color Surface = Color.FromArgb(218, 224, 235);
        internal static readonly Color Input = Color.FromArgb(241, 245, 251);
        internal static readonly Color Text = Color.FromArgb(35, 43, 57);
        internal static readonly Color Muted = Color.FromArgb(83, 95, 115);
        internal static readonly Color Accent = Color.FromArgb(35, 112, 218);
        private static readonly ConditionalWeakTable<Button, ButtonState> buttons = new ConditionalWeakTable<Button, ButtonState>();
        private static readonly ConditionalWeakTable<ToolStrip, MenuState> menus = new ConditionalWeakTable<ToolStrip, MenuState>();
        private sealed class ButtonState { internal bool Primary, Hot, Pressed; }
        private sealed class MenuState { internal bool Attached, Native; internal int Radius = 12; }

        internal static void Apply(Control root) {
            if(root == null) return;
            bool card = Object.Equals(root.Tag, "glass-card");
            bool insideCard = InsideCard(root);
            var button = root as Button;
            if(button != null) {
                if(!Object.Equals(button.Tag,"glass-navigation")) StyleButton(button, false);
            }
            else {
                if(!card) {
                    if(root is ComboBox || root is TextBoxBase || root is ListBox) root.BackColor = Input;
                    else if(insideCard && (root is Label || root is Panel || root is SliderValueControl || root is CheckBox || root is RadioButton)) root.BackColor = Color.Transparent;
                    else root.BackColor = Surface;
                }
                if(IsNeutral(root.ForeColor)) root.ForeColor = IsHint(root.ForeColor) ? Muted : Text;
            }
            var check = root as CheckBox;
            if(check != null) { check.FlatStyle = FlatStyle.Standard; check.UseVisualStyleBackColor = false; }
            var radio = root as RadioButton;
            if(radio != null) { radio.FlatStyle = FlatStyle.Standard; radio.UseVisualStyleBackColor = false; }
            var combo = root as ComboBox;
            if(combo != null) combo.FlatStyle = FlatStyle.Flat;
            var textBox = root as TextBoxBase;
            if(textBox != null) textBox.BorderStyle = BorderStyle.FixedSingle;
            var list = root as ListBox;
            if(list != null) list.BorderStyle = BorderStyle.FixedSingle;
            var slider = root as SliderValueControl;
            if(slider != null && slider.AccentColor == Color.FromArgb(84,190,255)) slider.AccentColor = Accent;
            foreach(Control child in root.Controls) Apply(child);
            root.Invalidate();
        }

        private static bool InsideCard(Control control) {
            for(Control parent = control.Parent; parent != null; parent = parent.Parent)
                if(Object.Equals(parent.Tag, "glass-card")) return true;
            return false;
        }
        private static bool IsHint(Color color) {
            return color.ToArgb() == Color.FromArgb(165,173,187).ToArgb() || color.ToArgb() == Muted.ToArgb() || color == SystemColors.GrayText;
        }
        private static bool IsNeutral(Color color) {
            int chroma = Math.Max(color.R,Math.Max(color.G,color.B))-Math.Min(color.R,Math.Min(color.G,color.B));
            return color.IsEmpty || color.IsSystemColor || color.A == 0 || chroma <= 36 ||
                color.ToArgb() == Text.ToArgb() || IsHint(color);
        }
        private static Color Mix(Color first, Color second, int percent) {
            return Color.FromArgb(first.R+(second.R-first.R)*percent/100, first.G+(second.G-first.G)*percent/100, first.B+(second.B-first.B)*percent/100);
        }

        internal static void StyleButton(Button button, bool primary) {
            if(button == null) return;
            ButtonState state;
            if(!buttons.TryGetValue(button, out state)) {
                state = new ButtonState(); buttons.Add(button, state);
                button.Resize += delegate { RoundButton(button); };
                button.MouseEnter += delegate { state.Hot = true; button.Invalidate(); };
                button.MouseLeave += delegate { state.Hot = false; state.Pressed = false; button.Invalidate(); };
                button.MouseDown += delegate(object sender, MouseEventArgs e) { if(e.Button == MouseButtons.Left) { state.Pressed = true; button.Invalidate(); } };
                button.MouseUp += delegate { state.Pressed = false; button.Invalidate(); };
                button.KeyDown += delegate(object sender, KeyEventArgs e) { if(e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { state.Pressed = true; button.Invalidate(); } };
                button.KeyUp += delegate { state.Pressed = false; button.Invalidate(); };
                button.LostFocus += delegate { state.Pressed = false; button.Invalidate(); };
                button.EnabledChanged += delegate { button.Invalidate(); };
                button.Paint += delegate(object sender, PaintEventArgs e) { PaintButton(button, state, e.Graphics); };
            }
            bool wasPrimary = state.Primary;
            state.Primary = primary;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.UseVisualStyleBackColor = false;
            if(primary) { button.BackColor = Accent; button.ForeColor = Color.White; }
            else if(wasPrimary || IsNeutral(button.BackColor)) { button.BackColor = Input; button.ForeColor = Text; }
            RoundButton(button); button.Invalidate();
        }

        private static void RoundButton(Button button) {
            if(button.Width < 1 || button.Height < 1) return;
            using(var path = GlassMaterial.Outline(new RectangleF(0,0,button.Width,button.Height), 7F*button.DeviceDpi/96F)) {
                Region previous = button.Region; button.Region = new Region(path); if(previous != null) previous.Dispose();
            }
        }
        private static void PaintButton(Button button, ButtonState state, Graphics g) {
            if(button.Width < 2 || button.Height < 2) return;
            var saved = g.Save();
            try {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Color fill = button.BackColor;
                if(!button.Enabled) fill = Mix(fill, Surface, 65);
                else if(state.Pressed) fill = Mix(fill, state.Primary ? Color.Black : Accent, state.Primary ? 12 : 16);
                else if(state.Hot) fill = Mix(fill, state.Primary ? Color.White : Accent, state.Primary ? 12 : 8);
                using(var path = GlassMaterial.Outline(new RectangleF(0.5F,0.5F,button.Width-1,button.Height-1),7F*button.DeviceDpi/96F))
                using(var brush = new SolidBrush(fill))
                using(var stroke = new Pen(state.Primary ? Color.FromArgb(100,255,255,255) : Color.FromArgb(220,255,255,255))) {
                    g.FillPath(brush,path); g.DrawPath(stroke,path);
                }
                using(var format = new StringFormat(StringFormat.GenericDefault))
                using(var brush = new SolidBrush(button.Enabled ? button.ForeColor : Muted)) {
                    format.Alignment = StringAlignment.Center; format.LineAlignment = StringAlignment.Center;
                    format.HotkeyPrefix = HotkeyPrefix.Show; format.Trimming = StringTrimming.EllipsisCharacter;
                    format.FormatFlags = StringFormatFlags.NoWrap;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    var bounds = new RectangleF(4,2,Math.Max(1,button.Width-8),Math.Max(1,button.Height-4));
                    g.DrawString(button.Text,button.Font,brush,bounds,format);
                }
                if(button.Focused) ControlPaint.DrawFocusRectangle(g,new Rectangle(4,4,Math.Max(1,button.Width-8),Math.Max(1,button.Height-8)),button.ForeColor,fill);
            } finally { g.Restore(saved); }
        }

        internal static ToolStripRenderer CreateMenuRenderer() { return new GlassMenuRenderer(); }

        internal static void AttachMenu(ContextMenuStrip menu, Func<Settings> settings) {
            if(menu == null) throw new ArgumentNullException("menu");
            if(settings == null) throw new ArgumentNullException("settings");
            AttachDropDown(menu, settings);
        }
        private static void AttachDropDown(ToolStripDropDown menu, Func<Settings> settings) {
            MenuState state = menus.GetValue(menu,delegate(ToolStrip owner) { return new MenuState(); });
            menu.Renderer = CreateMenuRenderer(); menu.BackColor = Surface; menu.ForeColor = Text;
            menu.Font = SystemFonts.MessageBoxFont;
            menu.Padding = new Padding(6);
            var dropdownMenu = menu as ToolStripDropDownMenu;
            if(dropdownMenu != null) { dropdownMenu.ShowImageMargin = false; dropdownMenu.ShowCheckMargin = true; }
            foreach(ToolStripItem item in menu.Items) {
                item.ForeColor = Text;
                var entry = item as ToolStripMenuItem;
                if(entry == null) continue;
                entry.Padding = new Padding(4,5,4,5);
                if(entry.HasDropDownItems) AttachDropDown(entry.DropDown, settings);
            }
            if(state.Attached) return;
            state.Attached = true;
            menu.Opened += delegate {
                Settings current = settings() ?? new Settings();
                bool glass = current.GlassEnabled && current.FrostPercent > 0;
                state.Radius = current.CornerRadius;
                state.Native = GlassMaterial.Apply(menu.Handle,glass,current.FrostPercent,current.OpacityPercent);
                if(state.Native) {
                    Region previous = menu.Region; menu.Region = null; if(previous != null) previous.Dispose();
                    if(!GlassMaterial.RoundNative(menu.Handle,state.Radius)) {
                        GlassMaterial.Apply(menu.Handle,false,0,0); state.Native = false;
                    }
                }
                if(!state.Native) {
                    using(var outline = GlassMaterial.Outline(new RectangleF(0,0,menu.Width,menu.Height),state.Radius*menu.DeviceDpi/96F)) {
                        Region previous = menu.Region; menu.Region = new Region(outline); if(previous != null) previous.Dispose();
                    }
                }
                menu.BackColor = state.Native ? Color.Black : Surface;
                menu.Invalidate();
            };
            menu.Closed += delegate { state.Native = false; };
        }

        private sealed class GlassMenuRenderer : ToolStripProfessionalRenderer {
            internal GlassMenuRenderer() { RoundedEdges = false; }
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) {
                MenuState state; bool native = menus.TryGetValue(e.ToolStrip,out state) && state.Native;
                float radius = native ? (state.Radius == 0 ? 0 : state.Radius <= 8 ? 4 : 8) : (state == null ? 12 : state.Radius)*e.ToolStrip.DeviceDpi/96F;
                var saved = e.Graphics.Save();
                try {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using(var path = GlassMaterial.Outline(new RectangleF(0,0,e.ToolStrip.Width,e.ToolStrip.Height),radius)) {
                        // Acrylic supplies the base; an opaque clear here would hide desktop blur.
                        if(!native) using(var fill = new SolidBrush(Surface)) e.Graphics.FillPath(fill,path);
                        using(var sheen = new LinearGradientBrush(new Point(0,0),new Point(0,Math.Max(1,e.ToolStrip.Height)),
                            Color.FromArgb(native?130:155,255,255,255),Color.FromArgb(native?35:60,255,255,255))) e.Graphics.FillPath(sheen,path);
                    }
                } finally { e.Graphics.Restore(saved); }
            }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) {
                MenuState state; bool native = menus.TryGetValue(e.ToolStrip,out state) && state.Native;
                float radius = native ? (state.Radius==0?0:state.Radius<=8?4:8) : (state==null?12:state.Radius)*e.ToolStrip.DeviceDpi/96F;
                var saved = e.Graphics.Save();
                try {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using(var path = GlassMaterial.Outline(new RectangleF(0.5F,0.5F,e.ToolStrip.Width-1,e.ToolStrip.Height-1),radius))
                    using(var stroke = new Pen(Color.FromArgb(200,255,255,255))) e.Graphics.DrawPath(stroke,path);
                } finally { e.Graphics.Restore(saved); }
            }
            protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) {
                if(!e.Item.Selected && !e.Item.Pressed) return;
                Rectangle bounds = new Rectangle(2,1,Math.Max(1,e.Item.Width-4),Math.Max(1,e.Item.Height-2));
                var saved = e.Graphics.Save();
                try {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using(var path = GlassMaterial.Outline(bounds,6F*e.ToolStrip.DeviceDpi/96F))
                    using(var brush = new SolidBrush(Color.FromArgb(e.Item.Enabled?60:20,Accent))) e.Graphics.FillPath(brush,path);
                } finally { e.Graphics.Restore(saved); }
            }
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {
                using(var brush = new SolidBrush(e.Item.Enabled ? Text : Muted))
                using(var format = new StringFormat(StringFormat.GenericDefault)) {
                    format.LineAlignment = StringAlignment.Center;
                    format.Alignment = (e.TextFormat & TextFormatFlags.Right) != 0 ? StringAlignment.Far : StringAlignment.Near;
                    format.HotkeyPrefix = HotkeyPrefix.Show; format.FormatFlags = StringFormatFlags.NoWrap;
                    var saved = e.Graphics.Save();
                    try { e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; e.Graphics.DrawString(e.Text,e.TextFont,brush,e.TextRectangle,format); }
                    finally { e.Graphics.Restore(saved); }
                }
            }
            protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) {
                Rectangle bounds = e.ImageRectangle;
                float cx = bounds.Left+bounds.Width/2F, cy = bounds.Top+bounds.Height/2F;
                var entry = e.Item as ToolStripMenuItem;
                var saved = e.Graphics.Save();
                try {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using(var pen = new Pen(e.Item.Enabled ? Accent : Muted,1.8F*e.ToolStrip.DeviceDpi/96F)) {
                        pen.StartCap = pen.EndCap = LineCap.Round;
                        if(entry != null && entry.CheckState == CheckState.Indeterminate) e.Graphics.DrawLine(pen,cx-4,cy,cx+4,cy);
                        else e.Graphics.DrawLines(pen,new[]{new PointF(cx-4,cy),new PointF(cx-1,cy+3),new PointF(cx+5,cy-4)});
                    }
                } finally { e.Graphics.Restore(saved); }
            }
            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor = e.Item.Enabled ? Text : Muted; base.OnRenderArrow(e); }
            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) {
                using(var line = new Pen(Color.FromArgb(45,Muted))) e.Graphics.DrawLine(line,10,e.Item.Height/2,Math.Max(10,e.Item.Width-10),e.Item.Height/2);
            }
        }
    }
}
