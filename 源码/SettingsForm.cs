using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace QingMonitor {
    // The dialog edits a copy. The caller decides when the accepted copy is saved.
    public sealed class SettingsForm : Form {
        private readonly Settings original;
        private readonly Snapshot snapshot;
        private readonly Color panelColor = GlassTheme.Surface;
        private readonly Color inputColor = GlassTheme.Input;
        private readonly Color textColor = GlassTheme.Text;
        private readonly Color hintColor = GlassTheme.Muted;
        private SliderValueControl opacity, scale, fontSize, frost, corner;
        private CheckBox glass;
        private SliderValueControl cpuYellow,cpuRed,gpuYellow,gpuRed,diskYellow,diskRed;
        private CheckBox temperatureColorsEnabled;
        private Button accent;
        private int accentArgb;
        private CheckBox topMost, locked, clickThrough, expanded, fpsEnabled;
        private SliderValueControl usageInterval, memoryInterval, temperatureInterval, diskInterval, diskTemperatureInterval, powerInterval, fpsInterval;
        private ComboBox gpu, disk, chartMetric, process;
        private CheckedListBox metrics, diskChoices;
        private CheckBox multiDisk;
        private RadioButton foreground, fixedProcess;
        private Button refreshProcesses;
        private Label processHint;
        private TextBox diagnostics;
        private bool loading;
        private readonly Timer previewTimer = new Timer { Interval = 100 };
        private SettingsPreviewControl preview;
        private Label previewStatus;
        private string validationMessage;
        private static readonly int[] IntervalSeconds = { 1, 2, 5, 10, 30 };

        public event Action<Settings> PreviewChanged;

        public Settings Result { get; private set; }

        public SettingsForm(Settings current, Snapshot snapshot) {
            if (current == null) throw new ArgumentNullException("current");
            original = current.Clone();
            Result = current.Clone();
            this.snapshot = snapshot ?? new Snapshot();
            Text = Program.ProductName + " · 设置";
            ClientSize = new Size(960, 720);
            MinimumSize = new Size(850, 620);
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = panelColor;
            ForeColor = textColor;
            ShowIcon = false;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            BuildInterface();
            LoadControls();
            GlassTheme.Apply(this);
            GlassTheme.StyleButton((Button)AcceptButton, true);
            UpdateAccentPreview();
            WirePreview(this);
            previewTimer.Tick += delegate { previewTimer.Stop(); PublishPreview(); };
            PublishPreview();
        }

        private void BuildInterface() {
            var tabs = new SettingsPages();
            tabs.Name = "settingsPages";
            tabs.AccessibleName = "设置页面";
            tabs.Dock = DockStyle.Fill;
            var appearancePage = AddPage(tabs, "外观");
            var samplingPage = AddPage(tabs, "采集间隔");
            var metricsPage = AddPage(tabs, "指标与设备");
            var fpsPage = AddPage(tabs, "FPS");
            var temperaturePage = AddPage(tabs, "温度配色");
            var diagnosticPage = AddPage(tabs, "采集状态");
            BuildAppearance(appearancePage);
            BuildSampling(samplingPage);
            BuildMetrics(metricsPage);
            BuildFps(fpsPage);
            BuildTemperatureColors(temperaturePage);
            BuildDiagnostics(diagnosticPage);

            var sidebar = new Panel { Dock = DockStyle.Left, Width = 156, Padding = new Padding(12, 20, 8, 12), BackColor = panelColor };
            sidebar.Name = "settingsSidebar";
            sidebar.AccessibleName = "设置导航";
            var brand = new Label { Text = "控制中心", AutoSize = false, Height = 40, Dock = DockStyle.Top, Font = new Font(Font.FontFamily, 14F, FontStyle.Bold), ForeColor = textColor };
            var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 10, 0, 0) };
            navigation.AccessibleName = "设置分类";
            string[] symbols = { "◐", "◷", "▦", "▻", "♨", "≡" };
            for (int i = 0; i < tabs.TabCount; i++) {
                int index = i;
                var item = new SettingsNavigationButton { Text = tabs.TabPages[i].Text, Symbol = symbols[i], Width = 134, Height = 42, Margin = new Padding(0, 0, 0, 6), Selected = i == 0 };
                item.Click += delegate { tabs.SelectedIndex = index; };
                navigation.Controls.Add(item);
            }
            tabs.SelectedIndexChanged += delegate {
                for (int i = 0; i < navigation.Controls.Count; i++) ((SettingsNavigationButton)navigation.Controls[i]).Selected = i == tabs.SelectedIndex;
            };
            sidebar.Controls.Add(navigation); sidebar.Controls.Add(brand);

            var previewHeader = new Panel { Dock = DockStyle.Top, Height = 156, Padding = new Padding(18, 12, 18, 6), BackColor = panelColor };
            previewHeader.AccessibleName = "外观预览区域";
            preview = new SettingsPreviewControl(snapshot) { Dock = DockStyle.Fill };
            previewStatus = new Label { Dock = DockStyle.Bottom, Height = 29, Text = "外观实时预览 · 取消恢复原样，确定保存", ForeColor = hintColor, Padding = new Padding(4, 6, 0, 0), AutoEllipsis = true };
            previewHeader.Controls.Add(preview); previewHeader.Controls.Add(previewStatus);

            var footer = new TableLayoutPanel();
            footer.Dock = DockStyle.Bottom;
            footer.Name = "settingsFooter";
            footer.AccessibleName = "配置操作";
            footer.Height = 62;
            footer.Padding = new Padding(12, 12, 12, 10);
            footer.ColumnCount = 2;
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var configButtons = new FlowLayoutPanel();
            configButtons.AutoSize = true;
            configButtons.WrapContents = false;
            var import = MakeButton("导入配置", delegate { ImportConfiguration(); });
            var export = MakeButton("导出配置", delegate { ExportConfiguration(); });
            configButtons.Controls.Add(import);
            configButtons.Controls.Add(export);
            footer.Controls.Add(configButtons, 0, 0);
            var confirmation = new FlowLayoutPanel();
            confirmation.AutoSize = true;
            confirmation.WrapContents = false;
            var ok = MakeButton("确定", delegate {
                Settings candidate;
                if (!TryGather(out candidate)) return;
                Result = candidate;
                DialogResult = DialogResult.OK;
                Close();
            });
            var cancel = MakeButton("取消", delegate { DialogResult = DialogResult.Cancel; Close(); });
            cancel.DialogResult = DialogResult.Cancel;
            confirmation.Controls.Add(ok);
            confirmation.Controls.Add(cancel);
            footer.Controls.Add(confirmation, 1, 0);
            AcceptButton = ok;
            CancelButton = cancel;
            Controls.Add(tabs);
            Controls.Add(sidebar);
            Controls.Add(previewHeader);
            Controls.Add(footer);
        }

        private TabPage AddPage(TabControl tabs, string title) {
            var page = new TabPage(title);
            page.BackColor = panelColor;
            page.ForeColor = textColor;
            page.Padding = new Padding(10, 0, 18, 10);
            page.AutoScroll = true;
            tabs.TabPages.Add(page);
            return page;
        }

        private TableLayoutPanel FormTable(Control parent) {
            var table = new GlassSettingsCard();
            table.Dock = DockStyle.Top;
            table.AutoSize = true;
            table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            table.ColumnCount = 2;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 158));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.Padding = new Padding(18, 14, 18, 12);
            parent.Controls.Add(table);
            return table;
        }

        private void AddField(TableLayoutPanel table, string title, Control control) {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var label = new Label();
            label.Text = title;
            label.AutoSize = true;
            label.Anchor = AnchorStyles.Left;
            label.Margin = new Padding(0, 8, 10, 12);
            control.Margin = new Padding(0, 4, 0, 12);
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            if (String.IsNullOrEmpty(control.AccessibleName)) control.AccessibleName = title;
            table.Controls.Add(label, 0, row);
            table.Controls.Add(control, 1, row);
        }

        private void AddWide(TableLayoutPanel table, Control control) {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Margin = new Padding(0, 4, 0, 10);
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            table.Controls.Add(control, 0, row);
            table.SetColumnSpan(control, 2);
        }

        private Label Hint(string message) {
            var label = new Label();
            label.Text = message;
            label.ForeColor = hintColor;
            label.AutoSize = true;
            label.MaximumSize = new Size(570, 0);
            return label;
        }

        private CheckBox MakeCheck(string title) {
            var check = new CheckBox();
            check.Text = title;
            check.AutoSize = true;
            check.Padding = new Padding(0, 3, 0, 3);
            return check;
        }

        private Button MakeButton(string title, EventHandler onClick) {
            var button = new Button();
            button.Text = title;
            button.AutoSize = true;
            button.MinimumSize = new Size(82, 30);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(77, 85, 98);
            button.BackColor = inputColor;
            button.ForeColor = textColor;
            button.Margin = new Padding(0, 0, 8, 0);
            button.Padding = new Padding(5, 1, 5, 1);
            button.Click += onClick;
            return button;
        }

        private SliderValueControl MakeNumeric(decimal minimum, decimal maximum, int decimals) {
            var control = new SliderValueControl();
            control.Minimum = minimum;
            control.Maximum = maximum;
            control.DecimalPlaces = decimals;
            control.Increment = decimals > 0 ? 0.5M : 1M;
            control.BackColor = panelColor;
            control.ForeColor = textColor;
            control.Width = 420;
            return control;
        }

        private ComboBox MakeCombo() {
            var control = new ComboBox();
            control.DropDownStyle = ComboBoxStyle.DropDownList;
            control.FlatStyle = FlatStyle.Flat;
            control.BackColor = inputColor;
            control.ForeColor = textColor;
            control.IntegralHeight = false;
            control.DropDownHeight = 220;
            return control;
        }

        private SliderValueControl MakeInterval() {
            var control = MakeNumeric(0, 4, 0);
            control.StepLabels = new[] { "1 秒", "2 秒", "5 秒", "10 秒", "30 秒" };
            return control;
        }

        private void BuildAppearance(TabPage page) {
            var table = FormTable(page);
            opacity = MakeNumeric(20, 100, 0);
            scale = MakeNumeric(70, 200, 0);
            fontSize = MakeNumeric(8, 20, 1);
            opacity.UnitText = "%"; scale.UnitText = "%"; fontSize.UnitText = " pt";
            AddField(table, "背景不透明度（%）", opacity);
            glass=MakeCheck("启用液态玻璃 / 毛玻璃材质");AddWide(table,glass);
            frost=MakeNumeric(0,100,0);corner=MakeNumeric(0,24,0);
            frost.UnitText="%";corner.UnitText=" px";
            AddField(table,"毛玻璃浓度（%）",frost);AddField(table,"圆角半径（px）",corner);
            AddWide(table,Hint("浓度越高，背景越雾化；设为 0 可关闭。文字保持清晰，系统不支持时使用半透明底色。"));
            AddWide(table,Hint("玻璃开启时使用系统圆角；关闭玻璃时按半径调节。"));
            AddField(table, "窗口缩放（%）", scale);
            AddField(table, "数值字体（pt）", fontSize);
            accent = MakeButton("选择强调色…", delegate {
                using (var dialog = new ColorDialog()) {
                    dialog.Color = Color.FromArgb(accentArgb);
                    dialog.FullOpen = true;
                    if (dialog.ShowDialog(this) == DialogResult.OK) {
                        accentArgb = dialog.Color.ToArgb();
                        UpdateAccentPreview();
                        QueuePreview();
                    }
                }
            });
            AddField(table, "强调色", accent);
            topMost = MakeCheck("窗口置顶");
            locked = MakeCheck("锁定位置（禁用拖动）");
            clickThrough = MakeCheck("鼠标穿透悬浮窗");
            expanded = MakeCheck("展开趋势图");
            AddWide(table, topMost);
            AddWide(table, locked);
            AddWide(table, clickThrough);
            AddWide(table, Hint("穿透选项仅保留在设置和托盘菜单中；Ctrl+Alt+F10 可解除穿透。"));
            AddWide(table, expanded);
            chartMetric = MakeCombo();
            foreach (var metric in Metric.Definitions.Values) chartMetric.Items.Add(new MetricItem(metric));
            AddField(table, "趋势图指标", chartMetric);
            AddWide(table, Hint("折叠时只显示数值，停止绘图与历史记录；展开后从新样本开始绘制。"));
            AddWide(table, Hint("勾选的功率、读写和 FPS 数值也会直接显示在右侧。左右边缘改宽度，上下边缘改高度，四角等比例缩放；空间不足时用右下角翻页。"));
            AddWide(table, MakeButton("按已选指标自动适配大小", delegate { Result.PanelWidth = 0; Result.PanelHeight = 0; QueuePreview(); }));
        }

        private void BuildSampling(TabPage page) {
            var table = FormTable(page);
            AddWide(table, Hint("拖动滑块选择 1、2、5、10 或 30 秒。向右采集更省，向左更新更快；确定后启用新的采集策略。"));
            usageInterval = MakeInterval(); memoryInterval = MakeInterval(); temperatureInterval = MakeInterval();
            diskInterval = MakeInterval(); diskTemperatureInterval = MakeInterval(); powerInterval = MakeInterval(); fpsInterval = MakeInterval();
            AddField(table, "CPU / GPU / 显存", usageInterval);
            AddField(table, "内存", memoryInterval);
            AddField(table, "CPU / GPU 温度", temperatureInterval);
            AddField(table, "硬盘负载 / 读写", diskInterval);
            AddField(table, "硬盘温度", diskTemperatureInterval);
            AddField(table, "硬件功率", powerInterval);
            AddField(table, "FPS 汇总 / 显示", fpsInterval);
            var actions = new FlowLayoutPanel();
            actions.AutoSize = true;
            actions.WrapContents = false;
            actions.Controls.Add(MakeButton("低负载预设", delegate { SetIntervals(5, 10, 10, 5, 30, 30, 2); }));
            actions.Controls.Add(MakeButton("恢复默认间隔", delegate { SetIntervals(2, 5, 5, 2, 10, 10, 2); }));
            AddWide(table, actions);
            AddWide(table, Hint("GPU 的负载、温度与功率可能由同一硬件更新一次读出，实际刷新按启用组的最短间隔进行。硬件不提供某项传感器时显示“—”；较长的间隔不会补出缺失数据。"));
            AddWide(table, Hint("FPS 开关开启期间持续监听帧事件，此处只调整汇总与显示间隔。折叠窗口不会关闭 FPS。"));
        }

        private void BuildMetrics(TabPage page) {
            var table = FormTable(page);
            gpu = MakeCombo(); disk = MakeCombo();
            AddField(table, "显卡", gpu);
            AddField(table, "单盘模式硬盘", disk);
            multiDisk=MakeCheck("同时显示多块硬盘");
            multiDisk.CheckedChanged+=delegate{disk.Enabled=!multiDisk.Checked;if(diskChoices!=null)diskChoices.Enabled=multiDisk.Checked;};
            AddWide(table,multiDisk);
            diskChoices=new CheckedListBox{BackColor=inputColor,ForeColor=textColor,BorderStyle=BorderStyle.FixedSingle,CheckOnClick=true,IntegralHeight=false,Height=94};
            AddField(table,"多盘模式显示设备",diskChoices);
            AddWide(table, Hint("勾选需要同时显示的硬盘；全选会显示全部物理盘。负载、温度和读写按盘分别列出，同一盘的多个盘符共用一组数据。关闭多盘模式可只显示上面选中的一块。"));
            metrics = new CheckedListBox();
            metrics.BackColor = inputColor;
            metrics.ForeColor = textColor;
            metrics.BorderStyle = BorderStyle.FixedSingle;
            metrics.CheckOnClick = true;
            metrics.IntegralHeight = false;
            metrics.Height = 186;
            foreach (var metric in Metric.Definitions.Values) metrics.Items.Add(new MetricItem(metric));
            AddField(table, "显示指标", metrics);
            AddWide(table, Hint("CPU 功率与 GPU 功率只表示对应硬件的传感器读数，均不是整机功率。未支持的温度或功率显示“—”，不会用 0 替代。"));
        }

        private void BuildFps(TabPage page) {
            var table = FormTable(page);
            fpsEnabled = MakeCheck("启用 FPS 采集（独立开关）");
            AddWide(table, fpsEnabled);
            AddWide(table, Hint("开启后持续监听，直到你手动关闭；与是否检测到游戏、窗口折叠状态无关。显示 FPS 指标与开启 FPS 采集是两个独立设置。"));
            foreground = new RadioButton(); foreground.Text = "跟随当前前台应用（默认）"; foreground.AutoSize = true;
            fixedProcess = new RadioButton(); fixedProcess.Text = "固定到指定进程"; fixedProcess.AutoSize = true;
            foreground.CheckedChanged += delegate { UpdateProcessControls(); };
            fixedProcess.CheckedChanged += delegate { UpdateProcessControls(); };
            // RadioButtons must have the same parent for mutual exclusion.
            var targets = new FlowLayoutPanel();
            targets.AutoSize = true; targets.FlowDirection = FlowDirection.TopDown; targets.WrapContents = false;
            targets.Controls.Add(foreground); targets.Controls.Add(fixedProcess);
            AddWide(table, targets);
            process = MakeCombo();
            AddField(table, "目标进程", process);
            refreshProcesses = MakeButton("刷新进程列表", delegate { RefreshProcessList(); });
            AddWide(table, refreshProcesses);
            processHint = Hint("进程列表只在打开此设置页或手动刷新时读取，不会每秒扫描。固定进程退出后需重新选择。");
            AddWide(table, processHint);
            AddWide(table, Hint("目标没有可用帧事件时显示“— / 无帧数据”，不会改用显示器刷新率或悬浮窗绘制速度。部分游戏的独占全屏模式可能遮挡桌面悬浮窗。"));
            AddWide(table, Hint("FPS 事件采集可能需要管理员权限或性能日志权限。采集失败的原因会显示在“采集状态”页；此对话框不会更改系统权限。"));
        }

        private void BuildDiagnostics(TabPage page) {
            var table = FormTable(page);
            AddWide(table, Hint("以下为打开设置时的采集快照，不额外启动传感器扫描。温度不可用可能与硬件支持、驱动或权限有关，详细原因以采集结果为准。"));
            diagnostics = new TextBox();
            diagnostics.Multiline = true;
            diagnostics.ReadOnly = true;
            diagnostics.ScrollBars = ScrollBars.Vertical;
            diagnostics.WordWrap = true;
            diagnostics.BackColor = inputColor;
            diagnostics.ForeColor = textColor;
            diagnostics.BorderStyle = BorderStyle.FixedSingle;
            diagnostics.Height = 440;
            diagnostics.Font = new Font("Microsoft YaHei UI", 9F);
            AddWide(table, diagnostics);
        }

        private void BuildTemperatureColors(TabPage page){
            var table=FormTable(page);
            temperatureColorsEnabled=MakeCheck("启用温度数值分级颜色");AddWide(table,temperatureColorsEnabled);
            AddWide(table,Hint("仅 CPU、GPU 和硬盘温度变色。低于黄色起点为绿色，达到黄色起点为黄色，达到红色起点为红色；无数据为灰色。"));
            AddTemperaturePair(table,"CPU 温度（°C）",out cpuYellow,out cpuRed);
            AddTemperaturePair(table,"GPU 核心温度（°C）",out gpuYellow,out gpuRed);
            AddTemperaturePair(table,"硬盘温度（°C）",out diskYellow,out diskRed);
            AddWide(table,MakeButton("恢复默认温度门槛",delegate{SetTemperatureThresholds(70,80,70,80,50,60);}));
            AddWide(table,Hint("默认为提前提醒散热的门槛，可按实际设备调整。硬盘默认按 NVMe SSD 选择；更换设备时请核对适合的温度范围。"));
            AddWide(table,Hint("按显示的整数温度判断；升温立即升级颜色，降温低于对应起点 2°C 后降级，减少来回跳色。沿用当前温度采集间隔。"));
        }
        private void AddTemperaturePair(TableLayoutPanel table,string label,out SliderValueControl yellow,out SliderValueControl red){
            yellow=MakeNumeric(10,137,0);red=MakeNumeric(13,140,0);
            yellow.UnitText="°C";red.UnitText="°C";
            yellow.AccentColor=TemperatureColors.Yellow;red.AccentColor=TemperatureColors.Red;
            yellow.AccessibleName=label+"黄色起点";red.AccessibleName=label+"红色起点";
            var pair=new TableLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=2,RowCount=2,Margin=Padding.Empty};
            pair.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,88));
            pair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            pair.RowStyles.Add(new RowStyle(SizeType.AutoSize));pair.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pair.Controls.Add(new Label{Text="黄色起点",AutoSize=true,ForeColor=Color.FromArgb(145,99,14),Anchor=AnchorStyles.Left,Margin=new Padding(0,0,8,0)},0,0);
            pair.Controls.Add(new Label{Text="红色起点",AutoSize=true,ForeColor=Color.FromArgb(170,49,65),Anchor=AnchorStyles.Left,Margin=new Padding(0,0,8,0)},0,1);
            yellow.Anchor=red.Anchor=AnchorStyles.Left|AnchorStyles.Right;
            yellow.Margin=new Padding(0,0,0,4);red.Margin=Padding.Empty;
            pair.Controls.Add(yellow,1,0);pair.Controls.Add(red,1,1);
            AddField(table,label,pair);
        }
        private void SetTemperatureThresholds(int cy,int cr,int gy,int gr,int dy,int dr){cpuYellow.Value=cy;cpuRed.Value=cr;gpuYellow.Value=gy;gpuRed.Value=gr;diskYellow.Value=dy;diskRed.Value=dr;}

        private void LoadControls() {
            loading = true;
            Result.Normalize();
            opacity.Value = Result.OpacityPercent;glass.Checked=Result.GlassEnabled;frost.Value=Result.FrostPercent;corner.Value=Result.CornerRadius;
            scale.Value = Result.ScalePercent;
            fontSize.Value = (decimal)Result.FontSize;
            accentArgb = Result.AccentArgb;
            UpdateAccentPreview();
            topMost.Checked = Result.TopMost; locked.Checked = Result.Locked;
            clickThrough.Checked = Result.ClickThrough; expanded.Checked = Result.Expanded;
            fpsEnabled.Checked = Result.FpsEnabled;
            temperatureColorsEnabled.Checked=Result.TemperatureColorsEnabled;
            SetTemperatureThresholds(Result.CpuTemperatureYellow,Result.CpuTemperatureRed,Result.GpuTemperatureYellow,Result.GpuTemperatureRed,Result.DiskTemperatureYellow,Result.DiskTemperatureRed);
            SetIntervals(Result.UsageSeconds, Result.MemorySeconds, Result.TemperatureSeconds, Result.DiskSeconds, Result.DiskTemperatureSeconds, Result.PowerSeconds, Result.FpsSeconds);
            LoadDevices(gpu, snapshot.Gpus, Result.GpuId, "自动选择显卡");
            LoadDevices(disk, snapshot.Disks, Result.DiskId, "自动选择硬盘");
            multiDisk.Checked=Result.MultiDisk;
            diskChoices.Items.Clear();
            var diskItems=new List<DeviceChoice>(snapshot.Disks??new DeviceChoice[0]);
            foreach(string id in Result.DiskIds)if(!diskItems.Exists(d=>d.Id==id))diskItems.Add(new DeviceChoice(id,"已保存硬盘（当前未发现）: "+id));
            foreach(var item in diskItems)diskChoices.Items.Add(item,Result.DiskIds.Length==0||Array.IndexOf(Result.DiskIds,item.Id)>=0);
            disk.Enabled=!multiDisk.Checked;diskChoices.Enabled=multiDisk.Checked;
            chartMetric.Items.Clear();foreach(var metric in Metric.Definitions.Values)chartMetric.Items.Add(new MetricItem(metric));
            foreach(var item in diskItems)foreach(string id in DiskMetrics.Ids)chartMetric.Items.Add(new MetricItem(DiskMetrics.Definition(id,item)));
            for (int n = 0; n < metrics.Items.Count; n++) metrics.SetItemChecked(n, Result.Shows(((MetricItem)metrics.Items[n]).Id));
            for (int n = 0; n < chartMetric.Items.Count; n++) if (((MetricItem)chartMetric.Items[n]).Id == Result.ChartMetric) chartMetric.SelectedIndex = n;
            if(chartMetric.SelectedIndex<0){for(int n=0;n<chartMetric.Items.Count;n++)if(((MetricItem)chartMetric.Items[n]).Id==DiskMetrics.BaseId(Result.ChartMetric)){chartMetric.SelectedIndex=n;break;}if(chartMetric.SelectedIndex<0)chartMetric.SelectedIndex=0;}
            foreground.Checked = Result.FixedProcessId <= 0;
            fixedProcess.Checked = !foreground.Checked;
            RefreshProcessList();
            diagnostics.Text = DiagnosticText();
            loading = false;
            UpdateProcessControls();
        }

        private void LoadDevices(ComboBox combo, DeviceChoice[] choices, string selectedId, string automaticLabel) {
            combo.Items.Clear();
            combo.Items.Add(new DeviceChoice("", automaticLabel));
            if (choices != null) foreach (DeviceChoice item in choices) combo.Items.Add(new DeviceChoice(item.Id, item.Name));
            int selected = 0;
            for (int n = 1; n < combo.Items.Count; n++) if (((DeviceChoice)combo.Items[n]).Id == selectedId) selected = n;
            if (selected == 0 && !String.IsNullOrEmpty(selectedId)) {
                combo.Items.Add(new DeviceChoice(selectedId, "已保存设备（当前未发现）: " + selectedId));
                selected = combo.Items.Count - 1;
            }
            combo.SelectedIndex = selected;
        }

        private void UpdateAccentPreview() {
            Color color = Color.FromArgb(accentArgb);
            accent.BackColor = Color.FromArgb(color.R, color.G, color.B);
            accent.ForeColor = color.GetBrightness() > 0.55F ? Color.FromArgb(15, 19, 25) : Color.White;
            accent.Text = String.Format("选择强调色…  #{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
        }

        private void SetIntervals(int usage, int memory, int temperature, int diskLoad, int diskTemperature, int power, int fps) {
            SelectInterval(usageInterval, usage); SelectInterval(memoryInterval, memory); SelectInterval(temperatureInterval, temperature);
            SelectInterval(diskInterval, diskLoad); SelectInterval(diskTemperatureInterval, diskTemperature); SelectInterval(powerInterval, power); SelectInterval(fpsInterval, fps);
        }

        private static void SelectInterval(SliderValueControl slider, int value) {
            int index = Array.IndexOf(IntervalSeconds, value);
            slider.Value = index < 0 ? 0 : index;
        }

        private static int ReadInterval(SliderValueControl slider) { return IntervalSeconds[(int)slider.Value]; }

        private void UpdateProcessControls() {
            if (loading || process == null) return;
            process.Enabled = fixedProcess.Checked;
            refreshProcesses.Enabled = fixedProcess.Checked;
        }

        private void RefreshProcessList() {
            int selectedId = Result.FixedProcessId;
            string selectedName = Result.FixedProcessName;
            var previous = process.SelectedItem as ProcessItem;
            if (!loading && previous != null) { selectedId = previous.Id; selectedName = previous.Name; }
            var items = new List<ProcessItem>();
            int inaccessible = 0;
            Process[] running;
            try { running = Process.GetProcesses(); }
            catch (Exception error) {
                processHint.Text = "无法读取进程列表: " + error.Message;
                return;
            }
            foreach (Process p in running) {
                try { if (p.Id != 0) items.Add(new ProcessItem(p.Id, p.ProcessName, false)); }
                catch { inaccessible++; }
                finally { p.Dispose(); }
            }
            items.Sort(delegate(ProcessItem a, ProcessItem b) {
                int order = StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name);
                return order == 0 ? a.Id.CompareTo(b.Id) : order;
            });
            process.Items.Clear();
            int selected = -1;
            foreach (ProcessItem item in items) {
                int index = process.Items.Add(item);
                if (item.Id == selectedId && (String.IsNullOrEmpty(selectedName) || String.Equals(item.Name, selectedName, StringComparison.OrdinalIgnoreCase))) selected = index;
            }
            if (selectedId > 0 && selected < 0) {
                selected = process.Items.Add(new ProcessItem(selectedId, String.IsNullOrEmpty(selectedName) ? "已保存目标" : selectedName, true));
            }
            process.SelectedIndex = selected >= 0 ? selected : (process.Items.Count > 0 ? 0 : -1);
            processHint.Text = "列表于 " + DateTime.Now.ToString("HH:mm:ss") + " 刷新，共 " + items.Count + " 个进程。固定进程退出后需重新选择。";
            if (inaccessible > 0) processHint.Text += " 另有 " + inaccessible + " 个进程无法读取名称。";
        }

        private bool TryGather(out Settings candidate) {
            return GatherSettings(out candidate, true);
        }

        private bool InvalidDraft(string message, bool notify) {
            validationMessage = message;
            if (notify) MessageBox.Show(this, message, "检查设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        private bool GatherSettings(out Settings candidate, bool notify) {
            validationMessage = null;
            candidate = Result.Clone();
            var visible = new List<string>();
            foreach (MetricItem item in metrics.CheckedItems) visible.Add(item.Id);
            if (visible.Count == 0) {
                return InvalidDraft("请至少选择一个显示指标。", notify);
            }
            candidate.Visible = visible.ToArray();
            candidate.GlassEnabled=glass.Checked;candidate.FrostPercent=(int)frost.Value;candidate.CornerRadius=(int)corner.Value;
            candidate.OpacityPercent = (int)opacity.Value; candidate.ScalePercent = (int)scale.Value; candidate.FontSize = (float)fontSize.Value;
            candidate.AccentArgb = accentArgb; candidate.TopMost = topMost.Checked; candidate.Locked = locked.Checked;
            candidate.ClickThrough = clickThrough.Checked; candidate.Expanded = expanded.Checked; candidate.FpsEnabled = fpsEnabled.Checked;
            if(cpuRed.Value-cpuYellow.Value<3||gpuRed.Value-gpuYellow.Value<3||diskRed.Value-diskYellow.Value<3) return InvalidDraft("每项红色起点需比黄色起点至少高 3°C。", notify);
            candidate.TemperatureColorsEnabled=temperatureColorsEnabled.Checked;
            candidate.CpuTemperatureYellow=(int)cpuYellow.Value;candidate.CpuTemperatureRed=(int)cpuRed.Value;
            candidate.GpuTemperatureYellow=(int)gpuYellow.Value;candidate.GpuTemperatureRed=(int)gpuRed.Value;
            candidate.DiskTemperatureYellow=(int)diskYellow.Value;candidate.DiskTemperatureRed=(int)diskRed.Value;
            candidate.UsageSeconds = ReadInterval(usageInterval); candidate.MemorySeconds = ReadInterval(memoryInterval); candidate.TemperatureSeconds = ReadInterval(temperatureInterval);
            candidate.DiskSeconds = ReadInterval(diskInterval); candidate.DiskTemperatureSeconds = ReadInterval(diskTemperatureInterval); candidate.PowerSeconds = ReadInterval(powerInterval); candidate.FpsSeconds = ReadInterval(fpsInterval);
            candidate.GpuId = ((DeviceChoice)gpu.SelectedItem).Id;
            candidate.DiskId = ((DeviceChoice)disk.SelectedItem).Id;
            candidate.MultiDisk=multiDisk.Checked;
            if(candidate.MultiDisk&&diskChoices.Items.Count>0){
                var selected=new List<string>();foreach(DeviceChoice choice in diskChoices.CheckedItems)selected.Add(choice.Id);
                if(selected.Count==0&&visible.Exists(DiskMetrics.IsDisk)) return InvalidDraft("请选择至少一块硬盘，或关闭所有硬盘显示指标。", notify);
                candidate.DiskIds=selected.Count==(snapshot.Disks??new DeviceChoice[0]).Length&&selected.TrueForAll(id=>Array.Exists(snapshot.Disks??new DeviceChoice[0],d=>d.Id==id))?new string[0]:selected.ToArray();
            }
            var selectedMetric = chartMetric.SelectedItem as MetricItem;
            if (selectedMetric != null) candidate.ChartMetric = selectedMetric.Id;
            if (foreground.Checked) { candidate.FixedProcessId = 0; candidate.FixedProcessName = ""; }
            else {
                var target = process.SelectedItem as ProcessItem;
                if (target == null) {
                    return InvalidDraft("请选择固定目标进程，或使用“跟随当前前台应用”。", notify);
                }
                candidate.FixedProcessId = target.Id; candidate.FixedProcessName = target.Name;
            }
            candidate.Normalize();
            return true;
        }

        private string DiagnosticText() {
            var builder = new StringBuilder();
            builder.AppendLine("传感器: " + snapshot.SensorStatus);
            builder.AppendLine("FPS: " + snapshot.FpsStatus);
            builder.AppendLine("累计样本: " + snapshot.Samples);
            if (snapshot.FpsChildPid > 0) builder.AppendLine("FPS 采集进程 PID: " + snapshot.FpsChildPid);
            builder.AppendLine();
            foreach (var definition in DiskMetrics.Rows(Result,snapshot)) {
                Metric metric;
                if (snapshot.Metrics == null || !snapshot.Metrics.TryGetValue(definition.Id, out metric)) {
                    builder.AppendLine(definition.Label + ": — / 尚无采集样本");
                    continue;
                }
                builder.AppendLine(metric.Label + ": " + metric.Display);
                builder.AppendLine("  最近采样: " + (metric.Timestamp == DateTime.MinValue ? "尚未采样" : metric.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")));
                if (!String.IsNullOrEmpty(metric.Detail)) builder.AppendLine("  " + metric.Detail);
                builder.AppendLine();
            }
            return builder.ToString();
        }

        private void ImportConfiguration() {
            using (var dialog = new OpenFileDialog()) {
                dialog.Title = "导入悬浮窗配置"; dialog.Filter = "JSON 配置 (*.json)|*.json|所有文件 (*.*)|*.*";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try {
                    LoadImportedSettings(dialog.FileName);
                } catch (Exception error) {
                    MessageBox.Show(this, "无法导入此配置，请检查文件格式。\r\n" + error.Message, "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void LoadImportedSettings(string path) {
            // Parse before touching controls: malformed files must leave even an unfinished draft intact.
            Settings imported = Settings.Read(path);
            Settings previousResult = Result.Clone(), previousDraft;
            bool validDraft = GatherSettings(out previousDraft, false);
            try {
                Result = imported; LoadControls(); PublishPreview();
            } catch {
                Result = validDraft ? previousDraft : previousResult;
                LoadControls(); PublishPreview(); Result = previousResult;
                throw;
            }
        }

        private void ExportConfiguration() {
            Settings candidate;
            if (!TryGather(out candidate)) return;
            using (var dialog = new SaveFileDialog()) {
                dialog.Title = "导出悬浮窗配置"; dialog.Filter = "JSON 配置 (*.json)|*.json";
                dialog.DefaultExt = "json"; dialog.AddExtension = true; dialog.FileName = Program.ProductName + "配置.json";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { candidate.Save(dialog.FileName); }
                catch (Exception error) { MessageBox.Show(this, "无法写入配置文件。\r\n" + error.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e) {
            previewTimer.Stop();
            if (DialogResult != DialogResult.OK) Result = original.Clone();
            base.OnFormClosing(e);
        }

        private void WirePreview(Control parent) {
            foreach (Control child in parent.Controls) {
                var slider = child as SliderValueControl;
                var check = child as CheckBox;
                var radio = child as RadioButton;
                var combo = child as ComboBox;
                var list = child as CheckedListBox;
                if (slider != null) slider.ValueChanged += delegate { QueuePreview(); };
                if (check != null) check.CheckedChanged += delegate { QueuePreview(); };
                if (radio != null) radio.CheckedChanged += delegate { QueuePreview(); };
                if (combo != null) combo.SelectedIndexChanged += delegate { QueuePreview(); };
                if (list != null) list.ItemCheck += delegate { QueuePreview(); };
                WirePreview(child);
            }
        }

        private void QueuePreview() {
            if (loading || IsDisposed || Disposing) return;
            previewTimer.Stop(); previewTimer.Start();
        }

        private void PublishPreview() {
            if (loading || IsDisposed || Disposing) return;
            Settings candidate;
            if (!GatherSettings(out candidate, false)) {
                previewStatus.Text = validationMessage;
                previewStatus.ForeColor = Color.FromArgb(167, 76, 38);
                return;
            }
            preview.UpdateSettings(candidate);
            previewStatus.ForeColor = hintColor;
            previewStatus.Text = "外观实时预览 · 采集与交互选项确定后启用，取消恢复原样";
            var handler = PreviewChanged;
            if (handler != null) handler(candidate.Clone());
        }

        protected override void OnShown(EventArgs e) {
            base.OnShown(e);
            var area = Screen.FromControl(this).WorkingArea;
            if (Height > area.Height || Width > area.Width) {
                MinimumSize = new Size(Math.Min(MinimumSize.Width, area.Width), Math.Min(MinimumSize.Height, area.Height));
                Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
                Location = new Point(Math.Max(area.Left, Math.Min(Left, area.Right - Width)), Math.Max(area.Top, Math.Min(Top, area.Bottom - Height)));
            }
            // Standard text/list controls use an opaque content material. Native acrylic on
            // their parent can turn GDI text white and retain stale pixels on Windows.
            SettingsWindowChrome.Apply(Handle);
        }

        protected override void OnPaintBackground(PaintEventArgs e) {
            base.OnPaintBackground(e);
        }

        protected override void Dispose(bool disposing) {
            if (disposing) previewTimer.Dispose();
            base.Dispose(disposing);
        }

        private sealed class MetricItem {
            public readonly string Id, Label;
            public MetricItem(Metric metric) {
                Id = metric.Id; Label = metric.Label;
                if (Id == "cpuPower" || Id == "gpuPower") Label += "（非整机功率）";
            }
            public override string ToString() { return Label; }
        }

        private sealed class ProcessItem {
            public readonly int Id;
            public readonly string Name;
            private readonly bool missing;
            public ProcessItem(int id, string name, bool missing) { Id = id; Name = name; this.missing = missing; }
            public override string ToString() { return Name + "  (PID " + Id + ")" + (missing ? " · 当前未运行" : ""); }
        }
    }
}
