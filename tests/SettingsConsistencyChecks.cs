using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using QingMonitor;

internal static class SettingsConsistencyChecks {
    static int failures;
    static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Field(object o,string n){return o.GetType().GetField(n,flags).GetValue(o);}
    static void Check(string n,bool okay){Console.WriteLine(n+"="+okay);if(!okay)failures++;}
    [STAThread] static int Main(){
        Application.EnableVisualStyles();
        using(var form=new SettingsForm(new Settings(),new Snapshot())){
            string[] fields={"usageInterval","memoryInterval","temperatureInterval","diskInterval","diskTemperatureInterval","powerInterval","fpsInterval"};
            Check("Sampling.AllGroupsUseSliders",fields.All(n=>Field(form,n) is SliderValueControl));
            var evt=typeof(SettingsForm).GetEvent("PreviewChanged");
            Check("Preview.HasDraftEvent",evt!=null);
            if(evt!=null&&fields.All(n=>Field(form,n) is SliderValueControl)){
                int count=0;Settings latest=null;
                Action<Settings> callback=delegate(Settings s){count++;latest=s;};evt.AddEventHandler(form,callback);
                var flush=typeof(SettingsForm).GetMethod("PublishPreview",flags);
                var interval=(SliderValueControl)Field(form,"usageInterval");
                int[] seconds={1,2,5,10,30};bool mappings=true;
                for(int i=0;i<5;i++){
                    interval.Value=i;flush.Invoke(form,null);mappings&=latest!=null&&latest.UsageSeconds==seconds[i];
                }
                Check("Sampling.DiscreteSecondsRoundTrip",mappings);
                Check("Preview.DraftDoesNotCommit",form.Result.UsageSeconds==2);
                ((SliderValueControl)Field(form,"fontSize")).Value=12.5M;flush.Invoke(form,null);
                Check("Preview.IncludesAppearance",latest.FontSize==12.5F&&form.Result.FontSize==11F);
                latest.Visible[0]="corrupt";flush.Invoke(form,null);
                Check("Preview.SnapshotIsIsolated",latest.Visible[0]!="corrupt");
                int before=count;
                ((SliderValueControl)Field(form,"cpuYellow")).Value=100;
                flush.Invoke(form,null);
                Check("Preview.InvalidDraftHasNoEvent",count==before);
                ((SliderValueControl)Field(form,"cpuRed")).Value=110;flush.Invoke(form,null);
                Check("Preview.ValidDraftResumes",count==before+1&&latest.CpuTemperatureYellow==100);
                string malformed=Path.Combine(Path.GetTempPath(),"QingMonitor-invalid-"+Guid.NewGuid().ToString("N")+".json");
                try {
                    File.WriteAllText(malformed,"{broken json");
                    bool rejected=false;
                    try{typeof(SettingsForm).GetMethod("LoadImportedSettings",flags).Invoke(form,new object[]{malformed});}catch(TargetInvocationException){rejected=true;}
                    Check("Import.MalformedPreservesUncommittedControls",rejected&&((SliderValueControl)Field(form,"fontSize")).Value==12.5M&&((SliderValueControl)Field(form,"cpuYellow")).Value==100);
                    Check("Import.MalformedPreservesLastPreview",latest.FontSize==12.5F&&latest.CpuTemperatureYellow==100);
                    var imported=new Settings{UsageSeconds=30,FontSize=14,OpacityPercent=60};imported.Save(malformed);
                    typeof(SettingsForm).GetMethod("LoadImportedSettings",flags).Invoke(form,new object[]{malformed});
                    Check("Import.ValidMapsIntervalsAndPublishes",((SliderValueControl)Field(form,"usageInterval")).Value==4&&latest.UsageSeconds==30&&latest.FontSize==14);
                } finally{File.Delete(malformed);}
                typeof(SettingsForm).GetMethod("OnFormClosing",flags).Invoke(form,new object[]{new FormClosingEventArgs(CloseReason.UserClosing,false)});
                Check("Preview.CancelRestoresOriginal",form.Result.FontSize==11F&&form.Result.UsageSeconds==2);
            }
        }
        Console.WriteLine("Failures="+failures);return failures==0?0:1;
    }
}
