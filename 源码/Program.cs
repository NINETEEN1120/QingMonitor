using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
[assembly:AssemblyTitle("我在监控你的电脑")]
[assembly:AssemblyProduct("我在监控你的电脑")]
[assembly:AssemblyDescription("轻量性能与温度悬浮窗")]
[assembly:AssemblyVersion("0.1.11.0")]
[assembly:AssemblyFileVersion("0.1.11.0")]
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8",FrameworkDisplayName=".NET Framework 4.8")]
namespace QingMonitor {
 internal static class Program {
  internal const string ProductName="我在监控你的电脑";
  [STAThread] static void Main(string[] args) {
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   string diag=null;Settings settings=Settings.Load();
   for(int i=0;i<args.Length;i++){if(args[i]=="--diagnostics"&&i+1<args.Length)diag=args[++i];if(args[i]=="--defaults")settings=new Settings();}
   if(diag==null&&(settings.LayoutVersion<1||!File.Exists(Settings.ConfigPath))){var screen=Screen.PrimaryScreen.Bounds;settings.LayoutVersion=1;settings.ScalePercent=100;settings.Expanded=false;settings.X=screen.Left+6;settings.Y=screen.Bottom-48;}
   if(diag!=null){var area=Screen.PrimaryScreen.WorkingArea;settings.X=area.Right-650;settings.Y=area.Top+60;settings.ClickThrough=true;}
   string user=WindowsIdentity.GetCurrent().User.Value.Replace('-','_');
   using(var mutex=new Mutex(false,"Local\\QingMonitor_"+user))using(var signal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\QingMonitor_Show_"+user)){
    bool owned=false;try{owned=mutex.WaitOne(Array.IndexOf(args,"--wait-instance")>=0?15000:0);}catch(AbandonedMutexException){owned=true;}
    if(!owned){signal.Set();return;}
    try {
     using(var overlay=new Overlay(settings,diag))using(var showTimer=new System.Windows.Forms.Timer{Interval=1000}){
      showTimer.Tick+=delegate{if(signal.WaitOne(0))overlay.Restore();};showTimer.Start();
      Application.ThreadException+=delegate(object sender,ThreadExceptionEventArgs e){Log(e.Exception);MessageBox.Show("遇到异常："+e.Exception.Message+"\n错误已保存在本地配置目录。",ProductName);};
      Application.Run(overlay);
     }
    }catch(Exception e){Log(e);MessageBox.Show("启动失败："+e.GetBaseException().Message,ProductName);}finally{mutex.ReleaseMutex();}
   }
  }
  private static void Log(Exception e){try{Directory.CreateDirectory(Settings.DataDirectory);File.AppendAllText(Path.Combine(Settings.DataDirectory,"errors.log"),DateTime.Now.ToString("O")+" "+e+Environment.NewLine);}catch{}}
 }
}
