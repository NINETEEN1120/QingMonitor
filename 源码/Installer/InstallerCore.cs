using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.ServiceProcess;
using Microsoft.Win32;

namespace QingMonitorSetup {
    internal sealed class PawnState {
        internal bool Registered,LibraryPresent,ServicePresent,Running;
        internal Version Version;
        internal string Label;
    }
    internal static class InstallerCore {
        internal const string ProductName="我在监控你的电脑";
        internal const string DriverHash="1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032";
        internal static string Hash(string file){using(var stream=File.OpenRead(file))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","");}
        internal static bool NeedsDriver(PawnState state) {
            if(!state.Registered&&!state.ServicePresent&&!state.LibraryPresent)return true;
            if(state.Registered&&state.LibraryPresent&&state.Version!=null&&state.Version>=new Version(2,2))return false;
            if(state.Registered&&state.LibraryPresent&&state.Version!=null&&state.Version.Major==2&&state.Version.Minor==1)return true;
            throw new InvalidOperationException("检测到旧版或不完整的 PawnIO。请先用 Windows“已安装的应用”修复或卸载该驱动，再重新运行本安装包。程序其他指标仍可使用。");
        }
        internal static PawnState ReadPawnState() {
            var state=new PawnState();string location=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"PawnIO");
            using(var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))
            using(var key=machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO")) {
                if(key!=null){state.Registered=true;location=Convert.ToString(key.GetValue("InstallLocation",location));Version parsed;if(Version.TryParse(Convert.ToString(key.GetValue("DisplayVersion","")),out parsed))state.Version=parsed;}
            }
            state.LibraryPresent=File.Exists(Path.Combine(location,"PawnIOLib.dll"));
            try {using(var service=new ServiceController("PawnIO")){state.Running=service.Status==ServiceControllerStatus.Running;state.ServicePresent=true;}}catch(InvalidOperationException){}
            state.Label=state.Registered?"检测到 PawnIO "+state.Version+(state.Running?"，正在运行":"，当前未运行"):state.ServicePresent||state.LibraryPresent?"检测到 PawnIO 残留，请先修复":"未安装，安装包已内置官方驱动";
            return state;
        }
        internal static void RejectLinks(string path) {
            for(var directory=new DirectoryInfo(Path.GetFullPath(path));directory!=null;directory=directory.Parent)
                if(directory.Exists&&(directory.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("安装路径包含目录链接，请选择普通文件夹："+directory.FullName);
        }
        internal static string Child(string root,string relative) {
            if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.IndexOf(':')>=0)throw new IOException("安装文件路径无效。");
            string prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string path=Path.GetFullPath(Path.Combine(root,relative));
            if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("安装文件路径越界。");
            RejectLinks(Path.GetDirectoryName(path));
            if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("安装文件不能是链接。");
            return path;
        }
        internal static string ValidateTarget(string input) {
            if(!Path.IsPathRooted(input))throw new IOException("请选择完整的安装路径。");
            string target=Path.GetFullPath(input).TrimEnd(Path.DirectorySeparatorChar);
            string drive=Path.GetPathRoot(target).TrimEnd(Path.DirectorySeparatorChar);
            string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd(Path.DirectorySeparatorChar);
            if(target.Equals(drive,StringComparison.OrdinalIgnoreCase)||target.Equals(windows,StringComparison.OrdinalIgnoreCase)||target.StartsWith(windows+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)
                ||target.Equals(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),StringComparison.OrdinalIgnoreCase)
                ||target.Equals(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),StringComparison.OrdinalIgnoreCase))throw new IOException("请选择程序专用文件夹。");
            RejectLinks(target);
            if(Directory.Exists(target)&&Directory.EnumerateFileSystemEntries(target).Any()) {
                string exe=Child(target,"QingMonitor.exe");
                if(!File.Exists(exe))throw new IOException("该文件夹不是现有程序目录，请选择空文件夹。");
                string name=FileVersionInfo.GetVersionInfo(exe).FileDescription;
                if(name!=ProductName&&name!="轻量性能悬浮窗")throw new IOException("该文件夹包含其他程序，请选择空文件夹。");
            }
            return target;
        }
        internal static void ExtractPayload(Stream stream,string destination) {
            Directory.CreateDirectory(destination);
            using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)) {
                var entries=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(var entry in zip.Entries) {
                    if(entry.FullName.EndsWith("/"))continue;
                    string path=Child(destination,entry.FullName);
                    if(!entries.Add(path)||entry.Length>64*1024*1024)throw new IOException("安装包文件无效。");
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using(var source=entry.Open())using(var output=File.Create(path))source.CopyTo(output);
                }
            }
        }
        // Preflight every destination before changing any existing file. Restore changed files on a copy error.
        internal static void CopyApplication(string source,string target,string backup) {
            Directory.CreateDirectory(target);Directory.CreateDirectory(backup);
            string[] files=Directory.GetFiles(source,"*",SearchOption.AllDirectories);var written=new List<string>();var existing=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string file in files) {
                string relative=file.Substring(source.Length+1);string destination=Child(target,relative);
                if(File.Exists(destination)) {
                    using(File.Open(destination,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
                    string save=Child(backup,relative);Directory.CreateDirectory(Path.GetDirectoryName(save));File.Copy(destination,save);existing.Add(relative);
                }
            }
            try {
                foreach(string file in files) {
                    string relative=file.Substring(source.Length+1),destination=Child(target,relative);Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    written.Add(relative);File.Copy(file,destination,true);
                    if(Hash(file)!=Hash(destination))throw new IOException("文件复制校验失败："+relative);
                }
            }catch {
                foreach(string relative in written.AsEnumerable().Reverse()) {
                    string destination=Child(target,relative);
                    if(existing.Contains(relative))File.Copy(Child(backup,relative),destination,true);else if(File.Exists(destination))File.Delete(destination);
                }
                throw;
            }
        }
        internal static string InstallDriver(string target,Action<string> progress) {
            var state=ReadPawnState();if(!NeedsDriver(state))return state.Label+(state.Running?"，无需重复安装。":"。请重启电脑后再核对温度。");
            string installer=Child(target,@"驱动\PawnIO_setup.exe");
            if(Hash(installer)!=DriverHash)throw new IOException("内置 PawnIO 安装器校验失败。");
            progress("正在安装官方 PawnIO 温度支持…");
            var start=new ProcessStartInfo(installer,"-install -silent"){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(installer)};
            using(var process=Process.Start(start)) {
                process.WaitForExit();int code=process.ExitCode;
                if(code==3010)return "PawnIO 安装完成，需要重启电脑。";
                if(code!=0)throw new IOException("PawnIO 安装未完成，返回码 "+code+"。可从程序目录“驱动”文件夹重试。");
            }
            var after=ReadPawnState();if(NeedsDriver(after))throw new IOException("PawnIO 安装后未检测到有效版本，请重启或手动重试。");
            return after.Running?"PawnIO 已安装并正在运行。":"PawnIO 已安装，请重启电脑后再核对温度。";
        }
        internal static void CreateShortcut(string target) {
            Type type=Type.GetTypeFromProgID("WScript.Shell");dynamic shell=Activator.CreateInstance(type);dynamic link=null;
            try {
                string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),ProductName+".lnk");
                // A desktop shortcut requests elevation so supported CPU/disk temperatures work on launch.
                if(File.Exists(path)) {
                    link=shell.CreateShortcut(path);string previous=(string)link.TargetPath;
                    if(!string.Equals(previous,Child(target,"QingMonitor.exe"),StringComparison.OrdinalIgnoreCase))throw new IOException("桌面已有同名快捷方式，已保留。可从程序目录启动。");
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);link=null;
                }
                link=shell.CreateShortcut(path);link.TargetPath=Child(target,"QingMonitor.exe");link.WorkingDirectory=target;link.Description=ProductName;link.IconLocation=Child(target,"QingMonitor.exe")+",0";link.Save();
                byte[] data=File.ReadAllBytes(path);if(data.Length<0x4c||BitConverter.ToInt32(data,0)!=0x4c)throw new IOException("快捷方式格式无效。");
                uint flags=BitConverter.ToUInt32(data,0x14)|0x2000;Buffer.BlockCopy(BitConverter.GetBytes(flags),0,data,0x14,4);File.WriteAllBytes(path,data);
            }finally {
                if(link!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
        }
        // Only clean the uniquely created working directory; never traverse junctions or target the install folder.
        internal static void CleanWork(string work,string parent) {
            string path=Path.GetFullPath(work),prefix=Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(path).StartsWith(".QingMonitor-setup-",StringComparison.Ordinal))throw new IOException("安装暂存路径无效。");
            if(!Directory.Exists(path))return;RejectLinks(path);
            foreach(string file in Directory.GetFiles(path,"*",SearchOption.AllDirectories)) {RejectLinks(Path.GetDirectoryName(file));if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("暂存文件是链接。");File.Delete(file);}
            foreach(string directory in Directory.GetDirectories(path,"*",SearchOption.AllDirectories).OrderByDescending(p=>p.Length)) {RejectLinks(directory);Directory.Delete(directory);}
            Directory.Delete(path);
        }
    }
}
