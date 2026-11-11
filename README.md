# Codex 代理管理器 2.6

Windows 桌面代理监测与管理工具，使用 C#、WinForms 和 .NET Framework 开发。可查看代理连接、流量和路线状态；可配置桌面浮标与节点恢复。功能依赖本地代理环境及兼容的 Mihomo 控制接口；自动恢复需自行配置。程序本身不提供代理服务或订阅。

## 下载安装

从本仓库的 Releases 下载 `CodexProxyManager-2.6-Windows-x64.zip`，解压后双击 `安装.cmd`。安装程序会将主程序复制到 `%USERPROFILE%\CodexProxyDesktop` 并创建桌面快捷方式。升级前请从托盘退出管理器；安装保留已有设置和记录，不修改自启动设置，也不自动启动软件。安装完成后可通过快捷方式打开。

程序为未签名版本，Windows 可能显示安全提示；可使用 Release 中的 SHA-256 校验文件检查下载是否完整。卸载可使用程序内的卸载功能；重要的配置和诊断记录建议先自行备份。

## 从源码构建

Windows x64，.NET Framework 4.x；在项目目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

产物位于 `build/CodexProxyDesktop.exe`。运行 `build/CodexProxyDesktop.exe --test21` 可执行隔离回归测试。源码随程序作为资源嵌入。安装包脚本位于 `installer`。

## 使用说明和限制

代理识别、连接情况和自动恢复效果取决于本机配置与网络环境。部分规则或路线无法确认时程序会保持不确定状态。网络探测响应不代表具体对话或任务已完成。真实断网、长期稳定性和所有代理配置未全面验证。开启恢复功能前请检查设置，相关策略组可能由其他程序共享。
