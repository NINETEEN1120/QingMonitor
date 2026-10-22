using System;
using System.Drawing;
using System.Reflection;
using System.IO;
using System.Windows.Forms;
using QingMonitor;

internal static class GlassControlsChecks {
    private static int failures;
    private static void Check(string name, bool pass) {
        Console.WriteLine(name + "=" + pass); if(!pass) failures++;
    }
    [STAThread] private static int Main(string[] args) {
        Application.EnableVisualStyles();
        using(var slider = new SliderValueControl()) {
            slider.Minimum = 0; slider.Maximum = 4;
            var property = typeof(SliderValueControl).GetProperty("StepLabels");
            Check("Interval.LabelApi", property != null);
            if(property != null) {
                string[] labels = { "1 秒", "2 秒", "5 秒", "10 秒", "30 秒" };
                property.SetValue(slider, labels, null);
                slider.Value = 2;
                Check("Interval.AccessibleLabel", slider.AccessibilityObject.Value == "5 秒");
                labels[2] = "changed";
                Check("Interval.DefensiveSet", slider.AccessibilityObject.Value == "5 秒");
                var returned = (string[])property.GetValue(slider, null); returned[2] = "changed";
                Check("Interval.DefensiveGet", slider.AccessibilityObject.Value == "5 秒");
                slider.AccessibilityObject.Value = "10 秒";
                Check("Interval.AccessibleSet", slider.Value == 3);
                var trackProperty = typeof(SliderValueControl).GetProperty("TrackBounds", BindingFlags.Instance|BindingFlags.NonPublic);
                RectangleF first = (RectangleF)trackProperty.GetValue(slider, null);
                labels = new[] { "1 秒", "2 秒", "a much longer middle step label", "10 秒", "30 秒" };
                property.SetValue(slider, labels, null);
                RectangleF longest = (RectangleF)trackProperty.GetValue(slider, null);
                Check("Interval.ReserveAllLabelWidths", longest.Width < first.Width);
                property.SetValue(slider, null, null);
                slider.Minimum = 8; slider.Maximum = 20; slider.Increment = 0.5M; slider.DecimalPlaces = 1;
                slider.Value = 11.24M;
                Check("Numeric.PreserveHalfPointSnapping", slider.Value == 11M);
                slider.Value = 11.5M; slider.UnitText = " pt";
                Check("Numeric.PreserveAccessibleValue", slider.AccessibilityObject.Value == "11.5 pt");
            }
            Check("Slider.LightReadableDefault", slider.BackColor.GetBrightness() > 0.7F && slider.ForeColor.GetBrightness() < 0.4F);
        }
        Type theme = typeof(SliderValueControl).Assembly.GetType("QingMonitor.GlassTheme");
        Check("Theme.Api", theme != null);
        if(theme != null) using(var root = new Panel()) using(var button = new Button()) using(var warm = new Label()) using(var card = new Panel()) using(var navigation = new Button()) using(var transparentSlider = new SliderValueControl()) {
            root.BackColor=Color.FromArgb(29,32,38);
            button.DialogResult=DialogResult.Cancel; button.Text="Cancel";
            warm.ForeColor=Color.FromArgb(224,126,25);
            card.Tag="glass-card";card.BackColor=Color.White;
            card.Controls.Add(transparentSlider);
            navigation.Tag="glass-navigation";navigation.BackColor=Color.FromArgb(35,112,218);
            root.Controls.Add(button);root.Controls.Add(warm);root.Controls.Add(card);
            root.Controls.Add(navigation);
            theme.GetMethod("Apply", BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{root});
            Check("Theme.LightSurface",root.BackColor==Color.FromArgb(218,224,235));
            Check("Theme.PreserveTemperatureColor",warm.ForeColor==Color.FromArgb(224,126,25));
            Check("Theme.PreserveCard",card.BackColor==Color.White);
            Check("Theme.PreserveDialogResult",button.DialogResult==DialogResult.Cancel);
            Check("Theme.RoundedButton",button.Region!=null);
            button.Size=new Size(130,45);
            Check("Theme.ButtonRegionFollowsResize",button.Region.IsVisible(120,20));
            Check("Theme.PreserveNavigationDrawing",navigation.Region==null&&navigation.BackColor==Color.FromArgb(35,112,218));
            Check("Theme.TransparentSliderInCard",transparentSlider.BackColor==Color.Transparent);
            var style=theme.GetMethod("StyleButton", BindingFlags.Static|BindingFlags.NonPublic);
            style.Invoke(null,new object[]{button,true});style.Invoke(null,new object[]{button,false});
            Check("Theme.ButtonCanDeselect",button.BackColor==Color.FromArgb(241,245,251));
        }
        if(theme != null) using(var menu = new ContextMenuStrip()) {
            var enabled=new ToolStripMenuItem("已启用的菜单项 (&E)");enabled.Checked=true;
            var submenu=new ToolStripMenuItem("更多设置");submenu.DropDownItems.Add("子菜单");
            menu.Items.Add(enabled);menu.Items.Add(submenu);menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("不可用"){Enabled=false});
            Func<Settings> settings=delegate{return new Settings{GlassEnabled=false};};
            var attach=theme.GetMethod("AttachMenu",BindingFlags.Static|BindingFlags.NonPublic);
            attach.Invoke(null,new object[]{menu,settings});
            menu.PerformLayout();
            Check("Menu.StandardAccessibleItems",menu.Items[0] is ToolStripMenuItem&&enabled.Checked);
            Check("Menu.SameRendererForSubmenus",menu.Renderer.GetType()==submenu.DropDown.Renderer.GetType());
            Check("Menu.CheckedStateHasColumn",!menu.ShowImageMargin&&menu.ShowCheckMargin);
            enabled.Select();
            Check("Menu.NativeSelectionPreserved",enabled.Selected);
            if(args.Length>0) {
                Directory.CreateDirectory(args[0]);
                using(var bitmap=new Bitmap(menu.Width,menu.Height)) {
                    menu.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));
                    bitmap.Save(Path.Combine(args[0],"glass-menu-fallback.png"));
                    Check("Menu.FallbackLightSurface",bitmap.GetPixel(10,10).GetBrightness()>0.65F);
                }
            }
        }
        Console.WriteLine("Failures="+failures);
        return failures==0?0:1;
    }
}
