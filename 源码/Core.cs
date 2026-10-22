using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Drawing;
using System.Linq;

namespace QingMonitor {
    [DataContract] public sealed class Settings {
        [DataMember] public int UsageSeconds = 2;
        [DataMember] public int MemorySeconds = 5;
        [DataMember] public int TemperatureSeconds = 5;
        [DataMember] public int DiskSeconds = 2;
        [DataMember] public int DiskTemperatureSeconds = 10;
        [DataMember] public int PowerSeconds = 10;
        [DataMember] public int FpsSeconds = 2;
        [DataMember] public int OpacityPercent = 88;
        [DataMember] public int ScalePercent = 100;
        [DataMember] public int PanelWidth;
        [DataMember] public int PanelHeight;
        [DataMember] public int LayoutVersion = 1;
        [DataMember] public bool GlassEnabled = true;
        [DataMember] public int FrostPercent = 40;
        [DataMember] public int CornerRadius = 14;
        [DataMember] public float FontSize = 11;
        [DataMember] public int AccentArgb = Color.FromArgb(84,190,255).ToArgb();
        [DataMember] public bool TemperatureColorsEnabled = true;
        [DataMember] public int CpuTemperatureYellow = 70, CpuTemperatureRed = 80;
        [DataMember] public int GpuTemperatureYellow = 70, GpuTemperatureRed = 80;
        [DataMember] public int DiskTemperatureYellow = 50, DiskTemperatureRed = 60;
        [DataMember] public bool TopMost = true;
        [DataMember] public bool Locked;
        [DataMember] public bool ClickThrough;
        [DataMember] public bool Expanded;
        [DataMember] public bool FpsEnabled;
        [DataMember] public int FixedProcessId;
        [DataMember] public string FixedProcessName = "";
        [DataMember] public string GpuId = "";
        [DataMember] public string DiskId = "";
        [DataMember] public bool MultiDisk = true;
        [DataMember] public string[] DiskIds = new string[0];
        [DataMember] public string ChartMetric = "cpu";
        [DataMember] public string[] Visible = {"cpu","gpu","vram","ram","cpuTemp","gpuTemp","disk","diskTemp","diskRead","diskWrite","fps"};
        [DataMember] public int X = 40;
        [DataMember] public int Y = 90;
        public static readonly string DataDirectory = ResolveDataDirectory();
        private static string ResolveDataDirectory() {
            string fallback=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QingMonitor");
            try {
                string marker=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"QingMonitor.data-path");
                if(File.Exists(marker)){string configured=File.ReadAllText(marker).Trim();if(Path.IsPathRooted(configured))return Path.GetFullPath(configured);}
            }catch{}
            return fallback;
        }
        public static string ConfigPath { get { return Path.Combine(DataDirectory,"settings.json"); } }
        public Settings Clone() { var c=(Settings)MemberwiseClone();c.Visible=Visible==null?null:(string[])Visible.Clone();c.DiskIds=DiskIds==null?new string[0]:(string[])DiskIds.Clone();return c; }
        private static DataContractJsonSerializer Serializer { get { return new DataContractJsonSerializer(typeof(Settings)); } }
        [OnDeserializing] private void Defaults(StreamingContext context) {
            UsageSeconds=2;MemorySeconds=5;TemperatureSeconds=5;DiskSeconds=2;DiskTemperatureSeconds=10;PowerSeconds=10;FpsSeconds=2;
            GlassEnabled=true;FrostPercent=40;CornerRadius=14;LayoutVersion=0;OpacityPercent=88;ScalePercent=100;FontSize=11;AccentArgb=Color.FromArgb(84,190,255).ToArgb();TopMost=true;
            TemperatureColorsEnabled=true;CpuTemperatureYellow=70;CpuTemperatureRed=80;GpuTemperatureYellow=70;GpuTemperatureRed=80;DiskTemperatureYellow=50;DiskTemperatureRed=60;
            GpuId="";DiskId="";MultiDisk=true;DiskIds=new string[0];FixedProcessName="";ChartMetric="cpu";X=40;Y=90;
            Visible=new[]{"cpu","gpu","vram","ram","cpuTemp","gpuTemp","disk","diskTemp","diskRead","diskWrite","fps"};
        }
        public static Settings Read(string path) { using(var s=File.OpenRead(path)) { var c=(Settings)Serializer.ReadObject(s); c.Normalize(); return c; } }
        public static Settings Load() { try { return Read(ConfigPath); } catch { return new Settings(); } }
        public void Save(string path) {
            Normalize(); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string tmp=path+".tmp-"+Guid.NewGuid().ToString("N");
            try {using(var s=File.Create(tmp)) Serializer.WriteObject(s,this);
                if(File.Exists(path)) File.Replace(tmp,path,null); else File.Move(tmp,path);
            }finally{try{if(File.Exists(tmp))File.Delete(tmp);}catch{}}
        }
        public void Normalize() {
            UsageSeconds=Interval(UsageSeconds,2); MemorySeconds=Interval(MemorySeconds,5); TemperatureSeconds=Interval(TemperatureSeconds,5);
            DiskSeconds=Interval(DiskSeconds,2); DiskTemperatureSeconds=Interval(DiskTemperatureSeconds,10); PowerSeconds=Interval(PowerSeconds,10); FpsSeconds=Interval(FpsSeconds,2);
            FrostPercent=Math.Max(0,Math.Min(100,FrostPercent));CornerRadius=Math.Max(0,Math.Min(24,CornerRadius));
            PanelWidth=Math.Max(0,Math.Min(8192,PanelWidth));PanelHeight=Math.Max(0,Math.Min(4320,PanelHeight));
            OpacityPercent=Math.Max(20,Math.Min(100,OpacityPercent)); ScalePercent=Math.Max(70,Math.Min(200,ScalePercent));if(float.IsNaN(FontSize)||float.IsInfinity(FontSize))FontSize=11; FontSize=Math.Max(8,Math.Min(20,FontSize));
            if(Visible==null || Visible.Length==0) Visible=new[]{"cpu","gpu","ram"};
            Visible=Array.FindAll(Visible,delegate(string id) {return id!=null&&Metric.Definitions.ContainsKey(id);}).Distinct().ToArray();
            if(Visible.Length==0)Visible=new[]{"cpu","gpu","ram"};
            if(Array.IndexOf(Visible,DiskMetrics.BaseId(ChartMetric??""))<0) ChartMetric=Visible[0];
            GpuId=GpuId??""; DiskId=DiskId??""; FixedProcessName=FixedProcessName??"";
            DiskIds=(DiskIds??new string[0]).Where(id=>DiskMetrics.Index(id)>=0).Select(id=>"physical:"+DiskMetrics.Index(id)).Distinct().OrderBy(id=>DiskMetrics.Index(id)).ToArray();
            NormalizeTemperatures(ref CpuTemperatureYellow,ref CpuTemperatureRed,70,80);
            NormalizeTemperatures(ref GpuTemperatureYellow,ref GpuTemperatureRed,70,80);
            NormalizeTemperatures(ref DiskTemperatureYellow,ref DiskTemperatureRed,50,60);
        }
        private static void NormalizeTemperatures(ref int yellow,ref int red,int defaultYellow,int defaultRed){if(yellow<10||yellow>137||red<13||red>140||red-yellow<3){yellow=defaultYellow;red=defaultRed;}}
        private static int Interval(int n,int fallback) {return n==1||n==2||n==5||n==10||n==30?n:fallback;}
        public bool Shows(string id) {return Array.IndexOf(Visible,id)>=0;}
    }
    public sealed class Metric {
        public string Id, Label, Unit;
        public double? Value;
        public string Detail;
        public DateTime Timestamp;
        public Metric(string id,string label,string unit) {Id=id;Label=label;Unit=unit;Detail="正在采集";}
        public Metric Copy() {return (Metric)MemberwiseClone();}
        public string Display {get {return !Value.HasValue?"—":Value.Value.ToString(Unit=="%"||Unit=="°C"||Unit=="FPS"?"0":"0.0")+" "+Unit;}}
        public static readonly Dictionary<string,Metric> Definitions=new Dictionary<string,Metric> {
            {"cpu",new Metric("cpu","CPU","%")},{"gpu",new Metric("gpu","GPU","%")},
            {"vram",new Metric("vram","显存","%")},{"ram",new Metric("ram","内存","%")},
            {"cpuTemp",new Metric("cpuTemp","CPU 温度","°C")},{"gpuTemp",new Metric("gpuTemp","GPU 温度","°C")},
            {"disk",new Metric("disk","硬盘负载","%")},{"diskTemp",new Metric("diskTemp","硬盘温度","°C")},
            {"diskRead",new Metric("diskRead","读取","MB/s")},{"diskWrite",new Metric("diskWrite","写入","MB/s")},
            {"fps",new Metric("fps","FPS","FPS")},{"cpuPower",new Metric("cpuPower","CPU 功率","W")},{"gpuPower",new Metric("gpuPower","GPU 功率","W")}
        };
    }
    public sealed class DeviceChoice {
        public string Id, Name;
        public DeviceChoice(string id,string name){Id=id;Name=name;}
        public override string ToString(){return Name;}
    }
    public sealed class Snapshot {
        public Dictionary<string,Metric> Metrics = new Dictionary<string,Metric>();
        public DeviceChoice[] Gpus = new DeviceChoice[0], Disks = new DeviceChoice[0];
        public string SensorStatus="正在发现硬件", FpsStatus="FPS 已关闭";
        public int FpsChildPid;
        public int FpsTargetPid;
        public long Samples;
    }
}
