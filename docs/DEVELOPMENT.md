# 开发与构建

## 环境

- Windows x64，.NET Framework 4.8 或更新版本。
- PowerShell 7（`pwsh`）。
- Windows 自带的 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`。

当前源码采用 C# 5 / WinForms，无 NuGet 联网还原、WebView 或 .NET SDK 构建依赖。`vendor/runtime` 保留本版本所用的原始依赖、许可证及驱动安装器，使完整克隆后的构建可离线进行。

```powershell
git clone https://github.com/NINETEEN1120/QingMonitor.git
cd QingMonitor
pwsh -File ./源码/build.ps1 -OutputDirectory ./artifacts/app
pwsh -File ./scripts/Test.ps1
pwsh -File ./源码/build-setup.ps1 -AppDirectory ./artifacts/app -OutputDirectory ./artifacts/setup
```

应用位于 `artifacts/app/QingMonitor.exe`，离线安装包位于 `artifacts/setup`。构建安装包时会验证内置 PawnIO 安装器的签名与哈希；离线环境中的 Windows 签名信任链状态可能影响该检查。构建不会安装驱动、启动采集或修改系统权限。

## 目录

| 目录 | 用途 |
| --- | --- |
| `源码/` | 应用、原生窗口互操作、安装器与构建脚本 |
| `tests/` | 可重复的配置、布局和控件检查，数据为合成样例 |
| `scripts/Test.ps1` | 不启动硬件采集的默认回归入口 |
| `vendor/runtime/` | 固定版本的第三方动态库、工具、驱动安装器与许可 |
| `vendor/manifest.json` | 分发依赖的 SHA-256 清单 |
| `docs/` | 用户说明、截图、路线图与第三方声明 |
| `.github/` | Windows 构建检查与 Issue 模板 |

## 组件

`Core` 负责配置和快照；`Collectors` 使用一个后台调度器按组采集；`FpsSource` 在独立开关开启时管理 PresentMon；`Overlay` 负责两行布局、图表和窗口恢复；`SettingsForm` 与 `SettingsShell` 负责设置草稿和预览。

设置预览不调用采集重配或写盘。确认后一次提交；取消、关闭或异常恢复原配置、位置和交互状态。后台样本通过快照传回 UI。新增功能应继续区分“显示指标”与“启动 FPS 采集”。

## 测试边界

默认检查覆盖纯几何、配置兼容、滑块档位、无效导入恢复、温度颜色和布局。GitHub Actions 使用 Windows runner 构建应用与安装包、运行这些检查并提供构建产物。CI 不代表温度传感器、内核驱动、真实游戏、混合 DPI 或其他电脑的验收。

本地需要测试传感器时，使用一份临时数据目录，并只对自己创建的实例做操作。不要自动关闭用户的监控程序或共用采集会话，也不要绕过 UAC。

## 配置与隐私

常规配置位于 `%LOCALAPPDATA%\QingMonitor`。可在程序旁放置仅含绝对目录路径的 `QingMonitor.data-path` 改用指定目录。这个标记、个人配置、日志、传感器清单和本地测试结果不进入仓库或发布包。

## 依赖与发布

升级第三方依赖时，核对上游发行、许可及对应源码，更新 `docs/THIRD_PARTY_NOTICES.md`、随包许可证和 `vendor/manifest.json`。应用自身 MIT 不覆盖这些第三方文件。库保持动态可替换，不通过应用内校验禁止用户换用兼容库；构建清单只用于核对官方分发输入。

发布前需从干净克隆构建、检查 ZIP 内容、验证本机启动与正常退出，再进行人工设备验证。公开 Release 的版本标签与程序版本需一致。未来签名、自动更新与安装体验改进见 [路线图](ROADMAP.md)。
