using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Microsoft.Win32;

namespace WinForge
{
    // ==================== BUILT-IN MICROSOFT BLOATWARE (APPX) & ONEDRIVE ====================

    internal sealed class BloatAppItem
    {
        public string PackageName;
        public string NameEn;
        public string NameTr;
        public string NameZh;
        public string NoteEn;
        public string NoteTr;
        public string NoteZh;
        public bool DefaultSafeBloat;
        public bool IsInstalled;
        public bool Selected;

        public BloatAppItem(string pkg, string nEn, string nTr, string nZh, string noteEn, string noteTr, string noteZh, bool safeDefault)
        {
            PackageName = pkg;
            NameEn = nEn; NameTr = nTr; NameZh = nZh;
            NoteEn = noteEn; NoteTr = noteTr; NoteZh = noteZh;
            DefaultSafeBloat = safeDefault;
        }

        public string GetName(string lang) { return Loc.T(lang, NameEn, NameTr, NameZh); }
        public string GetNote(string lang) { return Loc.T(lang, NoteEn, NoteTr, NoteZh); }
    }

    internal static class DebloatEngine
    {
        // Strictly curated safe-to-remove built-in Windows 10/11 Appx bloatware.
        // NEVER includes Store, AppInstaller (WinGet), Calculator, Photos, Terminal, or Security.
        public static List<BloatAppItem> BuildCatalog()
        {
            List<BloatAppItem> list = new List<BloatAppItem>();

            list.Add(new BloatAppItem("Clipchamp.Clipchamp", "Microsoft Clipchamp", "Microsoft Clipchamp", "Clipchamp 视频剪辑", "Preinstalled video editor", "Önyüklü video düzenleyici", "预装在线视频剪辑", true));
            list.Add(new BloatAppItem("Microsoft.WindowsFeedbackHub", "Feedback Hub", "Geri Bildirim Merkezi", "反馈中心", "Microsoft telemetry feedback", "Geri bildirim uygulaması", "微软诊断反馈应用", true));
            list.Add(new BloatAppItem("Microsoft.GetHelp", "Get Help", "Yardım Al", "获取帮助", "Support web wrapper", "Destek kısayolu", "在线支持网页壳", true));
            list.Add(new BloatAppItem("Microsoft.Getstarted", "Microsoft Tips", "İpuçları (Tips)", "使用技巧 (Tips)", "Intro tips & banners", "Başlangıç ipuçları", "新手引导与推送提示", true));
            list.Add(new BloatAppItem("Microsoft.BingNews", "Microsoft News", "Microsoft Haberler", "微软资讯 (News)", "MSN news feed app", "MSN haber uygulaması", "MSN 新闻推送应用", true));
            list.Add(new BloatAppItem("Microsoft.BingWeather", "MSN Weather", "MSN Hava Durumu", "MSN 天气", "Bing weather app", "Hava durumu uygulaması", "必应天气应用", true));
            list.Add(new BloatAppItem("Microsoft.BingSearch", "Bing Web Search", "Bing Web Araması", "必应搜索组件", "Bing search extension", "Bing arama eklentisi", "必应网页搜索扩展", true));
            list.Add(new BloatAppItem("Microsoft.OutlookForWindows", "New Outlook (Web)", "Yeni Outlook (Web)", "新版 Outlook 邮件", "Ad-supported web mail", "Reklamlı web e-posta", "新版网页封装邮件客户端", true));
            list.Add(new BloatAppItem("MSTeams", "Microsoft Teams (Home)", "Microsoft Teams (Ev)", "Microsoft Teams 家庭版", "Built-in consumer Teams", "Önyüklü Teams uygulaması", "系统预装消费版 Teams", true));
            list.Add(new BloatAppItem("Microsoft.MicrosoftOfficeHub", "Microsoft 365 Hub", "Microsoft 365 (Office)", "Microsoft 365 中心", "Office subscription launcher", "Office abonelik başlatıcı", "Office 订阅推广入口", true));
            list.Add(new BloatAppItem("Microsoft.MicrosoftSolitaireCollection", "Solitaire Collection", "Solitaire Oyunları", "微软纸牌合集", "Ad-supported card games", "Reklamlı kart oyunları", "内置含广告纸牌游戏", true));
            list.Add(new BloatAppItem("Microsoft.People", "Microsoft People", "Kişiler (People)", "人脉 (People)", "Legacy contacts app", "Eski kişiler uygulaması", "老旧联系人应用", true));
            list.Add(new BloatAppItem("Microsoft.WindowsMaps", "Windows Maps", "Windows Haritalar", "Windows 地图", "Offline maps app", "Harita uygulaması", "系统内置地图应用", true));
            list.Add(new BloatAppItem("Microsoft.549981C3F5F10", "Cortana (Legacy)", "Cortana (Eski)", "小娜 Cortana", "Deprecated assistant", "Kullanımdan kalkan asistan", "已停用的语音助手", true));
            list.Add(new BloatAppItem("MicrosoftCorporationII.QuickAssist", "Quick Assist", "Hızlı Yardım", "快速助手", "Remote support app", "Uzaktan yardım aracı", "微软远程协助工具", true));
            list.Add(new BloatAppItem("Microsoft.PowerAutomateDesktop", "Power Automate", "Power Automate", "Power Automate 自动化", "Enterprise RPA tool", "Otomasyon aracı", "桌面流程自动化工具", true));
            list.Add(new BloatAppItem("Microsoft.Windows.DevHome", "Dev Home", "Dev Home", "开发者主页 (Dev Home)", "Deprecated dev dashboard", "Geliştirici panosu", "预装开发者仪表盘", true));
            list.Add(new BloatAppItem("Microsoft.Todos", "Microsoft To Do", "Microsoft To Do", "微软待办 (To Do)", "Cloud task list", "Görev listesi", "云端任务待办清单", true));
            list.Add(new BloatAppItem("Microsoft.YourPhone", "Phone Link", "Telefon Bağlantısı", "手机连接 (Phone Link)", "Android/iPhone sync", "Telefon eşitleme", "手机同步后台常驻应用", false));
            list.Add(new BloatAppItem("Microsoft.MicrosoftStickyNotes", "Sticky Notes", "Yapışkan Notlar", "便笺 (Sticky Notes)", "Desktop notes app", "Masaüstü notları", "桌面便利贴应用", false));
            list.Add(new BloatAppItem("Microsoft.ZuneVideo", "Movies & TV", "Filmler ve TV", "电影和电视", "Legacy media player", "Eski video oynatıcı", "旧版视频播放器", true));
            list.Add(new BloatAppItem("Microsoft.ZuneMusic", "Media Player (Zune)", "Medya Oynatıcı", "Windows 媒体播放器", "Built-in music app", "Müzik çalar", "内置影音播放器", false));
            list.Add(new BloatAppItem("Microsoft.WindowsSoundRecorder", "Sound Recorder", "Ses Kaydedici", "录音机", "Voice recording app", "Ses kayıt aracı", "内置录音机应用", false));
            list.Add(new BloatAppItem("Microsoft.WindowsAlarms", "Clock & Alarms", "Saat ve Alarmlar", "时钟与闹钟", "Timer & alarms app", "Zamanlayıcı uygulaması", "闹钟与计时器", false));
            list.Add(new BloatAppItem("Microsoft.XboxGamingOverlay", "Xbox Game Bar", "Xbox Game Bar", "Xbox Game Bar 覆盖层", "In-game overlay (keep if Xbox)", "Oyun içi arayüz", "游戏录制覆盖层", false));
            list.Add(new BloatAppItem("Microsoft.XboxGameOverlay", "Xbox Gaming Overlay", "Xbox Oyun Katmanı", "Xbox 游戏核心覆盖", "Game Bar helper", "Game Bar yardımcısı", "Game Bar 辅助组件", false));
            list.Add(new BloatAppItem("Microsoft.Xbox.TCUI", "Xbox Live TCUI", "Xbox Live TCUI", "Xbox Live 界面组件", "Xbox login UI", "Xbox oturum arayüzü", "Xbox 账号界面组件", false));
            list.Add(new BloatAppItem("Microsoft.GamingApp", "Xbox App", "Xbox Uygulaması", "Xbox 客户端应用", "Game Pass client", "Game Pass istemcisi", "PC Game Pass 商店客户端", false));

            return list;
        }

        public static int ScanInstalledBloat(List<BloatAppItem> catalog)
        {
            int count = 0;
            try
            {
                StringBuilder sb = new StringBuilder();
                WinNative.RunProcessStreaming(
                    "powershell.exe",
                    "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Get-AppxPackage | Select-Object -ExpandProperty Name\"",
                    delegate(string line) { sb.AppendLine(line); },
                    null);

                HashSet<string> installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string raw in sb.ToString().Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    installed.Add(raw.Trim());

                foreach (BloatAppItem item in catalog)
                {
                    item.IsInstalled = installed.Contains(item.PackageName);
                    if (item.IsInstalled) count++;
                }
            }
            catch { }
            return count;
        }

        public static void RemoveSelectedBloat(List<BloatAppItem> selected, Action<string> log, Func<bool> isCancelled, out int removedCount, out int failedCount)
        {
            removedCount = 0;
            failedCount = 0;
            foreach (BloatAppItem item in selected)
            {
                if (isCancelled != null && isCancelled()) break;
                if (log != null) log("[Debloat] Removing " + item.NameEn + " (" + item.PackageName + ")...");
                int rc = WinNative.RunProcessStreaming(
                    "powershell.exe",
                    "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Get-AppxPackage -Name '" + item.PackageName + "' | Remove-AppxPackage -ErrorAction Stop\"",
                    log, isCancelled);
                if (rc == 0)
                {
                    removedCount++;
                    item.IsInstalled = false;
                    item.Selected = false;
                    if (log != null) log("[OK] Removed " + item.PackageName);
                }
                else
                {
                    failedCount++;
                    if (log != null) log("[WARN] " + item.PackageName + " exited with code " + rc);
                }
            }
        }

        // ==================== MICROSOFT ONEDRIVE REMOVE / REINSTALL ====================

        public static bool IsOneDriveInstalled()
        {
            string p1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\OneDrive\OneDrive.exe");
            string p2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft OneDrive\OneDrive.exe");
            string p3 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft OneDrive\OneDrive.exe");
            return File.Exists(p1) || File.Exists(p2) || File.Exists(p3);
        }

        public static string FindOneDriveSetupExe()
        {
            string sys32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\OneDriveSetup.exe");
            string wow64 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"SysWOW64\OneDriveSetup.exe");
            if (File.Exists(wow64)) return wow64;
            if (File.Exists(sys32)) return sys32;
            return "";
        }

        public static bool RemoveOneDrive(Action<string> log, Func<bool> isCancelled)
        {
            if (log != null) log("[OneDrive] Stopping OneDrive.exe process...");
            WinNative.RunProcessStreaming("taskkill.exe", "/f /im OneDrive.exe", null, null);

            string setup = FindOneDriveSetupExe();
            if (!string.IsNullOrEmpty(setup))
            {
                if (log != null) log("[OneDrive] Running official uninstaller: " + setup + " /uninstall");
                WinNative.RunProcessStreaming(setup, "/uninstall", log, isCancelled);
            }
            else
            {
                if (log != null) log("[OneDrive] Fallback: running WinGet uninstall Microsoft.OneDrive...");
                WinNative.RunProcessStreaming("winget.exe", "uninstall --id Microsoft.OneDrive --exact --silent --accept-source-agreements", log, isCancelled);
            }

            // Hide Explorer OneDrive navigation pane pin cleanly (reversible)
            RegHelper.WriteDword(Registry.CurrentUser, @"Software\Classes\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}", "System.IsPinnedToNameSpaceTree", 0);

            bool removed = !IsOneDriveInstalled();
            if (log != null) log(removed ? "[OneDrive] Removed cleanly (reinstall anytime via WinGet)." : "[OneDrive] Uninstaller finished.");
            return removed;
        }

        public static bool ReinstallOneDrive(Action<string> log, Func<bool> isCancelled)
        {
            RegHelper.WriteDword(Registry.CurrentUser, @"Software\Classes\CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}", "System.IsPinnedToNameSpaceTree", 1);
            if (log != null) log("[OneDrive] Reinstalling Microsoft OneDrive via WinGet (Microsoft.OneDrive)...");
            int rc = WinNative.RunProcessStreaming(
                "winget.exe",
                "install --id Microsoft.OneDrive --exact --silent --accept-package-agreements --accept-source-agreements",
                log, isCancelled);
            if (log != null) log("[OneDrive] Reinstall command finished (exit=" + rc + ").");
            return rc == 0 || IsOneDriveInstalled();
        }
    }

    // ==================== 1-CLICK DNS SWITCHER ====================

    internal sealed class DnsPreset
    {
        public string Id;
        public string NameEn;
        public string NameTr;
        public string NameZh;
        public string PrimaryV4;
        public string SecondaryV4;
        public string TagEn;
        public string TagTr;
        public string TagZh;
        public bool IsDhcp;

        public DnsPreset(string id, string nEn, string nTr, string nZh, string p4, string s4, string tEn, string tTr, string tZh, bool dhcp)
        {
            Id = id; NameEn = nEn; NameTr = nTr; NameZh = nZh;
            PrimaryV4 = p4; SecondaryV4 = s4;
            TagEn = tEn; TagTr = tTr; TagZh = tZh;
            IsDhcp = dhcp;
        }

        public string GetName(string lang) { return Loc.T(lang, NameEn, NameTr, NameZh); }
        public string GetTag(string lang) { return Loc.T(lang, TagEn, TagTr, TagZh); }
    }

    internal sealed class LiveDnsStatus
    {
        public List<string> ActiveAdapters = new List<string>();
        public string PrimaryAdapterName = "";
        public string CurrentDnsDisplay = "";
        public string ActivePresetId = "";
    }

    internal static class DnsEngine
    {
        public static List<DnsPreset> BuildPresets()
        {
            List<DnsPreset> list = new List<DnsPreset>();
            list.Add(new DnsPreset("dhcp", "DHCP (Default)", "DHCP (Varsayılan)", "DHCP 自动获取", "", "", "ISP / Router default", "Modem / ISS otomatik", "恢复路由器/运营商默认", true));
            list.Add(new DnsPreset("cloudflare", "Cloudflare", "Cloudflare", "Cloudflare DNS", "1.1.1.1", "1.0.0.1", "Fastest global privacy", "En hızlı gizlilik DNS", "全球高速隐私 DNS", false));
            list.Add(new DnsPreset("cloudflare_sec", "Cloudflare Malware", "Cloudflare Güvenli", "Cloudflare 安全拦截", "1.1.1.2", "1.0.0.2", "Blocks malware domains", "Zararlı siteleri engeller", "自动拦截恶意软件域名", false));
            list.Add(new DnsPreset("google", "Google DNS", "Google DNS", "Google 公共 DNS", "8.8.8.8", "8.8.4.4", "Reliable global DNS", "Kararlı genel DNS", "稳定可靠的公共 DNS", false));
            list.Add(new DnsPreset("quad9", "Quad9 Security", "Quad9 Güvenlik", "Quad9 安全 DNS", "9.9.9.9", "149.112.112.112", "Non-profit threat block", "Zararlı tehdit korumalı", "非营利威胁拦截 DNS", false));
            list.Add(new DnsPreset("adguard", "AdGuard Ad-Block", "AdGuard Reklam Engel", "AdGuard 去广告 DNS", "94.140.14.14", "94.140.15.15", "Blocks ads & trackers", "Reklam & izleyici engeller", "系统级屏蔽广告与追踪器", false));
            list.Add(new DnsPreset("opendns", "OpenDNS", "OpenDNS", "OpenDNS", "208.67.222.222", "208.67.220.220", "Cisco reliable DNS", "Cisco güvenilir DNS", "思科高可靠公共 DNS", false));
            return list;
        }

        public static LiveDnsStatus ReadLiveDnsStatus(List<DnsPreset> presets)
        {
            LiveDnsStatus st = new LiveDnsStatus();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType != NetworkInterfaceType.Ethernet &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.GigabitEthernet)
                        continue;

                    string descLow = (ni.Description ?? "").ToLowerInvariant();
                    if (descLow.Contains("virtual") || descLow.Contains("vmware") || descLow.Contains("hyper-v") ||
                        descLow.Contains("loopback") || descLow.Contains("bluetooth") || descLow.Contains("tap-"))
                        continue;

                    IPInterfaceProperties props = ni.GetIPProperties();
                    if (props == null || props.GatewayAddresses == null || props.GatewayAddresses.Count == 0)
                        continue;

                    st.ActiveAdapters.Add(ni.Name);
                    if (string.IsNullOrEmpty(st.PrimaryAdapterName))
                    {
                        st.PrimaryAdapterName = ni.Name;
                        List<string> v4Dns = new List<string>();
                        foreach (System.Net.IPAddress ip in props.DnsAddresses)
                        {
                            if (ip.AddressFamily == AddressFamily.InterNetwork)
                                v4Dns.Add(ip.ToString());
                        }
                        st.CurrentDnsDisplay = v4Dns.Count > 0 ? string.Join(", ", v4Dns.ToArray()) : "DHCP";
                        if (v4Dns.Count > 0 && presets != null)
                        {
                            foreach (DnsPreset p in presets)
                            {
                                if (!p.IsDhcp && string.Equals(p.PrimaryV4, v4Dns[0], StringComparison.OrdinalIgnoreCase))
                                {
                                    st.ActivePresetId = p.Id;
                                    break;
                                }
                            }
                        }
                        if (string.IsNullOrEmpty(st.ActivePresetId))
                            st.ActivePresetId = "dhcp";
                    }
                }
            }
            catch { }
            return st;
        }

        public static bool ApplyDnsPreset(DnsPreset preset, List<string> adapterNames, Action<string> log)
        {
            if (preset == null || adapterNames == null || adapterNames.Count == 0) return false;
            bool allOk = true;
            foreach (string adapter in adapterNames)
            {
                if (preset.IsDhcp)
                {
                    if (log != null) log("[DNS] Resetting '" + adapter + "' to DHCP (Automatic)...");
                    int rc = WinNative.RunProcessStreaming("netsh.exe", "interface ip set dns name=\"" + adapter + "\" source=dhcp", log, null);
                    allOk &= (rc == 0);
                }
                else
                {
                    if (log != null) log("[DNS] Setting '" + adapter + "' -> " + preset.PrimaryV4 + ", " + preset.SecondaryV4 + " (" + preset.NameEn + ")");
                    int rc1 = WinNative.RunProcessStreaming("netsh.exe", "interface ip set dns name=\"" + adapter + "\" static " + preset.PrimaryV4 + " primary", log, null);
                    int rc2 = WinNative.RunProcessStreaming("netsh.exe", "interface ip add dns name=\"" + adapter + "\" " + preset.SecondaryV4 + " index=2", log, null);
                    allOk &= (rc1 == 0 && rc2 == 0);
                }
            }
            WinNative.RunProcessStreaming("ipconfig.exe", "/flushdns", log, null);
            return allOk;
        }
    }
}
