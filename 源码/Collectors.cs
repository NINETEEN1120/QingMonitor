using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;

namespace QingMonitor {
    public sealed class Collectors : IDisposable {
        private readonly object gate=new object();
        private Settings settings;
        private readonly Snapshot state=new Snapshot();
        private readonly AutoResetEvent wake=new AutoResetEvent(false);
        private Thread thread;
        private volatile bool stopping, hidden;
        private int targetPid;
        private Computer computer;
        private IHardware cpu;
        private IHardware[] gpus=new IHardware[0], disks=new IHardware[0];
        private readonly Dictionary<string,double> next=new Dictionary<string,double>();
        private readonly Stopwatch clock=Stopwatch.StartNew();
        private readonly FpsSource fps;
        private NativeDisk diskPerformance;
        private ulong lastIdle,lastKernel,lastUser;
        private bool cpuBaseline;
        private bool admin;
        private bool fpsRetry=true, fpsWasEnabled;
        private string cpuName="Windows 全部逻辑处理器";
        private string discoveryError="";
        private readonly HashSet<IHardware> failedHardware=new HashSet<IHardware>();
        public event Action<Snapshot> Updated;
        public Collectors(Settings config,string baseDirectory) {
            settings=config.Clone(); fps=new FpsSource(baseDirectory);
            foreach(var p in Metric.Definitions) state.Metrics[p.Key]=p.Value.Copy();
        }
        public void Start() {thread=new Thread(Loop);thread.IsBackground=true;thread.Name="分组采集";thread.Start();}
        public void Configure(Settings config) {lock(gate){
            var old=settings;settings=config.Clone();
            ResetGroup("usage",old.UsageSeconds,settings.UsageSeconds,old,settings,new[]{"cpu","gpu","vram"},old.GpuId!=settings.GpuId);
            ResetGroup("temperature",old.TemperatureSeconds,settings.TemperatureSeconds,old,settings,new[]{"cpuTemp","gpuTemp"},old.GpuId!=settings.GpuId);
            ResetGroup("power",old.PowerSeconds,settings.PowerSeconds,old,settings,new[]{"cpuPower","gpuPower"},old.GpuId!=settings.GpuId);
            ResetGroup("memory",old.MemorySeconds,settings.MemorySeconds,old,settings,new[]{"ram"},false);
            bool diskChanged=old.DiskId!=settings.DiskId||old.MultiDisk!=settings.MultiDisk||!old.DiskIds.SequenceEqual(settings.DiskIds);
            ResetGroup("disk",old.DiskSeconds,settings.DiskSeconds,old,settings,new[]{"disk","diskRead","diskWrite"},diskChanged);
            ResetGroup("diskTemperature",old.DiskTemperatureSeconds,settings.DiskTemperatureSeconds,old,settings,new[]{"diskTemp"},diskChanged);
            SyncDiskMetrics(settings);
            if(diskChanged)foreach(string id in DiskMetrics.Ids){state.Metrics[id]=Metric.Definitions[id].Copy();}
            ResetGroup("fps",old.FpsSeconds,settings.FpsSeconds,old,settings,new[]{"fps"},old.FpsEnabled!=settings.FpsEnabled);
            if(!old.FpsEnabled&&settings.FpsEnabled)fpsRetry=true;
        } wake.Set();}
        private void ResetGroup(string group,int before,int after,Settings a,Settings b,string[] metrics,bool deviceChanged){if(before!=after||deviceChanged||metrics.Any(id=>!a.Shows(id)&&b.Shows(id)))next.Remove(group);}
        public void SetHidden(bool value) {hidden=value;wake.Set();}
        public void SetTarget(int pid) {Interlocked.Exchange(ref targetPid,pid);}
        public Snapshot Read() {lock(gate){return Copy();}}
        private Snapshot Copy() {
            var s=new Snapshot{Gpus=state.Gpus,Disks=state.Disks,SensorStatus=state.SensorStatus,FpsStatus=state.FpsStatus,FpsChildPid=state.FpsChildPid,FpsTargetPid=state.FpsTargetPid,Samples=state.Samples};
            foreach(var p in state.Metrics)s.Metrics[p.Key]=p.Value.Copy(); return s;
        }
        private bool Due(string group,int seconds,double now) {double n;if(!next.TryGetValue(group,out n)||now>=n){next[group]=now+seconds;return true;} return false;}
        private void Put(string id,double? value,string detail) {
            var m=state.Metrics[id]; m.Value=value.HasValue&&!double.IsNaN(value.Value)&&!double.IsInfinity(value.Value)?value:null;
            m.Detail=detail; m.Timestamp=DateTime.UtcNow;
        }
        private void Discover() {
            admin=new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            cpuName=CpuName();
            // Ordinary permissions cannot access CPU registers or SMART. Do not enumerate these sources until elevated.
            computer=new Computer {IsCpuEnabled=admin,IsGpuEnabled=true,IsStorageEnabled=admin,IsMotherboardEnabled=admin};
            try {computer.Open();cpu=computer.Hardware.FirstOrDefault(h=>h.HardwareType==HardwareType.Cpu);
                gpus=computer.Hardware.Where(h=>h.HardwareType.ToString().StartsWith("Gpu")).ToArray();
                disks=computer.Hardware.Where(h=>h.HardwareType==HardwareType.Storage).ToArray();
            }catch(Exception e){discoveryError=e.GetBaseException().Message;}
            state.Gpus=gpus.Select(h=>new DeviceChoice(h.Identifier.ToString(),h.Name)).ToArray();
            var choices=new List<DeviceChoice>();
            try {using(var q=new ManagementObjectSearcher("SELECT Index,Model FROM Win32_DiskDrive"))using(var results=q.Get())foreach(ManagementObject d in results)using(d){choices.Add(new DeviceChoice("physical:"+d["Index"],"磁盘 "+d["Index"]+" · "+d["Model"]));}}catch(Exception e){discoveryError+=" 磁盘发现："+e.Message;}
            state.Disks=choices.ToArray();
            SyncDiskMetrics(settings);
            try {diskPerformance=new NativeDisk();}catch(Exception e){discoveryError+=" 计数器："+e.Message;}
            state.SensorStatus=(admin?"管理员":"普通权限")+" · "+(cpu==null?CpuName():cpu.Name)+"\n"+
                "温度需要硬件支持。CPU 温度需要管理员权限及 PawnIO；硬盘温度通常需要管理员权限。\nGPU 同源传感器按所需最短周期合并读取；休眠磁盘不唤醒。"+discoveryError+BoardTemperatureInventory();
        }
        private string BoardTemperatureInventory(){
            if(!admin)return "\n主板温度清单：以管理员运行后发现传感器。";
            var lines=new List<string>();
            try{foreach(var board in computer.Hardware.Where(h=>h.HardwareType==HardwareType.Motherboard)){
                Update(board);
                foreach(var h in new[]{board}.Concat(board.SubHardware))foreach(var sensor in h.Sensors.Where(s=>s.SensorType==SensorType.Temperature)){
                    var v=sensor.Value;lines.Add(h.Name+" · "+sensor.Name+" = "+(v.HasValue?v.Value.ToString("0.0")+" °C":"未读到")+" · "+sensor.Identifier);
                }
            }}catch(Exception e){lines.Add("清单读取失败："+e.GetBaseException().Message);}
            string inventory="主板温度清单（启动时读取；通用编号尚未对应物理测点）：\n"+(lines.Count==0?"没有发现可用主板温度源":string.Join("\n",lines));
            try{Directory.CreateDirectory(Settings.DataDirectory);File.WriteAllText(Path.Combine(Settings.DataDirectory,"主板传感器清单.txt"),DateTime.Now.ToString("O")+"\n"+inventory);}catch{}
            return "\n"+inventory;
        }
        private void Loop() {
            try {
                lock(gate)Discover();
                Publish();
                while(!stopping) {
                    Settings c;bool retry;lock(gate){c=settings.Clone();retry=fpsRetry;fpsRetry=false;} double now=clock.Elapsed.TotalSeconds;bool changed=false;
                    if(c.FpsEnabled&&!fps.Running&&(retry||!fpsWasEnabled))fps.Start();
                    if(!c.FpsEnabled&&fps.Running)fps.Stop();
                    fpsWasEnabled=c.FpsEnabled;
                    int target=Interlocked.CompareExchange(ref targetPid,0,0);
                    lock(gate) {
                        c=settings.Clone(); // A configuration update must not be overwritten by an earlier loop snapshot.
                        string fs=c.FpsEnabled?fps.Status:"FPS 已关闭";
                        if((c.FpsEnabled&&state.FpsTargetPid!=target)||state.FpsStatus!=fs||(!fps.Running&&state.Metrics["fps"].Value.HasValue)){
                            Put("fps",null,c.FpsEnabled?fs:"点击 FPS 开关启用");state.FpsTargetPid=target;changed=true;
                        }
                        state.FpsStatus=c.FpsEnabled?fps.Status:"FPS 已关闭";state.FpsChildPid=fps.ChildPid;
                        if(!hidden) {
                            bool u=(c.Shows("cpu")||c.Shows("gpu")||c.Shows("vram"))&&Due("usage",c.UsageSeconds,now);
                            bool t=(c.Shows("cpuTemp")||c.Shows("gpuTemp"))&&Due("temperature",c.TemperatureSeconds,now);
                            bool p=(c.Shows("cpuPower")||c.Shows("gpuPower"))&&Due("power",c.PowerSeconds,now);
                            IHardware gpu=string.IsNullOrEmpty(c.GpuId)?gpus.FirstOrDefault():gpus.FirstOrDefault(h=>h.Identifier.ToString()==c.GpuId);
                            if((u&&(c.Shows("gpu")||c.Shows("vram")))||(t&&c.Shows("gpuTemp"))||(p&&c.Shows("gpuPower")))Update(gpu);
                            if(u){if(c.Shows("cpu"))ReadCpu();if(c.Shows("gpu"))ReadSensor("gpu",gpu,SensorType.Load,new[]{"GPU Core","D3D 3D","GPU D3D 3D"},false);
                                if(c.Shows("vram"))ReadVram(gpu);changed=true;}
                            if(c.Shows("ram")&&Due("memory",c.MemorySeconds,now)){ReadMemory();changed=true;}
                            if((t&&c.Shows("cpuTemp"))||(p&&c.Shows("cpuPower"))){if(admin)Update(cpu);}
                            if(t){if(c.Shows("cpuTemp")){if(admin)ReadSensor("cpuTemp",cpu,SensorType.Temperature,new[]{"Core (Tctl/Tdie)","CPU Package","CPU (Tctl/Tdie)","Core (Tdie)","CPU Cores (Average)"},true);else Put("cpuTemp",null,"需要以管理员运行，并安装官方 PawnIO 传感器驱动");}
                                if(c.Shows("gpuTemp"))ReadSensor("gpuTemp",gpu,SensorType.Temperature,new[]{"GPU Core","GPU Temperature"},true);changed=true;}
                            if((c.Shows("disk")||c.Shows("diskRead")||c.Shows("diskWrite"))&&Due("disk",c.DiskSeconds,now)){ReadDisk(c);changed=true;}
                            if(c.Shows("diskTemp")&&Due("diskTemperature",c.DiskTemperatureSeconds,now)){ReadDiskTemperature(c);changed=true;}
                            if(p){if(c.Shows("cpuPower")){if(admin)ReadSensor("cpuPower",cpu,SensorType.Power,new[]{"Package","CPU Package"},true);else Put("cpuPower",null,"需要管理员及 PawnIO；此项只是 CPU 功率");}
                                if(c.Shows("gpuPower"))ReadSensor("gpuPower",gpu,SensorType.Power,new[]{"GPU Power","GPU Package","GPU Board"},true);changed=true;}
                        }
                        if(c.FpsEnabled&&c.Shows("fps")&&Due("fps",c.FpsSeconds,now)){
                            Put("fps",c.FpsEnabled?fps.Sample(target):null,c.FpsEnabled?fps.Status+" · 目标 PID "+target:"点击 FPS 开关启用");state.FpsStatus=c.FpsEnabled?fps.Status:"FPS 已关闭";changed=true;
                        }
                        if(changed)state.Samples++;
                    }
                    if(changed)Publish();wake.WaitOne(500);
                }
            }catch(Exception e){lock(gate){state.SensorStatus+="\n采集异常："+e.GetBaseException().Message;}Publish();}
            finally {fps.Dispose(); if(diskPerformance!=null)diskPerformance.Dispose();if(computer!=null)try{computer.Close();}catch{} }
        }
        // A failed FPS start stays failed until the user turns it off/on, avoiding repeated launches.
        public void RetryFps(){lock(gate){fpsRetry=true;}wake.Set();}
        private void Publish(){var cb=Updated;if(cb!=null)cb(Read());}
        private void Update(IHardware h){if(h==null)return;failedHardware.Remove(h);try{h.Update();foreach(var sub in h.SubHardware)sub.Update();}catch(Exception e){discoveryError=e.GetBaseException().Message;failedHardware.Add(h);}}
        private void ReadCpu(){ulong i,k,u;if(!Native.GetSystemTimes(out i,out k,out u)){Put("cpu",null,"Windows CPU 计数器不可用");return;}
            ulong elapsed=k-lastKernel+u-lastUser,idle=i-lastIdle;
            Put("cpu",cpuBaseline&&elapsed>0?Math.Max(0,Math.Min(100,100.0*(elapsed-idle)/elapsed)):(double?)null,cpu==null?cpuName:cpu.Name);
            lastIdle=i;lastKernel=k;lastUser=u;cpuBaseline=true;
        }
        private void ReadMemory(){var m=new Native.MemoryStatus();m.Length=(uint)Marshal.SizeOf(m);if(Native.GlobalMemoryStatusEx(ref m))Put("ram",100.0*(m.Total-m.Available)/m.Total,string.Format("{0:0.0} / {1:0.0} GB",(m.Total-m.Available)/1073741824.0,m.Total/1073741824.0));else Put("ram",null,"Windows 内存计数器不可用");}
        private IEnumerable<ISensor> Sensors(IHardware h){return h==null?Enumerable.Empty<ISensor>():h.Sensors.Concat(h.SubHardware.SelectMany(s=>s.Sensors));}
        private void ReadSensor(string id,IHardware h,SensorType type,string[] names,bool rejectZero){
            if(h!=null&&failedHardware.Contains(h)){Put(id,null,"传感器读取失败："+discoveryError);return;}
            var candidates=Sensors(h).Where(s=>s.SensorType==type&&s.Value.HasValue).ToArray();ISensor best=null;
            foreach(string n in names){best=candidates.FirstOrDefault(s=>s.Name.Equals(n,StringComparison.OrdinalIgnoreCase));if(best!=null)break;}
            if(best==null&&type==SensorType.Temperature)best=candidates.FirstOrDefault(s=>!s.Name.Contains("Hot Spot")&&!s.Name.Contains("Memory"));
            double? v=best==null?null:(double?)best.Value.Value;
            if(rejectZero&&v<=0)v=null;
            Put(id,v,v.HasValue?h.Name+" · "+best.Name:(type==SensorType.Power?"硬件没有可用功率数据；不代表整机功率":"未提供有效传感器数据；检查权限及驱动"));
        }
        private void ReadVram(IHardware h){if(h!=null&&failedHardware.Contains(h)){Put("vram",null,"显存读取失败："+discoveryError);return;}var s=Sensors(h).ToArray();var used=s.FirstOrDefault(x=>x.Name=="GPU Memory Used"&&x.Value.HasValue);var total=s.FirstOrDefault(x=>x.Name=="GPU Memory Total"&&x.Value.HasValue);
            if(used!=null&&total!=null&&total.Value>0){Put("vram",100.0*used.Value.Value/total.Value.Value,string.Format("{0:0.00} / {1:0.00} GB",used.Value.Value/1024,total.Value.Value/1024));return;}
            ReadSensor("vram",h,SensorType.Load,new[]{"GPU Memory","GPU D3D Memory"},false);
        }
        private void SyncDiskMetrics(Settings c){
            var selected=DiskMetrics.Select(c,state.Disks);var keep=new HashSet<string>(selected.SelectMany(d=>DiskMetrics.Ids.Select(id=>DiskMetrics.Id(id,d.Id))));
            foreach(string id in state.Metrics.Keys.Where(id=>DiskMetrics.BaseId(id)!=id&&!keep.Contains(id)).ToArray())state.Metrics.Remove(id);
            foreach(var disk in selected)foreach(string id in DiskMetrics.Ids){string key=DiskMetrics.Id(id,disk.Id);if(!state.Metrics.ContainsKey(key))state.Metrics[key]=DiskMetrics.Definition(id,disk);}
        }
        private void PutDisk(string id,DeviceChoice disk,DeviceChoice primary,double? value,string detail){
            Put(DiskMetrics.Id(id,disk.Id),value,disk.Name+" · "+detail);
            if(primary!=null&&primary.Id==disk.Id)Put(id,value,disk.Name+" · "+detail);
        }
        private void ReadDisk(Settings c){
            var selected=DiskMetrics.Select(c,state.Disks);var primary=DiskMetrics.Primary(c,selected);
            var values=new Dictionary<int,NativeDisk.Values>();string error="";
            try{if(diskPerformance!=null)values=diskPerformance.ReadMany(selected.Select(d=>DiskMetrics.Index(d.Id)).ToArray());}catch(Exception e){error=" · "+e.Message;}
            foreach(var disk in selected){NativeDisk.Values v;if(!values.TryGetValue(DiskMetrics.Index(disk.Id),out v))v=new NativeDisk.Values();
                if(c.Shows("disk"))PutDisk("disk",disk,primary,v.Load,"活动时间"+error);
                if(c.Shows("diskRead"))PutDisk("diskRead",disk,primary,v.Read,"读取"+error);
                if(c.Shows("diskWrite"))PutDisk("diskWrite",disk,primary,v.Write,"写入"+error);
            }
            if(selected.Length==0)foreach(string id in new[]{"disk","diskRead","diskWrite"})if(c.Shows(id))Put(id,null,"未发现可用物理硬盘");
        }
        private void ReadDiskTemperature(Settings c){
            var selected=DiskMetrics.Select(c,state.Disks);var primary=DiskMetrics.Primary(c,selected);
            foreach(var disk in selected)try{ReadDiskTemperature(disk,primary);}catch(Exception e){PutDisk("diskTemp",disk,primary,null,"温度读取失败："+e.GetBaseException().Message);}
            if(selected.Length==0)Put("diskTemp",null,"未发现可用物理硬盘");
        }
        private void ReadDiskTemperature(DeviceChoice disk,DeviceChoice primary){
            int index=DiskMetrics.Index(disk.Id);IHardware h=disks.FirstOrDefault(x=>x.Identifier.ToString().EndsWith("/"+index));
            if(h==null){PutDisk("diskTemp",disk,primary,null,admin?"此物理盘没有温度源":"硬盘温度需要以管理员运行");return;}
            var property=h.GetType().GetProperty("Storage");
            if(property!=null){object dev=property.GetValue(h,null);var power=dev.GetType().GetProperty("IsDevicePowerOn");if(power!=null&&object.Equals(power.GetValue(dev,null),false)){PutDisk("diskTemp",disk,primary,null,"磁盘休眠，跳过温度采集");return;}}
            var force=h.GetType().GetProperty("ForceWakeup");if(force!=null)force.SetValue(h,false,null);
            string id=DiskMetrics.Id("diskTemp",disk.Id);Update(h);ReadSensor(id,h,SensorType.Temperature,new[]{"Temperature","Drive Temperature","Composite Temperature"},true);
            var value=state.Metrics[id];PutDisk("diskTemp",disk,primary,value.Value,value.Detail);
        }
        public void Dispose(){stopping=true;wake.Set();if(thread!=null&&!thread.Join(8000)){fps.Dispose();return;}wake.Dispose();}
        private static string CpuName(){try{using(var k=Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0")){return ((string)k.GetValue("ProcessorNameString","Windows 全部逻辑处理器")).Trim();}}catch{return "Windows 全部逻辑处理器";}}
    }
    internal static class Native {
        [DllImport("kernel32.dll")]internal static extern bool GetSystemTimes(out ulong idle,out ulong kernel,out ulong user);
        [StructLayout(LayoutKind.Sequential)]internal struct MemoryStatus {public uint Length,Load;public ulong Total,Available,TotalPage,AvailPage,TotalVirtual,AvailVirtual,AvailExtended;}
        [DllImport("kernel32.dll")]internal static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    }
    internal sealed class NativeDisk : IDisposable {
        internal sealed class Values {public double? Load,Read,Write;}
        private IntPtr query,idle,read,write;
        [StructLayout(LayoutKind.Sequential)]struct Formatted {public uint Status; public double Value;}
        [StructLayout(LayoutKind.Sequential)]struct Item {public IntPtr Name; public Formatted Value;}
        [DllImport("pdh.dll",CharSet=CharSet.Unicode)]static extern uint PdhOpenQuery(string source,IntPtr user,out IntPtr query);
        [DllImport("pdh.dll",CharSet=CharSet.Unicode)]static extern uint PdhAddEnglishCounter(IntPtr query,string path,IntPtr user,out IntPtr counter);
        [DllImport("pdh.dll")]static extern uint PdhCollectQueryData(IntPtr query);
        [DllImport("pdh.dll",CharSet=CharSet.Unicode)]static extern uint PdhGetFormattedCounterArray(IntPtr counter,uint format,ref uint size,out uint count,IntPtr buffer);
        [DllImport("pdh.dll")]static extern uint PdhCloseQuery(IntPtr query);
        public NativeDisk(){uint rc=PdhOpenQuery(null,IntPtr.Zero,out query);if(rc!=0)throw new Exception("PDH "+rc);try{Add("% Idle Time",out idle);Add("Disk Read Bytes/sec",out read);Add("Disk Write Bytes/sec",out write);PdhCollectQueryData(query);}catch{Dispose();throw;}}
        private void Add(string name,out IntPtr counter){uint rc=PdhAddEnglishCounter(query,"\\PhysicalDisk(*)\\"+name,IntPtr.Zero,out counter);if(rc!=0)throw new Exception("PDH "+rc);}
        public Dictionary<int,Values> ReadMany(int[] indices){
            var result=indices.Distinct().ToDictionary(index=>index,index=>new Values());
            if(result.Count==0||PdhCollectQueryData(query)!=0)return result;
            var i=CounterValues(idle);var r=CounterValues(read);var w=CounterValues(write);
            foreach(var pair in result){double value;if(i.TryGetValue(pair.Key,out value))pair.Value.Load=Math.Max(0,Math.Min(100,100-value));if(r.TryGetValue(pair.Key,out value))pair.Value.Read=Math.Max(0,value)/1000000;if(w.TryGetValue(pair.Key,out value))pair.Value.Write=Math.Max(0,value)/1000000;}
            return result;
        }
        private Dictionary<int,double> CounterValues(IntPtr counter){var result=new Dictionary<int,double>();uint size=0,count;PdhGetFormattedCounterArray(counter,0x8200,ref size,out count,IntPtr.Zero);if(size==0||size>1048576)return result;IntPtr buf=Marshal.AllocHGlobal((int)size);try{if(PdhGetFormattedCounterArray(counter,0x8200,ref size,out count,buf)!=0)return result;
            int stride=Marshal.SizeOf(typeof(Item));for(int j=0;j<count;j++){var item=(Item)Marshal.PtrToStructure(IntPtr.Add(buf,j*stride),typeof(Item));string n=Marshal.PtrToStringUni(item.Name);int index;int space=n==null?-1:n.IndexOf(' ');if(n!=null&&int.TryParse(space<0?n:n.Substring(0,space),out index)&&index>=0&&(item.Value.Status==0||item.Value.Status==1)&&!double.IsNaN(item.Value.Value)&&!double.IsInfinity(item.Value.Value))result[index]=item.Value.Value;}return result;}finally{Marshal.FreeHGlobal(buf);}}
        public void Dispose(){if(query!=IntPtr.Zero){PdhCloseQuery(query);query=IntPtr.Zero;}}
    }
}
