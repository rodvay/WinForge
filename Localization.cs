using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WinForge
{
    internal static class Loc
    {
        public static string DetectSystemLanguage()
        {
            try
            {
                string two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                if (string.Equals(two, "tr", StringComparison.OrdinalIgnoreCase)) return "tr";
                if (string.Equals(two, "zh", StringComparison.OrdinalIgnoreCase)) return "zh";
            }
            catch
            {
            }
            return "en";
        }

        public static string T(string lang, string en, string tr, string zh)
        {
            if (string.Equals(lang, "tr", StringComparison.OrdinalIgnoreCase)) return tr;
            if (string.Equals(lang, "zh", StringComparison.OrdinalIgnoreCase)) return zh;
            return en;
        }

        public static string FontFamily(string lang)
        {
            return string.Equals(lang, "zh", StringComparison.OrdinalIgnoreCase) ? "Microsoft YaHei UI" : "Segoe UI";
        }

        public static CultureInfo Culture(string lang)
        {
            try
            {
                if (string.Equals(lang, "tr", StringComparison.OrdinalIgnoreCase)) return CultureInfo.GetCultureInfo("tr-TR");
                if (string.Equals(lang, "zh", StringComparison.OrdinalIgnoreCase)) return CultureInfo.GetCultureInfo("zh-CN");
                return CultureInfo.GetCultureInfo("en-US");
            }
            catch
            {
                return CultureInfo.InvariantCulture;
            }
        }
    }

    internal sealed class UserPrefs
    {
        public string Language;

        public UserPrefs()
        {
            Language = Loc.DetectSystemLanguage();
        }

        private static string GetPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(Path.Combine(appData, "WinForge"), "settings.ini");
        }

        public static UserPrefs Load()
        {
            UserPrefs p = new UserPrefs();
            try
            {
                string path = GetPath();
                if (!File.Exists(path)) return p;
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    if (k == "Language" && (v == "tr" || v == "en" || v == "zh"))
                        p.Language = v;
                }
            }
            catch { }
            return p;
        }

        public void Save()
        {
            try
            {
                string path = GetPath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, "Language=" + (Language ?? "en") + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }

    internal static class WinNative
    {
        public const int WM_DPICHANGED = 0x02E0;
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_CAPTION_COLOR = 35;
        public const int DWMWA_TEXT_COLOR = 36;
        private const int WM_SETTINGCHANGE = 0x001A;
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);

        public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd, int Msg, IntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private const int EM_SETCUEBANNER = 0x1501;

        public static void SetCueBanner(IntPtr hwnd, string text)
        {
            try
            {
                if (hwnd != IntPtr.Zero)
                    SendMessage(hwnd, EM_SETCUEBANNER, new IntPtr(1), text ?? "");
            }
            catch { }
        }

        public static void EnableDpiAwareness()
        {
            try { if (SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)) return; } catch { }
            try { SetProcessDPIAware(); } catch { }
        }

        public static void ApplyDarkTitleBar(IntPtr hwnd, Color captionColor, Color textColor)
        {
            try
            {
                int enabled = 1;
                if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref enabled, sizeof(int));

                int capRef = captionColor.R | (captionColor.G << 8) | (captionColor.B << 16);
                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref capRef, sizeof(int));

                int txtRef = textColor.R | (textColor.G << 8) | (textColor.B << 16);
                DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref txtRef, sizeof(int));
            }
            catch { }
        }

        public static void BroadcastSettingsChange(string area)
        {
            try
            {
                IntPtr res;
                SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, area, 0x0002, 1000, out res);
            }
            catch { }
        }

        public static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal p = new WindowsPrincipal(id);
                    return p.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool RestartAsAdministrator(string extraArgs)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                if (!string.IsNullOrEmpty(extraArgs))
                    psi.Arguments = extraArgs;
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string GetWindowsVersionBadge()
        {
            try
            {
                Version v = Environment.OSVersion.Version;
                string osName = (v.Major >= 10 && v.Build >= 22000) ? "Windows 11" : "Windows 10";
                return string.Format("{0} · Build {1}", osName, v.Build);
            }
            catch
            {
                return "Windows 10/11";
            }
        }

        public static bool RestartExplorerSafely(Action<string> log)
        {
            try
            {
                if (log != null) log("Stopping explorer.exe...");
                Process[] procs = Process.GetProcessesByName("explorer");
                for (int i = 0; i < procs.Length; i++)
                {
                    try
                    {
                        procs[i].Kill();
                        procs[i].WaitForExit(3000);
                    }
                    catch { }
                    finally { procs[i].Dispose(); }
                }

                Thread.Sleep(500);

                // Check if Windows auto-restarted explorer; if not, start it explicitly.
                Process[] check = Process.GetProcessesByName("explorer");
                bool running = check != null && check.Length > 0;
                if (check != null)
                    for (int i = 0; i < check.Length; i++) check[i].Dispose();

                if (!running)
                {
                    if (log != null) log("Starting explorer.exe...");
                    Process.Start("explorer.exe");
                }
                if (log != null) log("Windows Explorer restarted cleanly.");
                return true;
            }
            catch (Exception ex)
            {
                if (log != null) log("Explorer restart error: " + ex.Message);
                return false;
            }
        }

        internal static Encoding ResolveProcessEncoding(string fileName)
        {
            string exe = Path.GetFileName(fileName ?? "").ToLowerInvariant();
            // sfc.exe outputs UTF-16 LE; reading as UTF-8 truncates lines at NUL bytes!
            if (exe == "sfc.exe" || exe == "sfc") return Encoding.Unicode;
            // winget & powershell (with UTF-8 OutputEncoding) output UTF-8
            if (exe == "winget.exe" || exe == "winget" || exe == "powershell.exe" || exe == "powershell" || exe == "pwsh.exe")
                return Encoding.UTF8;
            // Legacy Win32 console tools (DISM, net, netsh, ipconfig, bcdedit, powercfg, w32tm) output OEM codepage (GBK 936 on zh-CN, CP857 on tr-TR, CP437 on en-US)
            try { return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage); }
            catch { return Encoding.Default; }
        }

        public static int RunProcessStreaming(string fileName, string args, Action<string> onLine, Func<bool> isCancelled)
        {
            Encoding enc = ResolveProcessEncoding(fileName);
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = fileName;
            psi.Arguments = args;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = enc;
            psi.StandardErrorEncoding = enc;

            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrEmpty(e.Data) && onLine != null)
                    {
                        string clean = CleanConsoleLine(e.Data);
                        if (!string.IsNullOrEmpty(clean)) onLine(clean);
                    }
                };
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrEmpty(e.Data) && onLine != null)
                    {
                        string clean = CleanConsoleLine(e.Data);
                        if (!string.IsNullOrEmpty(clean)) onLine(clean);
                    }
                };

                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                while (!p.WaitForExit(150))
                {
                    if (isCancelled != null && isCancelled())
                    {
                        try { p.Kill(); } catch { }
                        return -1;
                    }
                }
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        public static int RunPowerShellEncoded(string script, Action<string> onLine, Func<bool> isCancelled)
        {
            string full = "$ProgressPreference = 'SilentlyContinue'; [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; " + (script ?? "");
            string b64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(full));
            return RunProcessStreaming("powershell.exe", "-NoProfile -NonInteractive -OutputFormat Text -ExecutionPolicy Bypass -EncodedCommand " + b64, onLine, isCancelled);
        }

        internal static string CleanConsoleLine(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            if (raw.IndexOf('\0') >= 0) raw = raw.Replace("\0", "");
            // Strip spinner-only noise & PowerShell CLIXML startup progress frames
            string t = raw.Trim();
            if (t.Length == 0) return "";
            if (t == "-" || t == "\\" || t == "|" || t == "/") return "";
            if (t == "#< CLIXML" || (t.StartsWith("<Objs ", StringComparison.Ordinal) && t.IndexOf("S=\"progress\"", StringComparison.Ordinal) >= 0))
                return "";
            return t;
        }
    }
}
