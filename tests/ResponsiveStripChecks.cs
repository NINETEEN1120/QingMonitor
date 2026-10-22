using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using QingMonitor;
internal static class ResponsiveStripChecks {
 static int passed;static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(object o,string n){return o.GetType().GetField(n,Flags).GetValue(o);}
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
 static Snapshot Sample(){var s=new Snapshot{Disks=new[]{new DeviceChoice("physical:0","System SSD"),new DeviceChoice("physical:1","Data SSD")}};foreach(var item in Metric.Definitions){var m=item.Value.Copy();m.Value=m.Unit=="°C"?53:m.Unit=="W"?77.5:28;m.Timestamp=DateTime.UtcNow;s.Metrics[m.Id]=m;}foreach(var d in s.Disks)foreach(var id in DiskMetrics.Ids){var m=DiskMetrics.Definition(id,d);m.Value=id=="diskTemp"?34:12.5;m.Timestamp=DateTime.UtcNow;s.Metrics[m.Id]=m;}return s;}
 static HashSet<string> HitIds(Overlay form){var ids=new HashSet<string>();var rows=(Metric[])Field(form,"rows");var hit=typeof(Overlay).GetMethod("StripMetricIndex",Flags);for(int y=(int)Field(form,"stripTop")+6;y<form.Height-3;y+=4)for(int x=6;x<form.Width-3;x+=4){int index=(int)hit.Invoke(form,new object[]{new Point(x,y)});if(index>=0)ids.Add(rows[index].Id);}return ids;}
 static void SetDimension(Settings c,string n,int value){typeof(Settings).GetField(n).SetValue(c,value);}
 [STAThread]static int Main(string[] args){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);string root=args[0];Directory.CreateDirectory(root);
  var c=new Settings{Visible=new[]{"cpu","gpu","ram","vram","cpuTemp","gpuTemp","diskTemp","cpuPower","gpuPower","diskRead","diskWrite","fps"},X=20,Y=100};
  using(var form=new Overlay(c,Path.Combine(root,"diagnostics.txt"))){
   ((NotifyIcon)Field(form,"tray")).Visible=false;var handle=form.Handle;typeof(Overlay).GetMethod("Receive",Flags).Invoke(form,new object[]{Sample()});
   var expected=((Metric[])Field(form,"rows")).Select(m=>m.Id).ToArray();var seen=HitIds(form);
   Check(seen.Contains("cpuPower")&&seen.Contains("gpuPower"),"both selected power values are directly visible with chart collapsed");
   Check(expected.All(seen.Contains),"automatic two-row width displays every selected disk IO FPS and power metric");
   Check(!c.Expanded&&(int)Field(form,"stripTop")==0&&form.Height==48,"all values remain in a two-row collapsed strip");
   Check(form.Width>600&&Screen.FromRectangle(form.Bounds).Bounds.Contains(form.Bounds),"automatic width grows and remains on screen");
   using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,form.ClientRectangle);image.Save(Path.Combine(root,"all-selected-two-rows.png"),ImageFormat.Png);}
   using(var beforeUpdate=new Bitmap(form.Width,form.Height))using(var afterUpdate=new Bitmap(form.Width,form.Height)){
    form.DrawToBitmap(beforeUpdate,form.ClientRectangle);var update=Sample();foreach(var metric in update.Metrics.Values)if(metric.Value.HasValue)metric.Value+=7;typeof(Overlay).GetMethod("Receive",Flags).Invoke(form,new object[]{update});form.DrawToBitmap(afterUpdate,form.ClientRectangle);
    int changed=0;for(int y=0;y<form.Height;y++)for(int x=0;x<form.Width;x++)if(beforeUpdate.GetPixel(x,y)!=afterUpdate.GetPixel(x,y))changed++;
    Check(changed>30,"fresh samples repaint numeric values without changing metric IDs or layout");
   }
   int autoWidth=form.Width;float font=((Font)Field(form,"body")).Size;
   var narrow=c.Clone();SetDimension(narrow,"PanelWidth",360);form.ApplyConfiguration(narrow);Check(form.Width==360&&form.Height==48&&Math.Abs(((Font)Field(form,"body")).Size-font)<0.01,"manual width does not change height or text size");
   int pages=(int)typeof(Overlay).GetMethod("StripPages",Flags).Invoke(form,null);Check(pages>1,"insufficient width has an explicit page count");
   seen.Clear();for(int page=0;page<pages;page++){typeof(Overlay).GetField("stripPage",Flags).SetValue(form,page);typeof(Overlay).GetMethod("ApplyAppearance",Flags,null,Type.EmptyTypes,null).Invoke(form,null);seen.UnionWith(HitIds(form));}
   Check(expected.All(seen.Contains),"all selected values are reachable across narrow-window pages");
   var tall=narrow.Clone();SetDimension(tall,"PanelHeight",100);form.ApplyConfiguration(tall);Check(form.Width==360&&form.Height==100&&Math.Abs(((Font)Field(form,"body")).Size-font)<0.01,"manual height changes vertical space without changing width or font");
   tall.Save(Path.Combine(root,"dimensions.json"));var reopened=Settings.Read(Path.Combine(root,"dimensions.json"));Check((int)typeof(Settings).GetField("PanelWidth").GetValue(reopened)==360&&(int)typeof(Settings).GetField("PanelHeight").GetValue(reopened)==100,"independent dimensions survive save and reload");
   form.ApplyConfiguration(c);int height=form.Height;var before=HitIds(form);form.ToggleChart();Check(form.Width==autoWidth&&form.Height==height+190&&HitIds(form).SetEquals(before),"chart adds only chart area and retains the same metric visibility");form.ToggleChart();
   Check(Field(Field(form,"collectors"),"thread")==null,"layout checks never start sensor sampling");form.Close();
  }Console.WriteLine("Passed="+passed);return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
