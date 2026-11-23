using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Win32;

namespace WinForge
{
    internal sealed class UpdatePolicyState
    {
        public bool IsSecurityDeferred; // Feature 365d + Quality 4d
        public bool ExcludeDrivers;
        public bool PreventAutoReboot;
        public int FeatureDeferDays;
        public int QualityDeferDays;
    }

    internal sealed class RepairActionItem
    {
        public string Id;
        public string NameEn;
        public string NameTr;
        public string NameZh;
        public string DescEn;
        public string DescTr;
        public string DescZh;
        public string CommandHint;
        public bool RequiresAdmin;
        public Action<Action<string>, Func<bool>> Execute;

        public string GetName(string lang) { return Loc.T(lang, NameEn, NameTr, NameZh); }
        public string GetDesc(string lang) { return Loc.T(lang, DescEn, DescTr, DescZh); }
    }

    internal sealed class WinFeatureItem
    {
        public string Id;
        public string NameEn;
        public string NameTr;
        public string NameZh;
        public string DescEn;
        public string DescTr;
        public string DescZh;
        public string FeatureHint;
        public Action<Action<string>, Func<bool>> EnableAction;
        public Action<Action<string>, Func<bool>> DisableAction;

        public string GetName(string lang) { return Loc.T(lang, NameEn, NameTr, NameZh); }
        public string GetDesc(string lang) { return Loc.T(lang, DescEn, DescTr, DescZh); }
    }

    internal sealed class ControlPanelShortcut
    {
        public string NameEn;
        public string NameTr;
        public string NameZh;
        public string Target;
        public string Args;

        public ControlPanelShortcut(string en, string tr, string zh, string target, string args)
        {
            NameEn = en; NameTr = tr; NameZh = zh; Target = target; Args = args;
        }

        public string GetName(string lang) { return Loc.T(lang, NameEn, NameTr, NameZh); }
    }

    internal static class UpdatesAndRepairEngine
    {
        private const string WuKey = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
        private const string WuAuKey = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";

        public static UpdatePolicyState ReadUpdatePolicy()
        {
            UpdatePolicyState s = new UpdatePolicyState();
            s.FeatureDeferDays = RegHelper.ReadDword(Registry.LocalMachine, WuKey, "DeferFeatureUpdatesPeriodInDays", 0);
            s.QualityDeferDays = RegHelper.ReadDword(Registry.LocalMachine, WuKey, "DeferQualityUpdatesPeriodInDays", 0);
            s.IsSecurityDeferred = RegHelper.ReadDword(Registry.LocalMachine, WuKey, "DeferFeatureUpdates", 0) == 1 && s.FeatureDeferDays >= 30;
            s.ExcludeDrivers = RegHelper.ReadDword(Registry.LocalMachine, WuKey, "ExcludeWUDriversInQualityUpdate", 0) == 1;
            s.PreventAutoReboot = RegHelper.ReadDword(Registry.LocalMachine, WuAuKey, "NoAutoRebootWithLoggedOnUsers", 0) == 1;
            return s;
        }

        public static bool ApplyDefaultUpdates(Action<string> log)
        {
            bool ok = true;
            ok &= RegHelper.DeleteValueSafe(Registry.LocalMachine, WuKey, "DeferFeatureUpdates");
            ok &= RegHelper.DeleteValueSafe(Registry.LocalMachine, WuKey, "DeferFeatureUpdatesPeriodInDays");
            ok &= RegHelper.DeleteValueSafe(Registry.LocalMachine, WuKey, "DeferQualityUpdates");
            ok &= RegHelper.DeleteValueSafe(Registry.LocalMachine, WuKey, "DeferQualityUpdatesPeriodInDays");
            ok &= RegHelper.DeleteValueSafe(Registry.LocalMachine, WuKey, "ExcludeWUDriversInQualityUpdate");
            ok &= RegHelper.DeleteValueSafe(Registry.LocalMachine, WuAuKey, "NoAutoRebootWithLoggedOnUsers");
            if (log != null) log(ok ? "[OK] Windows Update restored to Microsoft defaults." : "[ERROR] Administrator privileges required.");
            return ok;
        }

        public static bool ApplySecurityFocusedUpdates(Action<string> log)
        {
            bool ok = true;
            ok &= RegHelper.WriteDword(Registry.LocalMachine, WuKey, "DeferFeatureUpdates", 1);
            ok &= RegHelper.WriteDword(Registry.LocalMachine, WuKey, "DeferFeatureUpdatesPeriodInDays", 365);
            ok &= RegHelper.WriteDword(Registry.LocalMachine, WuKey, "DeferQualityUpdates", 1);
            ok &= RegHelper.WriteDword(Registry.LocalMachine, WuKey, "DeferQualityUpdatesPeriodInDays", 4);
            ok &= RegHelper.WriteDword(Registry.LocalMachine, WuAuKey, "NoAutoRebootWithLoggedOnUsers", 1);
            if (log != null) log(ok ? "[OK] Security-Focused Update policy applied (Feature +365d, Quality +4d, No forced reboot)." : "[ERROR] Administrator privileges required.");
            return ok;
        }

        public static bool SetExcludeDrivers(bool exclude, Action<string> log)
        {
            bool ok = exclude
                ? RegHelper.WriteDword(Registry.LocalMachine, WuKey, "ExcludeWUDriversInQualityUpdate", 1)
                : RegHelper.DeleteValueSafe(Registry.LocalMachine, WuKey, "ExcludeWUDriversInQualityUpdate");
            if (log != null) log(ok ? (exclude ? "[OK] Driver updates excluded from Windows Update." : "[OK] Driver updates restored to Windows default.") : "[ERROR] Administrator privileges required.");
            return ok;
        }

        public static bool SetPreventAutoReboot(bool prevent, Action<string> log)
        {
            bool ok = prevent
                ? RegHelper.WriteDword(Registry.LocalMachine, WuAuKey, "NoAutoRebootWithLoggedOnUsers", 1)
                : RegHelper.DeleteValueSafe(Registry.LocalMachine, WuAuKey, "NoAutoRebootWithLoggedOnUsers");
            if (log != null) log(ok ? (prevent ? "[OK] Forced auto-reboot while signed in disabled." : "[OK] Auto-reboot policy restored to default.") : "[ERROR] Administrator privileges required.");
            return ok;
        }

        public static List<RepairActionItem> BuildRepairActions()
        {
            List<RepairActionItem> list = new List<RepairActionItem>();

            list.Add(MakeRepair(
                "restore_point", true,
                "Create System Restore Point", "Sistem Geri Yükleme Noktası Oluştur", "创建系统还原点",
                "Creates a native Windows Restore Point before making major system changes.",
                "Büyük değişiklikler öncesi resmi Windows Sistem Geri Yükleme Noktası oluşturur.",
                "在进行系统优化前创建原生 Windows 系统还原点，随时可安全回滚。",
                "Checkpoint-Computer -Description 'WinForge Restore Point'",
                delegate(Action<string> log, Func<bool> cancel)
                {
                    if (log != null) log("Creating Windows System Restore Point...");
                    int rc = WinNative.RunProcessStreaming(
                        "powershell.exe",
                        "-NoProfile -ExecutionPolicy Bypass -Command \"Enable-ComputerRestore -Drive ($env:SystemDrive + '\\') -ErrorAction SilentlyContinue; Checkpoint-Computer -Description 'WinForge Restore Point' -RestorePointType 'MODIFY_SETTINGS'\"",
                        log, cancel);
                    if (log != null) log(rc == 0 ? "[DONE] System Restore Point created successfully." : "[NOTE] Restore Point creation finished (code " + rc + "). Windows limits 1 restore point per 24h by default.");
                }));

            list.Add(MakeRepair(
                "sfc_scan", true,
                "System File Checker (SFC /scannow)", "Sistem Dosyası Denetleyicisi (SFC)", "系统文件完整性检查 (SFC)",
                "Scans and repairs corrupted protected Windows OS files automatically.",
                "Bozuk veya eksik Windows sistem dosyalarını resmi olarak tarar ve onarır.",
                "调用微软官方 SFC 工具扫描并自动修复受损的受保护系统文件。",
                "sfc /scannow",
                delegate(Action<string> log, Func<bool> cancel)
                {
                    if (log != null) log("Running sfc /scannow (this may take a few minutes)...");
                    int rc = WinNative.RunProcessStreaming("sfc.exe", "/scannow", log, cancel);
                    if (log != null) log("[DONE] SFC completed (ExitCode=" + rc + ").");
                }));

            list.Add(MakeRepair(
                "dism_restore", true,
                "DISM Windows Image Repair", "DISM Sistem Görüntüsü Onarımı", "DISM 系统映像健康修复",
                "Checks and repairs the Windows component store using Microsoft servers.",
                "Windows bileşen deposunu (WinSxS) resmi DISM aracıyla denetler ve onarır.",
                "使用微软官方 DISM 工具检查并修复 Windows 组件存储映像。",
                "DISM /Online /Cleanup-Image /RestoreHealth",
                delegate(Action<string> log, Func<bool> cancel)
                {
                    if (log != null) log("Running DISM /Online /Cleanup-Image /RestoreHealth...");
                    int rc = WinNative.RunProcessStreaming("DISM.exe", "/Online /Cleanup-Image /RestoreHealth", log, cancel);
                    if (log != null) log("[DONE] DISM finished (ExitCode=" + rc + ").");
                }));

            list.Add(MakeRepair(
                "net_reset", true,
                "Flush DNS & Reset Network Stack", "DNS Temizle ve Ağ Yığınını Sıfırla", "刷新 DNS 缓存并重置网络协议栈",
                "Flushes DNS resolver cache and resets Winsock/IP stack to fix connection issues.",
                "DNS önbelleğini temizler, Winsock ve IP yapılandırmasını güvenli şekilde yeniler.",
                "清空 DNS 缓存并重置 Winsock/TCP-IP 协议栈，解决常见断网或网页打不开问题。",
                "ipconfig /flushdns + netsh winsock reset",
                delegate(Action<string> log, Func<bool> cancel)
                {
                    if (log != null) log("Flushing DNS cache...");
                    WinNative.RunProcessStreaming("ipconfig.exe", "/flushdns", log, cancel);
                    if (cancel != null && cancel()) return;
                    if (log != null) log("Resetting Winsock catalog...");
                    WinNative.RunProcessStreaming("netsh.exe", "winsock reset", log, cancel);
                    if (log != null) log("[DONE] Network stack refreshed. Reboot recommended if connection issues persist.");
                }));

            list.Add(MakeRepair(
                "wu_reset", true,
                "Reset Windows Update Components", "Windows Update Önbelleğini Sıfırla", "重置 Windows Update 更新缓存",
                "Stops update services, clears stuck SoftwareDistribution cache, and restarts services cleanly.",
                "Takılan Windows Update servislerini durdurur, SoftwareDistribution önbelleğini temizler ve yeniden başlatır.",
                "停止更新服务、清理卡死的 SoftwareDistribution 缓存目录并重新启动服务。",
                "net stop wuauserv/bits + SoftwareDistribution reset",
                delegate(Action<string> log, Func<bool> cancel)
                {
                    if (log != null) log("Stopping Windows Update services (wuauserv, bits, cryptsvc)...");
                    WinNative.RunProcessStreaming("net.exe", "stop wuauserv", log, cancel);
                    WinNative.RunProcessStreaming("net.exe", "stop bits", log, cancel);
                    WinNative.RunProcessStreaming("net.exe", "stop cryptsvc", log, cancel);
                    if (cancel != null && cancel()) return;

                    try
                    {
                        string sd = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution");
                        string sdBak = sd + ".bak_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                        if (System.IO.Directory.Exists(sd))
                        {
                            System.IO.Directory.Move(sd, sdBak);
                            if (log != null) log("Moved old SoftwareDistribution -> " + sdBak);
                        }
                    }
                    catch (Exception ex) { if (log != null) log("[NOTE] SoftwareDistribution rename skipped: " + ex.Message); }

                    if (log != null) log("Restarting Windows Update services...");
                    WinNative.RunProcessStreaming("net.exe", "start cryptsvc", log, cancel);
                    WinNative.RunProcessStreaming("net.exe", "start bits", log, cancel);
                    WinNative.RunProcessStreaming("net.exe", "start wuauserv", log, cancel);
                    if (log != null) log("[DONE] Windows Update components reset cleanly.");
                }));

            list.Add(MakeRepair(
                "winget_repair", false,
                "Repair / Re-Register WinGet", "WinGet Paket Yöneticisini Onar", "修复并重新注册 WinGet",
                "Re-registers Microsoft.DesktopAppInstaller (WinGet) and refreshes official sources.",
                "Microsoft.DesktopAppInstaller (WinGet) paketini yeniden kaydeder ve kaynakları tazeler.",
                "重新注册微软官方应用安装程序 (WinGet) 并重置软件源缓存。",
                "Add-AppxPackage -RegisterByFamilyName + winget source reset",
                delegate(Action<string> log, Func<bool> cancel)
                {
                    if (log != null) log("Re-registering Microsoft.DesktopAppInstaller...");
                    WinNative.RunProcessStreaming(
                        "powershell.exe",
                        "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe -ErrorAction SilentlyContinue\"",
                        log, cancel);
                    if (cancel != null && cancel()) return;
                    if (log != null) log("Refreshing WinGet sources...");
                    WinNative.RunProcessStreaming("winget.exe", "source update --accept-source-agreements", log, cancel);
                    if (log != null) log("[DONE] WinGet repair complete.");
                }));

            list.Add(MakeRepair(
                "ntp_sync", true,
                "Sync System Clock (NTP)", "Sistem Saatini Eşitle (NTP)", "强制同步网络标准时间 (NTP)",
                "Starts Windows Time service and forces an immediate NTP clock resync.",
                "Windows Saat servisini başlatıp sistem saatini internet sunucusuyla eşitler.",
                "启动 Windows 时间服务并立即向 NTP 服务器同步精确时间。",
                "net start w32time + w32tm /resync /force",
                delegate(Action<string> log, Func<bool> cancel)
                {
                    WinNative.RunProcessStreaming("net.exe", "start w32time", log, cancel);
                    WinNative.RunProcessStreaming("w32tm.exe", "/resync /force", log, cancel);
                    if (log != null) log("[DONE] NTP time resync requested.");
                }));

            list.Add(MakeRepair(
                "restart_explorer", false,
                "Restart Windows Explorer", "Windows Gezgini'ni Yeniden Başlat", "重启 Windows 资源管理器",
                "Cleanly restarts taskbar & desktop shell to apply UI changes immediately.",
                "Arayüz değişikliklerinin anında görünmesi için görev çubuğunu yeniler.",
                "安全重启桌面与任务栏进程，使右键菜单、深色模式等界面优化立即生效。",
                "explorer.exe restart",
                delegate(Action<string> log, Func<bool> cancel)
                {
                    WinNative.RestartExplorerSafely(log);
                }));

            return list;
        }

        private static RepairActionItem MakeRepair(
            string id, bool reqAdmin,
            string nEn, string nTr, string nZh, string dEn, string dTr, string dZh,
            string hint, Action<Action<string>, Func<bool>> exec)
        {
            RepairActionItem item = new RepairActionItem();
            item.Id = id; item.RequiresAdmin = reqAdmin;
            item.NameEn = nEn; item.NameTr = nTr; item.NameZh = nZh;
            item.DescEn = dEn; item.DescTr = dTr; item.DescZh = dZh;
            item.CommandHint = hint; item.Execute = exec;
            return item;
        }

        public static List<ControlPanelShortcut> BuildLegacyShortcuts()
        {
            List<ControlPanelShortcut> list = new List<ControlPanelShortcut>();
            list.Add(new ControlPanelShortcut("Control Panel", "Denetim Masası", "经典控制面板", "control.exe", ""));
            list.Add(new ControlPanelShortcut("Programs & Features", "Program Kaldır", "程序和功能", "control.exe", "appwiz.cpl"));
            list.Add(new ControlPanelShortcut("Network Adapters", "Ağ Bağlantıları", "网络连接", "control.exe", "ncpa.cpl"));
            list.Add(new ControlPanelShortcut("Sound Devices", "Ses Ayarları", "声音设备", "control.exe", "mmsys.cpl"));
            list.Add(new ControlPanelShortcut("Power Options", "Güç Seçenekleri", "电源选项", "control.exe", "powercfg.cpl"));
            list.Add(new ControlPanelShortcut("System Properties", "Sistem Özellikleri", "系统属性", "control.exe", "sysdm.cpl"));
            list.Add(new ControlPanelShortcut("Device Manager", "Aygıt Yöneticisi", "设备管理器", "mmc.exe", "devmgmt.msc"));
            list.Add(new ControlPanelShortcut("Windows Services", "Hizmetler", "系统服务", "mmc.exe", "services.msc"));
            list.Add(new ControlPanelShortcut("Disk Management", "Disk Yönetimi", "磁盘管理", "mmc.exe", "diskmgmt.msc"));
            list.Add(new ControlPanelShortcut("Startup Apps", "Başlangıç Uygulamaları", "开机启动项", "ms-settings:startupapps", ""));
            list.Add(new ControlPanelShortcut("Windows Firewall", "Güvenlik Duvarı", "防火墙设置", "control.exe", "firewall.cpl"));
            list.Add(new ControlPanelShortcut("Computer Mgmt", "Bilgisayar Yönetimi", "计算机管理", "mmc.exe", "compmgmt.msc"));
            list.Add(new ControlPanelShortcut("Mouse Properties", "Fare Ayarları", "鼠标属性", "control.exe", "main.cpl"));
            list.Add(new ControlPanelShortcut("Time & Date", "Tarih ve Saat", "日期和时间", "control.exe", "timedate.cpl"));
            list.Add(new ControlPanelShortcut("System Restore", "Sistem Geri Yükleme", "系统还原界面", "rstrui.exe", ""));
            return list;
        }

        public static List<WinFeatureItem> BuildWindowsFeatures()
        {
            List<WinFeatureItem> list = new List<WinFeatureItem>();

            list.Add(MakeFeature(
                "feat_wsl",
                "WSL (Windows Subsystem for Linux)", "WSL (Linux Alt Sistemi)", "WSL Linux 子系统",
                "Enables WSL & Virtual Machine Platform cleanly (reboot required).",
                "WSL ve Sanal Makine Platformunu resmi DISM ile açar (yeniden başlatma gerekir).",
                "通过官方 DISM 启用 WSL 与虚拟机平台组件（需重启生效）。",
                "DISM: Microsoft-Windows-Subsystem-Linux + VirtualMachinePlatform",
                delegate(Action<string> log, Func<bool> cancel)
                {
                    WinNative.RunProcessStreaming("DISM.exe", "/Online /Enable-Feature /FeatureName:Microsoft-Windows-Subsystem-Linux /All /NoRestart", log, cancel);
                    if (cancel != null && cancel()) return;
                    WinNative.RunProcessStreaming("DISM.exe", "/Online /Enable-Feature /FeatureName:VirtualMachinePlatform /All /NoRestart", log, cancel);
                },
                delegate(Action<string> log, Func<bool> cancel)
                {
                    WinNative.RunProcessStreaming("DISM.exe", "/Online /Disable-Feature /FeatureName:Microsoft-Windows-Subsystem-Linux /NoRestart", log, cancel);
                }));

            list.Add(MakeFeature(
                "feat_hyperv",
                "Hyper-V Virtualization", "Hyper-V Sanallaştırma", "Hyper-V 虚拟机平台",
                "Enables Microsoft Hyper-V hypervisor & management tools (Win Pro/Ent).",
                "Microsoft Hyper-V sanallaştırma altyapısını açar/kapatır (Win Pro/Ent).",
                "启用或关闭微软原生 Hyper-V 虚拟化平台（需专业版/企业版）。",
                "DISM: Microsoft-Hyper-V-All",
                delegate(Action<string> log, Func<bool> cancel) { WinNative.RunProcessStreaming("DISM.exe", "/Online /Enable-Feature /FeatureName:Microsoft-Hyper-V-All /All /NoRestart", log, cancel); },
                delegate(Action<string> log, Func<bool> cancel) { WinNative.RunProcessStreaming("DISM.exe", "/Online /Disable-Feature /FeatureName:Microsoft-Hyper-V-All /NoRestart", log, cancel); }));

            list.Add(MakeFeature(
                "feat_sandbox",
                "Windows Sandbox", "Windows Sandbox (Korumalı Alan)", "Windows 沙盒 (Sandbox)",
                "Lightweight disposable Windows desktop for safely testing untrusted files (Win Pro/Ent).",
                "Şüpheli dosyaları güvenle test etmek için tek kullanımlık izole Windows ortamı açar.",
                "启用一次性轻量隔离桌面环境，安全测试未知软件（需专业版/企业版）。",
                "DISM: Containers-DisposableClientForVM",
                delegate(Action<string> log, Func<bool> cancel) { WinNative.RunProcessStreaming("DISM.exe", "/Online /Enable-Feature /FeatureName:Containers-DisposableClientForVM /All /NoRestart", log, cancel); },
                delegate(Action<string> log, Func<bool> cancel) { WinNative.RunProcessStreaming("DISM.exe", "/Online /Disable-Feature /FeatureName:Containers-DisposableClientForVM /NoRestart", log, cancel); }));

            list.Add(MakeFeature(
                "feat_netfx3",
                ".NET Framework 3.5 (2.0 & 3.0)", ".NET Framework 3.5 (2.0 ve 3.0)", ".NET Framework 3.5 运行组件",
                "Installs legacy .NET 2.0/3.0/3.5 runtime required by classic apps & games.",
                "Klasik yazılım ve oyunların ihtiyaç duyduğu .NET 2.0/3.0/3.5 bileşenini kurar.",
                "安装经典老软件与游戏所需的 .NET 2.0/3.0/3.5 运行环境。",
                "DISM: NetFx3",
                delegate(Action<string> log, Func<bool> cancel) { WinNative.RunProcessStreaming("DISM.exe", "/Online /Enable-Feature /FeatureName:NetFx3 /All /NoRestart", log, cancel); },
                delegate(Action<string> log, Func<bool> cancel) { WinNative.RunProcessStreaming("DISM.exe", "/Online /Disable-Feature /FeatureName:NetFx3 /NoRestart", log, cancel); }));

            list.Add(MakeFeature(
                "feat_nfs",
                "NFS Network File Client", "NFS Ağ Dosya İstemcisi", "NFS 网络文件系统客户端",
                "Enables native mounting of Linux / NAS NFS network shares in Windows.",
                "Windows'ta Linux ve NAS cihazlarındaki NFS paylaşımlarını doğrudan bağlamayı açar.",
                "允许 Windows 原生挂载 Linux / NAS 设备的 NFS 网络共享目录。",
                "DISM: ServicesForNFS-ClientOnly",
                delegate(Action<string> log, Func<bool> cancel) { WinNative.RunProcessStreaming("DISM.exe", "/Online /Enable-Feature /FeatureName:ServicesForNFS-ClientOnly /All /NoRestart", log, cancel); },
                delegate(Action<string> log, Func<bool> cancel) { WinNative.RunProcessStreaming("DISM.exe", "/Online /Disable-Feature /FeatureName:ServicesForNFS-ClientOnly /NoRestart", log, cancel); }));

            list.Add(MakeFeature(
                "feat_openssh",
                "OpenSSH Server", "OpenSSH Sunucusu", "OpenSSH 服务端",
                "Installs Microsoft's native OpenSSH Server capability for remote SSH access.",
                "Uzaktan SSH erişimi için resmi Microsoft OpenSSH Sunucu bileşenini kurar/kaldırır.",
                "安装微软官方 OpenSSH 服务端，支持远程 SSH 登录本机。",
                "DISM Capability: OpenSSH.Server~~~~0.0.1.0",
                delegate(Action<string> log, Func<bool> cancel) { WinNative.RunProcessStreaming("DISM.exe", "/Online /Add-Capability /CapabilityName:OpenSSH.Server~~~~0.0.1.0", log, cancel); },
                delegate(Action<string> log, Func<bool> cancel) { WinNative.RunProcessStreaming("DISM.exe", "/Online /Remove-Capability /CapabilityName:OpenSSH.Server~~~~0.0.1.0", log, cancel); }));

            list.Add(MakeFeature(
                "feat_f8boot",
                "Legacy F8 Boot Recovery Menu", "Klasik F8 Kurtarma Önyükleme Menüsü", "传统 F8 开机高级恢复菜单",
                "Restores the classic F8 Safe Mode boot menu during PC startup (revert restores Standard).",
                "Bilgisayar açılırken F8 tuşuyla Güvenli Mod menüsüne erişimi açar (geri alınca Standart olur).",
                "恢复经典的开机按 F8 进入安全模式菜单（禁用即可还原现代默认引导）。",
                "bcdedit /set {default} bootmenupolicy legacy | standard",
                delegate(Action<string> log, Func<bool> cancel) { WinNative.RunProcessStreaming("bcdedit.exe", "/set {default} bootmenupolicy legacy", log, cancel); },
                delegate(Action<string> log, Func<bool> cancel) { WinNative.RunProcessStreaming("bcdedit.exe", "/set {default} bootmenupolicy standard", log, cancel); }));

            return list;
        }

        private static WinFeatureItem MakeFeature(
            string id, string nEn, string nTr, string nZh,
            string dEn, string dTr, string dZh, string hint,
            Action<Action<string>, Func<bool>> enableAct,
            Action<Action<string>, Func<bool>> disableAct)
        {
            WinFeatureItem f = new WinFeatureItem();
            f.Id = id; f.NameEn = nEn; f.NameTr = nTr; f.NameZh = nZh;
            f.DescEn = dEn; f.DescTr = dTr; f.DescZh = dZh;
            f.FeatureHint = hint; f.EnableAction = enableAct; f.DisableAction = disableAct;
            return f;
        }

        public static bool LaunchShortcut(ControlPanelShortcut s)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = s.Target;
                if (!string.IsNullOrEmpty(s.Args)) psi.Arguments = s.Args;
                psi.UseShellExecute = true;
                Process.Start(psi);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
