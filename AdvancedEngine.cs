using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace WinForge
{
    internal sealed class AdvancedStatus
    {
        public bool DefenderRealTimeEnabled = true;
        public bool DefenderPolicyDisabled = false;
        public bool DefenderTamperProtected = true;
        public bool DefenderChecked = false;

        public bool EdgeInstalled = false;
        public string EdgeVersion = "";
        public string EdgeSetupPath = "";

        public bool StoreInstalled = true;
        public string StoreVersion = "";
        public bool StoreChecked = false;
    }

    internal sealed class RegistryJunkEntry
    {
        public string Category; // "muicache", "appcompat", "appswitched", "mru"
        public string SubKey;
        public string ValueName;
        public string Reason;
    }

    internal sealed class RegistryScanResult
    {
        public bool Scanned = false;
        public int MuiCacheCount = 0;
        public int AppCompatCount = 0;
        public int AppSwitchedCount = 0;
        public int MruCount = 0;
        public List<RegistryJunkEntry> Entries = new List<RegistryJunkEntry>();
        public string LastBackupPath = "";

        public int TotalCount { get { return Entries != null ? Entries.Count : 0; } }
    }

    internal static class AdvancedEngine
    {
        private const string DefPolicyRoot = @"SOFTWARE\Policies\Microsoft\Windows Defender";
        private const string DefPolicyRt = @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection";
        private const string DefFeatures = @"SOFTWARE\Microsoft\Windows Defender\Features";

        internal const string KeyMuiCache = @"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache";
        internal const string KeyAppCompat = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Compatibility Assistant\Store";
        internal const string KeyAppSwitched = @"Software\Microsoft\Windows\CurrentVersion\Explorer\FeatureUsage\AppSwitched";
        internal const string KeyRunMru = @"Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU";
        internal const string KeyTypedPaths = @"Software\Microsoft\Windows\CurrentVersion\Explorer\TypedPaths";

        public static string GetBackupDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WinForge",
                "backups");
        }

        public static void OpenBackupDirectory()
        {
            try
            {
                string dir = GetBackupDirectory();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                ProcessStartInfo psi = new ProcessStartInfo("explorer.exe", "\"" + dir + "\"");
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch { }
        }

        public static void OpenWindowsSecurity()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("windowsdefender://threatsettings");
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch
            {
                try
                {
                    ProcessStartInfo psi2 = new ProcessStartInfo("windowsdefender:");
                    psi2.UseShellExecute = true;
                    Process.Start(psi2);
                }
                catch { }
            }
        }

        // ==================== LIVE STATUS DETECTION ====================

        public static AdvancedStatus ReadFastStatus()
        {
            AdvancedStatus st = new AdvancedStatus();

            // 1. Edge (instant filesystem check; never touches EdgeWebView2)
            string edgeExe = FindEdgeExePath();
            st.EdgeInstalled = !string.IsNullOrEmpty(edgeExe);
            if (st.EdgeInstalled)
            {
                try
                {
                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(edgeExe);
                    st.EdgeVersion = fvi != null && !string.IsNullOrEmpty(fvi.ProductVersion) ? fvi.ProductVersion : "";
                }
                catch { }
            }
            st.EdgeSetupPath = FindEdgeSetupExe();

            // 2. Defender Policy & Tamper Protection fast registry read
            int disSpy = RegHelper.ReadDword(Registry.LocalMachine, DefPolicyRoot, "DisableAntiSpyware", 0);
            int disRt = RegHelper.ReadDword(Registry.LocalMachine, DefPolicyRt, "DisableRealtimeMonitoring", 0);
            st.DefenderPolicyDisabled = (disSpy == 1 || disRt == 1);

            int tp = RegHelper.ReadDword(Registry.LocalMachine, DefFeatures, "TamperProtection", 1);
            st.DefenderTamperProtected = ((tp & 1) != 0);
            st.DefenderRealTimeEnabled = !st.DefenderPolicyDisabled || st.DefenderTamperProtected;

            return st;
        }

        public static AdvancedStatus ReadFullStatus()
        {
            AdvancedStatus st = ReadFastStatus();

            // Accurate live Defender status via official Get-MpComputerStatus (works without admin)
            try
            {
                string outTxt = RunQuickPowerShell(
                    "$m = Get-MpComputerStatus -ErrorAction SilentlyContinue; if ($m) { Write-Output ('RT=' + $m.RealTimeProtectionEnabled + ';TP=' + $m.IsTamperProtected) }",
                    6000);
                if (!string.IsNullOrEmpty(outTxt))
                {
                    if (outTxt.IndexOf("RT=False", StringComparison.OrdinalIgnoreCase) >= 0)
                        st.DefenderRealTimeEnabled = false;
                    else if (outTxt.IndexOf("RT=True", StringComparison.OrdinalIgnoreCase) >= 0)
                        st.DefenderRealTimeEnabled = true;

                    if (outTxt.IndexOf("TP=False", StringComparison.OrdinalIgnoreCase) >= 0)
                        st.DefenderTamperProtected = false;
                    else if (outTxt.IndexOf("TP=True", StringComparison.OrdinalIgnoreCase) >= 0)
                        st.DefenderTamperProtected = true;

                    st.DefenderChecked = true;
                }
            }
            catch { }

            // Accurate live Microsoft Store status via official Get-AppxPackage
            try
            {
                string storeOut = RunQuickPowerShell(
                    "$p = Get-AppxPackage -Name Microsoft.WindowsStore -ErrorAction SilentlyContinue | Select-Object -First 1; if ($p) { Write-Output ('STORE=YES;' + $p.Version) } else { Write-Output 'STORE=NO' }",
                    7000);
                if (!string.IsNullOrEmpty(storeOut))
                {
                    if (storeOut.IndexOf("STORE=YES;", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        st.StoreInstalled = true;
                        int idx = storeOut.IndexOf("STORE=YES;", StringComparison.OrdinalIgnoreCase);
                        string ver = storeOut.Substring(idx + 10).Trim();
                        int nl = ver.IndexOfAny(new char[] { '\r', '\n' });
                        st.StoreVersion = nl > 0 ? ver.Substring(0, nl).Trim() : ver;
                    }
                    else if (storeOut.IndexOf("STORE=NO", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        st.StoreInstalled = false;
                        st.StoreVersion = "";
                    }
                    st.StoreChecked = true;
                }
            }
            catch { }

            return st;
        }

        internal static string RunQuickPowerShell(string script, int timeoutMs)
        {
            try
            {
                string full = "$ProgressPreference = 'SilentlyContinue'; [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; " + (script ?? "");
                string b64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(full));
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = "-NoProfile -NonInteractive -OutputFormat Text -ExecutionPolicy Bypass -EncodedCommand " + b64;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;

                StringBuilder sb = new StringBuilder();
                object sbLock = new object();
                using (Process p = new Process())
                {
                    p.StartInfo = psi;
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            lock (sbLock) { sb.AppendLine(e.Data); }
                    };
                    p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { };
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(Math.Max(1000, timeoutMs)))
                    {
                        try { p.Kill(); } catch { }
                    }
                    else
                    {
                        p.WaitForExit();
                    }
                }
                lock (sbLock) { return sb.ToString(); }
            }
            catch
            {
                return "";
            }
        }

        // ==================== 1. WINDOWS DEFENDER POLICY ====================

        public static bool DisableDefenderPolicy(Action<string> log, out bool blockedByTamper)
        {
            blockedByTamper = false;
            AdvancedStatus st = ReadFullStatus();

            if (st.DefenderTamperProtected)
            {
                blockedByTamper = true;
                if (log != null)
                {
                    log("[Defender] Windows Security Tamper Protection is currently ENABLED.");
                    log("[Defender] Windows blocks third-party tools from disabling Defender while Tamper Protection is ON.");
                    log("[Defender] Open Windows Security -> Virus & threat protection settings -> turn OFF Tamper Protection first, then retry.");
                }
                return false;
            }

            if (log != null) log("[Defender] Applying official Microsoft Group Policy & MpPreference (Tamper Protection is OFF)...");

            bool ok1 = RegHelper.WriteDword(Registry.LocalMachine, DefPolicyRoot, "DisableAntiSpyware", 1);
            bool ok2 = RegHelper.WriteDword(Registry.LocalMachine, DefPolicyRt, "DisableRealtimeMonitoring", 1);

            int code = WinNative.RunPowerShellEncoded("Set-MpPreference -DisableRealtimeMonitoring $true", log, null);

            if (log != null)
            {
                log("[Defender] Policy written (HKLM=" + (ok1 && ok2 ? "OK" : "FAIL") + ", Set-MpPreference exit=" + code + ").");
            }
            return ok1 && ok2;
        }

        public static bool RestoreDefenderPolicy(Action<string> log)
        {
            if (log != null) log("[Defender] Restoring Windows Defender policy and Real-Time Protection...");

            RegHelper.DeleteValueSafe(Registry.LocalMachine, DefPolicyRt, "DisableRealtimeMonitoring");
            RegHelper.DeleteValueSafe(Registry.LocalMachine, DefPolicyRoot, "DisableAntiSpyware");

            int code = WinNative.RunPowerShellEncoded("Set-MpPreference -DisableRealtimeMonitoring $false", log, null);

            if (log != null) log("[Defender] Windows Defender defaults restored (exit=" + code + ").");
            return true;
        }

        // ==================== 2. MICROSOFT EDGE (REMOVE / REINSTALL) ====================

        public static string FindEdgeExePath()
        {
            string[] candidates = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge\Application\msedge.exe")
            };
            foreach (string c in candidates)
            {
                if (!string.IsNullOrEmpty(c) && File.Exists(c)) return c;
            }
            return "";
        }

        public static string FindEdgeSetupExe()
        {
            string[] bases = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge\Application")
            };

            string bestPath = "";
            Version bestVer = new Version(0, 0, 0, 0);

            foreach (string b in bases)
            {
                if (string.IsNullOrEmpty(b) || !Directory.Exists(b)) continue;
                try
                {
                    foreach (string dir in Directory.GetDirectories(b))
                    {
                        string setup = Path.Combine(dir, @"Installer\setup.exe");
                        if (!File.Exists(setup)) continue;
                        string folderName = Path.GetFileName(dir);
                        Version v;
                        if (Version.TryParse(folderName, out v))
                        {
                            if (v > bestVer) { bestVer = v; bestPath = setup; }
                        }
                        else if (string.IsNullOrEmpty(bestPath))
                        {
                            bestPath = setup;
                        }
                    }
                }
                catch { }
            }
            return bestPath;
        }

        public static bool RemoveEdge(Action<string> log, Func<bool> isCancelled)
        {
            string setup = FindEdgeSetupExe();
            if (!string.IsNullOrEmpty(setup) && File.Exists(setup))
            {
                if (log != null)
                {
                    log("[Edge] Running official Edge uninstaller (WebView2 Runtime is NOT touched):");
                    log("[Edge] " + setup);
                }
                int code = WinNative.RunProcessStreaming(
                    setup,
                    "--uninstall --system-level --verbose-logging --force-uninstall",
                    log, isCancelled);
                if (log != null) log("[Edge] Installer exited with code " + code + ".");
                Thread.Sleep(600);
            }

            if (!string.IsNullOrEmpty(FindEdgeExePath()))
            {
                if (log != null) log("[Edge] Fallback: running official WinGet uninstall Microsoft.Edge...");
                WinNative.RunProcessStreaming(
                    "winget.exe",
                    "uninstall --id Microsoft.Edge --exact --silent --accept-source-agreements",
                    log, isCancelled);
            }

            bool gone = string.IsNullOrEmpty(FindEdgeExePath());
            if (log != null)
                log(gone ? "[Edge] Microsoft Edge removed cleanly (EdgeWebView2 intact)." : "[Edge] Note: Edge binary is still present — Windows may require closing Edge processes or EEA uninstall policy.");
            return gone;
        }

        public static bool ReinstallEdge(Action<string> log, Func<bool> isCancelled)
        {
            if (log != null) log("[Edge] Installing Microsoft Edge via official WinGet (Microsoft.Edge)...");
            int code = WinNative.RunProcessStreaming(
                "winget.exe",
                "install --id Microsoft.Edge --exact --silent --accept-package-agreements --accept-source-agreements",
                log, isCancelled);
            bool ok = code == 0 || !string.IsNullOrEmpty(FindEdgeExePath());
            if (log != null) log(ok ? "[Edge] Microsoft Edge installed cleanly." : "[Edge] WinGet exited with code " + code + ".");
            return ok;
        }

        // ==================== 3. MICROSOFT STORE (REMOVE / REINSTALL) ====================

        public static bool RemoveStore(Action<string> log, Func<bool> isCancelled)
        {
            if (log != null) log("[Store] Removing Microsoft.WindowsStore Appx package (WinGet / AppInstaller stays intact)...");
            int code = WinNative.RunPowerShellEncoded("Get-AppxPackage -Name Microsoft.WindowsStore | Remove-AppxPackage -ErrorAction Stop", log, isCancelled);
            if (log != null) log("[Store] Remove command finished (exit=" + code + ").");
            return code == 0;
        }

        public static bool ReinstallStore(Action<string> log, Func<bool> isCancelled)
        {
            if (log != null) log("[Store] Reinstalling Microsoft Store via official Microsoft wsreset.exe -i...");

            // First try re-registering local provisioned package if available
            WinNative.RunPowerShellEncoded(
                "Get-AppxPackage -AllUsers Microsoft.WindowsStore | ForEach-Object { Add-AppxPackage -DisableDevelopmentMode -Register ($_.InstallLocation + '\\AppxManifest.xml') -ErrorAction SilentlyContinue }",
                log, isCancelled);

            // Official built-in Microsoft Store installer trigger (Windows 10/11)
            string wsreset = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsreset.exe");
            int code = WinNative.RunProcessStreaming(
                File.Exists(wsreset) ? wsreset : "wsreset.exe",
                "-i",
                log, isCancelled);

            if (log != null) log("[Store] Official restore triggered (exit=" + code + "). Microsoft Store finishes installing in 10-30s.");
            return code == 0;
        }

        // ==================== 4. SAFE BACKUP-FIRST REGISTRY CLEANUP ====================

        private static HashSet<char> GetReadyFixedDrives()
        {
            HashSet<char> set = new HashSet<char>();
            try
            {
                foreach (DriveInfo di in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (di.DriveType == DriveType.Fixed && di.IsReady && di.Name.Length >= 1)
                            set.Add(char.ToUpperInvariant(di.Name[0]));
                    }
                    catch { }
                }
            }
            catch { }
            return set;
        }

        internal static bool IsSafeOrphanedFilePath(string rawPath, HashSet<char> fixedDrives, string[] allowedExtensions)
        {
            if (string.IsNullOrEmpty(rawPath) || rawPath.Length < 7) return false;
            string p = rawPath.Trim().Trim('"');
            if (p.Length < 7) return false;

            // Must start with absolute local drive letter e.g. C:\
            if (!char.IsLetter(p[0]) || p[1] != ':' || p[2] != '\\') return false;

            // Skip environment variables, shell prefixes, UNC, or unmounted/removable drives
            if (p.IndexOf('%') >= 0 || p.IndexOf('@') >= 0) return false;
            if (!fixedDrives.Contains(char.ToUpperInvariant(p[0]))) return false;

            // Safety guard: NEVER touch core Windows OS binaries or UWP WindowsApps
            string lower = p.ToLowerInvariant();
            if (lower.IndexOf(@"\windows\system32\", StringComparison.Ordinal) >= 0) return false;
            if (lower.IndexOf(@"\windows\syswow64\", StringComparison.Ordinal) >= 0) return false;
            if (lower.IndexOf(@"\windows\winsxs\", StringComparison.Ordinal) >= 0) return false;
            if (lower.IndexOf(@"\program files\windowsapps\", StringComparison.Ordinal) >= 0) return false;

            bool extMatch = false;
            for (int i = 0; i < allowedExtensions.Length; i++)
            {
                if (lower.EndsWith(allowedExtensions[i], StringComparison.Ordinal))
                {
                    extMatch = true;
                    break;
                }
            }
            if (!extMatch) return false;

            // Check invalid path chars before File.Exists
            try
            {
                return !File.Exists(p);
            }
            catch
            {
                return false;
            }
        }

        public static RegistryScanResult ScanSafeRegistry()
        {
            return ScanSafeRegistryUnder(Registry.CurrentUser, KeyMuiCache, KeyAppCompat, KeyAppSwitched, KeyRunMru, KeyTypedPaths);
        }

        internal static RegistryScanResult ScanSafeRegistryUnder(
            RegistryKey root,
            string muiKey,
            string appCompatKey,
            string appSwitchedKey,
            string runMruKey,
            string typedPathsKey)
        {
            RegistryScanResult res = new RegistryScanResult();
            res.Scanned = true;
            HashSet<char> fixedDrives = GetReadyFixedDrives();
            string[] exeExts = new string[] { ".exe", ".msi", ".bat", ".cmd" };
            string[] muiExts = new string[] { ".exe", ".dll", ".cpl", ".msc" };

            // 1. Orphaned MuiCache entries
            ScanPathValues(root, muiKey, "muicache", true, fixedDrives, muiExts, res);

            // 2. Orphaned AppCompat Compatibility Assistant Store entries
            ScanPathValues(root, appCompatKey, "appcompat", false, fixedDrives, exeExts, res);

            // 3. Orphaned FeatureUsage\AppSwitched direct paths
            ScanPathValues(root, appSwitchedKey, "appswitched", false, fixedDrives, new string[] { ".exe" }, res);

            // 4. Explorer RunMRU & TypedPaths history (MRU)
            ScanMruKey(root, runMruKey, true, res);
            ScanMruKey(root, typedPathsKey, false, res);

            return res;
        }

        private static void ScanPathValues(
            RegistryKey root,
            string subKey,
            string category,
            bool stripMuiSuffix,
            HashSet<char> fixedDrives,
            string[] exts,
            RegistryScanResult res)
        {
            if (string.IsNullOrEmpty(subKey)) return;
            try
            {
                using (RegistryKey k = root.OpenSubKey(subKey, false))
                {
                    if (k == null) return;
                    foreach (string name in k.GetValueNames())
                    {
                        if (string.IsNullOrEmpty(name)) continue;
                        string candidate = name;
                        if (stripMuiSuffix)
                        {
                            if (candidate.EndsWith(".FriendlyAppName", StringComparison.OrdinalIgnoreCase))
                                candidate = candidate.Substring(0, candidate.Length - ".FriendlyAppName".Length);
                            else if (candidate.EndsWith(".ApplicationCompany", StringComparison.OrdinalIgnoreCase))
                                candidate = candidate.Substring(0, candidate.Length - ".ApplicationCompany".Length);
                            else
                                continue; // Ignore LangID or unknown MuiCache metadata
                        }

                        if (IsSafeOrphanedFilePath(candidate, fixedDrives, exts))
                        {
                            RegistryJunkEntry entry = new RegistryJunkEntry();
                            entry.Category = category;
                            entry.SubKey = subKey;
                            entry.ValueName = name;
                            entry.Reason = "Missing file: " + candidate;
                            res.Entries.Add(entry);

                            if (category == "muicache") res.MuiCacheCount++;
                            else if (category == "appcompat") res.AppCompatCount++;
                            else if (category == "appswitched") res.AppSwitchedCount++;
                        }
                    }
                }
            }
            catch { }
        }

        private static void ScanMruKey(RegistryKey root, string subKey, bool isRunMru, RegistryScanResult res)
        {
            if (string.IsNullOrEmpty(subKey)) return;
            try
            {
                using (RegistryKey k = root.OpenSubKey(subKey, false))
                {
                    if (k == null) return;
                    foreach (string name in k.GetValueNames())
                    {
                        if (string.IsNullOrEmpty(name)) continue;
                        if (isRunMru)
                        {
                            if (name.Equals("MRUList", StringComparison.OrdinalIgnoreCase)) continue;
                            if (name.Length != 1 || !char.IsLetter(name[0])) continue;
                        }
                        else
                        {
                            if (!name.StartsWith("url", StringComparison.OrdinalIgnoreCase)) continue;
                        }

                        RegistryJunkEntry entry = new RegistryJunkEntry();
                        entry.Category = "mru";
                        entry.SubKey = subKey;
                        entry.ValueName = name;
                        entry.Reason = isRunMru ? "RunMRU history" : "Explorer TypedPaths history";
                        res.Entries.Add(entry);
                        res.MruCount++;
                    }
                }
            }
            catch { }
        }

        private static string EscapeRegString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string FormatHexBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "";
            StringBuilder sb = new StringBuilder(bytes.Length * 3);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(bytes[i].ToString("x2"));
            }
            return sb.ToString();
        }

        public static string ExportRegBackup(RegistryKey root, string rootHeaderName, List<RegistryJunkEntry> entries, string outPath)
        {
            string dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            Dictionary<string, List<string>> byKey = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (RegistryJunkEntry e in entries)
            {
                List<string> list;
                if (!byKey.TryGetValue(e.SubKey, out list))
                {
                    list = new List<string>();
                    byKey[e.SubKey] = list;
                }
                if (!list.Contains(e.ValueName)) list.Add(e.ValueName);
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Windows Registry Editor Version 5.00");
            sb.AppendLine("; WinForge Automatic Registry Cleanup Backup — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("; Double-click this .reg file anytime to restore every cleaned entry 100%.");
            sb.AppendLine();

            foreach (KeyValuePair<string, List<string>> kv in byKey)
            {
                using (RegistryKey k = root.OpenSubKey(kv.Key, false))
                {
                    if (k == null) continue;
                    sb.AppendLine("[" + rootHeaderName + "\\" + kv.Key + "]");

                    // Also include MRUList if backing up RunMRU
                    if (kv.Key.EndsWith("RunMRU", StringComparison.OrdinalIgnoreCase))
                    {
                        object mruList = k.GetValue("MRUList", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        if (mruList != null)
                            sb.AppendLine("\"MRUList\"=\"" + EscapeRegString(mruList.ToString()) + "\"");
                    }

                    foreach (string valName in kv.Value)
                    {
                        object val = k.GetValue(valName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        if (val == null) continue;
                        RegistryValueKind kind = k.GetValueKind(valName);
                        string quotedName = "\"" + EscapeRegString(valName) + "\"";

                        if (kind == RegistryValueKind.String)
                        {
                            sb.AppendLine(quotedName + "=\"" + EscapeRegString(val.ToString()) + "\"");
                        }
                        else if (kind == RegistryValueKind.DWord)
                        {
                            uint dv = unchecked((uint)Convert.ToInt32(val));
                            sb.AppendLine(quotedName + "=dword:" + dv.ToString("x8"));
                        }
                        else if (kind == RegistryValueKind.QWord)
                        {
                            byte[] qb = BitConverter.GetBytes(Convert.ToInt64(val));
                            sb.AppendLine(quotedName + "=hex(b):" + FormatHexBytes(qb));
                        }
                        else if (kind == RegistryValueKind.Binary && val is byte[])
                        {
                            sb.AppendLine(quotedName + "=hex:" + FormatHexBytes((byte[])val));
                        }
                        else if (kind == RegistryValueKind.ExpandString)
                        {
                            byte[] eb = Encoding.Unicode.GetBytes(val.ToString() + "\0");
                            sb.AppendLine(quotedName + "=hex(2):" + FormatHexBytes(eb));
                        }
                        else if (kind == RegistryValueKind.MultiString && val is string[])
                        {
                            string joined = string.Join("\0", (string[])val) + "\0\0";
                            byte[] mb = Encoding.Unicode.GetBytes(joined);
                            sb.AppendLine(quotedName + "=hex(7):" + FormatHexBytes(mb));
                        }
                    }
                    sb.AppendLine();
                }
            }

            // Standard Windows .reg files use UTF-16 LE with BOM
            File.WriteAllText(outPath, sb.ToString(), new UnicodeEncoding(false, true));
            return outPath;
        }

        public static int CleanScannedRegistry(
            RegistryKey root,
            string rootHeaderName,
            RegistryScanResult scan,
            string backupFilePath,
            Action<string> log)
        {
            if (scan == null || scan.Entries == null || scan.Entries.Count == 0)
                return 0;

            // Step 1: MANDATORY .reg backup first!
            string savedPath = ExportRegBackup(root, rootHeaderName, scan.Entries, backupFilePath);
            if (!File.Exists(savedPath) || new FileInfo(savedPath).Length < 40)
            {
                if (log != null) log("[Registry] ERROR: Could not verify .reg backup — cleanup aborted for safety!");
                return 0;
            }
            scan.LastBackupPath = savedPath;
            if (log != null) log("[Registry] Automatic .reg backup verified: " + savedPath);

            // Step 2: Clean only verified entries
            int removed = 0;
            Dictionary<string, List<string>> byKey = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (RegistryJunkEntry e in scan.Entries)
            {
                List<string> list;
                if (!byKey.TryGetValue(e.SubKey, out list))
                {
                    list = new List<string>();
                    byKey[e.SubKey] = list;
                }
                list.Add(e.ValueName);
            }

            foreach (KeyValuePair<string, List<string>> kv in byKey)
            {
                try
                {
                    using (RegistryKey k = root.OpenSubKey(kv.Key, true))
                    {
                        if (k == null) continue;
                        foreach (string name in kv.Value)
                        {
                            try
                            {
                                k.DeleteValue(name, false);
                                removed++;
                            }
                            catch { }
                        }
                        // If we cleaned RunMRU, reset MRUList to empty cleanly
                        if (kv.Key.EndsWith("RunMRU", StringComparison.OrdinalIgnoreCase))
                        {
                            try { k.SetValue("MRUList", "", RegistryValueKind.String); } catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (log != null) log("[Registry] Skipped " + kv.Key + ": " + ex.Message);
                }
            }

            if (log != null) log("[Registry] Cleaned " + removed + " verified orphaned/MRU entries. Restore anytime via backup .reg.");
            return removed;
        }
    }
}
