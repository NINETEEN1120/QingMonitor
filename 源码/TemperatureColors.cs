using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace QingMonitor {
    public sealed class TemperatureColors {
        private readonly Dictionary<string,int> levels=new Dictionary<string,int>();
        public static readonly Color Green=Color.FromArgb(60,190,127),Yellow=Color.FromArgb(240,178,58),Red=Color.FromArgb(235,91,84),Missing=Color.FromArgb(105,112,124);
        public void Clear(){levels.Clear();}
        private static bool Thresholds(string id,Settings c,out int yellow,out int red){
            id=DiskMetrics.BaseId(id);
            yellow=c.CpuTemperatureYellow;red=c.CpuTemperatureRed;
            if(id=="cpuTemp")return true;
            if(id=="gpuTemp"){yellow=c.GpuTemperatureYellow;red=c.GpuTemperatureRed;return true;}
            if(id=="diskTemp"){yellow=c.DiskTemperatureYellow;red=c.DiskTemperatureRed;return true;}
            return false;
        }
        public bool Update(Snapshot snapshot,Settings c){
            bool changed=false;
            foreach(string id in levels.Keys.Where(id=>!snapshot.Metrics.ContainsKey(id)).ToArray()){levels.Remove(id);changed=true;}
            foreach(string id in snapshot.Metrics.Keys){
                int yellow,red;if(!Thresholds(id,c,out yellow,out red))continue;
                int previous;bool existed=levels.TryGetValue(id,out previous);Metric metric;
                if(!c.TemperatureColorsEnabled||!snapshot.Metrics.TryGetValue(id,out metric)||!metric.Value.HasValue||double.IsNaN(metric.Value.Value)||double.IsInfinity(metric.Value.Value)){
                    if(existed){levels.Remove(id);changed=true;}continue;
                }
                // Compare the same integer that the overlay displays.
                double value=Math.Round(metric.Value.Value,0,MidpointRounding.AwayFromZero);
                int next=value>=red?2:value>=yellow?1:0;
                if(existed&&next<previous){
                    if(previous==2&&value>=red-2)next=2;
                    else if(next==0&&value>=yellow-2)next=1;
                }
                if(!existed||previous!=next){levels[id]=next;changed=true;}
            }
            return changed;
        }
        public Color ValueColor(Metric metric,Settings c,Color normal){
            if(!metric.Value.HasValue||double.IsNaN(metric.Value.Value)||double.IsInfinity(metric.Value.Value))return Missing;
            int yellow,red,level;
            if(!c.TemperatureColorsEnabled||!Thresholds(metric.Id,c,out yellow,out red))return normal;
            if(!levels.TryGetValue(metric.Id,out level))level=0;
            return level==2?Red:level==1?Yellow:Green;
        }
    }
}
