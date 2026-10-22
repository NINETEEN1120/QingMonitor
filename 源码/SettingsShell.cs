using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QingMonitor {
    internal static class SettingsShapes {
        internal static GraphicsPath Round(RectangleF bounds, float radius) {
            var path = new GraphicsPath();
            float d = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right-d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right-d, bounds.Bottom-d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom-d, d, d, 90, 90); path.CloseFigure();
            return path;
        }
    }

    internal sealed class SettingsPages : TabControl {
        public SettingsPages() { Appearance=TabAppearance.FlatButtons; ItemSize=new Size(1,1); SizeMode=TabSizeMode.Fixed; Padding=Point.Empty; }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x1328&&!DesignMode) { // TCM_ADJUSTRECT: navigation is provided by the accessible sidebar.
                // Preserve the caller's coordinate origin. WinForms can pass Bounds in parent coordinates.
                m.Result=IntPtr.Zero; return;
            }
            base.WndProc(ref m);
        }
    }

    internal static class SettingsWindowChrome {
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr handle,int attribute,ref int value,int size);
        internal static void Apply(IntPtr handle) {
            try {
                int caption=ColorTranslator.ToWin32(GlassTheme.Surface),text=ColorTranslator.ToWin32(GlassTheme.Text),light=0;
                DwmSetWindowAttribute(handle,20,ref light,4);
                DwmSetWindowAttribute(handle,35,ref caption,4);DwmSetWindowAttribute(handle,36,ref text,4);
                GlassMaterial.RoundNative(handle,18);
            } catch(DllNotFoundException){} catch(EntryPointNotFoundException){}
        }
    }

    internal sealed class SettingsNavigationButton : Button {
        private bool selected,hover;
        public string Symbol { get; set; }
        public bool Selected { get{return selected;} set{selected=value;Invalidate();} }
        public SettingsNavigationButton() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Tag="glass-navigation";TextAlign=ContentAlignment.MiddleLeft;
        }
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.Clear(Parent==null?GlassTheme.Surface:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
            float factor=DeviceDpi/96f;
            using(var path=SettingsShapes.Round(new RectangleF(1,1,Width-3,Height-3),10*factor)) {
                if(selected||hover)using(var brush=new SolidBrush(selected?Color.FromArgb(221,235,252):Color.FromArgb(237,243,252)))g.FillPath(brush,path);
                if(selected)using(var pen=new Pen(Color.FromArgb(190,214,245)))g.DrawPath(pen,path);
                if(Focused)using(var pen=new Pen(GlassTheme.Accent)){pen.DashStyle=DashStyle.Dot;g.DrawPath(pen,path);}
            }
            using(var brush=new SolidBrush(selected?Color.FromArgb(28,100,194):GlassTheme.Muted))
            using(var symbolFont=new Font("Segoe UI Symbol",13F))g.DrawString(Symbol??"",symbolFont,brush,11*factor,(Height-symbolFont.GetHeight(g))/2);
            using(var font=new Font(Font,selected?FontStyle.Bold:FontStyle.Regular))
            using(var brush=new SolidBrush(GlassTheme.Text))g.DrawString(Text,font,brush,38*factor,(Height-font.GetHeight(g))/2);
        }
    }

    internal sealed class GlassSettingsCard : TableLayoutPanel {
        public GlassSettingsCard() {
            DoubleBuffered=true;Tag="glass-card";BackColor=GlassTheme.Surface;
        }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=SettingsShapes.Round(new RectangleF(0.5F,0.5F,Width-2,Height-2),16*DeviceDpi/96f)) {
                using(var fill=new LinearGradientBrush(ClientRectangle,Color.FromArgb(248,250,254),Color.FromArgb(237,242,249),90F))e.Graphics.FillPath(fill,path);
                using(var pen=new Pen(Color.FromArgb(255,255,255)))e.Graphics.DrawPath(pen,path);
            }
            base.OnPaint(e);
        }
    }

    // A lightweight layout preview uses the opening snapshot; it never starts sensors, hooks or a tray icon.
    internal sealed class SettingsPreviewControl : Control {
        private readonly Snapshot snapshot;
        private Settings settings=new Settings();
        public SettingsPreviewControl(Snapshot snapshot) {
            this.snapshot=snapshot;DoubleBuffered=true;ResizeRedraw=true;Tag="glass-preview";
            AccessibleName="悬浮窗实时外观预览";AccessibleDescription="使用打开设置时的采集数据。修改外观即时预览，采集策略确定后启用。";
        }
        public void UpdateSettings(Settings value){settings=value.Clone();Invalidate();}
        private sealed class Pair { public Metric Top,Bottom; }
        private List<Pair> Pairs() {
            var rows=DiskMetrics.Rows(settings,snapshot);var pairs=new List<Pair>();
            var top=Overlay.OrderStripCategory(rows,0);var bottom=Overlay.OrderStripCategory(rows,1);
            for(int i=0;i<Math.Max(top.Length,bottom.Length);i++)pairs.Add(new Pair{Top=i<top.Length?top[i]:null,Bottom=i<bottom.Length?bottom[i]:null});
            var extras=rows.Where(m=>m.Unit!="%"&&m.Unit!="°C").OrderBy(m=>m.Unit=="W"?0:m.Unit=="MB/s"?1:2).ToArray();
            for(int i=0;i<extras.Length;i+=2)pairs.Add(new Pair{Top=extras[i],Bottom=i+1<extras.Length?extras[i+1]:null});
            return pairs;
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            float dpi=DeviceDpi/96f;
            using(var title=new Font("Microsoft YaHei UI",10F,FontStyle.Bold))
            using(var text=new SolidBrush(GlassTheme.Text))g.DrawString("实时外观预览",title,text,4*dpi,0);
            using(var hint=new Font("Microsoft YaHei UI",8F))
            using(var text=new SolidBrush(GlassTheme.Muted))g.DrawString("打开设置时的数据 · 悬浮窗同步预览",hint,text,126*dpi,3*dpi);
            var stage=new RectangleF(1,29*dpi,Width-3,Height-30*dpi);if(stage.Height<10)return;
            using(var path=SettingsShapes.Round(stage,15*dpi))
            using(var fill=new LinearGradientBrush(stage,Color.FromArgb(180,206,244),Color.FromArgb(214,195,227),12F))g.FillPath(fill,path);
            var pairs=Pairs();if(pairs.Count==0)return;
            float layoutScale=settings.ScalePercent/100F;
            float naturalWidth=88+pairs.Count*108;
            float width=Math.Min(stage.Width-24*dpi,(settings.PanelWidth>0?settings.PanelWidth:naturalWidth)*layoutScale*dpi);
            float height=Math.Min(stage.Height-12*dpi,(settings.PanelHeight>0?settings.PanelHeight:48)*layoutScale*dpi);
            var dock=new RectangleF(stage.Left+12*dpi,stage.Top+(stage.Height-height)/2,width,height);
            float radius=(settings.GlassEnabled?(settings.CornerRadius==0?0:settings.CornerRadius<=8?7:14):settings.CornerRadius)*dpi;
            using(var path=SettingsShapes.Round(dock,radius)) {
                int alpha=settings.GlassEnabled?Math.Min(240,100+settings.FrostPercent+settings.OpacityPercent/2):(int)(settings.OpacityPercent*2.55);
                using(var fill=new SolidBrush(Color.FromArgb(alpha,233,239,249)))g.FillPath(fill,path);
                using(var pen=new Pen(Color.FromArgb(205,255,255,255)))g.DrawPath(pen,path);
            }
            var state=g.Save();g.SetClip(dock);
            float fit=Math.Min(1,width/(naturalWidth*dpi));
            float column=(width-58*dpi*fit)/pairs.Count;
            float fontPixels=Math.Max(7*dpi,13*settings.FontSize/11F*layoutScale*dpi*fit);
            using(var font=new Font("Segoe UI",fontPixels,FontStyle.Regular,GraphicsUnit.Pixel))
            using(var bold=new Font("Segoe UI",fontPixels,FontStyle.Bold,GraphicsUnit.Pixel)) {
                for(int i=0;i<pairs.Count;i++){
                    DrawMetric(g,pairs[i].Top,font,bold,dock.Left+8*dpi+i*column,dock.Top+height*0.08F,column,height*0.44F);
                    DrawMetric(g,pairs[i].Bottom,font,bold,dock.Left+8*dpi+i*column,dock.Top+height*0.53F,column,height*0.44F);
                }
                using(var brush=new SolidBrush(Color.FromArgb(settings.AccentArgb)))g.DrawString("☷",bold,brush,dock.Right-25*dpi*fit,dock.Top+height*0.25F);
            }
            g.Restore(state);
        }
        private void DrawMetric(Graphics g,Metric metric,Font font,Font bold,float x,float y,float width,float height) {
            if(metric==null)return;
            string id=DiskMetrics.BaseId(metric.Id),label=Overlay.ShortLabel(metric);
            string value=metric.Value.HasValue?metric.Value.Value.ToString(metric.Unit=="W"||metric.Unit=="MB/s"?"0.0":"0")+(metric.Unit=="°C"?"°":metric.Unit=="FPS"?"":metric.Unit):"—";
            float required=g.MeasureString(label,font).Width+g.MeasureString(value,bold).Width+8;
            if(width<=16)return;
            if(required>width){
                float fit=(width-10)/required;
                using(var smaller=new Font(font.FontFamily,font.Size*fit,FontStyle.Regular,GraphicsUnit.Pixel))
                using(var smallerBold=new Font(bold.FontFamily,bold.Size*fit,FontStyle.Bold,GraphicsUnit.Pixel))DrawMetric(g,metric,smaller,smallerBold,x,y,width,height);
                return;
            }
            float labelWidth=g.MeasureString(label,font).Width;
            using(var brush=new SolidBrush(GlassTheme.Muted))g.DrawString(label,font,brush,x,y);
            Color color=GlassTheme.Text;
            if(metric.Unit=="°C"&&settings.TemperatureColorsEnabled&&metric.Value.HasValue){
                int yellow=id=="cpuTemp"?settings.CpuTemperatureYellow:id=="gpuTemp"?settings.GpuTemperatureYellow:settings.DiskTemperatureYellow;
                int red=id=="cpuTemp"?settings.CpuTemperatureRed:id=="gpuTemp"?settings.GpuTemperatureRed:settings.DiskTemperatureRed;
                color=metric.Value.Value>=red?TemperatureColors.Red:metric.Value.Value>=yellow?TemperatureColors.Yellow:TemperatureColors.Green;
            }
            using(var brush=new SolidBrush(color))g.DrawString(value,bold,brush,new RectangleF(x+labelWidth+3,y,Math.Max(1,width-labelWidth-5),height),StringFormat.GenericTypographic);
        }
    }
}
