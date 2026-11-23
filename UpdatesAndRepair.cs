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
            return list;
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
