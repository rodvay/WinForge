<p align="center">
  <img src="assets/banner-zh.png" alt="WinForge — 安全透明、100% 可还原的 Windows 装机优化与维护工具箱" width="100%" />
</p>

<h1 align="center">WinForge — Windows 装机优化与维护工具箱</h1>

<p align="center">
  <strong>仅约 180 KB、单文件免安装的原生 Windows 10/11 工具箱（灵感源自 Chris Titus WinUtil）：集 WinGet 批量装机、21 项实时检测且 100% 可还原的系统优化、微软官方更新策略控制、系统修复以及可选「高级选项」工具于一体。</strong>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/系统支持-Windows%2010%20%2F%2011-0078D6?style=for-the-badge&logo=windows&logoColor=white" alt="Windows 10 / 11" />
  <img src="https://img.shields.io/badge/体积-180%20KB%20单文件%20EXE-34D399?style=for-the-badge" alt="180 KB 单文件 EXE" />
  <img src="https://img.shields.io/badge/界面语言-简体中文%20%7C%20EN%20%7C%20TR-38BDF8?style=for-the-badge" alt="简体中文 英语 土耳其语" />
  <img src="https://img.shields.io/badge/开源协议-MIT-F59E0B?style=for-the-badge" alt="MIT 开源协议" />
  <a href="https://linux.do"><img src="https://img.shields.io/badge/开源社区-LINUX%20DO-1C1C1E?style=for-the-badge&logo=linux&logoColor=38BDF8" alt="LINUX DO 社区" /></a>
</p>

<p align="center">
  <a href="README.md"><b>English</b></a> &nbsp;·&nbsp; <b>简体中文</b> &nbsp;·&nbsp; <a href="README.md#türkçe"><b>Türkçe</b></a> &nbsp;·&nbsp; <a href="https://github.com/rodvay/WinForge/releases/latest"><b>下载最新版本</b></a>
</p>

---

## 核心特色

- **纯原生编译单文件 EXE**：无需运行 PowerShell 在线脚本，彻底告别 ExecutionPolicy 执行策略报错与网络拉取失败，启动不到 100 毫秒。
- **全部优化实时读取、自动备份、100% 可还原**：打开即读取系统真实状态，每项清晰标出对应注册表路径；应用任何更改前自动保存快照备份至 `%APPDATA%\WinForge\backups\`，随时一键全部恢复微软默认。
- **独立可选的「高级选项」标签（强制二次确认 + 一键还原）**：为有进阶需求的用户提供可选的 Defender 策略控制（自动检测防篡改保护，绝不暴力绕过）、保留 WebView2 卸载/重装 Edge、保留 WinGet 卸载/重装微软商店，以及先自动导出标准 `.reg` 备份再清理的 HKCU 安全注册表清理。**不会**随“一键推荐配置”自动启用。
- **系统语言智能自动适配**：中文 Windows 自动以**简体中文**启动，土耳其语系统自动以土耳其语启动，其余语言默认英文，右上角 **`TR | EN | 中文`** 可随时切换。

---

## 五大功能模块

### 1. 软件安装（WinGet）
- 内置 **36 款经实测校验的官方 WinGet 软件包**（浏览器、开发工具、系统工具、影音创作、通讯与密码管理、游戏与运行库）。
- 支持实时搜索过滤、检测本机已安装状态、静默批量安装（带实时流式控制台日志与“停止”中断）以及一键 **全部软件升级 (`winget upgrade --all`)**。

### 2. 系统优化（21 项可还原开关）
- **界面与资源管理器（10 项 HKCU，无需管理员权限）**：显示文件扩展名、显示隐藏文件、恢复 Win11 经典右键菜单、深色模式、关闭开始菜单 Bing 搜索、资源管理器默认打开「此电脑」、任务栏右键「结束任务」、任务栏左对齐、任务栏时钟显示秒数、关闭开始菜单与锁屏推广推荐。
- **隐私与遥测（6 项）**：禁用广告 ID、关闭诊断个性化体验、隐藏任务栏 Copilot 按钮、遥测降至最低必需级别 (`AllowTelemetry = 1`)、关闭活动历史记录、阻止更新后静默安装推广应用。
- **性能与电源（5 项）**：开启游戏模式、1:1 关闭鼠标加速、开启硬件加速 GPU 计划 (HAGS)、高性能电源计划、台式机关闭休眠释放 `hiberfil.sys`。

### 3. 系统更新（Windows Update 官方策略）
- **Windows 默认策略**：清除自定义策略，恢复微软原生更新体验。
- **安全优先模式（推荐）**：正常接收安全补丁，同时将大版本功能更新延迟 **365 天**、质量更新延迟 **4 天**，避免新版 Bug 与强制重启。
- **独立开关**：禁止 Windows Update 自动覆盖显卡/声卡驱动、禁止登录期间强制自动重启。

### 4. 修复与工具
- 内置微软官方维护命令（带实时控制台输出）：**创建系统还原点**、**`sfc /scannow`**、**`DISM /RestoreHealth`**、**DNS 刷新与 Winsock 重置**、**安全重启资源管理器**。
- 10 个经典 Windows 管理面板一键直达（控制面板、程序和功能、网络连接、声音设备、电源选项、系统属性、设备管理器、系统服务、磁盘管理、开机启动项）。

### 5. 高级选项（可选工具 · 二次确认 · 均可还原）
- **Windows Defender 实时保护策略**：实时检测保护状态与 **篡改保护 (Tamper Protection)**；使用微软官方组策略与 `Set-MpPreference` 管理（绝不绕过安全边界，支持一键直达安全中心与一键恢复 Defender）。
- **Microsoft Edge 浏览器**：调用官方安装程序 / WinGet 卸载或重装 Edge 浏览器，**完整保留 Edge WebView2 运行时**，不影响其他软件。
- **Microsoft Store 应用商店**：通过官方 Appx 命令卸载，或通过微软内置 **`wsreset -i`** 一键重装；完整保留 WinGet。
- **安全注册表清理（自动 `.reg` 备份）**：只读扫描本地固定磁盘的 `MuiCache`、`AppCompatFlags`、`FeatureUsage\AppSwitched` 失效程序残留及 `RunMRU / TypedPaths` 历史；绝不触碰 `CLSID`、`System32`、驱动或未挂载外置盘；清理前自动导出可双击 100% 还原的 UTF-16 LE `.reg` 备份至 `%APPDATA%\WinForge\backups\`。

---

## 界面预览

<table>
  <tr>
    <td align="center" width="50%">
      <img src="assets/screenshot-tweaks-zh.png" alt="WinForge 简体中文系统优化界面" width="100%" /><br />
      <sub><b>简体中文系统优化界面</b> — 实时读取状态、展示注册表路径</sub>
    </td>
    <td align="center" width="50%">
      <img src="assets/screenshot-advanced-zh.png" alt="WinForge 简体中文高级选项界面" width="100%" /><br />
      <sub><b>高级选项（可选）</b> — Defender / Edge / 商店与自动 .reg 备份清理</sub>
    </td>
  </tr>
  <tr>
    <td align="center" width="50%">
      <img src="assets/screenshot-apps-en.png" alt="WinGet 软件批量安装" width="100%" /><br />
      <sub><b>WinGet 软件安装</b> — 36 款官方校验包与实时搜索</sub>
    </td>
    <td align="center" width="50%">
      <img src="assets/screenshot-updates-en.png" alt="Windows 更新策略" width="100%" /><br />
      <sub><b>Windows 更新策略</b> — 安全延迟与驱动保护</sub>
    </td>
  </tr>
</table>

---

## 下载与源码编译

1. 前往 **[Releases 发布页](https://github.com/rodvay/WinForge/releases/latest)** 下载 **`WinForge.exe`**（仅约 180 KB），双击即用。
2. 自行编译（仅需 Windows 10/11 自带 `.NET Framework 4.x` 编译器，无需 Visual Studio）：

```bat
git clone https://github.com/rodvay/WinForge.git
cd WinForge
build.bat
```

## 社区与友情链接

- 感谢 **[LINUX DO](https://linux.do)** 社区的开源分享氛围与各位佬友的支持与反馈！

## 开源协议

基于 **MIT License** 开源 — 详情见 [LICENSE](LICENSE)。
