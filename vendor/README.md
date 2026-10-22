# 固定构建依赖

`runtime/` 中的第三方 DLL、PresentMon 与 PawnIO 安装器保持上游原始字节。本仓库保留这些输入用于完整克隆后的离线构建；不是本项目自行实现或重新授予 MIT 许可的组件。

- 文件版本、许可、源码位置和原始署名：[第三方声明](../docs/THIRD_PARTY_NOTICES.md)。
- 完整许可及 PawnIO.Modules 对应源码归档：`runtime/licenses/`。
- SHA-256：`manifest.json`，构建应用时核对。
- 官方驱动安装器：`runtime/驱动/PawnIO_setup.exe`，构建不会执行或安装它。驱动的官方二进制许可与源码许可不同。

请不要提交个人数据目录标记、编译出的 QingMonitor.exe 或系统安装后的驱动文件。
