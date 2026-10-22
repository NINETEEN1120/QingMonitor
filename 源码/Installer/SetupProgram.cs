using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;
[assembly:AssemblyTitle("我在监控你的电脑 · 安装")]
[assembly:AssemblyProduct("我在监控你的电脑")]
[assembly:AssemblyVersion("0.1.11.0")]
[assembly:AssemblyFileVersion("0.1.11.0")]
[assembly:System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
namespace QingMonitorSetup {
    internal sealed class SetupForm : Form {
        readonly TextBox folder=new TextBox();
        readonly CheckBox temperature=new CheckBox(),shortcut=new CheckBox(),launch=new CheckBox();
        readonly Button install=new Button(),browse=new Button();
        readonly Label state=new Label();
        readonly ProgressBar progress=new ProgressBar();
        bool busy,finished;string installed;
        internal SetupForm() {
            Text=InstallerCore.ProductName+" · 安装";ClientSize=new Size(570,450);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
            Font=new Font("Microsoft YaHei UI",9);BackColor=Color.FromArgb(247,249,252);
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("QingMonitor.Icon"))Icon=new Icon(stream,32,32);
            var picture=new PictureBox{Location=new Point(22,22),Size=new Size(58,58),SizeMode=PictureBoxSizeMode.CenterImage,Image=Icon.ToBitmap()};Controls.Add(picture);
            Controls.Add(new Label{Text=InstallerCore.ProductName,Location=new Point(92,25),Size=new Size(430,30),Font=new Font(Font.FontFamily,15,FontStyle.Bold)});
            Controls.Add(new Label{Text="0.1.11 · 轻量性能与温度悬浮窗",Location=new Point(94,61),AutoSize=true});
            Controls.Add(new Label{Text="安装位置",Location=new Point(24,107),AutoSize=true});folder.SetBounds(24,132,426,28);
            folder.Text=Directory.Exists(@"D:\")?@"D:\插件\性能悬浮窗":Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),InstallerCore.ProductName);Controls.Add(folder);
            browse.Text="浏览…";browse.SetBounds(461,129,85,31);browse.Click+=delegate{using(var dialog=new FolderBrowserDialog{Description="选择程序专用文件夹",SelectedPath=folder.Text})if(dialog.ShowDialog(this)==DialogResult.OK)folder.Text=dialog.SelectedPath;};Controls.Add(browse);
            temperature.Text="安装 CPU 温度支持（PawnIO，已内置）";temperature.Checked=true;temperature.SetBounds(24,179,520,28);Controls.Add(temperature);
            shortcut.Text="创建桌面快捷方式（启动时确认管理员权限）";shortcut.Checked=true;shortcut.SetBounds(24,215,520,28);Controls.Add(shortcut);
            launch.Text="完成后打开程序";launch.Checked=true;launch.SetBounds(24,250,520,28);Controls.Add(launch);
            state.Text=InstallerCore.ReadPawnState().Label;state.SetBounds(24,290,522,72);Controls.Add(state);
            progress.SetBounds(24,383,360,22);Controls.Add(progress);install.Text="安装";install.SetBounds(411,379,135,34);install.Click+=async delegate{if(finished){Close();return;}await Install();};Controls.Add(install);
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy)e.Cancel=true;};
        }
        void Status(string text){if(InvokeRequired){BeginInvoke((Action)(()=>Status(text)));return;}state.Text=text;}
        async Task Install() {
            string target;try{target=InstallerCore.ValidateTarget(folder.Text);}catch(Exception error){MessageBox.Show(this,error.Message,Text);return;}
            bool driver=temperature.Checked,desktop=shortcut.Checked,open=launch.Checked;
            busy=true;install.Enabled=false;browse.Enabled=false;folder.Enabled=false;temperature.Enabled=false;shortcut.Enabled=false;launch.Enabled=false;progress.Style=ProgressBarStyle.Marquee;
            try {
                string note=await Task.Run(()=>{
                    string parent=Path.GetDirectoryName(target);Directory.CreateDirectory(parent);string work=Path.Combine(parent,".QingMonitor-setup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
                    try {
                        Status("正在校验并解压程序…");string payload=Path.Combine(work,"payload.zip");
                        using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("QingMonitor.Payload"))using(var output=File.Create(payload))resource.CopyTo(output);
                        if(InstallerCore.Hash(payload)!=PackageInfo.PayloadHash)throw new IOException("安装包校验失败，请重新取得完整安装包。");
                        string app=Path.Combine(work,"app");using(var zip=File.OpenRead(payload))InstallerCore.ExtractPayload(zip,app);
                        if(InstallerCore.Hash(Path.Combine(app,@"驱动\PawnIO_setup.exe"))!=InstallerCore.DriverHash)throw new IOException("内置驱动校验失败。");
                        Status("正在安装程序，请先退出旧版悬浮窗…");InstallerCore.CopyApplication(app,target,Path.Combine(work,"backup"));installed=target;
                        string result="程序安装完成。";
                        if(driver)try{result+="\n"+InstallerCore.InstallDriver(target,Status);}catch(Exception error){result+="\n温度支持未完成："+error.Message;}
                        if(desktop)try{InstallerCore.CreateShortcut(target);}catch(Exception error){result+="\n"+error.Message;}
                        return result;
                    }finally{InstallerCore.CleanWork(work,parent);}
                });
                Status(note);finished=true;progress.Style=ProgressBarStyle.Blocks;progress.Value=100;install.Text="完成";
                if(open)Process.Start(new ProcessStartInfo(Path.Combine(installed,"QingMonitor.exe")){UseShellExecute=true,WorkingDirectory=installed});
            }catch(Exception error){Status("安装未完成："+error.GetBaseException().Message);browse.Enabled=true;folder.Enabled=true;temperature.Enabled=true;shortcut.Enabled=true;launch.Enabled=true;progress.Style=ProgressBarStyle.Blocks;}
            finally{busy=false;install.Enabled=true;}
        }
    }
    internal static class SetupProgram {
        [STAThread]static void Main() {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            try{if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)){MessageBox.Show("安装需要管理员权限，请右键安装包并选择“以管理员身份运行”。",InstallerCore.ProductName);return;}Application.Run(new SetupForm());}
            catch(Exception error){MessageBox.Show(error.GetBaseException().Message,InstallerCore.ProductName+" · 安装");}
        }
    }
}
