using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
        public bool InLaptopPreset;
        public bool NeedsExplorerRestart;
        public bool DefaultWindowsState;

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
        private const string UltPerfTemplateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
        private const string EdgePolicyKey = @"SOFTWARE\Policies\Microsoft\Edge";
        private const string WinForgeMarkerKey = @"SOFTWARE\WinForge";

        // Curated safe background/telemetry/legacy Windows services verified on Win10/11
        // Never touches Networking, Audio, Defender, Store, Bluetooth, Print, or Update services.
        internal static readonly string[] SafeManualServices = new string[]
        {
            "MapsBroker", "RetailDemo", "WMPNetworkSvc", "WerSvc", "SysMain",
            "lfsvc", "WalletService", "wisvc", "XblAuthManager", "XblGameSave",
            "XboxNetApiSvc", "XboxGipSvc", "TrkWks", "WpcMonSvc", "TroubleshootingSvc", "PcaSvc"
        };

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

            list.Add(Make(
                "disable_sticky_keys", TweakCategory.UiExplorer, false, true, false,
                "Disable Sticky Keys Shortcut Prompt", "Yapışkan Tuşlar Uyarısını Kapat (5x Shift)", "关闭粘滞键弹窗提示（连按 5 次 Shift）",
                "Stops the loud Sticky Keys popup when pressing Shift 5 times in games.",
                "Oyunlarda art arda 5 kez Shift'e basınca çıkan Yapışkan Tuşlar uyarısını kapatır.",
                "关闭连按 5 次 Shift 弹出的粘滞键提示窗口，避免游戏中误触中断。",
                @"HKCU\Control Panel\Accessibility\StickyKeys\Flags = 506",
                delegate
                {
                    int fl;
                    string v = RegHelper.ReadString(Registry.CurrentUser, @"Control Panel\Accessibility\StickyKeys", "Flags", "510");
                    return int.TryParse(v, out fl) ? ((fl & 4) == 0) : (v == "506");
                },
                delegate { return RegHelper.WriteString(Registry.CurrentUser, @"Control Panel\Accessibility\StickyKeys", "Flags", "506"); },
                delegate { return RegHelper.WriteString(Registry.CurrentUser, @"Control Panel\Accessibility\StickyKeys", "Flags", "510"); }
            ));

            list.Add(Make(
                "numlock_startup", TweakCategory.UiExplorer, false, false, false,
                "Enable NumLock on Startup", "Açılışta NumLock Otomatik Açık Olsun", "开机自动开启 NumLock 数字小键盘",
                "Automatically keeps NumLock enabled when logging into Windows.",
                "Windows açılışında sayısal klavye NumLock tuşunun açık gelmesini sağlar.",
                "登录 Windows 时自动保持小键盘 NumLock 处于开启状态。",
                @"HKCU\Control Panel\Keyboard\InitialKeyboardIndicators = 2",
                delegate
                {
                    string v = RegHelper.ReadString(Registry.CurrentUser, @"Control Panel\Keyboard", "InitialKeyboardIndicators", "0");
                    return v == "2" || v == "2147483650";
                },
                delegate { return RegHelper.WriteString(Registry.CurrentUser, @"Control Panel\Keyboard", "InitialKeyboardIndicators", "2"); },
                delegate { return RegHelper.WriteString(Registry.CurrentUser, @"Control Panel\Keyboard", "InitialKeyboardIndicators", "0"); }
            ));

            list.Add(Make(
                "always_show_scrollbars", TweakCategory.UiExplorer, false, false, false,
                "Always Show Windows Scrollbars", "Kaydırma Çubuklarını Her Zaman Göster", "始终显示 Windows 滚动条",
                "Prevents Windows 11 Settings and UWP apps from auto-hiding scrollbars.",
                "Windows 11 Ayarlar ve modern uygulamalarda kaydırma çubuklarının gizlenmesini önler.",
                "禁止 Windows 11 设置与现代应用自动隐藏细滚动条。",
                @"HKCU\Control Panel\Accessibility\DynamicScrollbars = 0",
                delegate { return RegHelper.ReadDword(Registry.CurrentUser, @"Control Panel\Accessibility", "DynamicScrollbars", 1) == 0; },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, @"Control Panel\Accessibility", "DynamicScrollbars", 0); },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, @"Control Panel\Accessibility", "DynamicScrollbars", 1); }
            ));

            list.Add(Make(
                "hide_task_view", TweakCategory.UiExplorer, false, false, false,
                "Hide Task View Button on Taskbar", "Görev Çubuğundan Görev Görünümünü Gizle", "隐藏任务栏「任务视图」按钮",
                "Declutters the taskbar by hiding the Task View icon (Win+Tab still works).",
                "Görev çubuğunu sadeleştirir (Win+Tab kısayolu çalışmaya devam eder).",
                "精简任务栏图标布局（Win+Tab 快捷键依然正常可用）。",
                @"HKCU\...\Explorer\Advanced\ShowTaskViewButton = 0",
                delegate { return RegHelper.ReadDword(Registry.CurrentUser, ExplorerAdv, "ShowTaskViewButton", 1) == 0; },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, ExplorerAdv, "ShowTaskViewButton", 0); },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, ExplorerAdv, "ShowTaskViewButton", 1); }
            ));

            list.Add(Make(
                "hide_settings_home", TweakCategory.UiExplorer, false, false, false,
                "Hide Windows Settings 'Home' Page", "Windows Ayarlar 'Giriş' Sayfasını Gizle", "隐藏系统设置「主页」推荐页",
                "Skips the Microsoft 365/OneDrive promo Home page and opens Settings directly on System.",
                "Ayarlar açıldığında Microsoft 365 önerileri yerine doğrudan Sistem sekmesini açar.",
                "打开系统设置时跳过广告推广主页，直接进入「系统」设置项。",
                @"HKCU\...\Policies\Explorer\SettingsPageVisibility = hide:home",
                delegate
                {
                    string v = RegHelper.ReadString(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "SettingsPageVisibility", "");
                    return v.IndexOf("hide:home", StringComparison.OrdinalIgnoreCase) >= 0;
                },
                delegate { return RegHelper.WriteString(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "SettingsPageVisibility", "hide:home"); },
                delegate { return RegHelper.DeleteValueSafe(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "SettingsPageVisibility"); }
            ));

            list.Add(Make(
                "visual_fx_perf", TweakCategory.UiExplorer, false, false, true,
                "Visual Effects: Best Performance (Keep Smooth Fonts)", "Görsel Efektler: En İyi Performans (Net Yazı Korunur)", "视觉效果：最佳性能（保留字体平滑）",
                "Disables heavy window animations and shadows while keeping crisp ClearType fonts.",
                "Ağır pencere animasyonlarını kapatırken ClearType yazı netliğini korur.",
                "关闭多余窗口动画与阴影以加快响应，同时保留清晰的字体边缘平滑。",
                @"HKCU\...\VisualEffects\VisualFXSetting = 2",
                delegate { return RegHelper.ReadDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 0) == 2; },
                delegate
                {
                    bool a = RegHelper.WriteDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2);
                    bool b = RegHelper.WriteString(Registry.CurrentUser, @"Control Panel\Desktop", "FontSmoothing", "2");
                    return a && b;
                },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 0); }
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

            list.Add(Make(
                "disable_widgets", TweakCategory.Privacy, true, true, true,
                "Disable Windows Widgets & News Feed", "Windows Araç Takımlarını (Widgets) Kapat", "关闭 Windows 小组件与资讯流",
                "Disables the taskbar Widgets panel and MSN news feed background process.",
                "Görev çubuğu araç takımlarını ve arka plandaki MSN haber akışını kapatır.",
                "禁用任务栏小组件面板与后台 MSN 资讯推送进程，节省内存。",
                @"HKLM\SOFTWARE\Policies\Microsoft\Dsh\AllowNewsAndInterests = 0",
                delegate { return RegHelper.ReadDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 1) == 0; },
                delegate { return RegHelper.WriteDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0); },
                delegate { return RegHelper.DeleteValueSafe(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests"); }
            ));

            list.Add(Make(
                "disable_location", TweakCategory.Privacy, true, true, false,
                "Disable System Location Tracking", "Sistem Konum Takibini Kapat", "禁用系统定位追踪服务",
                "Denies global Windows location tracking consent (reversible anytime).",
                "Genel Windows konum erişimi iznini kapatır (istediğiniz zaman geri açılır).",
                "关闭 Windows 全局定位追踪权限，提升隐私保护（可随时恢复）。",
                @"HKLM\...\ConsentStore\location\Value = Deny",
                delegate { return RegHelper.ReadString(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", "Allow").Equals("Deny", StringComparison.OrdinalIgnoreCase); },
                delegate { return RegHelper.WriteString(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", "Deny"); },
                delegate { return RegHelper.WriteString(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", "Allow"); }
            ));

            list.Add(Make(
                "disable_delivery_opt", TweakCategory.Privacy, true, true, false,
                "Disable Delivery Optimization (P2P Updates)", "Teslim İyileştirme (P2P Güncelleme Paylaşımı) Kapat", "关闭传递优化（禁止 P2P 上传更新）",
                "Stops Windows Update from using your upload bandwidth to share updates with other PCs.",
                "Windows Update'in internet yükleme hızınızı diğer PC'lere güncelleme dağıtmak için kullanmasını engeller.",
                "阻止 Windows 更新占用你的上行带宽向局域网或互联网其他电脑分发更新。",
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization\DODownloadMode = 0",
                delegate { return RegHelper.ReadDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", -1) == 0; },
                delegate { return RegHelper.WriteDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0); },
                delegate { return RegHelper.DeleteValueSafe(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode"); }
            ));

            list.Add(Make(
                "disable_windows_ai", TweakCategory.Privacy, true, false, false,
                "Disable Windows Copilot & AI Recall Policy", "Windows Copilot ve AI Recall Politikası Kapat", "禁用 Windows Copilot 与 AI 回顾策略",
                "Applies official Microsoft Group Policy to turn off Windows Copilot & Recall snapshots.",
                "Resmi Grup İlkesi ile Windows Copilot ve AI Recall ekran analizini devre dışı bırakır.",
                "通过微软官方组策略禁用 Windows Copilot 与 AI Recall 屏幕回顾分析功能。",
                @"HKLM\...\WindowsCopilot\TurnOffWindowsCopilot = 1 & WindowsAI",
                delegate { return RegHelper.ReadDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 0) == 1; },
                delegate
                {
                    bool a = RegHelper.WriteDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1);
                    bool b = RegHelper.WriteDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1);
                    return a && b;
                },
                delegate
                {
                    bool a = RegHelper.DeleteValueSafe(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot");
                    bool b = RegHelper.DeleteValueSafe(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis");
                    return a && b;
                }
            ));

            list.Add(Make(
                "edge_debloat_policy", TweakCategory.Privacy, true, true, false,
                "Microsoft Edge Debloat Policy (Non-Destructive)", "Microsoft Edge Sadeleştirme Politikası", "精简 Microsoft Edge 后台与侧边栏策略",
                "Disables Edge Startup Boost, Background Mode, Sidebar & Shopping Assistant via Policy.",
                "Edge'i silmeden arka plan çalışmasını, hızlı başlatmayı, kenar çubuğunu ve alışveriş asistanını kapatır.",
                "不卸载 Edge，仅通过官方策略关闭开机后台驻留、启动增强、侧边栏与购物弹窗。",
                @"HKLM\SOFTWARE\Policies\Microsoft\Edge\*",
                delegate { return RegHelper.ReadDword(Registry.LocalMachine, EdgePolicyKey, "StartupBoostEnabled", 1) == 0 && RegHelper.ReadDword(Registry.LocalMachine, EdgePolicyKey, "BackgroundModeEnabled", 1) == 0; },
                delegate
                {
                    bool ok = true;
                    ok &= RegHelper.WriteDword(Registry.LocalMachine, EdgePolicyKey, "BackgroundModeEnabled", 0);
                    ok &= RegHelper.WriteDword(Registry.LocalMachine, EdgePolicyKey, "StartupBoostEnabled", 0);
                    ok &= RegHelper.WriteDword(Registry.LocalMachine, EdgePolicyKey, "HubsSidebarEnabled", 0);
                    ok &= RegHelper.WriteDword(Registry.LocalMachine, EdgePolicyKey, "EdgeShoppingAssistantEnabled", 0);
                    ok &= RegHelper.WriteDword(Registry.LocalMachine, EdgePolicyKey, "HideFirstRunExperience", 1);
                    return ok;
                },
                delegate
                {
                    RegHelper.DeleteValueSafe(Registry.LocalMachine, EdgePolicyKey, "BackgroundModeEnabled");
                    RegHelper.DeleteValueSafe(Registry.LocalMachine, EdgePolicyKey, "StartupBoostEnabled");
                    RegHelper.DeleteValueSafe(Registry.LocalMachine, EdgePolicyKey, "HubsSidebarEnabled");
                    RegHelper.DeleteValueSafe(Registry.LocalMachine, EdgePolicyKey, "EdgeShoppingAssistantEnabled");
                    RegHelper.DeleteValueSafe(Registry.LocalMachine, EdgePolicyKey, "HideFirstRunExperience");
                    return true;
                }
            ));

            list.Add(Make(
                "disable_bg_apps", TweakCategory.Privacy, false, false, false,
                "Disable UWP Background Apps", "Arka Plan UWP Uygulamalarını Kapat", "禁用 UWP 应用后台运行",
                "Prevents idle Store apps from waking up in the background to save RAM & battery.",
                "Kullanılmayan Mağaza uygulamalarının arka planda çalışıp RAM ve pil tüketmesini önler.",
                "禁止微软商店 UWP 应用在后台静默运行，降低内存占用并延长续航。",
                @"HKCU\...\BackgroundAccessApplications\GlobalUserDisabled = 1",
                delegate { return RegHelper.ReadDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 0) == 1; },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1); },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 0); }
            ));

            // ==================== 3. PERFORMANCE & POWER ====================

            TweakDefinition gameMode = Make(
                "enable_game_mode", TweakCategory.Performance, false, true, false,
                "Enable Windows Game Mode", "Windows Oyun Modunu Etkinleştir", "开启 Windows 游戏模式",
                "Prioritizes game processes and suppresses background update interruptions while playing.",
                "Oyun oynarken arka plan görevlerini kısıtlayıp FPS kararlılığını artırır.",
                "游戏时优先分配系统资源，抑制后台更新与通知干扰，提升帧率稳定性。",
                @"HKCU\Software\Microsoft\GameBar\AutoGameModeEnabled = 1",
                delegate { return RegHelper.ReadDword(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1) == 1; },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1); },
                delegate { return RegHelper.WriteDword(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 0); }
            );
            gameMode.DefaultWindowsState = true;
            list.Add(gameMode);

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
                delegate { return EnableHighPerformancePower(); },
                delegate { return RunPowerCfg("/setactive " + BalancedGuid); }
            ));

            list.Add(Make(
                "disable_hibernation", TweakCategory.Performance, true, true, false,
                "Disable Hibernation (Desktop · Free hiberfil.sys)", "Hazırda Bekletmeyi Kapat (Masaüstü · hiberfil.sys)", "关闭休眠并释放 hiberfil.sys 空间（台式机）",
                "Disables hibernation on desktop PCs to reclaim several GB of C: drive space (excluded from Laptop preset).",
                "Masaüstü PC'lerde hazırda bekletmeyi kapatıp C: diskinde GB'larca yer açar (Dizüstü profilinde kapalı).",
                "适合台式机：关闭系统休眠释放数 GB 的 C 盘空间（笔记本推荐配置默认不勾选）。",
                "powercfg /h off (revert: powercfg /h on)",
                delegate { return RegHelper.ReadDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", 1) == 0; },
                delegate { return RunPowerCfg("/h off"); },
                delegate { return RunPowerCfg("/h on"); },
                false // laptopPreset = false!
            ));

            list.Add(Make(
                "ultimate_perf_power", TweakCategory.Performance, true, false, false,
                "Ultimate Performance Power Plan (Desktop)", "Nihai Performans Güç Planı (Masaüstü)", "启用「卓越性能」电源方案（台式机）",
                "Unlocks & activates Windows' hidden Ultimate Performance scheme (revert restores Balanced).",
                "Gizli Windows Nihai Performans güç planını açıp etkinleştirir (geri alınca Dengeli olur).",
                "解锁并激活 Windows 隐藏的「卓越性能」电源计划（还原切回默认平衡方案）。",
                "powercfg -duplicatescheme e9a42b02-... & /setactive",
                delegate { return IsUltimatePerformancePowerActive(); },
                delegate { return EnableUltimatePerformancePower(); },
                delegate { return RunPowerCfg("/setactive " + BalancedGuid); },
                false
            ));

            list.Add(Make(
                "services_manual", TweakCategory.Performance, true, true, false,
                "Set Safe Non-Essential Services to Manual", "Gereksiz Windows Servislerini 'Manuel'e Al", "将非核心系统服务设为「手动」启动",
                "Chris Titus classic: switches 16 safe idle services from Auto to Manual (auto-saves exact originals for 100% revert).",
                "Chris Titus klasiği: 16 güvenli servisi Otomatik'ten Manuel'e alır; orijinal değerleri yedekler ve %100 geri alır.",
                "经典安全优化：将 16 项非关键服务从自动改为手动，修改前自动保存原始状态，随时 100% 还原。",
                @"HKLM\SYSTEM\CurrentControlSet\Services\* (Start: 2 -> 3)",
                delegate { return RegHelper.ReadDword(Registry.LocalMachine, WinForgeMarkerKey, "ServicesManualApplied", 0) == 1; },
                delegate { return ApplySafeServicesToManual(); },
                delegate { return RevertSafeServicesFromBackup(); },
                true
            ));

            list.Add(Make(
                "enable_long_paths", TweakCategory.Performance, true, true, false,
                "Enable NTFS Long File Paths (>260 chars)", "NTFS Uzun Dosya Yollarını Etkinleştir (>260)", "启用 NTFS 长文件路径支持（突破 260 字符）",
                "Removes the legacy 260-character MAX_PATH limit for Git, Node.js, and deep project trees.",
                "Git, Node.js ve derin proje klasörleri için eski 260 karakter yol sınırını kaldırır.",
                "解除传统 260 字符路径长度限制，避免 Git / Node.js 与嵌套目录报错。",
                @"HKLM\SYSTEM\CurrentControlSet\Control\FileSystem\LongPathsEnabled = 1",
                delegate { return RegHelper.ReadDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\FileSystem", "LongPathsEnabled", 0) == 1; },
                delegate { return RegHelper.WriteDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\FileSystem", "LongPathsEnabled", 1); },
                delegate { return RegHelper.WriteDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\FileSystem", "LongPathsEnabled", 0); },
                true
            ));

            list.Add(Make(
                "verbose_boot_bsod", TweakCategory.Performance, true, false, false,
                "Verbose Boot / Logon & Detailed BSoD Info", "Ayrıntılı Açılış / Oturum ve BSoD Bilgisi", "开启详细开机状态与蓝屏诊断参数",
                "Shows detailed service status during boot/shutdown and hex diagnostics on crash screens.",
                "Açılış/kapanışta hangi servisin çalıştığını ve hata ekranında ayrıntılı kodları gösterir.",
                "在开机与关机时显示详细服务加载状态，并在蓝屏界面展示完整故障参数。",
                @"HKLM\...\Policies\System\VerboseStatus = 1 & CrashControl\DisplayParameters = 1",
                delegate { return RegHelper.ReadDword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "VerboseStatus", 0) == 1; },
                delegate
                {
                    bool a = RegHelper.WriteDword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "VerboseStatus", 1);
                    bool b = RegHelper.WriteDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\CrashControl", "DisplayParameters", 1);
                    return a && b;
                },
                delegate
                {
                    bool a = RegHelper.WriteDword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "VerboseStatus", 0);
                    bool b = RegHelper.WriteDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\CrashControl", "DisplayParameters", 0);
                    return a && b;
                },
                false
            ));

            list.Add(Make(
                "prefer_ipv4", TweakCategory.Performance, true, false, false,
                "Prefer IPv4 Over IPv6 (Keep Loopback Safe)", "IPv4'ü IPv6'ya Tercih Et (Güvenli RFC)", "优先使用 IPv4（保留 IPv6 本地环回）",
                "Uses Microsoft's official DisabledComponents = 0x20 to prefer IPv4 without breaking Windows services.",
                "Microsoft'un resmi 0x20 değerini kullanarak Windows servislerini bozmadan IPv4'ü önceliklendirir.",
                "采用微软官方 0x20 参数优先走 IPv4 路由，同时保留系统本地环回，不破坏任何服务。",
                @"HKLM\...\Services\Tcpip6\Parameters\DisabledComponents = 0x20",
                delegate { return RegHelper.ReadDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters", "DisabledComponents", 0) == 0x20; },
                delegate { return RegHelper.WriteDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters", "DisabledComponents", 0x20); },
                delegate { return RegHelper.DeleteValueSafe(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters", "DisabledComponents"); },
                false
            ));

            list.Add(Make(
                "disable_teredo", TweakCategory.Performance, true, false, false,
                "Disable Teredo IPv6 Tunneling", "Teredo IPv6 Tünellemeyi Kapat", "禁用 Teredo IPv6 隧道",
                "Disables legacy Teredo IPv6 transition tunneling via official Group Policy (reversible).",
                "Eski Teredo IPv6 geçiş tünellemesini resmi Grup İlkesi ile kapatır.",
                "通过官方组策略禁用老旧 Teredo 隧道过渡协议（如需 Xbox 联机可随时一键还原）。",
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\TCPIP\v6Transition\Teredo_State = Disabled",
                delegate { return RegHelper.ReadString(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\TCPIP\v6Transition", "Teredo_State", "").Equals("Disabled", StringComparison.OrdinalIgnoreCase); },
                delegate
                {
                    bool a = RegHelper.WriteString(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\TCPIP\v6Transition", "Teredo_State", "Disabled");
                    WinNative.RunProcessStreaming("netsh.exe", "interface teredo set state disabled", null, null);
                    return a;
                },
                delegate
                {
                    bool a = RegHelper.DeleteValueSafe(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\TCPIP\v6Transition", "Teredo_State");
                    WinNative.RunProcessStreaming("netsh.exe", "interface teredo set state default", null, null);
                    return a;
                },
                false
            ));

            list.Add(Make(
                "utc_hardware_clock", TweakCategory.Performance, true, false, false,
                "Use UTC Hardware Clock (Linux Dual-Boot)", "UTC Donanım Saati Kullan (Linux Dual-Boot)", "使用 UTC 硬件时钟（解决双系统时间错乱）",
                "Fixes the 3-hour / timezone clock jump when dual-booting Windows alongside Linux or macOS.",
                "Windows ve Linux/macOS çift önyüklemede (dual-boot) yaşanan saat kaymasını kalıcı olarak çözer.",
                "修复 Windows 与 Linux / macOS 双系统切换后系统时间偏差问题。",
                @"HKLM\...\Control\TimeZoneInformation\RealTimeIsUniversal = 1",
                delegate { return RegHelper.ReadDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\TimeZoneInformation", "RealTimeIsUniversal", 0) == 1; },
                delegate { return RegHelper.WriteDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\TimeZoneInformation", "RealTimeIsUniversal", 1); },
                delegate { return RegHelper.DeleteValueSafe(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\TimeZoneInformation", "RealTimeIsUniversal"); },
                false
            ));

            return list;
        }

        private static TweakDefinition Make(
            string id, TweakCategory cat, bool requiresAdmin, bool recommended, bool needsExplorerRestart,
            string nEn, string nTr, string nZh, string dEn, string dTr, string dZh, string hint,
            Func<bool> check, Func<bool> apply, Func<bool> revert)
        {
            return Make(id, cat, requiresAdmin, recommended, needsExplorerRestart, nEn, nTr, nZh, dEn, dTr, dZh, hint, check, apply, revert, recommended);
        }

        private static TweakDefinition Make(
            string id, TweakCategory cat, bool requiresAdmin, bool desktopPreset, bool needsExplorerRestart,
            string nEn, string nTr, string nZh, string dEn, string dTr, string dZh, string hint,
            Func<bool> check, Func<bool> apply, Func<bool> revert, bool laptopPreset)
        {
            TweakDefinition t = new TweakDefinition();
            t.Id = id; t.Category = cat; t.RequiresAdmin = requiresAdmin;
            t.InRecommendedPreset = desktopPreset; t.InLaptopPreset = laptopPreset;
            t.NeedsExplorerRestart = needsExplorerRestart;
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

        private static readonly Regex PowerGuidRegex = new Regex(
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
            RegexOptions.Compiled);

        internal static string ExtractPowerGuidFromLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return "";
            Match m = PowerGuidRegex.Match(line);
            return m.Success ? m.Value.ToLowerInvariant() : "";
        }

        private static bool IsHighPerformancePowerActive()
        {
            try
            {
                StringBuilder outBuf = new StringBuilder();
                int rc = WinNative.RunProcessStreaming("powercfg.exe", "/getactivescheme", delegate(string line) { outBuf.AppendLine(line); }, null);
                if (rc != 0) return false;
                string activeGuid = ExtractPowerGuidFromLine(outBuf.ToString());
                if (string.IsNullOrEmpty(activeGuid)) return false;
                if (string.Equals(activeGuid, HighPerfGuid, StringComparison.OrdinalIgnoreCase)) return true;
                string saved = RegHelper.ReadString(Registry.LocalMachine, WinForgeMarkerKey, "HighPerfGuid", "");
                return !string.IsNullOrEmpty(saved) && string.Equals(activeGuid, saved, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool EnableHighPerformancePower()
        {
            if (RunPowerCfg("/setactive " + HighPerfGuid)) return true;
            string saved = RegHelper.ReadString(Registry.LocalMachine, WinForgeMarkerKey, "HighPerfGuid", "");
            if (!string.IsNullOrEmpty(saved) && RunPowerCfg("/setactive " + saved)) return true;
            string dupGuid = DuplicatePowerSchemeAndGetGuid(HighPerfGuid);
            if (!string.IsNullOrEmpty(dupGuid))
            {
                RegHelper.WriteString(Registry.LocalMachine, WinForgeMarkerKey, "HighPerfGuid", dupGuid);
                return RunPowerCfg("/setactive " + dupGuid);
            }
            return false;
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

        private static string DuplicatePowerSchemeAndGetGuid(string templateGuid)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                if (WinNative.RunProcessStreaming("powercfg.exe", "-duplicatescheme " + templateGuid, delegate(string line) { sb.AppendLine(line); }, null) == 0)
                {
                    return ExtractPowerGuidFromLine(sb.ToString());
                }
            }
            catch { }
            return "";
        }

        internal static string FindUltimatePerformanceGuidFromList(string powerCfgListOutput)
        {
            if (string.IsNullOrEmpty(powerCfgListOutput)) return "";
            foreach (string raw in powerCfgListOutput.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (raw.IndexOf("Ultimate Performance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    raw.IndexOf("Nihai Performans", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    raw.IndexOf("卓越性能", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string guid = ExtractPowerGuidFromLine(raw);
                    if (!string.IsNullOrEmpty(guid)) return guid;
                }
            }
            return "";
        }

        private static string FindUltimatePerformanceGuid()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                if (WinNative.RunProcessStreaming("powercfg.exe", "/list", delegate(string line) { sb.AppendLine(line); }, null) != 0)
                    return "";
                string listOut = sb.ToString();
                string byName = FindUltimatePerformanceGuidFromList(listOut);
                if (!string.IsNullOrEmpty(byName)) return byName;

                string saved = RegHelper.ReadString(Registry.LocalMachine, WinForgeMarkerKey, "UltimatePerfGuid", "");
                if (!string.IsNullOrEmpty(saved) && listOut.IndexOf(saved, StringComparison.OrdinalIgnoreCase) >= 0)
                    return saved;
            }
            catch { }
            return "";
        }

        private static bool IsUltimatePerformancePowerActive()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                if (WinNative.RunProcessStreaming("powercfg.exe", "/getactivescheme", delegate(string line) { sb.AppendLine(line); }, null) != 0)
                    return false;
                string txt = sb.ToString();
                if (txt.IndexOf("Ultimate Performance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    txt.IndexOf("Nihai Performans", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    txt.IndexOf("卓越性能", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    txt.IndexOf(UltPerfTemplateGuid, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                string activeGuid = ExtractPowerGuidFromLine(txt);
                string saved = RegHelper.ReadString(Registry.LocalMachine, WinForgeMarkerKey, "UltimatePerfGuid", "");
                return !string.IsNullOrEmpty(activeGuid) && !string.IsNullOrEmpty(saved) &&
                       string.Equals(activeGuid, saved, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool EnableUltimatePerformancePower()
        {
            string guid = FindUltimatePerformanceGuid();
            if (string.IsNullOrEmpty(guid))
            {
                guid = DuplicatePowerSchemeAndGetGuid(UltPerfTemplateGuid);
                if (string.IsNullOrEmpty(guid)) guid = FindUltimatePerformanceGuid();
            }
            if (string.IsNullOrEmpty(guid)) guid = UltPerfTemplateGuid;
            if (!string.Equals(guid, UltPerfTemplateGuid, StringComparison.OrdinalIgnoreCase))
                RegHelper.WriteString(Registry.LocalMachine, WinForgeMarkerKey, "UltimatePerfGuid", guid);
            return RunPowerCfg("/setactive " + guid);
        }

        private static string GetServicesBackupFile()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(Path.Combine(Path.Combine(appData, "WinForge"), "backups"), "services-original.ini");
        }

        private static bool ApplySafeServicesToManual()
        {
            string bFile = GetServicesBackupFile();
            string dir = Path.GetDirectoryName(bFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // Save original Start modes ONCE before changing anything (never overwrite pre-tweak originals)
            if (!File.Exists(bFile))
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# WinForge Original Service Start Values — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                foreach (string svc in SafeManualServices)
                {
                    int cur = RegHelper.ReadDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\" + svc, "Start", -1);
                    if (cur >= 0) sb.AppendLine(svc + "=" + cur);
                }
                File.WriteAllText(bFile, sb.ToString(), Encoding.UTF8);
            }

            foreach (string svc in SafeManualServices)
            {
                string keyPath = @"SYSTEM\CurrentControlSet\Services\" + svc;
                int cur = RegHelper.ReadDword(Registry.LocalMachine, keyPath, "Start", -1);
                // Only switch Automatic (2) -> Manual (3); never re-enable Disabled (4) services
                if (cur == 2)
                    RegHelper.WriteDword(Registry.LocalMachine, keyPath, "Start", 3);
            }
            return RegHelper.WriteDword(Registry.LocalMachine, WinForgeMarkerKey, "ServicesManualApplied", 1);
        }

        private static bool RevertSafeServicesFromBackup()
        {
            string bFile = GetServicesBackupFile();
            if (File.Exists(bFile))
            {
                foreach (string raw in File.ReadAllLines(bFile, Encoding.UTF8))
                {
                    if (string.IsNullOrEmpty(raw) || raw.StartsWith("#")) continue;
                    int eq = raw.IndexOf('=');
                    if (eq <= 0) continue;
                    string svc = raw.Substring(0, eq).Trim();
                    int orig;
                    if (int.TryParse(raw.Substring(eq + 1).Trim(), out orig) && orig >= 0 && orig <= 4)
                    {
                        string keyPath = @"SYSTEM\CurrentControlSet\Services\" + svc;
                        if (RegHelper.KeyExists(Registry.LocalMachine, keyPath))
                            RegHelper.WriteDword(Registry.LocalMachine, keyPath, "Start", orig);
                    }
                }
            }
            else
            {
                string[] defaultAuto = new string[] { "MapsBroker", "SysMain", "TrkWks", "PcaSvc" };
                foreach (string svc in defaultAuto)
                {
                    string keyPath = @"SYSTEM\CurrentControlSet\Services\" + svc;
                    if (RegHelper.KeyExists(Registry.LocalMachine, keyPath))
                        RegHelper.WriteDword(Registry.LocalMachine, keyPath, "Start", 2);
                }
            }
            RegHelper.DeleteValueSafe(Registry.LocalMachine, WinForgeMarkerKey, "ServicesManualApplied");
            return true;
        }
    }

    // ==================== ZERO-DEPENDENCY JSON CONFIG EXPORT / IMPORT ====================
    internal static class WinForgeConfigIo
    {
        public static string SerializeProfile(List<WingetAppItem> apps, List<TweakDefinition> tweaks)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"winforge_version\": \"2.0.4\",");
            sb.AppendLine("  \"exported_at\": \"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\",");

            sb.Append("  \"apps\": [");
            bool first = true;
            if (apps != null)
            {
                foreach (WingetAppItem a in apps)
                {
                    if (!a.Selected) continue;
                    if (!first) sb.Append(", ");
                    sb.Append("\"" + EscapeJson(a.Id) + "\"");
                    first = false;
                }
            }
            sb.AppendLine("],");

            sb.Append("  \"tweaks\": [");
            first = true;
            if (tweaks != null)
            {
                foreach (TweakDefinition t in tweaks)
                {
                    if (!t.DesiredState) continue;
                    if (!first) sb.Append(", ");
                    sb.Append("\"" + EscapeJson(t.Id) + "\"");
                    first = false;
                }
            }
            sb.AppendLine("]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        public static void ExportProfileToFile(string path, List<WingetAppItem> apps, List<TweakDefinition> tweaks)
        {
            File.WriteAllText(path, SerializeProfile(apps, tweaks), Encoding.UTF8);
        }

        public static bool ImportProfileFromFile(string path, List<WingetAppItem> apps, List<TweakDefinition> tweaks, bool isAdmin, out int appsMatched, out int tweaksMatched)
        {
            appsMatched = 0;
            tweaksMatched = 0;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            string json = File.ReadAllText(path, Encoding.UTF8);
            return ApplySerializedProfile(json, apps, tweaks, isAdmin, out appsMatched, out tweaksMatched);
        }

        public static bool ApplySerializedProfile(string json, List<WingetAppItem> apps, List<TweakDefinition> tweaks, bool isAdmin, out int appsMatched, out int tweaksMatched)
        {
            appsMatched = 0;
            tweaksMatched = 0;
            if (string.IsNullOrEmpty(json)) return false;

            HashSet<string> appIds = ParseJsonStringArray(json, "apps");
            HashSet<string> tweakIds = ParseJsonStringArray(json, "tweaks");
            if (appIds.Count == 0 && tweakIds.Count == 0) return false;

            if (apps != null && appIds.Count > 0)
            {
                foreach (WingetAppItem a in apps)
                {
                    a.Selected = appIds.Contains(a.Id);
                    if (a.Selected) appsMatched++;
                }
            }
            if (tweaks != null && tweakIds.Count > 0)
            {
                foreach (TweakDefinition t in tweaks)
                {
                    if (t.RequiresAdmin && !isAdmin) continue;
                    bool want = tweakIds.Contains(t.Id);
                    t.DesiredState = want;
                    if (want) tweaksMatched++;
                }
            }
            return true;
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static HashSet<string> ParseJsonStringArray(string json, string fieldName)
        {
            HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string marker = "\"" + fieldName + "\"";
            int idx = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return set;
            int open = json.IndexOf('[', idx + marker.Length);
            if (open < 0) return set;
            int close = json.IndexOf(']', open + 1);
            if (close < 0) return set;

            string inner = json.Substring(open + 1, close - open - 1);
            int i = 0;
            while (i < inner.Length)
            {
                int q1 = inner.IndexOf('"', i);
                if (q1 < 0) break;
                StringBuilder item = new StringBuilder();
                int j = q1 + 1;
                while (j < inner.Length)
                {
                    char c = inner[j];
                    if (c == '\\' && j + 1 < inner.Length)
                    {
                        item.Append(inner[j + 1]);
                        j += 2;
                    }
                    else if (c == '"')
                    {
                        break;
                    }
                    else
                    {
                        item.Append(c);
                        j++;
                    }
                }
                string val = item.ToString().Trim();
                if (!string.IsNullOrEmpty(val)) set.Add(val);
                i = j + 1;
            }
            return set;
        }
    }
}
