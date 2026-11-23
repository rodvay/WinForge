using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace WinForge
{
    internal enum TweakCategory
    {
        UiExplorer = 0,
        Privacy = 1,
        Performance = 2
    }

    internal sealed class TweakDefinition
    {
        public string Id;
        public TweakCategory Category;
        public string NameEn;
        public string NameTr;
        public string NameZh;
        public string DescEn;
        public string DescTr;
        public string DescZh;
        public string TargetHint;
        public bool RequiresAdmin;
        public bool InRecommendedPreset;
        public bool NeedsExplorerRestart;

        public Func<bool> CheckApplied;
        public Func<bool> ApplyAction;
        public Func<bool> RevertAction;

        // Live UI state
        public bool IsCurrentlyApplied;
        public bool DesiredState;

        public string GetName(string lang) { return Loc.T(lang, NameEn, NameTr, NameZh); }
        public string GetDesc(string lang) { return Loc.T(lang, DescEn, DescTr, DescZh); }
    }

    internal sealed class TweakApplyResult
    {
        public int AppliedCount;
        public int RevertedCount;
        public int FailedCount;
        public int SkippedAdminCount;
        public bool RecommendExplorerRestart;
        public string BackupFilePath;
    }

    internal static class RegHelper
    {
        public static int ReadDword(RegistryKey root, string subKey, string name, int fallback)
        {
            try
            {
                using (RegistryKey k = root.OpenSubKey(subKey, false))
                {
                    if (k == null) return fallback;
                    object v = k.GetValue(name);
                    if (v == null) return fallback;
                    return Convert.ToInt32(v);
                }
            }
            catch
            {
                return fallback;
            }
        }

        public static string ReadString(RegistryKey root, string subKey, string name, string fallback)
        {
            try
            {
                using (RegistryKey k = root.OpenSubKey(subKey, false))
                {
                    if (k == null) return fallback;
                    object v = k.GetValue(name);
                    return v != null ? v.ToString() : fallback;
                }
            }
            catch
            {
                return fallback;
            }
        }

        public static bool KeyExists(RegistryKey root, string subKey)
        {
            try
            {
                using (RegistryKey k = root.OpenSubKey(subKey, false))
                    return k != null;
            }
            catch
            {
                return false;
            }
        }

        public static bool WriteDword(RegistryKey root, string subKey, string name, int value)
        {
            try
            {
                using (RegistryKey k = root.CreateSubKey(subKey))
                {
                    if (k == null) return false;
                    k.SetValue(name, value, RegistryValueKind.DWord);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool WriteString(RegistryKey root, string subKey, string name, string value)
        {
            try
            {
                using (RegistryKey k = root.CreateSubKey(subKey))
                {
                    if (k == null) return false;
                    k.SetValue(name, value ?? "", RegistryValueKind.String);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool DeleteValueSafe(RegistryKey root, string subKey, string name)
        {
            try
            {
                using (RegistryKey k = root.OpenSubKey(subKey, true))
                {
                    if (k == null) return true;
                    if (k.GetValue(name) != null)
                        k.DeleteValue(name, false);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool DeleteSubKeyTreeSafe(RegistryKey root, string subKey)
        {
            try
            {
                if (!KeyExists(root, subKey)) return true;
                root.DeleteSubKeyTree(subKey, false);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    internal static class TweaksEngine
    {
        private const string ClassicMenuClsid = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
        private const string ClassicMenuInproc = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";
        private const string ExplorerAdv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string ContentDeliv = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        private const string HighPerfGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        private const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";

        public static List<TweakDefinition> BuildCatalog()
        {
            List<TweakDefinition> list = new List<TweakDefinition>();

            // ==================== 1. UI & EXPLORER (100% HKCU, Zero Admin Needed) ====================

            list.Add(Make(
                "show_file_ext", TweakCategory.UiExplorer, false, true, true,
                "Show File Extensions", "Dosya Uzantılarını Göster", "显示文件扩展名",
                "Always display full file extensions (.exe, .pdf, .zip) in File Explorer.",
                "Dosya Gezgini'nde tüm dosya uzantılarını (.exe, .pdf, .zip) görünür yapar.",
                "在文件资源管理器中始终显示完整文件后缀（如 .exe、.pdf），防止伪装文件。",
                @"HKCU\...\Explorer\Advanced\HideFileExt = 0",
                delegate { return RegHelper.ReadDword(Registry.CurrentUser, ExplorerAdv, "HideFileExt", 1) == 0; },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, ExplorerAdv, "HideFileExt", 0); },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, ExplorerAdv, "HideFileExt", 1); }
            ));

            list.Add(Make(
                "show_hidden_files", TweakCategory.UiExplorer, false, false, true,
                "Show Hidden Files & Folders", "Gizli Dosya ve Klasörleri Göster", "显示隐藏文件和文件夹",
                "Reveals hidden AppData and configuration folders in File Explorer.",
                "AppData gibi gizli sistem ve yapılandırma klasörlerini görünür kılar.",
                "显示 AppData 等默认隐藏的配置目录，方便排查问题与开发调试。",
                @"HKCU\...\Explorer\Advanced\Hidden = 1",
                delegate { return RegHelper.ReadDword(Registry.CurrentUser, ExplorerAdv, "Hidden", 2) == 1; },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, ExplorerAdv, "Hidden", 1); },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, ExplorerAdv, "Hidden", 2); }
            ));

            list.Add(Make(
                "classic_context_menu", TweakCategory.UiExplorer, false, true, true,
                "Classic Right-Click Context Menu (Win11)", "Klasik Sağ Tık Menüsü (Windows 11)", "恢复 Windows 11 经典右键菜单",
                "Restores the full classic context menu without clicking 'Show more options'.",
                "'Daha fazla seçenek göster' tıklamadan doğrudan klasik sağ tık menüsünü açar.",
                "恢复完整的经典右键菜单，无需每次多点一次「显示更多选项」。",
                @"HKCU\Software\Classes\CLSID\{86ca1aa0-...}\InprocServer32",
                delegate { return RegHelper.KeyExists(Registry.CurrentUser, ClassicMenuInproc); },
                delegate { return RegHelper.WriteString(Registry.CurrentUser, ClassicMenuInproc, "", ""); },
                delegate { return RegHelper.DeleteSubKeyTreeSafe(Registry.CurrentUser, ClassicMenuClsid); }
            ));

            list.Add(Make(
                "dark_mode", TweakCategory.UiExplorer, false, true, false,
                "Enable Windows & Apps Dark Mode", "Windows ve Uygulamalar Karanlık Mod", "开启系统与应用深色模式",
                "Switches both Windows shell and supported apps to Dark Mode.",
                "Hem Windows arayüzünü hem desteklenen uygulamaları koyu temaya geçirir.",
                "同时将 Windows 系统界面与支持深色主题的应用切换为深色模式。",
                @"HKCU\...\Themes\Personalize\AppsUseLightTheme = 0",
                delegate
                {
                    return RegHelper.ReadDword(Registry.CurrentUser, Personalize, "AppsUseLightTheme", 1) == 0 &&
                           RegHelper.ReadDword(Registry.CurrentUser, Personalize, "SystemUsesLightTheme", 1) == 0;
                },
                delegate
                {
                    bool a = RegHelper.WriteDword(Registry.CurrentUser, Personalize, "AppsUseLightTheme", 0);
                    bool b = RegHelper.WriteDword(Registry.CurrentUser, Personalize, "SystemUsesLightTheme", 0);
                    WinNative.BroadcastSettingsChange("ImmersiveColorSet");
                    return a && b;
                },
                delegate
                {
                    bool a = RegHelper.WriteDword(Registry.CurrentUser, Personalize, "AppsUseLightTheme", 1);
                    bool b = RegHelper.WriteDword(Registry.CurrentUser, Personalize, "SystemUsesLightTheme", 1);
                    WinNative.BroadcastSettingsChange("ImmersiveColorSet");
                    return a && b;
                }
            ));

            list.Add(Make(
                "disable_bing_search", TweakCategory.UiExplorer, false, true, true,
                "Disable Bing Web Search in Start Menu", "Başlat Menüsünde Bing Web Aramasını Kapat", "禁用开始菜单 Bing 网页搜索",
                "Makes Start Menu search faster and strictly local — no web clutter.",
                "Başlat aramasını hızlandırır ve yalnızca yerel dosya/uygulamaları arar.",
                "让开始菜单搜索更快更纯净，仅搜索本地应用与文件，去除网页推荐。",
                @"HKCU\Software\Policies\Microsoft\Windows\Explorer\DisableSearchBoxSuggestions = 1",
                delegate { return RegHelper.ReadDword(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 0) == 1; },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1); },
                delegate { return RegHelper.DeleteValueSafe(Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions"); }
            ));

            list.Add(Make(
                "explorer_this_pc", TweakCategory.UiExplorer, false, true, false,
                "Open File Explorer to 'This PC'", "Dosya Gezgini'ni 'Bu Bilgisayar' ile Aç", "文件资源管理器默认打开「此电脑」",
                "Opens directly to drives ('This PC') instead of Home / Quick Access.",
                "Dosya Gezgini açıldığında Giriş yerine doğrudan diskleri (Bu Bilgisayar) açar.",
                "打开资源管理器时直接进入「此电脑」磁盘视图，而非「主页」。",
                @"HKCU\...\Explorer\Advanced\LaunchTo = 1",
                delegate { return RegHelper.ReadDword(Registry.CurrentUser, ExplorerAdv, "LaunchTo", 2) == 1; },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, ExplorerAdv, "LaunchTo", 1); },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, ExplorerAdv, "LaunchTo", 2); }
            ));

            list.Add(Make(
                "taskbar_end_task", TweakCategory.UiExplorer, false, true, false,
                "Enable 'End Task' in Taskbar Right-Click", "Görev Çubuğu Sağ Tıkta 'Görevi Sonlandır'", "任务栏右键启用「结束任务」",
                "Adds instant 'End Task' to taskbar app right-click menus on Windows 11.",
                "Görev Yöneticisi açmadan görev çubuğundan sağ tıkla uygulama kapatmayı açar.",
                "在 Windows 11 任务栏图标右键菜单中直接增加「结束任务」快捷按钮。",
                @"HKCU\...\Explorer\Advanced\TaskbarDeveloperSettings\TaskbarEndTask = 1",
                delegate { return RegHelper.ReadDword(Registry.CurrentUser, ExplorerAdv + @"\TaskbarDeveloperSettings", "TaskbarEndTask", 0) == 1; },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, ExplorerAdv + @"\TaskbarDeveloperSettings", "TaskbarEndTask", 1); },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, ExplorerAdv + @"\TaskbarDeveloperSettings", "TaskbarEndTask", 0); }
            ));

            list.Add(Make(
                "taskbar_left", TweakCategory.UiExplorer, false, false, false,
                "Align Taskbar Icons to Left (Win11)", "Görev Çubuğunu Sola Hizala (Win11)", "Windows 11 任务栏图标左对齐",
                "Moves Start & pinned taskbar icons from center back to the classic left edge.",
                "Başlat ve simgeleri ortadan klasik sol hizaya alır.",
                "将任务栏开始按钮与图标从居中改回经典的左下角对齐。",
                @"HKCU\...\Explorer\Advanced\TaskbarAl = 0",
                delegate { return RegHelper.ReadDword(Registry.CurrentUser, ExplorerAdv, "TaskbarAl", 1) == 0; },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, ExplorerAdv, "TaskbarAl", 0); },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, ExplorerAdv, "TaskbarAl", 1); }
            ));

            list.Add(Make(
                "clock_seconds", TweakCategory.UiExplorer, false, false, false,
                "Show Seconds in Taskbar Clock", "Görev Çubuğu Saatinde Saniyeyi Göster", "在任务栏时钟显示秒数",
                "Displays live HH:MM:SS directly in the Windows system tray clock.",
                "Sağ alttaki Windows saatinde saniyeyi canlı olarak gösterir.",
                "在右下角系统托盘时钟中直接显示实时秒数（时:分:秒）。",
                @"HKCU\...\Explorer\Advanced\ShowSecondsInSystemClock = 1",
                delegate { return RegHelper.ReadDword(Registry.CurrentUser, ExplorerAdv, "ShowSecondsInSystemClock", 0) == 1; },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, ExplorerAdv, "ShowSecondsInSystemClock", 1); },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, ExplorerAdv, "ShowSecondsInSystemClock", 0); }
            ));

            list.Add(Make(
                "disable_start_recs", TweakCategory.UiExplorer, false, true, true,
                "Disable Start Tips, Suggestions & Lock Ads", "Başlat Önerilerini ve Kilit Ekranı Reklamlarını Kapat", "关闭开始菜单推荐与锁屏广告提示",
                "Turns off Windows spotlight tips, Start recommendations, and promo suggestions.",
                "Başlat menüsü önerilerini, Windows ipuçlarını ve kilit ekranı reklamlarını kapatır.",
                "关闭开始菜单推广推荐、系统使用提示与锁屏界面趣味广告内容。",
                @"HKCU\...\ContentDeliveryManager & Start_IrisRecommendations = 0",
                delegate
                {
                    return RegHelper.ReadDword(Registry.CurrentUser, ExplorerAdv, "Start_IrisRecommendations", 1) == 0 &&
                           RegHelper.ReadDword(Registry.CurrentUser, ContentDeliv, "SubscribedContent-338388Enabled", 1) == 0;
                },
                delegate
                {
                    bool ok = true;
                    ok &= RegHelper.WriteDword(Registry.CurrentUser, ExplorerAdv, "Start_IrisRecommendations", 0);
                    ok &= RegHelper.WriteDword(Registry.CurrentUser, ContentDeliv, "SubscribedContent-338388Enabled", 0);
                    ok &= RegHelper.WriteDword(Registry.CurrentUser, ContentDeliv, "SubscribedContent-338389Enabled", 0);
                    ok &= RegHelper.WriteDword(Registry.CurrentUser, ContentDeliv, "SubscribedContent-353694Enabled", 0);
                    ok &= RegHelper.WriteDword(Registry.CurrentUser, ContentDeliv, "SubscribedContent-353696Enabled", 0);
                    ok &= RegHelper.WriteDword(Registry.CurrentUser, ContentDeliv, "RotatingLockScreenOverlayEnabled", 0);
                    return ok;
                },
                delegate
                {
                    bool ok = true;
                    ok &= RegHelper.WriteDword(Registry.CurrentUser, ExplorerAdv, "Start_IrisRecommendations", 1);
                    ok &= RegHelper.WriteDword(Registry.CurrentUser, ContentDeliv, "SubscribedContent-338388Enabled", 1);
                    ok &= RegHelper.WriteDword(Registry.CurrentUser, ContentDeliv, "SubscribedContent-338389Enabled", 1);
                    ok &= RegHelper.WriteDword(Registry.CurrentUser, ContentDeliv, "SubscribedContent-353694Enabled", 1);
                    ok &= RegHelper.WriteDword(Registry.CurrentUser, ContentDeliv, "SubscribedContent-353696Enabled", 1);
                    ok &= RegHelper.WriteDword(Registry.CurrentUser, ContentDeliv, "RotatingLockScreenOverlayEnabled", 1);
                    return ok;
                }
            ));

            // ==================== 2. PRIVACY & TELEMETRY ====================

            list.Add(Make(
                "disable_ad_id", TweakCategory.Privacy, false, true, false,
                "Disable Windows Advertising ID", "Windows Reklam Kimliğini Kapat", "禁用 Windows 广告 ID 追踪",
                "Prevents apps from using your system Advertising ID for targeted ads.",
                "Uygulamaların hedefli reklam için Windows reklam kimliğinizi kullanmasını engeller.",
                "阻止应用使用系统广告 ID 进行跨应用个性化广告追踪。",
                @"HKCU\...\AdvertisingInfo\Enabled = 0",
                delegate { return RegHelper.ReadDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 1) == 0; },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0); },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 1); }
            ));

            list.Add(Make(
                "disable_tailored_exp", TweakCategory.Privacy, false, true, false,
                "Disable Tailored Diagnostic Experiences", "Kişiselleştirilmiş Tanılama Önerilerini Kapat", "关闭基于诊断数据的个性化体验",
                "Stops Microsoft from using diagnostic data for personalized tips and offers.",
                "Tanılama verilerinizin kişisel öneri ve teklifler için kullanılmasını durdurur.",
                "禁止微软利用系统诊断数据推送个性化推荐与提示。",
                @"HKCU\...\Privacy\TailoredExperiencesWithDiagnosticDataEnabled = 0",
                delegate { return RegHelper.ReadDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 1) == 0; },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0); },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 1); }
            ));

            list.Add(Make(
                "hide_copilot_btn", TweakCategory.Privacy, false, false, false,
                "Hide Copilot Button on Taskbar", "Görev Çubuğundan Copilot Düğmesini Gizle", "隐藏任务栏 Copilot 按钮",
                "Removes the Copilot shortcut icon from the taskbar cleanly (reversible).",
                "Görev çubuğundaki Copilot simgesini temiz şekilde gizler.",
                "从任务栏隐藏 Copilot 快捷按钮（可随时一键恢复）。",
                @"HKCU\...\Explorer\Advanced\ShowCopilotButton = 0",
                delegate { return RegHelper.ReadDword(Registry.CurrentUser, ExplorerAdv, "ShowCopilotButton", 1) == 0; },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, ExplorerAdv, "ShowCopilotButton", 0); },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, ExplorerAdv, "ShowCopilotButton", 1); }
            ));

            list.Add(Make(
                "reduce_telemetry", TweakCategory.Privacy, true, true, false,
                "Reduce Telemetry to Required Minimum", "Telemetriyi Zorunlu Asgari Düzeye İndir", "将系统遥测降至最低必需级别",
                "Sets Windows diagnostic telemetry policy to Basic/Required (1) without breaking updates.",
                "Güncellemeleri bozmadan Windows tanılama telemetrisini en düşük düzeye (1) çeker.",
                "将 Windows 诊断遥测策略设为最低必需级别（1），在不影响系统更新的前提下保护隐私。",
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection\AllowTelemetry = 1",
                delegate { return RegHelper.ReadDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", -1) == 1; },
                delegate { return RegHelper.WriteDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 1); },
                delegate { return RegHelper.DeleteValueSafe(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry"); }
            ));

            list.Add(Make(
                "disable_activity_history", TweakCategory.Privacy, true, true, false,
                "Disable Activity History Tracking", "Etkinlik Geçmişi Takibini Kapat", "禁用系统活动历史记录追踪",
                "Stops Windows from logging and uploading local app/file activity history.",
                "Windows'un uygulama ve dosya etkinlik geçmişini kaydedip yüklemesini kapatır.",
                "停止 Windows 在本地记录并向云端同步应用与文件活动历史。",
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\System\PublishUserActivities = 0",
                delegate { return RegHelper.ReadDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 1) == 0; },
                delegate
                {
                    bool a = RegHelper.WriteDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0);
                    bool b = RegHelper.WriteDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0);
                    return a && b;
                },
                delegate
                {
                    bool a = RegHelper.DeleteValueSafe(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities");
                    bool b = RegHelper.DeleteValueSafe(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities");
                    return a && b;
                }
            ));

            list.Add(Make(
                "disable_consumer_bloat", TweakCategory.Privacy, true, true, false,
                "Block Auto-Installed Promo Apps", "Otomatik Kurulan Sponsorlu Uygulamaları Engelle", "阻止系统自动静默安装推广应用",
                "Prevents Windows from silently installing suggested Store apps after updates.",
                "Güncellemelerden sonra Windows'un kendiliğinden sponsorlu uygulama kurmasını engeller.",
                "阻止 Windows 更新后自动静默安装微软商店推广软件与休闲游戏。",
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent\DisableWindowsConsumerFeatures = 1",
                delegate { return RegHelper.ReadDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 0) == 1; },
                delegate { return RegHelper.WriteDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1); },
                delegate { return RegHelper.DeleteValueSafe(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures"); }
            ));

            // ==================== 3. PERFORMANCE & POWER ====================

            list.Add(Make(
                "enable_game_mode", TweakCategory.Performance, false, true, false,
                "Enable Windows Game Mode", "Windows Oyun Modunu Etkinleştir", "开启 Windows 游戏模式",
                "Prioritizes game processes and suppresses background update interruptions while playing.",
                "Oyun oynarken arka plan görevlerini kısıtlayıp FPS kararlılığını artırır.",
                "游戏时优先分配系统资源，抑制后台更新与通知干扰，提升帧率稳定性。",
                @"HKCU\Software\Microsoft\GameBar\AutoGameModeEnabled = 1",
                delegate { return RegHelper.ReadDword(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1) == 1; },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1); },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 0); }
            ));

            list.Add(Make(
                "disable_mouse_accel", TweakCategory.Performance, false, false, false,
                "Disable Mouse Acceleration (1:1 Raw Aim)", "Fare İvmesini Kapat (Hassasiyeti Artır Kapat)", "关闭鼠标指针精确度加速 (1:1 定位)",
                "Turns off 'Enhance pointer precision' for consistent 1:1 mouse movement in games.",
                "Oyunlarda ve günlük kullanımda tutarlı 1:1 fare hareketi için ivmelenmeyi kapatır.",
                "关闭「提高指针精确度」，在 FPS 游戏与日常操作中获得一致的 1:1 鼠标移动。",
                @"HKCU\Control Panel\Mouse\MouseSpeed = 0",
                delegate { return RegHelper.ReadString(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "1") == "0"; },
                delegate
                {
                    bool a = RegHelper.WriteString(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "0");
                    bool b = RegHelper.WriteString(Registry.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", "0");
                    bool c = RegHelper.WriteString(Registry.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", "0");
                    return a && b && c;
                },
                delegate
                {
                    bool a = RegHelper.WriteString(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "1");
                    bool b = RegHelper.WriteString(Registry.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", "6");
                    bool c = RegHelper.WriteString(Registry.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", "10");
                    return a && b && c;
                }
            ));

            list.Add(Make(
                "enable_hags", TweakCategory.Performance, true, false, false,
                "Hardware-Accelerated GPU Scheduling (HAGS)", "Donanım Hızlandırmalı GPU Zamanlaması (HAGS)", "开启硬件加速 GPU 计划 (HAGS)",
                "Offloads GPU frame scheduling to the graphics card (reboot required for change).",
                "GPU kare zamanlamasını ekran kartına devreder (değişiklik için yeniden başlatma gerekir).",
                "由显卡硬件直接负责帧调度，有助于降低延迟（更改后需重启生效）。",
                @"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\HwSchMode = 2",
                delegate { return RegHelper.ReadDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 1) == 2; },
                delegate { return RegHelper.WriteDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2); },
                delegate { return RegHelper.WriteDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 1); }
            ));

            list.Add(Make(
                "high_perf_power", TweakCategory.Performance, true, false, false,
                "High Performance Power Plan", "Yüksek Performans Güç Planı", "启用高性能电源计划",
                "Activates Windows High Performance power scheme (revert restores Balanced).",
                "Windows Yüksek Performans güç planını etkinleştirir (geri alınca Dengeli olur).",
                "切换为 Windows 高性能电源方案（还原时切回默认「平衡」方案）。",
                "powercfg /setactive High Performance",
                delegate { return IsHighPerformancePowerActive(); },
                delegate { return RunPowerCfg("/setactive " + HighPerfGuid); },
                delegate { return RunPowerCfg("/setactive " + BalancedGuid); }
            ));

            list.Add(Make(
                "disable_hibernation", TweakCategory.Performance, true, false, false,
                "Disable Hibernation (Free hiberfil.sys)", "Hazırda Bekletmeyi Kapat (hiberfil.sys Temizle)", "关闭休眠并释放 hiberfil.sys 空间",
                "Disables hibernation on desktop PCs to reclaim several GB of C: drive space.",
                "Masaüstü PC'lerde hazırda bekletmeyi kapatıp C: diskinde GB'larca yer açar.",
                "适合台式机：关闭系统休眠功能，可立即释放数 GB 的 C 盘 hiberfil.sys 空间。",
                "powercfg /h off (revert: powercfg /h on)",
                delegate { return RegHelper.ReadDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", 1) == 0; },
                delegate { return RunPowerCfg("/h off"); },
                delegate { return RunPowerCfg("/h on"); }
            ));

            return list;
        }

        private static TweakDefinition Make(
            string id, TweakCategory cat, bool requiresAdmin, bool recommended, bool needsExplorerRestart,
            string nEn, string nTr, string nZh, string dEn, string dTr, string dZh, string hint,
            Func<bool> check, Func<bool> apply, Func<bool> revert)
        {
            TweakDefinition t = new TweakDefinition();
            t.Id = id; t.Category = cat; t.RequiresAdmin = requiresAdmin;
            t.InRecommendedPreset = recommended; t.NeedsExplorerRestart = needsExplorerRestart;
            t.NameEn = nEn; t.NameTr = nTr; t.NameZh = nZh;
            t.DescEn = dEn; t.DescTr = dTr; t.DescZh = dZh;
            t.TargetHint = hint;
            t.CheckApplied = check; t.ApplyAction = apply; t.RevertAction = revert;
            return t;
        }

        public static void RefreshLiveStates(List<TweakDefinition> list)
        {
            foreach (TweakDefinition t in list)
            {
                bool cur = false;
                try { cur = t.CheckApplied != null && t.CheckApplied(); } catch { }
                t.IsCurrentlyApplied = cur;
                t.DesiredState = cur;
            }
        }

        public static string SaveSnapshotBackup(List<TweakDefinition> list)
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(Path.Combine(appData, "WinForge"), "backups");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "snapshot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".ini");
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# WinForge Tweak Snapshot — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                foreach (TweakDefinition t in list)
                    sb.AppendLine(t.Id + "=" + (t.IsCurrentlyApplied ? "1" : "0"));
                File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
                return file;
            }
            catch
            {
                return "";
            }
        }

        public static TweakApplyResult ApplyPendingChanges(List<TweakDefinition> list, bool isAdmin, Action<string> log)
        {
            TweakApplyResult res = new TweakApplyResult();
            res.BackupFilePath = SaveSnapshotBackup(list);
            if (!string.IsNullOrEmpty(res.BackupFilePath) && log != null)
                log("Backup saved: " + res.BackupFilePath);

            foreach (TweakDefinition t in list)
            {
                if (t.DesiredState == t.IsCurrentlyApplied)
                    continue;

                if (t.RequiresAdmin && !isAdmin)
                {
                    res.SkippedAdminCount++;
                    if (log != null) log("[SKIP - Admin Required] " + t.NameEn);
                    continue;
                }

                bool ok;
                if (t.DesiredState)
                {
                    ok = t.ApplyAction != null && t.ApplyAction();
                    if (ok)
                    {
                        res.AppliedCount++;
                        if (t.NeedsExplorerRestart) res.RecommendExplorerRestart = true;
                        if (log != null) log("[APPLIED] " + t.NameEn);
                    }
                    else
                    {
                        res.FailedCount++;
                        if (log != null) log("[FAILED] " + t.NameEn);
                    }
                }
                else
                {
                    ok = t.RevertAction != null && t.RevertAction();
                    if (ok)
                    {
                        res.RevertedCount++;
                        if (t.NeedsExplorerRestart) res.RecommendExplorerRestart = true;
                        if (log != null) log("[REVERTED] " + t.NameEn);
                    }
                    else
                    {
                        res.FailedCount++;
                        if (log != null) log("[FAILED REVERT] " + t.NameEn);
                    }
                }
            }

            RefreshLiveStates(list);
            return res;
        }

        private static bool IsHighPerformancePowerActive()
        {
            try
            {
                StringBuilder outBuf = new StringBuilder();
                int rc = WinNative.RunProcessStreaming("powercfg.exe", "/getactivescheme", delegate(string line) { outBuf.AppendLine(line); }, null);
                if (rc != 0) return false;
                string txt = outBuf.ToString().ToLowerInvariant();
                return txt.Contains(HighPerfGuid);
            }
            catch
            {
                return false;
            }
        }

        private static bool RunPowerCfg(string args)
        {
            try
            {
                return WinNative.RunProcessStreaming("powercfg.exe", args, null, null) == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
