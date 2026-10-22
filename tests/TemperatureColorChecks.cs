using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using QingMonitor;
internal static class TemperatureColorChecks {
 static int passed;
 static void Check(bool pass,string name){if(!pass)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
 static object Field(object obj,string name){return obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);}
 static Snapshot Sample(string id,double? value){var s=new Snapshot();s.Metrics[id]=Metric.Definitions[id].Copy();s.Metrics[id].Value=value;s.Metrics[id].Timestamp=DateTime.UtcNow;return s;}
 static Color Step(TemperatureColors colors,Settings c,string id,double? value){var s=Sample(id,value);colors.Update(s,c);return colors.ValueColor(s.Metrics[id],c,Color.White);}
 static void Render(string root,Settings config,int cpu,int gpu,int disk,string name){
  using(var form=new Overlay(config,Path.Combine(root,"test-diagnostics.txt"))){
   ((NotifyIcon)Field(form,"tray")).Visible=false;var handle=form.Handle;
   var s=new Snapshot();foreach(var p in Metric.Definitions)s.Metrics[p.Key]=p.Value.Copy();
   s.Metrics["cpu"].Value=14;s.Metrics["gpu"].Value=11;s.Metrics["vram"].Value=30;s.Metrics["ram"].Value=52;s.Metrics["disk"].Value=9;
   s.Metrics["diskRead"].Value=6.9;s.Metrics["diskWrite"].Value=1.2;s.Metrics["cpuPower"].Value=56.9;
   s.Metrics["cpuTemp"].Value=cpu;s.Metrics["gpuTemp"].Value=gpu;s.Metrics["diskTemp"].Value=disk;
   foreach(var m in s.Metrics.Values)m.Timestamp=DateTime.UtcNow;
   typeof(Overlay).GetMethod("Receive",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[]{s});
   int label=(int)Field(form,"labelWidth"),padding=(int)Field(form,"padding"),left=padding+label;
   var categories=typeof(Overlay).GetMethod("StripCategory",BindingFlags.Instance|BindingFlags.NonPublic);
   Check(((Metric[])categories.Invoke(form,new object[]{0})).All(m=>m.Unit=="%")&&((Metric[])categories.Invoke(form,new object[]{1})).All(m=>m.Unit=="°C"),"separate percentage and temperature rows "+name);
   Check(!form.Visible&&Field(Field(form,"collectors"),"thread")==null,"offscreen no collection "+name);
   Check(form.Width<=710*2&&form.Height<=48*2,"compact taskbar geometry "+name);
   using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(root,name+".png"),ImageFormat.Png);
    foreach(var expected in new[]{TemperatureColors.Green,TemperatureColors.Yellow,TemperatureColors.Red}.Where(color=>new[]{cpu,gpu,disk}.Length==3)){
     // Exact value colors are present in the mixed-level example; antialiasing also contains intermediate pixels.
     if(name!="mixed-levels")break;int count=0;for(int y=0;y<bitmap.Height;y++)for(int x=left;x<bitmap.Width;x++)if(bitmap.GetPixel(x,y).ToArgb()==expected.ToArgb())count++;
     Check(count>5,"rendered value pixels "+expected.ToArgb());
    }
   }
   form.Close();
  }
 }
 [STAThread] static int Main(string[] args){try{
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);string root=args[0];Directory.CreateDirectory(root);
  Settings c=Settings.Read(args[1]);Check(c.ScalePercent==76&&c.OpacityPercent==61&&c.Visible.Contains("cpuPower")&&c.ClickThrough,"old layout preserved");
  Check(c.TemperatureColorsEnabled&&c.CpuTemperatureYellow==70&&c.CpuTemperatureRed==80&&c.DiskTemperatureYellow==50&&c.DiskTemperatureRed==60,"old config new defaults");
  foreach(string id in new[]{"cpuTemp","gpuTemp","diskTemp"}){
   int yellow=id=="diskTemp"?50:70,red=id=="diskTemp"?60:80;var colors=new TemperatureColors();
   Check(Step(colors,c,id,yellow-1)==TemperatureColors.Green,id+" green");
   Check(Step(colors,c,id,yellow)==TemperatureColors.Yellow,id+" yellow boundary");
   Check(Step(colors,c,id,red)==TemperatureColors.Red,id+" red boundary");
   Check(Step(colors,c,id,red-2)==TemperatureColors.Red,id+" red cooldown hold");
   Check(Step(colors,c,id,red-3)==TemperatureColors.Yellow,id+" red cooldown release");
   Check(Step(colors,c,id,yellow-2)==TemperatureColors.Yellow,id+" yellow cooldown hold");
   Check(Step(colors,c,id,yellow-3)==TemperatureColors.Green,id+" yellow cooldown release");
   Check(Step(colors,c,id,red+.5)==TemperatureColors.Red,id+" fractional display rounding");
   Check(Step(colors,c,id,null)==TemperatureColors.Missing,id+" missing gray");
   Check(Step(colors,c,id,yellow-1)==TemperatureColors.Green,id+" recovered reading resets severity");
  }
  var state=new TemperatureColors();foreach(string id in new[]{"cpu","gpu","vram","ram","disk","diskRead","diskWrite","fps","cpuPower","gpuPower"})Check(Step(state,c,id,99)==Color.White,id+" unchanged color");
  c.TemperatureColorsEnabled=false;Check(Step(state,c,"cpuTemp",100)==Color.White,"disabled colors");c.TemperatureColorsEnabled=true;c.CpuTemperatureYellow=60;c.CpuTemperatureRed=65;
  Check(Step(state,c,"cpuTemp",65)==TemperatureColors.Red,"custom thresholds");c.Save(Path.Combine(root,"custom-settings.json"));var round=Settings.Read(Path.Combine(root,"custom-settings.json"));Check(round.CpuTemperatureYellow==60&&round.CpuTemperatureRed==65,"threshold persistence");
  c.CpuTemperatureYellow=100;c.CpuTemperatureRed=int.MinValue;c.Normalize();Check(c.CpuTemperatureYellow==70&&c.CpuTemperatureRed==80,"invalid imported thresholds normalized");
  using(var dialog=new SettingsForm(c,new Snapshot())){
   object[] parameters={null};Check((bool)typeof(SettingsForm).GetMethod("TryGather",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(dialog,parameters),"settings gather");
   ((SliderValueControl)Field(dialog,"cpuYellow")).Value=65;((SliderValueControl)Field(dialog,"cpuRed")).Value=78;
   typeof(SettingsForm).GetMethod("TryGather",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(dialog,parameters);var edited=(Settings)parameters[0];Check(edited.CpuTemperatureYellow==65&&edited.CpuTemperatureRed==78,"settings custom controls");
   Check(c.CpuTemperatureYellow==70,"dialog source config untouched");
  }
  var preview=new Settings();preview.Visible=Metric.Definitions.Keys.Where(id=>id!="gpuPower").ToArray();
  Render(root,preview,61,27,39,"normal-levels");Render(root,preview,72,83,39,"mixed-levels");
  Console.WriteLine("Passed="+passed);return 0;
 }catch(Exception error){Console.WriteLine(error);return 1;}}
}
