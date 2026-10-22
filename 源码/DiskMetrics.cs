using System;
using System.Collections.Generic;
using System.Linq;

namespace QingMonitor {
    public static class DiskMetrics {
        public static readonly string[] Ids={"disk","diskTemp","diskRead","diskWrite"};
        public static bool IsDisk(string id){return Array.IndexOf(Ids,id)>=0;}
        public static int Index(string id){int index;return id!=null&&id.StartsWith("physical:",StringComparison.Ordinal)&&int.TryParse(id.Substring(9),out index)&&index>=0?index:-1;}
        public static string Id(string metric,string disk){return metric+"@"+disk;}
        public static string BaseId(string id){if(id==null)return "";int at=id.IndexOf('@');return at>0&&IsDisk(id.Substring(0,at))&&Index(id.Substring(at+1))>=0?id.Substring(0,at):id;}
        public static DeviceChoice[] Select(Settings c,DeviceChoice[] available){
            available=(available??new DeviceChoice[0]).Where(d=>Index(d.Id)>=0).OrderBy(d=>Index(d.Id)).ToArray();
            string[] ids=c.MultiDisk?(c.DiskIds??new string[0]):new[]{string.IsNullOrEmpty(c.DiskId)?(available.Length>0?available[0].Id:""):c.DiskId};
            if(c.MultiDisk&&ids.Length==0)return available;
            return ids.Where(id=>Index(id)>=0).Distinct().OrderBy(id=>Index(id)).Select(id=>available.FirstOrDefault(d=>d.Id==id)??new DeviceChoice(id,"硬盘 "+Index(id)+" · 当前未发现")).ToArray();
        }
        public static DeviceChoice Primary(Settings c,DeviceChoice[] selected){return selected.FirstOrDefault(d=>d.Id==c.DiskId)??selected.FirstOrDefault();}
        public static Metric Definition(string metric,DeviceChoice disk){
            var m=Metric.Definitions[metric].Copy();m.Id=Id(metric,disk.Id);
            m.Label="硬盘"+Index(disk.Id)+" "+(metric=="disk"?"负载":metric=="diskTemp"?"温度":metric=="diskRead"?"读取":"写入");
            m.Detail=disk.Name+" · 等待采集";return m;
        }
        public static Metric[] Rows(Settings c,Snapshot snapshot){
            var selected=Select(c,snapshot.Disks);bool multiple=c.MultiDisk&&selected.Length>0,inserted=false;
            var result=new List<Metric>();
            foreach(string id in c.Visible){
                if(multiple&&IsDisk(id)){
                    if(inserted)continue;inserted=true;
                    foreach(var disk in selected)foreach(string metric in c.Visible.Where(IsDisk)){
                        Metric m;result.Add(snapshot.Metrics.TryGetValue(Id(metric,disk.Id),out m)?m:Definition(metric,disk));
                    }
                }else{Metric m;result.Add(snapshot.Metrics.TryGetValue(id,out m)?m:Metric.Definitions[id]);}
            }
            return result.ToArray();
        }
        public static string ChartId(Settings c,Metric[] rows){
            if(rows.Any(m=>m.Id==c.ChartMetric))return c.ChartMetric;
            string id=Id(BaseId(c.ChartMetric),c.DiskId);
            if(rows.Any(m=>m.Id==id))return id;
            var match=rows.FirstOrDefault(m=>BaseId(m.Id)==BaseId(c.ChartMetric));return (match??rows[0]).Id;
        }
    }
}
