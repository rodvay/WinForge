using System;
using System.Collections.Generic;
using System.Text;

namespace WinForge
{
    internal enum AppGroup
    {
        Browsers = 0,
        Developer = 1,
        Utilities = 2,
        Media = 3,
        Communication = 4,
        GamingRuntimes = 5
    }

    internal sealed class WingetAppItem
    {
        public string Id;
        public string Name;
        public AppGroup Group;
        public string TagEn;
        public string TagTr;
        public string TagZh;
        public bool Selected;
        public bool IsInstalled;

        public WingetAppItem(string id, string name, AppGroup group, string tagEn, string tagTr, string tagZh)
        {
            Id = id;
            Name = name;
            Group = group;
            TagEn = tagEn;
            TagTr = tagTr;
            TagZh = tagZh;
        }

        public string GetTag(string lang) { return Loc.T(lang, TagEn, TagTr, TagZh); }
    }

    internal static class WingetEngine
    {
        public static string GetGroupName(AppGroup g, string lang)
        {
            if (g == AppGroup.Browsers) return Loc.T(lang, "Browsers", "Tarayıcılar", "浏览器");
            if (g == AppGroup.Developer) return Loc.T(lang, "Developer Tools", "Geliştirici Araçları", "开发工具");
            if (g == AppGroup.Utilities) return Loc.T(lang, "System Utilities", "Sistem Araçları", "系统工具");
            if (g == AppGroup.Media) return Loc.T(lang, "Media & Creative", "Medya ve Tasarım", "影音与创作");
            if (g == AppGroup.Communication) return Loc.T(lang, "Chat & Security", "İletişim ve Güvenlik", "通讯与密码");
            return Loc.T(lang, "Gaming & Runtimes", "Oyun ve Kütüphaneler", "游戏与运行库");
        }

        public static List<WingetAppItem> BuildVerifiedCatalog()
        {
            // Every single WinGet ID below has been verified against the live Microsoft WinGet repository.
            List<WingetAppItem> list = new List<WingetAppItem>();

            // Browsers
            list.Add(new WingetAppItem("Google.Chrome", "Google Chrome", AppGroup.Browsers, "Fast web browser", "Hızlı web tarayıcı", "主流高速浏览器"));
            list.Add(new WingetAppItem("Mozilla.Firefox", "Mozilla Firefox", AppGroup.Browsers, "Open-source browser", "Açık kaynak tarayıcı", "开源隐私浏览器"));
            list.Add(new WingetAppItem("Brave.Brave", "Brave Browser", AppGroup.Browsers, "Ad-blocking privacy", "Reklam engelleyici", "内置去广告隐私浏览器"));
            list.Add(new WingetAppItem("Vivaldi.Vivaldi", "Vivaldi", AppGroup.Browsers, "Power-user browser", "Gelişmiş özelleştirme", "高度可定制极客浏览器"));

            // Developer Tools
            list.Add(new WingetAppItem("Microsoft.VisualStudioCode", "VS Code", AppGroup.Developer, "Code editor", "Kod editörü", "轻量级代码编辑器"));
            list.Add(new WingetAppItem("Git.Git", "Git", AppGroup.Developer, "Version control", "Sürüm kontrol", "分布式版本控制工具"));
            list.Add(new WingetAppItem("OpenJS.NodeJS.LTS", "Node.js LTS", AppGroup.Developer, "JavaScript runtime", "JS çalışma ortamı", "JS 长期支持运行环境"));
            list.Add(new WingetAppItem("Python.Python.3.13", "Python 3.13", AppGroup.Developer, "Python interpreter", "Python dili", "Python 3.13 解释器"));
            list.Add(new WingetAppItem("Microsoft.WindowsTerminal", "Windows Terminal", AppGroup.Developer, "Modern terminal", "Modern terminal", "微软现代终端"));
            list.Add(new WingetAppItem("Microsoft.PowerShell", "PowerShell 7", AppGroup.Developer, "Cross-platform shell", "Modern PowerShell", "跨平台 PowerShell 7"));
            list.Add(new WingetAppItem("Notepad++.Notepad++", "Notepad++", AppGroup.Developer, "Fast text editor", "Hızlı metin editörü", "经典轻量文本编辑器"));
            list.Add(new WingetAppItem("Docker.DockerDesktop", "Docker Desktop", AppGroup.Developer, "Containers", "Konteyner platformu", "容器开发运行平台"));

            // System Utilities
            list.Add(new WingetAppItem("7zip.7zip", "7-Zip", AppGroup.Utilities, "Archive manager", "Arşiv sıkıştırıcı", "开源免费压缩工具"));
            list.Add(new WingetAppItem("voidtools.Everything", "Everything", AppGroup.Utilities, "Instant file search", "Anında dosya arama", "毫秒级本地文件搜索"));
            list.Add(new WingetAppItem("Microsoft.PowerToys", "PowerToys", AppGroup.Utilities, "Win11 power suite", "Windows güç araçları", "微软官方效率增强套件"));
            list.Add(new WingetAppItem("ShareX.ShareX", "ShareX", AppGroup.Utilities, "Screenshot & capture", "Ekran görüntüsü", "开源截图与录屏神器"));
            list.Add(new WingetAppItem("AntibodySoftware.WizTree", "WizTree", AppGroup.Utilities, "Disk space analyzer", "Disk alan analizi", "极速硬盘空间分析"));
            list.Add(new WingetAppItem("Rufus.Rufus", "Rufus", AppGroup.Utilities, "Bootable USB creator", "Önyüklenebilir USB", "系统启动 U 盘制作"));
            list.Add(new WingetAppItem("REALiX.HWiNFO", "HWiNFO", AppGroup.Utilities, "Hardware sensors", "Donanım sensörleri", "专业硬件与温度监测"));
            list.Add(new WingetAppItem("CPUID.CPU-Z", "CPU-Z", AppGroup.Utilities, "CPU & RAM specs", "İşlemci ve RAM", "经典硬件规格检测"));

            // Media & Creative
            list.Add(new WingetAppItem("VideoLAN.VLC", "VLC Media Player", AppGroup.Media, "Plays any format", "Her formatı oynatır", "开源全能影音播放器"));
            list.Add(new WingetAppItem("OBSProject.OBSStudio", "OBS Studio", AppGroup.Media, "Recording & stream", "Kayıt ve yayın", "开源录屏与直播软件"));
            list.Add(new WingetAppItem("GIMP.GIMP", "GIMP", AppGroup.Media, "Image editor", "Görsel düzenleyici", "开源图像处理软件"));
            list.Add(new WingetAppItem("BlenderFoundation.Blender", "Blender", AppGroup.Media, "3D creation suite", "3B tasarım", "开源三维建模渲染套件"));
            list.Add(new WingetAppItem("SumatraPDF.SumatraPDF", "SumatraPDF", AppGroup.Media, "Ultra-fast PDF", "Çok hızlı PDF", "极速轻量 PDF 阅读器"));
            list.Add(new WingetAppItem("Audacity.Audacity", "Audacity", AppGroup.Media, "Audio editor", "Ses düzenleyici", "开源音频剪辑工具"));

            // Chat & Security
            list.Add(new WingetAppItem("Discord.Discord", "Discord", AppGroup.Communication, "Voice & community", "Sesli sohbet", "游戏语音与社区通讯"));
            list.Add(new WingetAppItem("Telegram.TelegramDesktop", "Telegram", AppGroup.Communication, "Cloud messaging", "Bulut mesajlaşma", "云端即时通讯客户端"));
            list.Add(new WingetAppItem("9NKSQGP7F2NH", "WhatsApp", AppGroup.Communication, "Messaging app", "Mesajlaşma", "即时通讯客户端"));
            list.Add(new WingetAppItem("Zoom.Zoom", "Zoom", AppGroup.Communication, "Video meetings", "Video toplantı", "高清视频会议软件"));
            list.Add(new WingetAppItem("Bitwarden.Bitwarden", "Bitwarden", AppGroup.Communication, "Open password vault", "Açık kaynak şifre kasası", "开源跨平台密码管理器"));
            list.Add(new WingetAppItem("KeePassXCTeam.KeePassXC", "KeePassXC", AppGroup.Communication, "Offline password db", "Çevrimdışı şifre kasası", "开源离线本地密码库"));

            // Gaming & Runtimes
            list.Add(new WingetAppItem("Valve.Steam", "Steam", AppGroup.GamingRuntimes, "PC game store", "PC oyun platformu", "PC 游戏数字发行平台"));
            list.Add(new WingetAppItem("EpicGames.EpicGamesLauncher", "Epic Games", AppGroup.GamingRuntimes, "Games launcher", "Oyun başlatıcı", "Epic 游戏平台客户端"));
            list.Add(new WingetAppItem("Microsoft.VCRedist.2015+.x64", "VC++ 2015-2022 x64", AppGroup.GamingRuntimes, "Required game runtime", "Oyun/uygulama paketi", "常用游戏与软件运行库"));
            list.Add(new WingetAppItem("qBittorrent.qBittorrent", "qBittorrent", AppGroup.GamingRuntimes, "Open-source torrent", "Açık kaynak torrent", "开源无广告 BT 下载器"));

            return list;
        }

        public static bool IsWingetAvailable(out string versionText)
        {
            versionText = "";
            try
            {
                StringBuilder sb = new StringBuilder();
                int rc = WinNative.RunProcessStreaming("winget.exe", "--version", delegate(string line) { sb.Append(line); }, null);
                versionText = sb.ToString().Trim();
                return rc == 0 && !string.IsNullOrEmpty(versionText);
            }
            catch
            {
                return false;
            }
        }

        public static int DetectInstalledApps(List<WingetAppItem> catalog, Func<bool> isCancelled)
        {
            int found = 0;
            try
            {
                StringBuilder allOut = new StringBuilder();
                int rc = WinNative.RunProcessStreaming(
                    "winget.exe",
                    "list --accept-source-agreements",
                    delegate(string line) { allOut.AppendLine(line); },
                    isCancelled);

                if (rc != 0) return 0;
                string dump = allOut.ToString();
                foreach (WingetAppItem app in catalog)
                {
                    app.IsInstalled = dump.IndexOf(app.Id, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (app.IsInstalled) found++;
                }
            }
            catch { }
            return found;
        }

        public static void InstallSelected(
            List<WingetAppItem> selected,
            Action<string> onLog,
            Action<int, int, string> onProgress,
            Func<bool> isCancelled,
            out int succeeded,
            out int failed)
        {
            succeeded = 0;
            failed = 0;

            for (int i = 0; i < selected.Count; i++)
            {
                if (isCancelled != null && isCancelled())
                    break;

                WingetAppItem app = selected[i];
                if (onProgress != null) onProgress(i, selected.Count, app.Name);
                if (onLog != null) onLog(string.Format("=== [{0}/{1}] Installing {2} ({3}) ===", i + 1, selected.Count, app.Name, app.Id));

                string args = string.Format(
                    "install --id \"{0}\" --exact --silent --accept-source-agreements --accept-package-agreements",
                    app.Id);

                int rc = WinNative.RunProcessStreaming("winget.exe", args, onLog, isCancelled);
                // 0 = installed, -1978335189 (0x8A15002B) = already installed & up to date
                if (rc == 0 || unchecked((uint)rc) == 0x8A15002B || unchecked((uint)rc) == 0x8A150061)
                {
                    succeeded++;
                    app.IsInstalled = true;
                    app.Selected = false;
                    if (onLog != null) onLog("[OK] " + app.Name);
                }
                else if (isCancelled != null && isCancelled())
                {
                    if (onLog != null) onLog("[CANCELLED] " + app.Name);
                    break;
                }
                else
                {
                    failed++;
                    if (onLog != null) onLog(string.Format("[WARN] {0} exited with code 0x{1:X8}", app.Name, rc));
                }
            }

            if (onProgress != null) onProgress(selected.Count, selected.Count, "");
        }

        public static int UpgradeAllApps(Action<string> onLog, Func<bool> isCancelled)
        {
            if (onLog != null) onLog("=== Running: winget upgrade --all ===");
            return WinNative.RunProcessStreaming(
                "winget.exe",
                "upgrade --all --silent --accept-source-agreements --accept-package-agreements",
                onLog,
                isCancelled);
        }
    }
}
