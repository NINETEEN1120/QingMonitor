using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;

namespace QingMonitor {
    public sealed class Overlay : Form {
        private Settings config;
        private Snapshot snapshot=new Snapshot();
        private Metric[] rows=new Metric[0];
        private readonly TemperatureColors temperatureColors=new TemperatureColors();
        private readonly Collectors collectors;
        private readonly NotifyIcon tray;
        private readonly ContextMenuStrip menu=new ContextMenuStrip();
        private readonly ToolTip tips=new ToolTip();
        private readonly Timer foreground=new Timer{Interval=1000};
        private readonly Queue<KeyValuePair<DateTime,double>> history=new Queue<KeyValuePair<DateTime,double>>();
        private Font body,heading,small;
        private Icon ownedIcon;
        private int rowHeight,padding,labelWidth;
        private int stripTop,stripPage,stripColumns;
        private Rectangle pageButton;
        private int controlsLeft;
        private int metricCellWidth;
        private int[] columnLeft=new int[4],columnWidth=new int[4];
        private sealed class StripPair {internal Metric Top,Bottom;internal bool Extra;internal int Width,TopIndex=-1,BottomIndex=-1;}
        private StripPair[] stripPairs=new StripPair[0];
        private readonly List<int> stripPageStarts=new List<int>();
        private int pairStart,pairEnd,minimumPanelWidth,minimumPanelHeight;
        private bool nativeGlassActive;
        private int materialSignature=int.MinValue;
        private Size roundedSize;private int roundedRadius=-1;
        private ToolStripMenuItem clickThroughMenu;
        private float scale;
        private float layoutFit=1f;
        private Rectangle collapseButton,fpsButton,settingsButton,hideButton;
        private DateTime lastChart;
        private int hoverRow=-1;
        private bool saving,closing,hiddenByUser,restoringWindow,settingsOpen;
        private sealed class SettingsPreview {
            internal Settings Original;
            internal Rectangle Bounds;
            internal int Page;
        }
        private SettingsPreview settingsPreview;
        private IntPtr lastForeground,hotKeyWindow;
        private IntPtr reorderHook,desktopWindow;
        private WinEventCallback reorderCallback;
        private bool orderCheckQueued;
        private int orderWatchGeneration;
        private uint windowOrderThread;
        private long windowRepairs,topMostRaises;
        private bool interactiveResize;
        private bool layingOut;
        private Rectangle resizeStart;
        private int resizeStartPercent;
        private bool resizeGeometrySeeded;
        private float resizeStartScale;
        private Size measuredSize;
        [StructLayout(LayoutKind.Sequential)]private struct NativeRect {public int Left,Top,Right,Bottom;}
        private readonly string diagnosticsPath;
        private readonly Stopwatch uptime=Stopwatch.StartNew();
        public long GraphSamples,GraphPaints,NumericPaints;
        [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr hwnd,out int pid);
        [DllImport("user32.dll")]static extern bool ReleaseCapture();
        [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr w,IntPtr l);
        [DllImport("user32.dll",EntryPoint="GetWindowLong")]static extern int GetWindowLong(IntPtr hwnd,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLong")]static extern int SetWindowLong(IntPtr hwnd,int index,int value);
        [DllImport("user32.dll")]static extern bool RegisterHotKey(IntPtr hwnd,int id,uint mods,uint key);
        [DllImport("user32.dll")]static extern bool UnregisterHotKey(IntPtr hwnd,int id);
        [DllImport("user32.dll")]static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr hwnd,int command);
        [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr hwnd,uint command);
        [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
        [DllImport("user32.dll")]static extern IntPtr GetDesktopWindow();
        private delegate void WinEventCallback(IntPtr hook,uint eventId,IntPtr hwnd,int objectId,int childId,uint thread,uint time);
        [DllImport("user32.dll")]static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,WinEventCallback callback,uint process,uint thread,uint flags);
        [DllImport("user32.dll")]static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
        [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out int value,int size);
        protected override bool ShowWithoutActivation {get{return true;}}
        protected override CreateParams CreateParams {get {var c=base.CreateParams;c.ExStyle|=0x08000000;if(config!=null&&config.GlassEnabled&&config.FrostPercent>0&&!interactiveResize)c.Style&=~0x40000;else c.Style|=0x40000;return c;}}
        public Overlay(Settings c,string diag) {
            config=c;diagnosticsPath=diag;Text=Program.ProductName;Name="QingMonitorOverlay";
            FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;
            SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);
            BackColor=Color.Black;ForeColor=Color.FromArgb(35,43,57);AutoScaleMode=AutoScaleMode.None;
            ownedIcon=MakeIcon();Icon=ownedIcon;
            tray=new NotifyIcon{Icon=ownedIcon,Text=Program.ProductName,Visible=true,ContextMenuStrip=menu};
            tray.DoubleClick+=delegate{Restore();};
            BuildMenu();ContextMenuStrip=menu;menu.Opening+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(settingsOpen)e.Cancel=true;};
            collectors=new Collectors(config,AppDomain.CurrentDomain.BaseDirectory);
            collectors.Updated+=delegate(Snapshot s){if(!closing&&IsHandleCreated)try{BeginInvoke((Action)(()=>Receive(s)));}catch(InvalidOperationException){};};
            rows=DiskMetrics.Rows(config,snapshot);Location=new Point(config.X,config.Y);ApplyAppearance();ClampToScreens();
            Shown+=delegate{ApplyWindowStyle();EnsureWindowOrder();collectors.Start();foreground.Start();LogWindow("startup");};
            foreground.Tick+=delegate{int pid=ResolveTarget();collectors.SetTarget(pid);CheckWindowVisibility();};
            MouseDown+=OnDown;MouseMove+=OnMove;
            Move+=delegate{if(!saving&&!restoringWindow&&WindowState==FormWindowState.Normal&&!IsIconic(Handle)){config.X=Left;config.Y=Top;}};
        }
        private int ResolveTarget(){var target=settingsPreview==null?config:settingsPreview.Original;if(target.FixedProcessId>0){try{var p=Process.GetProcessById(target.FixedProcessId);string n=target.FixedProcessName;if(n.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))n=n.Substring(0,n.Length-4);using(p){if(p.ProcessName.Equals(n,StringComparison.OrdinalIgnoreCase))return p.Id;}}catch{}return 0;}int pid;GetWindowThreadProcessId(GetForegroundWindow(),out pid);return pid==Process.GetCurrentProcess().Id?0:pid;}
        private void Receive(Snapshot s){var updatedRows=DiskMetrics.Rows(config,s);bool layoutChanged=!rows.Select(m=>m.Id+"|"+m.Label).SequenceEqual(updatedRows.Select(m=>m.Id+"|"+m.Label));
            bool paint=layoutChanged||s.FpsStatus!=snapshot.FpsStatus||updatedRows.Any(m=>!snapshot.Metrics.ContainsKey(m.Id)||m.Display!=snapshot.Metrics[m.Id].Display);
            paint=temperatureColors.Update(s,config)||paint;
            if(layoutChanged||config.ChartMetric=="fps"&&s.FpsTargetPid!=snapshot.FpsTargetPid)ResetChart();snapshot=s;rows=updatedRows;
            if(!layoutChanged)foreach(var pair in stripPairs){if(pair.TopIndex>=0)pair.Top=rows[pair.TopIndex];if(pair.BottomIndex>=0)pair.Bottom=rows[pair.BottomIndex];}
            if(layoutChanged){hoverRow=-1;ApplyAppearance();ClampToScreens();}
            Metric chartValue=rows.First(x=>x.Id==DiskMetrics.ChartId(config,rows));if(config.Expanded&&Visible&&chartValue.Timestamp>lastChart){lastChart=chartValue.Timestamp;if(chartValue.Value.HasValue){paint=true;history.Enqueue(new KeyValuePair<DateTime,double>(chartValue.Timestamp,chartValue.Value.Value));GraphSamples++;while(history.Count>90||history.Count>1&&(chartValue.Timestamp-history.Peek().Key).TotalSeconds>180)history.Dequeue();}}
            if(Visible&&paint)Invalidate();if(diagnosticsPath!=null)WriteDiagnostics();
        }
        private void ResetChart(){history.Clear();lastChart=DateTime.UtcNow;}
        private void BuildMenu(){menu.Items.Clear();
            menu.Items.Add("显示 / 恢复悬浮窗",null,delegate{Restore();});menu.Items.Add("隐藏到托盘",null,delegate{HideToTray();});
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("停靠任务栏左侧",null,delegate{DockToTaskbar();});
            menu.Items.Add("按已选指标自动适配大小",null,delegate{config.PanelWidth=0;config.PanelHeight=0;ApplyAppearance();ClampToScreens();SaveConfig();});
            menu.Items.Add("重置窗口位置",null,delegate{var area=Screen.PrimaryScreen.WorkingArea;config.X=area.Left+20;config.Y=area.Top+20;Restore();SaveConfig();});
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("展开 / 折叠图表",null,delegate{ToggleChart();});menu.Items.Add("开启 / 关闭 FPS",null,delegate{ToggleFps();});
            clickThroughMenu=new ToolStripMenuItem("鼠标穿透");clickThroughMenu.Checked=config.ClickThrough;clickThroughMenu.Click+=delegate{ToggleClickThrough();};menu.Items.Add(clickThroughMenu);
            menu.Items.Add(new ToolStripSeparator());menu.Items.Add("设置…",null,delegate{OpenSettings();});menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("以管理员重新运行…",null,delegate{RestartAdmin();});
            menu.Items.Add("CPU 温度支持说明",null,delegate{string p=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"驱动","说明.txt");if(File.Exists(p))Process.Start(new ProcessStartInfo(p){UseShellExecute=true});else Process.Start(new ProcessStartInfo("https://pawnio.eu/"){UseShellExecute=true});});
            menu.Items.Add("打开程序目录",null,delegate{Process.Start("explorer.exe",AppDomain.CurrentDomain.BaseDirectory);});
            menu.Items.Add("使用说明",null,delegate{string p=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"使用说明.txt");if(File.Exists(p))Process.Start(p);});
            menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出",null,delegate{Close();});
            GlassTheme.AttachMenu(menu,delegate{return config;});
        }
        public void ApplyConfiguration(Settings c){config=c;config.Normalize();rows=DiskMetrics.Rows(config,snapshot);hoverRow=-1;temperatureColors.Clear();temperatureColors.Update(snapshot,config);ResetChart();ApplyAppearance();Location=new Point(config.X,config.Y);ClampToScreens();ApplyWindowStyle();collectors.Configure(config);SaveConfig();}
        public void ToggleClickThrough(){config.ClickThrough=!config.ClickThrough;ApplyWindowStyle();SaveConfig();Invalidate();}
        public void DockToTaskbar(){var screen=Screen.FromRectangle(Bounds);config.ScalePercent=100;config.Expanded=false;ApplyAppearance();Location=new Point(screen.Bounds.Left+6,screen.Bounds.Bottom-Height);config.X=Left;config.Y=Top;Restore();SaveConfig();}
        public void ToggleChart(){config.Expanded=!config.Expanded;ResetChart();ApplyAppearance();ClampToScreens();SaveConfig();}
        public void ToggleFps(){if(settingsOpen)return;config.FpsEnabled=!config.FpsEnabled;collectors.Configure(config);if(config.FpsEnabled)collectors.RetryFps();SaveConfig();Invalidate();}
        public void HideToTray(){hiddenByUser=true;ResetChart();Hide();collectors.SetHidden(true);LogWindow("user-hide");if(diagnosticsPath!=null)WriteDiagnostics();}
        public void Restore(){hiddenByUser=false;RestoreWindow("user-restore");}
        private void RestoreWindow(string reason){
            if(closing||restoringWindow)return;restoringWindow=true;
            try{
                // Show() alone leaves an iconic window minimized. Restore native state without taking game focus.
                ShowWindow(Handle,4);WindowState=FormWindowState.Normal;Show();
                ApplyAppearance();Location=new Point(config.X,config.Y);ClampToScreens();config.X=Left;config.Y=Top;
                ApplyWindowStyle();EnsureWindowOrder();collectors.SetHidden(false);ResetChart();Invalidate();
                windowRepairs++;LogWindow(reason);if(diagnosticsPath!=null)WriteDiagnostics();
            }finally{restoringWindow=false;}
        }
        private void EnsureWindowOrder(){
            if(closing||hiddenByUser||!IsHandleCreated)return;
            bool wasTop=(GetWindowLong(Handle,-20)&8)!=0;
            SetWindowPos(Handle,new IntPtr(config.TopMost?-1:-2),0,0,0,0,0x13);
            if(config.TopMost){topMostRaises++;if(!wasTop)LogWindow("topmost-repaired");}
        }
        private void CheckWindowVisibility(){
            if(closing||hiddenByUser||restoringWindow||settingsOpen||menu.Visible||interactiveResize||!IsHandleCreated)return;
            try{
                IntPtr active=GetForegroundWindow();bool changed=active!=lastForeground;lastForeground=active;
                bool onScreen=Screen.AllScreens.Any(s=>s.Bounds.IntersectsWith(Bounds));
                if(!Visible||!IsWindowVisible(Handle)||IsIconic(Handle)||WindowState!=FormWindowState.Normal||!onScreen){RestoreWindow("automatic-window-recovery");return;}
                // Topmost is a group, not a promise to stay above the taskbar or another topmost window.
                bool covered=config.TopMost&&HasCoveringWindow();
                if(config.TopMost&&(changed||(GetWindowLong(Handle,-20)&8)==0||covered)){EnsureWindowOrder();if(covered)LogWindow(HasCoveringWindow()?"z-order-occlusion-blocked":"z-order-occlusion-repaired");}
            }catch(Exception e){LogWindow("recovery-error "+e.GetBaseException().Message);}
        }
        private bool HasCoveringWindow(){
            int visited=0;for(IntPtr window=GetWindow(Handle,3);window!=IntPtr.Zero&&visited++<256;){
                IntPtr previous=GetWindow(window,3);NativeRect r;int cloaked;
                if(IsWindowVisible(window)&&!IsIconic(window)&&GetWindow(window,4)!=Handle&&GetWindowRect(window,out r)
                    &&Bounds.IntersectsWith(Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom))
                    &&(DwmGetWindowAttribute(window,14,out cloaked,4)!=0||cloaked==0))return true;
                if(previous==window)return false;window=previous;
            }return false;
        }
        private void QueueWindowOrderCheck(){
            if(closing||config==null||!config.TopMost||!Visible||!IsHandleCreated||orderCheckQueued)return;
            orderCheckQueued=true;int generation=orderWatchGeneration;
            try{BeginInvoke((Action)(()=>{if(generation!=orderWatchGeneration)return;orderCheckQueued=false;if(!closing&&IsHandleCreated)CheckWindowVisibility();}));}
            catch(InvalidOperationException){orderCheckQueued=false;}
        }
        private void WatchWindowOrder(){
            desktopWindow=GetDesktopWindow();windowOrderThread=GetCurrentThreadId();if(reorderHook!=IntPtr.Zero)return;
            reorderCallback=delegate(IntPtr hook,uint eventId,IntPtr hwnd,int objectId,int childId,uint thread,uint time){
                // Only the desktop's top-level window list matters; ignore app control/accessibility events.
                if(thread!=windowOrderThread&&hwnd==desktopWindow&&objectId==-4&&childId==0)QueueWindowOrderCheck();
            };
            reorderHook=SetWinEventHook(0x8004,0x8004,IntPtr.Zero,reorderCallback,0,0,0);
        }
        private void StopWatchingWindowOrder(){orderWatchGeneration++;if(reorderHook!=IntPtr.Zero){UnhookWinEvent(reorderHook);reorderHook=IntPtr.Zero;}orderCheckQueued=false;}
        private void ReflowWindow(string reason){
            if(closing||interactiveResize)return;
            if(!hiddenByUser){RestoreWindow(reason);return;}
            restoringWindow=true;try{ApplyAppearance();Location=new Point(config.X,config.Y);ClampToScreens();config.X=Left;config.Y=Top;LogWindow(reason+"-hidden");}finally{restoringWindow=false;}
        }
        private void QueueReflow(string reason){if(!closing&&IsHandleCreated)BeginInvoke((Action)(()=>ReflowWindow(reason)));}
        private void LogWindow(string reason){try{
            string folder=diagnosticsPath==null?Settings.DataDirectory:Path.GetDirectoryName(diagnosticsPath);Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,"window-events.log");if(File.Exists(path)&&new FileInfo(path).Length>256*1024)File.WriteAllText(path,"Log size limit reached; retaining current events.\n");
            File.AppendAllText(path,DateTime.Now.ToString("O")+" "+reason+" visible="+Visible+" iconic="+(IsHandleCreated&&IsIconic(Handle))+" topmost="+(IsHandleCreated&&(GetWindowLong(Handle,-20)&8)!=0)+" userHidden="+hiddenByUser+" bounds="+Bounds+Environment.NewLine);
        }catch{}}
        public void OpenSettings(){
            if(settingsOpen||closing)return;
            Settings accepted=null;
            try{
                BeginSettingsPreview();
                using(var form=new SettingsForm(settingsPreview.Original,snapshot)){
                    form.TopMost=TopMost;form.PreviewChanged+=PreviewSettings;
                    try{if(form.ShowDialog(this)==DialogResult.OK)accepted=form.Result.Clone();}
                    finally{form.PreviewChanged-=PreviewSettings;}
                }
            }finally{EndSettingsPreview();}
            if(accepted!=null&&!closing)ApplyConfiguration(accepted);
            if(!closing&&!hiddenByUser)EnsureWindowOrder();
        }
        private void BeginSettingsPreview(){
            if(settingsPreview!=null)throw new InvalidOperationException("A settings preview is already active.");
            settingsPreview=new SettingsPreview{Original=config.Clone(),Bounds=Bounds,Page=stripPage};
            settingsOpen=true;config=config.Clone();ApplyWindowStyle();
        }
        private void PreviewSettings(Settings draft){
            if(settingsPreview==null||closing||draft==null)return;
            var next=draft.Clone();next.Normalize();
            // Input behavior stays committed while the modal dialog is open. In particular,
            // a live click-through change must never make the owner or dialog unreachable.
            next.ClickThrough=settingsPreview.Original.ClickThrough;next.Locked=settingsPreview.Original.Locked;next.TopMost=settingsPreview.Original.TopMost;
            next.X=config.X;next.Y=config.Y;
            var updatedRows=DiskMetrics.Rows(next,snapshot);
            bool chartChanged=next.Expanded!=config.Expanded||DiskMetrics.ChartId(next,updatedRows)!=DiskMetrics.ChartId(config,rows);
            config=next;rows=updatedRows;hoverRow=-1;
            temperatureColors.Clear();temperatureColors.Update(snapshot,config);
            if(chartChanged)ResetChart();
            ApplyAppearance();ClampToScreens();Invalidate();
        }
        private void EndSettingsPreview(){
            var preview=settingsPreview;if(preview==null)return;
            bool restoring=restoringWindow;restoringWindow=true;
            try{
                config=preview.Original;rows=DiskMetrics.Rows(config,snapshot);stripPage=preview.Page;hoverRow=-1;
                temperatureColors.Clear();temperatureColors.Update(snapshot,config);ResetChart();
                if(!IsDisposed)ApplyAppearance();
            }finally{
                try{if(!IsDisposed)Bounds=preview.Bounds;}
                finally{
                    settingsPreview=null;settingsOpen=false;restoringWindow=restoring;
                    if(!IsDisposed&&!Disposing){ApplyWindowStyle();Invalidate();if(!closing&&!hiddenByUser)EnsureWindowOrder();}
                }
            }
        }
        private void RestartAdmin(){try{SaveConfig();var start=new ProcessStartInfo(Application.ExecutablePath,"--wait-instance"){UseShellExecute=true,Verb="runas",WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory};Process.Start(start);Close();}catch(Exception e){MessageBox.Show("管理员运行未完成："+e.Message,Program.ProductName);}}
        private void SaveConfig(){if(settingsPreview!=null)return;string path=diagnosticsPath==null?Settings.ConfigPath:Path.Combine(Path.GetDirectoryName(diagnosticsPath),"test-settings.json");try{config.Save(path);}catch(Exception e){tray.ShowBalloonTip(5000,"配置保存失败",path+"\n"+e.GetBaseException().Message,ToolTipIcon.Warning);}}
        private void ApplyAppearance(){ApplyAppearance(true);}
        private void ApplyAppearance(bool resizeWindow){
            layoutFit=1f;
            ApplyStripAppearance(resizeWindow);
        }
        private void ApplyStripAppearance(bool resizeWindow){
            layingOut=true;try{
            var screen=Screen.FromRectangle(Bounds);bool bottomAnchored=Math.Abs(Bottom-screen.Bounds.Bottom)<=4;int bottom=Bottom;
            scale=config.ScalePercent/100f*layoutFit;
            int stripHeight=(int)Math.Round(48*scale);
            stripTop=config.Expanded?(int)Math.Round(190*scale):0;
            if(body!=null){body.Dispose();heading.Dispose();small.Dispose();}
            body=new Font("Segoe UI",13*config.FontSize/11f*scale,FontStyle.Bold,GraphicsUnit.Pixel);
            heading=new Font("Microsoft YaHei UI",12*scale,FontStyle.Bold,GraphicsUnit.Pixel);small=new Font("Microsoft YaHei UI",11*scale,FontStyle.Regular,GraphicsUnit.Pixel);
            padding=Math.Max(6,(int)(12*scale));rowHeight=Math.Max((int)Math.Round(21*scale),(int)Math.Ceiling(body.GetHeight()+2*scale));labelWidth=(int)(6*scale);stripHeight=rowHeight*2+(int)Math.Round(6*scale);
            metricCellWidth=(int)(90*scale);int gap=(int)Math.Round(18*scale),dataLeft=padding+(int)(34*scale);
            stripPairs=MakeStripPairs();minimumPanelWidth=dataLeft+stripPairs.Max(p=>p.Width)+(int)Math.Round(92*scale);minimumPanelHeight=stripHeight;
            int naturalWidth=dataLeft+stripPairs.Sum(p=>p.Width)+gap*(stripPairs.Length-1)+(int)Math.Round(92*scale);
            int width=Math.Max(minimumPanelWidth,config.PanelWidth>0?(int)Math.Round(config.PanelWidth*scale):naturalWidth);
            int availableWidth=screen.Bounds.Width-12;
            if(minimumPanelWidth>availableWidth&&layoutFit>0.05f){layoutFit*=0.98f*availableWidth/minimumPanelWidth;ApplyStripAppearance(resizeWindow);return;}
            width=Math.Min(width,availableWidth);
            stripHeight=Math.Max(minimumPanelHeight,config.PanelHeight>0?(int)Math.Round(config.PanelHeight*scale):minimumPanelHeight);
            stripHeight=Math.Min(stripHeight,Math.Max(minimumPanelHeight,screen.Bounds.Height-stripTop));rowHeight=(stripHeight-(int)Math.Round(6*scale))/2;
            measuredSize=new Size(width,stripTop+stripHeight);MinimumSize=new Size(1,1);
            if(resizeWindow){saving=true;Size=measuredSize;if(bottomAnchored)Top=bottom-Height;saving=false;}
            int x=width-(int)(72*scale),y=stripTop+(int)(3*scale)+Math.Max(0,(rowHeight-(int)(20*scale))/2);
            controlsLeft=x;settingsButton=new Rectangle(x+(int)(40*scale),y,(int)(26*scale),(int)(20*scale));
            fpsButton=new Rectangle(x,y,(int)(35*scale),(int)(20*scale));collapseButton=new Rectangle(x,y+rowHeight,(int)(26*scale),(int)(20*scale));
            pageButton=new Rectangle(x+(int)(28*scale),collapseButton.Y,(int)(38*scale),(int)(20*scale));hideButton=Rectangle.Empty;
            int available=x-(int)(8*scale)-dataLeft,used=0;stripPageStarts.Clear();stripPageStarts.Add(0);
            for(int i=0;i<stripPairs.Length;i++){int next=stripPairs[i].Width+(used>0?gap:0);if(used>0&&used+next>available){stripPageStarts.Add(i);used=0;next=stripPairs[i].Width;}used+=next;}
            stripPage=Math.Min(stripPage,StripPages()-1);pairStart=stripPageStarts[stripPage];pairEnd=stripPage+1<StripPages()?stripPageStarts[stripPage+1]:stripPairs.Length;
            stripColumns=pairEnd-pairStart;columnLeft=new int[stripColumns];columnWidth=new int[stripColumns];int data=dataLeft;
            for(int col=0;col<stripColumns;col++){columnLeft[col]=data;columnWidth[col]=stripPairs[pairStart+col].Width;data+=columnWidth[col]+gap;}
            UpdateRoundedRegion();if(IsHandleCreated)ApplyWindowStyle();TopMost=config.TopMost;Invalidate();
            }finally{layingOut=false;}
        }
        private static int Measure(string text,Font font){return TextRenderer.MeasureText(text,font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding).Width;}
        private static string ValueTemplate(string unit){return unit=="%"?"100 %":unit=="°C"?"999 °C":unit=="FPS"?"9999 FPS":"9999.9 "+unit;}
        private static string StripValue(Metric metric){return metric.Display.Replace(" %","%").Replace(" °C","°").Replace(" W","W").Replace(" MB/s","MB/s").Replace(" FPS","");}
        private static string StripTemplate(Metric metric){return metric.Unit=="%"?"100%":metric.Unit=="°C"?"199°":metric.Unit=="FPS"?"9999":metric.Unit=="W"?"1999.9W":"99999.9MB/s";}
        private StripPair[] MakeStripPairs(){
            var result=new List<StripPair>();var top=StripCategory(0);var bottom=StripCategory(1);
            for(int i=0;i<Math.Max(top.Length,bottom.Length);i++)result.Add(new StripPair{Top=i<top.Length?top[i]:null,Bottom=i<bottom.Length?bottom[i]:null});
            var extras=ExtraMetrics().OrderBy(m=>m.Unit=="W"?0:m.Unit=="MB/s"?1:2).ToArray();
            for(int i=0;i<extras.Length;i+=2)result.Add(new StripPair{Top=extras[i],Bottom=i+1<extras.Length?extras[i+1]:null,Extra=true});
            foreach(var pair in result){pair.TopIndex=pair.Top==null?-1:Array.IndexOf(rows,pair.Top);pair.BottomIndex=pair.Bottom==null?-1:Array.IndexOf(rows,pair.Bottom);int content=0;foreach(var metric in new[]{pair.Top,pair.Bottom})if(metric!=null)content=Math.Max(content,Measure(ShortLabel(metric),small)+labelWidth+Measure(StripTemplate(metric),body));pair.Width=content;}
            return result.ToArray();
        }
        private void ApplyWindowStyle(){
            bool glass=config.GlassEnabled&&config.FrostPercent>0;
            int style=GetWindowLong(Handle,-16),wanted=glass&&!interactiveResize?style&~0x40000:style|0x40000;
            if(style!=wanted){SetWindowLong(Handle,-16,wanted);SetWindowPos(Handle,IntPtr.Zero,0,0,0,0,0x37);materialSignature=int.MinValue;}
            int signature=(glass?1000000:0)+config.FrostPercent*1000+config.OpacityPercent;
            double targetOpacity=glass?0.99:config.OpacityPercent/100.0;
            bool opacityChanged=Math.Abs(Opacity-targetOpacity)>0.001;if(opacityChanged)Opacity=targetOpacity;
            int ex=GetWindowLong(Handle,-20);ex|=0x08000000|0x80000|0x80;if(config.ClickThrough&&settingsPreview==null)ex|=0x20;else ex&=~0x20;SetWindowLong(Handle,-20,ex);
            if(signature!=materialSignature||opacityChanged){nativeGlassActive=GlassMaterial.Apply(Handle,glass,config.FrostPercent,config.OpacityPercent);materialSignature=signature;}
            if(glass&&!nativeGlassActive)Opacity=config.OpacityPercent/100.0;
            UpdateRoundedRegion();
            TopMost=config.TopMost;if(clickThroughMenu!=null)clickThroughMenu.Checked=config.ClickThrough;
        }
        private void UpdateRoundedRegion(){
            if(nativeGlassActive&&IsHandleCreated){var previous=Region;if(previous!=null){Region=null;previous.Dispose();}roundedRadius=-1;if(GlassMaterial.RoundNative(Handle,config.CornerRadius))return;GlassMaterial.Apply(Handle,false,0,0);nativeGlassActive=false;Opacity=config.OpacityPercent/100.0;}
            int radius=(int)Math.Round(config.CornerRadius*scale);if(Size==roundedSize&&radius==roundedRadius)return;
            roundedSize=Size;roundedRadius=radius;var old=Region;
            using(var path=GlassMaterial.Outline(new RectangleF(0,0,Width,Height),radius))Region=new Region(path);
            if(old!=null)old.Dispose();
        }
        protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);if(config!=null){UpdateRoundedRegion();if(IsHandleCreated)ApplyWindowStyle();}}
        private void PaintGlass(Graphics g){
            float radius=nativeGlassActive?(config.CornerRadius==0?0:config.CornerRadius<=8?4:8):config.CornerRadius*scale;
            if(!nativeGlassActive)g.Clear(Color.FromArgb(218,224,235));g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=GlassMaterial.Outline(new RectangleF(0.5f,0.5f,Width-1,Height-1),Math.Max(0,radius-0.5f)))
            using(var sheen=new LinearGradientBrush(new PointF(0,0),new PointF(0,Height),Color.FromArgb(210,255,255,255),Color.FromArgb(72,255,255,255)))
            using(var pen=new Pen(sheen,1f))g.DrawPath(pen,path);
            if(!nativeGlassActive)using(var path=GlassMaterial.Outline(new RectangleF(1,1,Width-2,Height-2),Math.Max(0,radius-1)))
                using(var wash=new LinearGradientBrush(new Point(0,0),new Point(Width,Height),Color.FromArgb(30,215,225,255),Color.FromArgb(0,215,225,255)))g.FillPath(wash,path);
            g.SmoothingMode=SmoothingMode.None;
        }
        private void ClampToScreens(){var a=Screen.FromRectangle(Bounds).Bounds;Location=new Point(Math.Max(a.Left,Math.Min(Left,a.Right-Width)),Math.Max(a.Top,Math.Min(Top,a.Bottom-Math.Min(Height,a.Height))));config.X=Left;config.Y=Top;}
        protected override void OnPaint(PaintEventArgs e){
            if(closing||layingOut||body==null||small==null||heading==null)return;
            var g=e.Graphics;PaintGlass(g);NumericPaints++;
            int dataLeft=padding+(int)(34*scale),right=controlsLeft-(int)(8*scale),cellWidth=metricCellWidth;
            for(int row=0;row<2;row++){
                int y=stripTop+(int)(3*scale)+row*rowHeight;
                if(stripPairs.Skip(pairStart).Take(pairEnd-pairStart).Any(p=>!p.Extra&&(row==0?p.Top:p.Bottom)!=null))Draw(g,row==0?"占用":"温度",small,Color.FromArgb(83,95,115),new Rectangle(padding+(int)(1*scale),y,(int)(31*scale),rowHeight),false);
                for(int i=pairStart;i<pairEnd;i++){
                    int col=i-pairStart;var cell=new Rectangle(columnLeft[col],y,columnWidth[col],rowHeight);
                    Metric m=row==0?stripPairs[i].Top:stripPairs[i].Bottom;if(m==null)continue;string label=ShortLabel(m);
                    int labelPixels=Measure(label,small),valueLeft=cell.Left+labelPixels+labelWidth;
                    Draw(g,label,small,Color.FromArgb(83,95,115),new Rectangle(cell.Left,cell.Top,labelPixels,cell.Height),false);
                    Draw(g,StripValue(m),body,temperatureColors.ValueColor(m,config,ForeColor),new Rectangle(valueLeft,cell.Top,Math.Max(1,cell.Right-valueLeft),cell.Height),false);
                }
            }
            for(int col=1;col<stripColumns;col++)if(stripPairs[pairStart+col].Extra&&!stripPairs[pairStart+col-1].Extra)using(var split=new Pen(Color.FromArgb(35,72,86,108)))g.DrawLine(split,columnLeft[col]-9*scale,stripTop+10*scale,columnLeft[col]-9*scale,Height-10*scale);
            using(var line=new Pen(Color.FromArgb(45,72,86,108)))g.DrawLine(line,controlsLeft-10*scale,stripTop+10*scale,controlsLeft-10*scale,Height-10*scale);
            DrawSettingsGlyph(g,settingsButton);
            Draw(g,"FPS",small,config.FpsEnabled?Color.FromArgb(35,112,181):Color.FromArgb(83,95,115),fpsButton,false);
            Draw(g,config.Expanded?"▾":"▴",heading,ForeColor,collapseButton,false);
            Draw(g,(stripPage+1)+"/"+StripPages(),small,Color.FromArgb(83,95,115),pageButton,true);
            if(config.Expanded){
                GraphPaints++;DrawGraph(g,0);
            }
        }
        private Metric[] StripCategory(int row){
            return OrderStripCategory(rows,row);
        }
        internal static Metric[] OrderStripCategory(Metric[] source,int row){
            string[] order={"cpu","cpuTemp","gpu","gpuTemp","ram","vram","diskTemp","disk"};
            return source.Where(m=>m.Unit==(row==0?"%":"°C")).OrderBy(m=>Array.IndexOf(order,DiskMetrics.BaseId(m.Id)))
                .ThenBy(m=>{int at=m.Id.IndexOf('@');return at<0?-1:DiskMetrics.Index(m.Id.Substring(at+1));})
                .ToArray();
        }
        private void DrawSettingsGlyph(Graphics g,Rectangle r){
            float x=r.Left+r.Width/2f,y=r.Top+r.Height/2f;g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var p=new Pen(Color.FromArgb(68,81,101),1.2f*scale)){
                p.StartCap=LineCap.Round;p.EndCap=LineCap.Round;
                g.DrawLine(p,x-6*scale,y-3*scale,x+6*scale,y-3*scale);g.DrawLine(p,x-6*scale,y+3*scale,x+6*scale,y+3*scale);
                g.DrawEllipse(p,x-3*scale,y-5*scale,4*scale,4*scale);g.DrawEllipse(p,x+1*scale,y+1*scale,4*scale,4*scale);
            }g.SmoothingMode=SmoothingMode.None;
        }
        private Metric[] ExtraMetrics(){return rows.Where(m=>m.Unit!="%"&&m.Unit!="°C").ToArray();}
        private int ExtraColumns(){return Math.Max(1,(int)((Width-padding*2)/(190*scale)));}
        private int StripPages(){return Math.Max(1,stripPageStarts.Count);}
        internal static string ShortLabel(Metric m){
            string id=DiskMetrics.BaseId(m.Id),disk="盘";int at=m.Id.IndexOf('@');if(at>=0)disk+=DiskMetrics.Index(m.Id.Substring(at+1));
            switch(id){case "cpuTemp":return "CPU";case "gpuTemp":return "GPU";case "diskTemp":return "硬"+disk;case "disk":return "硬"+disk;case "diskRead":return "硬"+disk+"读";case "diskWrite":return "硬"+disk+"写";case "cpuPower":return "CPU功率";case "gpuPower":return "GPU功率";default:return m.Label;}
        }
        private int StripMetricIndex(Point p){
            int dataLeft=padding+(int)(34*scale);
            if(p.Y<stripTop+3*scale||p.X<dataLeft||p.X>=controlsLeft-8*scale)return -1;
            int col=Array.FindIndex(columnLeft,x=>p.X>=x&&p.X<x+columnWidth[Array.IndexOf(columnLeft,x)]),row=(p.Y-stripTop-(int)(3*scale))/Math.Max(1,rowHeight);
            if(col<0||col>=stripColumns||row<0||row>1)return -1;var pair=stripPairs[pairStart+col];Metric metric=row==0?pair.Top:pair.Bottom;return metric==null?-1:Array.IndexOf(rows,metric);
        }
        private void Draw(Graphics g,string text,Font f,Color color,Rectangle rect,bool right){
            if(rect.Width<=0||rect.Height<=0)return;
            g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using(var format=new StringFormat(StringFormat.GenericTypographic)){format.FormatFlags|=StringFormatFlags.NoWrap;format.Alignment=right?StringAlignment.Far:StringAlignment.Near;format.LineAlignment=StringAlignment.Center;format.Trimming=StringTrimming.EllipsisCharacter;
                using(var brush=new SolidBrush(color))g.DrawString(text,f,brush,rect,format);}
        }
        private void DrawGraph(Graphics g,int y){Metric m=rows.First(x=>x.Id==DiskMetrics.ChartId(config,rows));Draw(g,m.Label+" · 最近 3 分钟",small,Color.FromArgb(75,87,108),new Rectangle(padding,y,Width-padding*2,(int)(28*scale)),false);
            var rect=new Rectangle(padding,y+(int)(33*scale),Width-padding*2,(int)(86*scale));
            using(var p=new Pen(Color.FromArgb(30,75,87,108)))for(int i=0;i<3;i++)g.DrawLine(p,rect.Left,rect.Top+i*rect.Height/2,rect.Right,rect.Top+i*rect.Height/2);
            if(history.Count<2){Draw(g,"等待新的采集样本",small,Color.FromArgb(105,117,135),rect,false);return;}
            var items=history.ToArray();double max=m.Unit=="%"?100:Math.Max(1,items.Max(x=>x.Value)*1.15);double span=Math.Max(10,(items[items.Length-1].Key-items[0].Key).TotalSeconds);
            var points=items.Select(v=>new PointF((float)(rect.Right-(items[items.Length-1].Key-v.Key).TotalSeconds/span*rect.Width),(float)(rect.Bottom-Math.Min(1,v.Value/max)*rect.Height))).ToArray();
            g.SmoothingMode=SmoothingMode.AntiAlias;using(var p=new Pen(Color.FromArgb(config.AccentArgb),1.6f*scale))g.DrawLines(p,points);g.SmoothingMode=SmoothingMode.None;
        }
        private void OnDown(object sender,MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;
            if(settingsButton.Contains(e.Location)){OpenSettings();return;}if(fpsButton.Contains(e.Location)){ToggleFps();return;}
            if(collapseButton.Contains(e.Location)){ToggleChart();return;}
            if(pageButton.Contains(e.Location)){stripPage=(stripPage+1)%StripPages();hoverRow=-2;ApplyAppearance();return;}
            int row=StripMetricIndex(e.Location);if(row>=0){config.ChartMetric=rows[row].Id;if(config.Expanded){ResetChart();Invalidate();SaveConfig();}}
            if(!config.Locked){ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}
        }
        private void OnMove(object sender,MouseEventArgs e){int row=StripMetricIndex(e.Location);if(row==hoverRow)return;hoverRow=row;
            if(row<0)tips.SetToolTip(this,"右上：FPS / 设置；右下：图表 / 指标翻页\n左右边缘改宽度，上下边缘改高度，四角等比例缩放\n拖动数值区移动；Ctrl+Alt+F10 解除穿透\n鼠标穿透仅从托盘菜单或设置切换。");
            else{Metric m=rows[row];tips.SetToolTip(this,m.Label+"  "+m.Display+"\n"+m.Detail+"\n"+(m.Timestamp==default(DateTime)?"等待采集":"采集于 "+m.Timestamp.ToLocalTime().ToString("HH:mm:ss")));}}
        private Rectangle ResizeFromEdge(int edge,Rectangle proposed){
            if(config.Locked||config.ClickThrough)return Bounds;
            if(!interactiveResize){resizeStart=Bounds;resizeStartPercent=config.ScalePercent;interactiveResize=true;resizeGeometrySeeded=false;}
            if(!resizeGeometrySeeded){resizeStartScale=scale;config.PanelWidth=(int)Math.Round(resizeStart.Width/resizeStartScale);config.PanelHeight=(int)Math.Round((resizeStart.Height-stripTop)/resizeStartScale);resizeGeometrySeeded=true;}
            if(edge==1||edge==2||edge==3||edge==6){
                var screen=Screen.FromRectangle(resizeStart).Bounds;var sized=OverlayResize.AxisBounds(resizeStart,proposed,edge,new Size(minimumPanelWidth,stripTop+minimumPanelHeight),screen.Size);
                if(edge==1||edge==2)config.PanelWidth=(int)Math.Round(sized.Width/resizeStartScale);else config.PanelHeight=(int)Math.Round((sized.Height-stripTop)/resizeStartScale);
                ApplyAppearance(false);
            }else{var area=Screen.FromRectangle(resizeStart).Bounds;int screenLimit=(int)Math.Floor(resizeStartPercent*Math.Min((area.Width-12.0)/resizeStart.Width,area.Height/(double)resizeStart.Height));config.ScalePercent=Math.Min(OverlayResize.ScaleFor(resizeStart,proposed,edge,resizeStartPercent),Math.Max(70,screenLimit));ApplyAppearance(false);}
            return OverlayResize.Anchor(resizeStart,measuredSize,edge);
        }
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);materialSignature=int.MinValue;if(config!=null){ApplyWindowStyle();hotKeyWindow=Handle;RegisterHotKey(hotKeyWindow,1,0x4003,0x79);WatchWindowOrder();BeginInvoke((Action)(()=>{if(!closing&&IsHandleCreated){ApplyWindowStyle();if(!hiddenByUser)EnsureWindowOrder();}}));}}
        protected override void OnHandleDestroyed(EventArgs e){StopWatchingWindowOrder();if(hotKeyWindow!=IntPtr.Zero){UnregisterHotKey(hotKeyWindow,1);hotKeyWindow=IntPtr.Zero;}base.OnHandleDestroyed(e);}
        protected override void WndProc(ref Message m){
            if(m.Msg==0xA1&&m.WParam.ToInt32()>=10&&m.WParam.ToInt32()<=17&&!config.Locked&&!config.ClickThrough){interactiveResize=true;ApplyWindowStyle();}
            if(m.Msg==0x31E){materialSignature=int.MinValue;ApplyWindowStyle();Invalidate();}
            if(m.Msg==0x83){m.Result=IntPtr.Zero;return;}
            if(m.Msg==0x84){long packed=m.LParam.ToInt64();var point=PointToClient(new Point((short)(packed&0xffff),(short)((packed>>16)&0xffff)));int hit=OverlayResize.HitTest(point,ClientSize,Math.Max(6,(int)(6*scale)),!config.Locked&&!config.ClickThrough);if(hit!=0||ClientRectangle.Contains(point)){m.Result=new IntPtr(hit!=0?hit:1);return;}}
            if(m.Msg==0x231){resizeStart=Bounds;resizeStartPercent=config.ScalePercent;interactiveResize=true;resizeGeometrySeeded=false;}
            if(m.Msg==0x214){var r=(NativeRect)Marshal.PtrToStructure(m.LParam,typeof(NativeRect));var sized=ResizeFromEdge(m.WParam.ToInt32(),Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom));r.Left=sized.Left;r.Top=sized.Top;r.Right=sized.Right;r.Bottom=sized.Bottom;Marshal.StructureToPtr(r,m.LParam,false);m.Result=new IntPtr(1);return;}
            if(m.Msg==0x232){interactiveResize=false;config.X=Left;config.Y=Top;ApplyAppearance();ClampToScreens();SaveConfig();}
            if(diagnosticsPath!=null&&m.Msg==0x8001){switch(m.WParam.ToInt32()){case 1:ToggleChart();break;case 2:ToggleFps();break;case 3:HideToTray();break;case 4:Restore();break;case 5:Close();break;case 6:if(!settingsOpen){config.UsageSeconds=m.LParam.ToInt32();collectors.Configure(config);}break;case 7:OpenSettings();break;}m.Result=IntPtr.Zero;return;}
            if(m.Msg==0x312&&m.WParam.ToInt32()==1){config.ClickThrough=false;config.Locked=false;ApplyWindowStyle();Restore();SaveConfig();}
            if(m.Msg==0x2E0){base.WndProc(ref m);ApplyAppearance();ClampToScreens();return;}base.WndProc(ref m);
            if(m.Msg==0x7E||m.Msg==0x1A&&m.WParam.ToInt32()==0x2F)QueueReflow("display-change");
            if(m.Msg==0x218&&(m.WParam.ToInt32()==7||m.WParam.ToInt32()==18))QueueReflow("resume");
        }
        protected override void OnFormClosing(FormClosingEventArgs e){closing=true;if(settingsPreview!=null)EndSettingsPreview();StopWatchingWindowOrder();SaveConfig();foreground.Stop();UnregisterHotKey(Handle,1);collectors.Dispose();tray.Visible=false;tray.Dispose();tips.Dispose();menu.Dispose();foreground.Dispose();if(ownedIcon!=null)ownedIcon.Dispose();if(body!=null){body.Dispose();heading.Dispose();small.Dispose();}base.OnFormClosing(e);}
        public void WriteDiagnostics(){try{var s=collectors.Read();var path=diagnosticsPath;var lines=new List<string>{"time="+DateTime.Now.ToString("O"),"uptime="+uptime.Elapsed.TotalSeconds,"visible="+Visible,"expanded="+config.Expanded,"graphSamples="+GraphSamples,"graphPaints="+GraphPaints,"numericPaints="+NumericPaints,"samples="+s.Samples,"fpsPid="+s.FpsChildPid,"fpsStatus="+s.FpsStatus,"usageInterval="+config.UsageSeconds,"userHidden="+hiddenByUser,"windowState="+WindowState,"glassActive="+nativeGlassActive,"frostPercent="+config.FrostPercent,"cornerRadius="+config.CornerRadius,"nativeTopMost="+((GetWindowLong(Handle,-20)&8)!=0),"windowRepairs="+windowRepairs,"topMostRaises="+topMostRaises};foreach(var p in s.Metrics)lines.Add(p.Key+"="+p.Value.Display+" | "+p.Value.Timestamp.ToString("O")+" | "+p.Value.Detail);File.WriteAllLines(path,lines,System.Text.Encoding.UTF8);}catch{}}
        private static Icon MakeIcon(){using(var stream=typeof(Overlay).Assembly.GetManifestResourceStream("QingMonitor.Icon"))return new Icon(stream);}
    }
}
