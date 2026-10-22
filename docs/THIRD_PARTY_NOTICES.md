# QingMonitor 0.1.11 · 第三方声明

核对日期：2026-10-09。适用范围：本版本 Windows x64 程序、离线安装包及源码分享包。

本项目自行编写的程序及源码采用 MIT 许可，项目自身的 LICENSE 随包提供。下列第三方文件保留各自的原许可与版权声明；项目 MIT 许可不替代这些文件的许可。下列 9 个 DLL 和 PresentMon 控制台工具均以未修改的上游二进制文件分发。

## 分发文件、版本及许可

| 随包文件 | 实际版本 | 原许可 | 版权及署名 |
| --- | --- | --- | --- |
| LibreHardwareMonitorLib.dll | 0.9.6，提交 3d331e3370efb858411f19511373eff65a218701 | MPL-2.0；其嵌入的 PawnIO.Modules 另见下文 LGPL-2.1 声明 | LibreHardwareMonitor and Contributors；原始源码中的其他版权行予以保留 |
| DiskInfoToolkit.dll | 1.1.2，提交 25319eae5781e75bcf141e844ceab2afe94d40ea | MPL-2.0 | Florian K. |
| BlackSharp.Core.dll | 1.0.7，提交 c70b735c6cec123ee8a046ac4a0bc6c606f52cf0 | MPL-2.0 | Florian K.；上游 LICENSE 原文为 Copyright (c) 2025 Florian K. |
| RAMSPDToolkit-NDD.dll | 1.4.2，提交 3b47b960e0830fef344624ad5e389675d5f0a1ce | MPL-2.0 | Florian K. |
| HidSharp.dll | 2.6.4 | Apache-2.0 | Copyright 2010-2025 James F. Bellinger |
| System.Memory.dll | 4.6.3，提交 f62ca0009b038cab4725a720f386623a969d73ad | MIT | © Microsoft Corporation. All rights reserved.；许可正文署名为 .NET Foundation and Contributors |
| System.Buffers.dll | 4.6.1，提交 6b84308c9ad012f53240d72c1d716d7e42546483 | MIT | 同上 |
| System.Numerics.Vectors.dll | 4.6.1，提交 6b84308c9ad012f53240d72c1d716d7e42546483 | MIT | 同上 |
| System.Runtime.CompilerServices.Unsafe.dll | 6.1.2，提交 f62ca0009b038cab4725a720f386623a969d73ad | MIT | 同上 |
| PresentMon.exe | 官方 PresentMon-2.6.0-x64.exe，文件名调整后内容未改 | MIT | Copyright (C) 2017-2024 Intel Corporation |

DLL 版本及版权依据为实际文件的 ProductVersion、LegalCopyright 和相应 NuGet 元数据；PresentMon 依据为官方 v2.6.0 发布文件及原许可。所分发 PresentMon.exe 的 Authenticode 签名核验为 Valid，签名者为 Intel Corporation。

系统现有 .NET Framework 提供的 System.Windows.Forms、System.Drawing、System.Management 等程序集不复制到本程序包。本表不列开发缓存和未采用的 GUI 依赖。

## MPL-2.0 文件的对应源码

本版本未修改以下 MPL 文件。接收者可从这些固定提交取得对应的完整上游源码；原始源码及其中的版权、许可说明继续适用 MPL-2.0。

| 文件 | 固定源码位置 | 对应源码 ZIP |
| --- | --- | --- |
| LibreHardwareMonitorLib.dll | https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/3d331e3370efb858411f19511373eff65a218701 | https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/archive/3d331e3370efb858411f19511373eff65a218701.zip |
| DiskInfoToolkit.dll | https://github.com/Blacktempel/DiskInfoToolkit/tree/25319eae5781e75bcf141e844ceab2afe94d40ea | https://github.com/Blacktempel/DiskInfoToolkit/archive/25319eae5781e75bcf141e844ceab2afe94d40ea.zip |
| BlackSharp.Core.dll | https://github.com/Blacktempel/BlackSharp/tree/c70b735c6cec123ee8a046ac4a0bc6c606f52cf0 | https://github.com/Blacktempel/BlackSharp/archive/c70b735c6cec123ee8a046ac4a0bc6c606f52cf0.zip |
| RAMSPDToolkit-NDD.dll | https://github.com/Blacktempel/RAMSPDToolkit/tree/3b47b960e0830fef344624ad5e389675d5f0a1ce | https://github.com/Blacktempel/RAMSPDToolkit/archive/3b47b960e0830fef344624ad5e389675d5f0a1ce.zip |

随包 `licenses/MPL-2.0.txt` 保留 MPL-2.0 完整正文。许可证的固定上游位置为各上述提交的根目录 LICENSE；LibreHardwareMonitor 对应源码中的文件版权声明，包括其历史贡献者署名，按原样保留。

## LibreHardwareMonitor 内嵌的 PawnIO.Modules

LibreHardwareMonitorLib.dll 内包含 PawnIO.Modules 的已签名模块资源。此版本的上游资源 README 明确标注模块发布版本为 0.1.6，对应源码提交为 `4aa792beb020a14c8261072e9786d2dfb38489d9`。上游 THIRD-PARTY-NOTICES 将这些模块列为 GNU Lesser General Public License 2.1（LGPL-2.1）。模块项目及其原始贡献者的版权说明在随包完整源码中保留。

- 模块原项目及固定源码：https://github.com/namazso/PawnIO.Modules/tree/4aa792beb020a14c8261072e9786d2dfb38489d9
- 原始模块发布：https://github.com/namazso/PawnIO.Modules/releases/tag/0.1.6
- 完整对应源码归档：https://github.com/namazso/PawnIO.Modules/archive/refs/tags/0.1.6.zip
- LHM 对模块版本的原始说明：https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/3d331e3370efb858411f19511373eff65a218701/LibreHardwareMonitorLib/Resources/PawnIo/README
- LHM 原始第三方声明：https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/3d331e3370efb858411f19511373eff65a218701/THIRD-PARTY-NOTICES.txt

程序包与源码分享包均保留 `licenses/LGPL-2.1.txt`、`licenses/LHM-THIRD-PARTY-NOTICES.txt` 和 `licenses/PawnIO.Modules-0.1.6-source.zip`，提供完整许可、上游声明及模块对应源码。模块源码归档内也保留原始 COPYING 文件。

本程序通过独立的 LibreHardwareMonitorLib.dll 使用硬件库。应用自身没有对该 DLL 设置签名或校验值锁定；接收者可替换兼容 DLL，或从固定上游源码重新构建库，并使用随包的应用源码与构建脚本重新编译。这里的 SHA256 仅用于核对所分发文件。

保留的 LHM 原始第三方声明还列有 Aga.Controls 的 BSD 声明；它属于原上游项目的 GUI 组件。本程序包未包含 Aga.Controls，也未引入该 GUI 组件代码。

## HidSharp 的原始署名与源码

原始 NOTICE 的内容保留如下，并同样存在于 `licenses/HidSharp-LICENSE.txt` 开头：

> HIDSharp
> Copyright 2010-2025 James F. Bellinger <http://software.seekye.com/hidsharp>

`licenses/HidSharp-LICENSE.txt` 保留官方 2.6.4 发行中的版权说明及 Apache-2.0 条款；`licenses/Apache-2.0.txt` 提供完整 Apache-2.0 正文。

- 官方项目及 2.6.4 下载说明：https://software.seekye.com/hidsharp
- 2.6.4 官方归档：https://software.seekye.com/files/oss/hidsharp/HIDSharp264.zip
- 作者公布的源代码仓库：https://github.com/SeekHisKingdom/HIDSharp
- 对应 NuGet 元数据：https://www.nuget.org/packages/HidSharp/2.6.4

## Microsoft MIT 程序集的源码

上述四个 Microsoft NuGet 包的 repository 元数据均指向 `dotnet/maintenance-packages`。版本与对应提交如下；包声明为 MIT，原始 DLL 版权与许可正文中的署名均保留。

- System.Memory 4.6.3 与 System.Runtime.CompilerServices.Unsafe 6.1.2：
  https://github.com/dotnet/maintenance-packages/tree/f62ca0009b038cab4725a720f386623a969d73ad
  https://github.com/dotnet/maintenance-packages/archive/f62ca0009b038cab4725a720f386623a969d73ad.zip
- System.Buffers 4.6.1 与 System.Numerics.Vectors 4.6.1：
  https://github.com/dotnet/maintenance-packages/tree/6b84308c9ad012f53240d72c1d716d7e42546483
  https://github.com/dotnet/maintenance-packages/archive/6b84308c9ad012f53240d72c1d716d7e42546483.zip

两个提交的原始许可文件均为根目录 LICENSE。`licenses/Microsoft-MIT.txt` 保留 MIT 完整正文及 `Copyright (c) .NET Foundation and Contributors` 署名；各 DLL 的 `© Microsoft Corporation. All rights reserved.` 原始文件元数据保持不变。

## PresentMon 的许可与源码

本版本将官方 `PresentMon-2.6.0-x64.exe` 按字节原样复制为 `PresentMon.exe`，作为独立采集进程调用。`licenses/PresentMon-MIT.txt` 保留 Intel 的原始 MIT 许可及版权说明。

- 官方发布：https://github.com/GameTechDev/PresentMon/releases/tag/v2.6.0
- 对应源码提交：https://github.com/GameTechDev/PresentMon/tree/e13fce6acdb55a808fd8318175a56863e532d95f
- 完整对应源码：https://github.com/GameTechDev/PresentMon/archive/e13fce6acdb55a808fd8318175a56863e532d95f.zip
- 该提交原始许可：https://github.com/GameTechDev/PresentMon/blob/e13fce6acdb55a808fd8318175a56863e532d95f/LICENSE.txt

## 随包 PawnIO 官方安装器

离线安装包和便携分享包包含字节内容未修改的官方签名 PawnIO 2.2.0 安装器，由 namazso 发布。官方二进制版本为专有许可，官方集成说明明确允许分发安装器；这与其 GPL/LGPL 源码版本及上文的 LGPL 模块分别许可。项目自身 MIT 许可不覆盖官方二进制安装器。

- 再分发条件：https://github.com/namazso/PawnIO.Modules/wiki/Using-PawnIO-Modules
- 固定发行：https://github.com/namazso/PawnIO.Setup/releases/tag/2.2.0
- 原始下载：https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe
- 官方主页：https://pawnio.eu/
- SHA256：`1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032`
- 构建时 Authenticode 为 Valid，签名者 namazso.eu。

用户在离线安装器勾选温度支持时，通过官方安装器 `-install -silent` 安装正式签名版本，已安装兼容版本时跳过。安装需要管理员权限，可返回需要重启的成功状态。应用本体不更改驱动访问权限或 Windows 安全设置；应用退出、移动、删除不移除共享 PawnIO 驱动。

`驱动/说明.txt` 和 `licenses/PawnIO-redistribution.txt` 保留版本、来源、哈希及分发说明。

## 分发文件 SHA256

| 文件 | SHA256 |
| --- | --- |
| LibreHardwareMonitorLib.dll | 6EBC194316536BA61AF5BE24508AD9FCBB2ECC685E716C12E787C79530F66BF0 |
| DiskInfoToolkit.dll | 1ACBF51B3C10C51C986CF43021680D34A2E38D9A5BA652BCFA9A1B5F7FC09800 |
| BlackSharp.Core.dll | CAFB93AFCC8D8A367E21F619673D05C06887D8964867FED1371F02DED1CD3E23 |
| RAMSPDToolkit-NDD.dll | B6882354C7C8EC186617E421507743DBFAE09C5C1FC24CEF76A1D0C0C26651DE |
| HidSharp.dll | D86690EFDE30EA9179F669320F39148853793B743A98B531AFEAF30598E22F54 |
| System.Memory.dll | D5E8E4866F9CFA66F7765660F84B210198893E55335487AFE5EBDA342C0E913D |
| System.Buffers.dll | 2D78D770C9CB997199154AE8C018B9F1D1EFBC86729F7264DDE6DBAD2A12CAC3 |
| System.Numerics.Vectors.dll | 20C2FA81B8C70D651099D762954F285FD4F942E63B2D7217C145DAB8D4B2F4C9 |
| System.Runtime.CompilerServices.Unsafe.dll | 08CBD7278B66F1E68425A82D4B97181A4130D93E3DD91831407ABA7212CCDACF |
| PresentMon.exe | B2A706BC6AD475749E3B7E3409263AA1E6906D45BDCF993F6DBC0F660188F1AF |
| licenses/PawnIO.Modules-0.1.6-source.zip | 665B2F7A028F8A317BC35CD6FBF64986115909239019FF2D30D19260D7DD0982 |

再次分发本版本时，请保留本声明、licenses 目录中的完整许可及模块源码归档，以及项目自身的 LICENSE。若更换依赖版本或修改其源码，应相应更新版本、源码位置、变更说明和文件摘要。
