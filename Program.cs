using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("WinForge — Complete Reversible Windows Utility")]
[assembly: AssemblyDescription("Lightweight (~210 KB), zero-dependency, 100% reversible Windows 10/11 utility: 60 verified WinGet apps, 40 live tweaks (Desktop/Laptop presets + JSON config), Bloatware/OneDrive debloater, Windows Update & 1-click DNS, Optional Features & Repairs, and Optional Advanced tab (EN/TR/zh-CN).")]
[assembly: AssemblyProduct("WinForge")]
[assembly: AssemblyVersion("2.0.1.0")]
[assembly: AssemblyFileVersion("2.0.1.0")]

namespace WinForge
{
    internal sealed class LaunchConfig
    {
        public bool DemoMode;
        public string ForceLang;
        public int InitialTab;

        public static LaunchConfig Parse(string[] args)
        {
            LaunchConfig cfg = new LaunchConfig();
            if (args == null) return cfg;
            for (int i = 0; i < args.Length; i++)
            {
                string a = (args[i] ?? "").Trim().ToLowerInvariant();
                if (a == "--demo") cfg.DemoMode = true;
                else if (a == "--lang" && i + 1 < args.Length)
                {
                    string l = (args[++i] ?? "").Trim().ToLowerInvariant();
                    if (l == "tr" || l == "en" || l == "zh") cfg.ForceLang = l;
                }
                else if (a == "--tab" && i + 1 < args.Length)
                {
                    int t;
                    if (int.TryParse(args[++i], out t) && t >= 0 && t <= 5) cfg.InitialTab = t;
                }
            }
            return cfg;
        }
    }

    internal sealed class ClickZone
    {
        public RectangleF Rect;
        public string ActionKey;
        public object Payload;

        public ClickZone(RectangleF r, string key, object payload)
        {
            Rect = r; ActionKey = key; Payload = payload;
        }
    }

    internal sealed class ForgeContentPanel : Control
    {
        public static readonly Color ColBg = Color.FromArgb(13, 17, 23);
        public static readonly Color ColCard = Color.FromArgb(22, 27, 34);
        public static readonly Color ColCardHover = Color.FromArgb(28, 35, 45);
        public static readonly Color ColBorder = Color.FromArgb(40, 49, 62);
        public static readonly Color ColBorderHover = Color.FromArgb(64, 78, 98);
        public static readonly Color ColTextPrimary = Color.FromArgb(240, 246, 252);
        public static readonly Color ColTextBody = Color.FromArgb(204, 212, 220);
        public static readonly Color ColTextSecondary = Color.FromArgb(139, 148, 158);
        public static readonly Color ColTextMuted = Color.FromArgb(110, 118, 129);
        public static readonly Color ColSafe = Color.FromArgb(52, 211, 153);
        public static readonly Color ColSafeBg = Color.FromArgb(18, 46, 36);
        public static readonly Color ColSafeBd = Color.FromArgb(36, 122, 92);
        public static readonly Color ColCyan = Color.FromArgb(56, 189, 248);
        public static readonly Color ColCyanBg = Color.FromArgb(18, 40, 56);
        public static readonly Color ColCyanBd = Color.FromArgb(42, 118, 162);
        public static readonly Color ColAmber = Color.FromArgb(251, 191, 36);
        public static readonly Color ColAmberBg = Color.FromArgb(48, 38, 18);
        public static readonly Color ColAmberBd = Color.FromArgb(150, 102, 22);
        public static readonly Color ColDanger = Color.FromArgb(248, 113, 113);
        public static readonly Color ColDangerBg = Color.FromArgb(52, 22, 26);
        public static readonly Color ColDangerHover = Color.FromArgb(74, 28, 34);
        public static readonly Color ColDangerBd = Color.FromArgb(165, 52, 60);
        public static readonly Color ColBtnBg = Color.FromArgb(30, 37, 48);
        public static readonly Color ColBtnHover = Color.FromArgb(44, 54, 68);
        public static readonly Color ColBtnBd = Color.FromArgb(58, 70, 88);
        public static readonly Color ColPrimaryBg = Color.FromArgb(16, 124, 88);
        public static readonly Color ColPrimaryHover = Color.FromArgb(20, 148, 105);
        public static readonly Color ColScrollTrack = Color.FromArgb(18, 23, 30);
        public static readonly Color ColScrollThumb = Color.FromArgb(54, 65, 82);
        public static readonly Color ColScrollThumbHov = Color.FromArgb(80, 96, 118);

        private float _scale = 1.0f;
        private string _lang = "en";
        private int _activeTab;
        private bool _isAdmin;
        private bool _isBusy;
        private string _appSearch = "";
        private int _scrollY;
        private int _contentHeight;
        private string _hoverKey = "";
        private object _hoverPayload;
        private bool _hoverScroll;
        private bool _draggingScroll;
        private int _dragStartY;
        private int _dragStartScroll;

        private readonly List<ClickZone> _zones = new List<ClickZone>();

        public List<WingetAppItem> Apps = new List<WingetAppItem>();
        public List<TweakDefinition> Tweaks = new List<TweakDefinition>();
        public List<BloatAppItem> BloatApps = new List<BloatAppItem>();
        public bool OneDriveInstalled = false;
        public UpdatePolicyState UpdateState = new UpdatePolicyState();
        public List<DnsPreset> DnsPresets = new List<DnsPreset>();
        public LiveDnsStatus DnsStatus = new LiveDnsStatus();
        public List<WinFeatureItem> WinFeatures = new List<WinFeatureItem>();
        public List<RepairActionItem> Repairs = new List<RepairActionItem>();
        public List<ControlPanelShortcut> Shortcuts = new List<ControlPanelShortcut>();
        public AdvancedStatus AdvStatus = new AdvancedStatus();
        public RegistryScanResult RegScan = new RegistryScanResult();
        public bool WingetOk = true;
        public string WingetVersion = "";

        public event Action<string, object> ActionInvoked;

        private Font _fSection;
        private Font _fTitle;
        private Font _fSub;
        private Font _fSmall;
        private Font _fBadge;
        private Font _fBtn;
        private Font _fBig;

        public ForgeContentPanel()
        {
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);
            BackColor = ColBg;
            RebuildFonts();
        }

        public void Configure(float scale, string lang, int tab, bool isAdmin, bool isBusy)
        {
            bool tabChanged = (_activeTab != tab);
            _scale = scale;
            _lang = lang ?? "en";
            _activeTab = tab;
            _isAdmin = isAdmin;
            _isBusy = isBusy;
            if (tabChanged) _scrollY = 0;
            RebuildFonts();
            Invalidate();
        }

        public void SetAppSearch(string query)
        {
            _appSearch = (query ?? "").Trim();
            _scrollY = 0;
            Invalidate();
        }

        private float Sf(float v) { return v * _scale; }
        private float PtToPx(float pt) { return pt * (96f / 72f) * _scale; }
        private int ScrollW() { return (int)Math.Round(10f * _scale); }
        private string T(string en, string tr, string zh) { return Loc.T(_lang, en, tr, zh); }

        private void RebuildFonts()
        {
            DisposeFonts();
            string fam = Loc.FontFamily(_lang);
            _fSection = new Font(fam, PtToPx(8.6f), FontStyle.Bold, GraphicsUnit.Pixel);
            _fTitle = new Font(fam, PtToPx(9.4f), FontStyle.Bold, GraphicsUnit.Pixel);
            _fSub = new Font(fam, PtToPx(8.0f), FontStyle.Regular, GraphicsUnit.Pixel);
            _fSmall = new Font("Segoe UI", PtToPx(7.6f), FontStyle.Regular, GraphicsUnit.Pixel);
            _fBadge = new Font(fam, PtToPx(6.9f), FontStyle.Bold, GraphicsUnit.Pixel);
            _fBtn = new Font(fam, PtToPx(8.3f), FontStyle.Bold, GraphicsUnit.Pixel);
            _fBig = new Font(fam, PtToPx(11.5f), FontStyle.Bold, GraphicsUnit.Pixel);
        }

        private void DisposeFonts()
        {
            if (_fSection != null) { _fSection.Dispose(); _fSection = null; }
            if (_fTitle != null) { _fTitle.Dispose(); _fTitle = null; }
            if (_fSub != null) { _fSub.Dispose(); _fSub = null; }
            if (_fSmall != null) { _fSmall.Dispose(); _fSmall = null; }
            if (_fBadge != null) { _fBadge.Dispose(); _fBadge = null; }
            if (_fBtn != null) { _fBtn.Dispose(); _fBtn = null; }
            if (_fBig != null) { _fBig.Dispose(); _fBig = null; }
        }

        private int MaxScroll() { return Math.Max(0, _contentHeight - ClientSize.Height); }

        private void ClampScroll()
        {
            int max = MaxScroll();
            if (_scrollY > max) _scrollY = max;
            if (_scrollY < 0) _scrollY = 0;
        }

        private RectangleF GetScrollThumbRect()
        {
            if (_contentHeight <= ClientSize.Height || ClientSize.Height <= 0) return RectangleF.Empty;
            float trackX = ClientSize.Width - ScrollW();
            float trackH = ClientSize.Height;
            float thumbH = Math.Max(Sf(32f), trackH * ((float)ClientSize.Height / _contentHeight));
            float ratio = (float)_scrollY / Math.Max(1, MaxScroll());
            return new RectangleF(trackX + Sf(2f), ratio * (trackH - thumbH), ScrollW() - Sf(4f), thumbH);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); ClampScroll(); Invalidate(); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (MaxScroll() <= 0) return;
            _scrollY -= (e.Delta / 120) * (int)Math.Round(56f * _scale);
            ClampScroll();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_draggingScroll)
            {
                RectangleF thumb = GetScrollThumbRect();
                float avail = Math.Max(1f, ClientSize.Height - thumb.Height);
                _scrollY = _dragStartScroll + (int)Math.Round(((e.Y - _dragStartY) / avail) * MaxScroll());
                ClampScroll();
                Invalidate();
                return;
            }

            RectangleF tr = GetScrollThumbRect();
            bool onScroll = !tr.IsEmpty && e.X >= ClientSize.Width - ScrollW() - Sf(2f);

            string newKey = "";
            object newPayload = null;
            if (!onScroll)
            {
                for (int i = _zones.Count - 1; i >= 0; i--)
                {
                    if (_zones[i].Rect.Contains(e.Location))
                    {
                        newKey = _zones[i].ActionKey;
                        newPayload = _zones[i].Payload;
                        break;
                    }
                }
            }

            if (newKey != _hoverKey || !object.Equals(newPayload, _hoverPayload) || onScroll != _hoverScroll)
            {
                _hoverKey = newKey;
                _hoverPayload = newPayload;
                _hoverScroll = onScroll;
                Cursor = (!string.IsNullOrEmpty(newKey) || onScroll) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!string.IsNullOrEmpty(_hoverKey) || _hoverScroll)
            {
                _hoverKey = ""; _hoverPayload = null; _hoverScroll = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button != MouseButtons.Left) return;

            RectangleF tr = GetScrollThumbRect();
            if (!tr.IsEmpty && e.X >= ClientSize.Width - ScrollW() - Sf(2f))
            {
                _draggingScroll = true; _dragStartY = e.Y; _dragStartScroll = _scrollY;
                Capture = true;
                return;
            }

            for (int i = _zones.Count - 1; i >= 0; i--)
            {
                if (_zones[i].Rect.Contains(e.Location))
                {
                    if (ActionInvoked != null)
                        ActionInvoked(_zones[i].ActionKey, _zones[i].Payload);
                    break;
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_draggingScroll) { _draggingScroll = false; Capture = false; }
        }

        private bool IsHovered(string key, object payload)
        {
            return _hoverKey == key && object.Equals(_hoverPayload, payload);
        }

        private void AddZone(RectangleF r, string key, object payload)
        {
            if (r.Bottom >= 0 && r.Top <= ClientSize.Height)
                _zones.Add(new ClickZone(r, key, payload));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(ColBg);

            _zones.Clear();
            float availW = ClientSize.Width - ScrollW() - Sf(8f);
            if (availW < Sf(200f)) availW = ClientSize.Width;

            if (_activeTab == 0) _contentHeight = PaintAppsTab(g, availW);
            else if (_activeTab == 1) _contentHeight = PaintTweaksTab(g, availW);
            else if (_activeTab == 2) _contentHeight = PaintDebloatTab(g, availW);
            else if (_activeTab == 3) _contentHeight = PaintUpdatesTab(g, availW);
            else if (_activeTab == 4) _contentHeight = PaintRepairTab(g, availW);
            else _contentHeight = PaintAdvancedTab(g, availW);

            if (_scrollY > MaxScroll()) _scrollY = MaxScroll();

            if (_contentHeight > ClientSize.Height)
            {
                float trackX = ClientSize.Width - ScrollW();
                using (SolidBrush tb = new SolidBrush(ColScrollTrack))
                    g.FillRectangle(tb, trackX, 0, ScrollW(), ClientSize.Height);

                RectangleF thumb = GetScrollThumbRect();
                using (GraphicsPath tp = Rounded(thumb, Sf(3f)))
                using (SolidBrush sb = new SolidBrush((_hoverScroll || _draggingScroll) ? ColScrollThumbHov : ColScrollThumb))
                    g.FillPath(sb, tp);
            }
        }

        // ==================== TAB 0: APPS (WINGET) ====================
        private int PaintAppsTab(Graphics g, float availW)
        {
            float y = -_scrollY;

            if (!WingetOk)
            {
                RectangleF warn = new RectangleF(0, y, availW, Sf(42f));
                DrawBox(g, warn, Sf(9f), ColAmberBg, ColAmberBd);
                DrawText(g, T("WinGet CLI was not found on this PC. Install 'App Installer' from Microsoft Store to enable app installs.",
                    "Bu bilgisayarda WinGet bulunamadı. Microsoft Store'dan 'Uygulama Yükleyici' kurun.",
                    "当前系统未检测到 WinGet，请从 Microsoft Store 安装「应用安装程序」后使用。"),
                    _fSub, ColAmber, new RectangleF(warn.X + Sf(14f), warn.Y, warn.Width - Sf(28f), warn.Height), StringAlignment.Near);
                y += Sf(52f);
            }

            int cols = availW >= Sf(540f) ? 2 : 1;
            float gap = Sf(10f);
            float cardW = (availW - gap * (cols - 1)) / cols;
            float cardH = Sf(54f);

            AppGroup[] groups = new AppGroup[]
            {
                AppGroup.Browsers, AppGroup.Developer, AppGroup.Utilities,
                AppGroup.Media, AppGroup.Productivity, AppGroup.Communication, AppGroup.GamingRuntimes
            };

            foreach (AppGroup grp in groups)
            {
                List<WingetAppItem> matches = new List<WingetAppItem>();
                foreach (WingetAppItem app in Apps)
                {
                    if (app.Group != grp) continue;
                    if (!string.IsNullOrEmpty(_appSearch))
                    {
                        bool hit = app.Name.IndexOf(_appSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   app.Id.IndexOf(_appSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   app.GetTag(_lang).IndexOf(_appSearch, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!hit) continue;
                    }
                    matches.Add(app);
                }

                if (matches.Count == 0) continue;

                // Section header
                if (y + Sf(24f) >= 0 && y <= ClientSize.Height)
                {
                    DrawText(g, WingetEngine.GetGroupName(grp, _lang).ToUpperInvariant(), _fSection, ColCyan,
                        new RectangleF(Sf(2f), y + Sf(4f), availW, Sf(20f)), StringAlignment.Near);
                }
                y += Sf(28f);

                for (int i = 0; i < matches.Count; i++)
                {
                    int col = i % cols;
                    float x = col * (cardW + gap);
                    RectangleF cr = new RectangleF(x, y, cardW, cardH);

                    if (cr.Bottom >= -4 && cr.Top <= ClientSize.Height + 4)
                    {
                        WingetAppItem app = matches[i];
                        bool hov = IsHovered("app_toggle", app);
                        DrawBox(g, cr, Sf(9f), hov ? ColCardHover : ColCard, app.Selected ? ColSafeBd : (hov ? ColBorderHover : ColBorder));
                        AddZone(cr, "app_toggle", app);

                        // Checkbox
                        float cb = Sf(17f);
                        RectangleF cbRc = new RectangleF(cr.X + Sf(12f), cr.Y + (cr.Height - cb) * 0.5f, cb, cb);
                        DrawCheckbox(g, cbRc, app.Selected, ColSafe);

                        // Name + optional INSTALLED badge
                        float tx = cr.X + Sf(38f);
                        float nameMaxW = cr.Width - Sf(50f);
                        SizeF nSz = MeasureTight(g, app.Name, _fTitle);
                        float drawNW = Math.Min(nSz.Width + Sf(4f), nameMaxW - (app.IsInstalled ? Sf(74f) : 0f));
                        DrawTextTight(g, app.Name, _fTitle, ColTextPrimary, new RectangleF(tx, cr.Y + Sf(8f), drawNW, Sf(19f)));

                        if (app.IsInstalled)
                        {
                            DrawTag(g, tx + drawNW + Sf(6f), cr.Y + Sf(9f),
                                T("INSTALLED", "KURULU", "已安装"), ColSafeBg, ColSafeBd, ColSafe, cr.Right - Sf(10f));
                        }

                        // Subline: description + WinGet ID
                        string sub = app.GetTag(_lang) + "  ·  " + app.Id;
                        DrawText(g, sub, _fSub, ColTextSecondary, new RectangleF(tx, cr.Y + Sf(29f), nameMaxW, Sf(17f)), StringAlignment.Near);
                    }

                    if (col == cols - 1 || i == matches.Count - 1)
                        y += cardH + Sf(8f);
                }

                y += Sf(8f);
            }

            return (int)Math.Ceiling(y + _scrollY + Sf(8f));
        }

        // ==================== TAB 1: TWEAKS ====================
        private int PaintTweaksTab(Graphics g, float availW)
        {
            float y = -_scrollY;

            // Sub-toolbar inside Tweaks tab (Desktop / Laptop presets + Revert + Restart Explorer + Apply)
            float tbH = Sf(30f);
            float bx = 0f;
            RectangleF rDesk = new RectangleF(bx, y, Sf(126f), tbH);
            DrawPillBtn(g, rDesk, T("Desktop Preset", "Masaüstü Profili", "台式机推荐"), IsHovered("tweak_preset", null), false, !_isBusy);
            if (!_isBusy) AddZone(rDesk, "tweak_preset", null);
            bx = rDesk.Right + Sf(7f);

            RectangleF rLap = new RectangleF(bx, y, Sf(118f), tbH);
            DrawPillBtn(g, rLap, T("Laptop Preset", "Dizüstü Profili", "笔记本推荐"), IsHovered("tweak_preset_laptop", null), false, !_isBusy);
            if (!_isBusy) AddZone(rLap, "tweak_preset_laptop", null);
            bx = rLap.Right + Sf(7f);

            RectangleF rRevert = new RectangleF(bx, y, Sf(134f), tbH);
            DrawPillBtn(g, rRevert, T("Revert All Defaults", "Varsayılana Dön", "全部恢复默认"), IsHovered("tweak_revert_all", null), false, !_isBusy);
            if (!_isBusy) AddZone(rRevert, "tweak_revert_all", null);
            bx = rRevert.Right + Sf(7f);

            RectangleF rExp = new RectangleF(bx, y, Sf(128f), tbH);
            DrawPillBtn(g, rExp, T("Restart Explorer", "Gezgini Yenile", "重启资源管理器"), IsHovered("restart_explorer", null), false, !_isBusy);
            if (!_isBusy) AddZone(rExp, "restart_explorer", null);

            // Count pending changes
            int pending = 0;
            foreach (TweakDefinition t in Tweaks)
                if (t.DesiredState != t.IsCurrentlyApplied) pending++;

            RectangleF rApply = new RectangleF(availW - Sf(172f), y, Sf(172f), tbH);
            string applyLbl = pending > 0
                ? string.Format(T("Apply Changes ({0})", "Uygula ({0})", "应用更改 ({0})"), pending)
                : T("No Pending Changes", "Değişiklik Yok", "暂无待应用更改");
            DrawPillBtn(g, rApply, applyLbl, IsHovered("tweak_apply", null), pending > 0, !_isBusy && pending > 0);
            if (!_isBusy && pending > 0) AddZone(rApply, "tweak_apply", null);

            y += tbH + Sf(14f);

            TweakCategory[] cats = new TweakCategory[] { TweakCategory.UiExplorer, TweakCategory.Privacy, TweakCategory.Performance };
            foreach (TweakCategory cat in cats)
            {
                string secName;
                if (cat == TweakCategory.UiExplorer) secName = T("UI & FILE EXPLORER (16 · ZERO ADMIN NEEDED)", "ARAYÜZ VE DOSYA GEZGİNİ (16 · YÖNETİCİ GEREKTİRMEZ)", "界面与资源管理器（16 项 · 无需管理员权限）");
                else if (cat == TweakCategory.Privacy) secName = T("PRIVACY, TELEMETRY & POLICIES (12 · 100% REVERSIBLE)", "GİZLİLİK, TELEMETRİ VE İLKELER (12 · %100 GERİ ALINABİLİR)", "隐私保护、遥测与策略精简（12 项 · 100% 可还原）");
                else secName = T("PERFORMANCE, SERVICES & SYSTEM (12)", "PERFORMANS, SERVİSLER VE SİSTEM (12)", "性能、服务手动化与系统优化（12 项）");

                if (y + Sf(24f) >= 0 && y <= ClientSize.Height)
                    DrawText(g, secName, _fSection, ColCyan, new RectangleF(Sf(2f), y + Sf(2f), availW, Sf(20f)), StringAlignment.Near);
                y += Sf(26f);

                foreach (TweakDefinition t in Tweaks)
                {
                    if (t.Category != cat) continue;
                    float rowH = Sf(62f);
                    RectangleF cr = new RectangleF(0, y, availW, rowH);

                    if (cr.Bottom >= -4 && cr.Top <= ClientSize.Height + 4)
                    {
                        bool hov = IsHovered("tweak_toggle", t);
                        bool isChanged = (t.DesiredState != t.IsCurrentlyApplied);
                        Color bd = isChanged ? ColAmberBd : (hov ? ColBorderHover : ColBorder);
                        DrawBox(g, cr, Sf(9f), hov ? ColCardHover : ColCard, bd);
                        if (!_isBusy) AddZone(cr, "tweak_toggle", t);

                        float tx = cr.X + Sf(14f);
                        float rightEdge = cr.Right - Sf(76f);
                        string title = t.GetName(_lang);
                        SizeF tSz = MeasureTight(g, title, _fTitle);
                        float drawW = Math.Min(tSz.Width + Sf(4f), rightEdge - tx - Sf(120f));
                        DrawTextTight(g, title, _fTitle, ColTextPrimary, new RectangleF(tx, cr.Y + Sf(9f), drawW, Sf(20f)));

                        float tagX = tx + drawW + Sf(8f);
                        if (t.RequiresAdmin)
                            tagX = DrawTag(g, tagX, cr.Y + Sf(11f), T("ADMIN", "YÖNETİCİ", "需管理员"), ColAmberBg, ColAmberBd, ColAmber, rightEdge);
                        else
                            tagX = DrawTag(g, tagX, cr.Y + Sf(11f), T("SAFE", "GÜVENLİ", "安全"), ColSafeBg, ColSafeBd, ColSafe, rightEdge);

                        if (isChanged)
                        {
                            string pTxt = t.DesiredState ? T("PENDING ON", "AÇILACAK", "待开启") : T("PENDING OFF", "KAPATILACAK", "待还原");
                            DrawTag(g, tagX, cr.Y + Sf(11f), pTxt, ColCyanBg, ColCyanBd, ColCyan, rightEdge);
                        }

                        string sub = t.GetDesc(_lang) + "  ·  " + t.TargetHint;
                        DrawText(g, sub, _fSub, ColTextSecondary, new RectangleF(tx, cr.Y + Sf(33f), rightEdge - tx - Sf(8f), Sf(19f)), StringAlignment.Near);

                        // Modern Toggle Switch on right
                        DrawToggleSwitch(g, new RectangleF(cr.Right - Sf(58f), cr.Y + (cr.Height - Sf(22f)) * 0.5f, Sf(42f), Sf(22f)),
                            t.DesiredState, t.RequiresAdmin && !_isAdmin);
                    }

                    y += rowH + Sf(8f);
                }

                y += Sf(10f);
            }

            return (int)Math.Ceiling(y + _scrollY + Sf(8f));
        }

        // ==================== TAB 2: DEBLOAT (APPX BLOATWARE & ONEDRIVE) ====================
        private int PaintDebloatTab(Graphics g, float availW)
        {
            float y = -_scrollY;

            // Toolbar
            float tbH = Sf(30f);
            float bx = 0f;
            RectangleF rScan = new RectangleF(bx, y, Sf(134f), tbH);
            DrawPillBtn(g, rScan, T("Scan Installed", "Kuruluları Tara", "扫描已安装"), IsHovered("bloat_scan", null), false, !_isBusy);
            if (!_isBusy) AddZone(rScan, "bloat_scan", null);
            bx = rScan.Right + Sf(8f);

            RectangleF rSafe = new RectangleF(bx, y, Sf(152f), tbH);
            DrawPillBtn(g, rSafe, T("Select Safe Bloat", "Güvenli Bloat Seç", "勾选常见预装"), IsHovered("bloat_select_safe", null), false, !_isBusy);
            if (!_isBusy) AddZone(rSafe, "bloat_select_safe", null);
            bx = rSafe.Right + Sf(8f);

            int selCnt = 0;
            foreach (BloatAppItem b in BloatApps) if (b.Selected) selCnt++;

            RectangleF rClr = new RectangleF(bx, y, Sf(82f), tbH);
            DrawPillBtn(g, rClr, T("Clear", "Temizle", "清空"), IsHovered("bloat_clear", null), false, !_isBusy && selCnt > 0);
            if (!_isBusy && selCnt > 0) AddZone(rClr, "bloat_clear", null);

            RectangleF rRem = new RectangleF(availW - Sf(186f), y, Sf(186f), tbH);
            string remLbl = selCnt > 0
                ? string.Format(T("Remove Selected ({0})", "Seçilenleri Kaldır ({0})", "卸载所选 ({0})"), selCnt)
                : T("Select Apps to Remove", "Kaldırılacak Seçin", "请勾选要卸载的应用");
            DrawDangerPillBtn(g, rRem, remLbl, IsHovered("bloat_remove", null), !_isBusy && selCnt > 0);
            if (!_isBusy && selCnt > 0) AddZone(rRem, "bloat_remove", null);

            y += tbH + Sf(12f);

            // Dedicated Microsoft OneDrive Card
            float odH = Sf(84f);
            RectangleF od = new RectangleF(0, y, availW, odH);
            if (od.Bottom >= -4 && od.Top <= ClientSize.Height + 4)
            {
                DrawBox(g, od, Sf(10f), ColCard, ColBorder);
                float tx = od.X + Sf(16f);
                float maxX = od.Right - Sf(16f);
                string tOd = T("Microsoft OneDrive", "Microsoft OneDrive", "Microsoft OneDrive 网盘");
                float wOd = Math.Min(MeasureTight(g, tOd, _fTitle).Width + Sf(4f), Sf(260f));
                DrawTextTight(g, tOd, _fTitle, ColTextPrimary, new RectangleF(tx, od.Y + Sf(9f), wOd, Sf(19f)));
                if (OneDriveInstalled)
                    DrawTag(g, tx + wOd + Sf(8f), od.Y + Sf(10f), T("INSTALLED", "KURULU", "已安装"), ColCyanBg, ColCyanBd, ColCyan, maxX);
                else
                    DrawTag(g, tx + wOd + Sf(8f), od.Y + Sf(10f), T("REMOVED", "KALDIRILDI", "已卸载"), ColSafeBg, ColSafeBd, ColSafe, maxX);

                DrawText(g, T("Cleanly uninstalls OneDrive via official OneDriveSetup.exe & unpins Explorer sidebar, or reinstalls anytime via WinGet.",
                    "Resmi OneDriveSetup.exe ile OneDrive'ı temiz şekilde kaldırır veya istediğiniz zaman WinGet ile yeniden kurar.",
                    "通过官方 OneDriveSetup.exe 干净卸载并取消资源管理器侧边栏固定，随时可通过 WinGet 一键重装。"),
                    _fSub, ColTextSecondary, new RectangleF(tx, od.Y + Sf(31f), od.Width - Sf(32f), Sf(17f)), StringAlignment.Near);

                float by = od.Bottom - Sf(33f);
                RectangleF bOdRem = new RectangleF(tx, by, Sf(158f), Sf(25f));
                DrawDangerPillBtn(g, bOdRem, T("Remove OneDrive", "OneDrive'ı Kaldır", "卸载 OneDrive"), IsHovered("onedrive_remove", null), !_isBusy && OneDriveInstalled);
                if (!_isBusy && OneDriveInstalled) AddZone(bOdRem, "onedrive_remove", null);

                RectangleF bOdInst = new RectangleF(bOdRem.Right + Sf(8f), by, Sf(196f), Sf(25f));
                DrawPillBtn(g, bOdInst, T("Reinstall OneDrive (WinGet)", "OneDrive'ı Yeniden Kur", "重装 OneDrive (WinGet)"), IsHovered("onedrive_reinstall", null), !OneDriveInstalled, !_isBusy && WingetOk);
                if (!_isBusy && WingetOk) AddZone(bOdInst, "onedrive_reinstall", null);
            }
            y += odH + Sf(14f);

            DrawText(g, T("PREINSTALLED MICROSOFT BLOATWARE (28 SAFE APPX · STORE / WINGET / PHOTOS NEVER TOUCHED)",
                "ÖNYÜKLÜ MICROSOFT BLOATWARE (28 GÜVENLİ APPX · MAĞAZA / WINGET / FOTOĞRAFLAR KORUNUR)",
                "系统预装应用精简（28 项安全可选 · 绝不误删商店 / WinGet / 照片 / 计算器）"),
                _fSection, ColCyan, new RectangleF(Sf(2f), y, availW, Sf(20f)), StringAlignment.Near);
            y += Sf(26f);

            int cols = availW >= Sf(540f) ? 2 : 1;
            float gap = Sf(10f);
            float cardW = (availW - gap * (cols - 1)) / cols;
            float cardH = Sf(52f);

            for (int i = 0; i < BloatApps.Count; i++)
            {
                int col = i % cols;
                float x = col * (cardW + gap);
                RectangleF cr = new RectangleF(x, y, cardW, cardH);

                if (cr.Bottom >= -4 && cr.Top <= ClientSize.Height + 4)
                {
                    BloatAppItem item = BloatApps[i];
                    bool hov = IsHovered("bloat_toggle", item);
                    DrawBox(g, cr, Sf(9f), hov ? ColCardHover : ColCard, item.Selected ? ColDangerBd : (hov ? ColBorderHover : ColBorder));
                    if (!_isBusy) AddZone(cr, "bloat_toggle", item);

                    float cb = Sf(17f);
                    DrawCheckbox(g, new RectangleF(cr.X + Sf(12f), cr.Y + (cr.Height - cb) * 0.5f, cb, cb), item.Selected, ColDanger);

                    float tx = cr.X + Sf(38f);
                    float maxW = cr.Width - Sf(50f);
                    string name = item.GetName(_lang);
                    float drawNW = Math.Min(MeasureTight(g, name, _fTitle).Width + Sf(4f), maxW - Sf(84f));
                    DrawTextTight(g, name, _fTitle, item.IsInstalled ? ColTextPrimary : ColTextSecondary, new RectangleF(tx, cr.Y + Sf(7f), drawNW, Sf(19f)));

                    if (item.IsInstalled)
                        DrawTag(g, tx + drawNW + Sf(6f), cr.Y + Sf(8f), T("INSTALLED", "KURULU", "已安装"), ColAmberBg, ColAmberBd, ColAmber, cr.Right - Sf(10f));
                    else
                        DrawTag(g, tx + drawNW + Sf(6f), cr.Y + Sf(8f), T("ABSENT", "YOK", "未安装"), ColSafeBg, ColSafeBd, ColSafe, cr.Right - Sf(10f));

                    DrawText(g, item.GetNote(_lang) + "  ·  " + item.PackageName, _fSub, ColTextSecondary,
                        new RectangleF(tx, cr.Y + Sf(27f), maxW, Sf(17f)), StringAlignment.Near);
                }

                if (col == cols - 1 || i == BloatApps.Count - 1)
                    y += cardH + Sf(8f);
            }

            return (int)Math.Ceiling(y + _scrollY + Sf(8f));
        }

        // ==================== TAB 3: WINDOWS UPDATE & 1-CLICK DNS ====================
        private int PaintUpdatesTab(Graphics g, float availW)
        {
            float y = -_scrollY;

            if (!_isAdmin)
            {
                RectangleF banner = new RectangleF(0, y, availW, Sf(44f));
                DrawBox(g, banner, Sf(9f), ColAmberBg, ColAmberBd);
                DrawText(g, T("Windows Update policies live in HKLM and require Administrator rights.",
                    "Windows Update ilkeleri HKLM altında bulunur ve Yönetici izni gerektirir.",
                    "Windows 更新策略位于系统注册表 HKLM，需要管理员权限才能更改。"),
                    _fSub, ColAmber, new RectangleF(banner.X + Sf(14f), banner.Y, banner.Width - Sf(170f), banner.Height), StringAlignment.Near);

                RectangleF elevBtn = new RectangleF(banner.Right - Sf(148f), banner.Y + Sf(7f), Sf(136f), Sf(30f));
                DrawPillBtn(g, elevBtn, T("Run as Admin ↗", "Yönetici Aç ↗", "以管理员运行 ↗"), IsHovered("elevate", null), false, true);
                AddZone(elevBtn, "elevate", null);
                y += Sf(56f);
            }

            // Live Policy Status Card
            RectangleF statCard = new RectangleF(0, y, availW, Sf(68f));
            DrawBox(g, statCard, Sf(10f), ColCard, ColBorder);
            DrawText(g, T("CURRENT WINDOWS UPDATE POLICY", "MEVCUT WINDOWS UPDATE DURUMU", "当前 WINDOWS 更新策略状态"),
                _fSection, ColTextMuted, new RectangleF(Sf(16f), y + Sf(10f), availW - Sf(32f), Sf(18f)), StringAlignment.Near);

            string statusSummary = UpdateState.IsSecurityDeferred
                ? T("Security-Focused Mode Active (Feature updates deferred 365 days · Quality updates +4 days)",
                    "Güvenlik Odaklı Mod Aktif (Özellik güncellemeleri 365 gün · Kalite +4 gün ertelendi)",
                    "已启用安全优先模式（大版本功能更新延迟 365 天 · 质量更新延迟 4 天）")
                : T("Windows Default Policy (All feature & quality updates delivered normally)",
                    "Windows Varsayılan İlkesi (Tüm güncellemeler standart şekilde alınır)",
                    "Windows 默认更新策略（按微软默认节奏接收功能与安全更新）");

            DrawText(g, statusSummary, _fBig, UpdateState.IsSecurityDeferred ? ColSafe : ColCyan,
                new RectangleF(Sf(16f), y + Sf(32f), availW - Sf(32f), Sf(26f)), StringAlignment.Near);
            y += Sf(82f);

            // 2 Preset Cards Side-by-Side
            float gap = Sf(12f);
            float pW = (availW - gap) * 0.5f;
            float pH = Sf(138f);

            // Preset 1: Default
            RectangleF cDef = new RectangleF(0, y, pW, pH);
            DrawBox(g, cDef, Sf(11f), ColCard, !UpdateState.IsSecurityDeferred ? ColCyanBd : ColBorder);
            DrawText(g, T("Windows Default", "Windows Varsayılanı", "Windows 默认策略"), _fBig, ColTextPrimary,
                new RectangleF(cDef.X + Sf(16f), cDef.Y + Sf(14f), cDef.Width - Sf(32f), Sf(24f)), StringAlignment.Near);
            DrawTextWrap(g, T("Removes all custom deferral and driver exclusion policies. Restores stock Microsoft update behavior cleanly.",
                "Tüm özel erteleme ve sürücü engelleme ilkelerini kaldırır. Standart Windows Update davranışını geri yükler.",
                "清除所有自定义延迟与驱动屏蔽策略，完整恢复微软官方默认 Windows Update 行为。"),
                _fSub, ColTextSecondary, new RectangleF(cDef.X + Sf(16f), cDef.Y + Sf(44f), cDef.Width - Sf(32f), Sf(46f)));
            RectangleF btnDef = new RectangleF(cDef.X + Sf(16f), cDef.Bottom - Sf(40f), Sf(158f), Sf(28f));
            DrawPillBtn(g, btnDef, T("Restore Defaults", "Varsayılanı Uygula", "恢复默认策略"), IsHovered("wu_default", null), false, _isAdmin && !_isBusy);
            if (_isAdmin && !_isBusy) AddZone(btnDef, "wu_default", null);

            // Preset 2: Security-Focused
            RectangleF cSec = new RectangleF(pW + gap, y, pW, pH);
            DrawBox(g, cSec, Sf(11f), ColCard, UpdateState.IsSecurityDeferred ? ColSafeBd : ColBorder);
            DrawText(g, T("Security-Focused (Recommended)", "Güvenlik Odaklı (Önerilen)", "安全优先模式（推荐）"), _fBig, ColSafe,
                new RectangleF(cSec.X + Sf(16f), cSec.Y + Sf(14f), cSec.Width - Sf(32f), Sf(24f)), StringAlignment.Near);
            DrawTextWrap(g, T("Keeps critical security patches flowing while deferring major Windows feature upgrades by 365 days and preventing forced auto-restarts.",
                "Güvenlik yamalarını almaya devam eder; büyük özellik güncellemelerini 365 gün erteler ve zorla yeniden başlatmayı kapatır.",
                "正常接收重要安全补丁，同时将大版本功能更新延迟 365 天、质量更新延迟 4 天并禁止登录时自动强制重启。"),
                _fSub, ColTextSecondary, new RectangleF(cSec.X + Sf(16f), cSec.Y + Sf(44f), cSec.Width - Sf(32f), Sf(46f)));
            RectangleF btnSec = new RectangleF(cSec.X + Sf(16f), cSec.Bottom - Sf(40f), Sf(176f), Sf(28f));
            DrawPillBtn(g, btnSec, T("Apply Security Mode", "Güvenlik Modunu Uygula", "应用安全优先模式"), IsHovered("wu_security", null), true, _isAdmin && !_isBusy);
            if (_isAdmin && !_isBusy) AddZone(btnSec, "wu_security", null);

            y += pH + Sf(18f);

            // Extra Independent Toggles
            DrawText(g, T("FINE-GRAINED UPDATE CONTROLS", "EK GÜNCELLEME SEÇENEKLERİ", "独立更新控制项"), _fSection, ColCyan,
                new RectangleF(Sf(2f), y, availW, Sf(20f)), StringAlignment.Near);
            y += Sf(26f);

            y = DrawUpdateToggleCard(g, y, availW, "wu_toggle_drivers",
                T("Exclude Driver Updates from Windows Update", "Windows Update'in Sürücü Kurmasını Engelle", "禁止 Windows 更新自动覆盖硬件驱动"),
                T("Prevents Windows Update from overwriting your AMD / NVIDIA / audio drivers automatically.",
                  "Windows Update'in ekran kartı (AMD/NVIDIA) veya ses sürücülerinizi otomatik değiştirmesini engeller.",
                  "防止 Windows Update 自动覆盖您手动安装的 AMD / NVIDIA 显卡或声卡驱动。"),
                UpdateState.ExcludeDrivers);

            y = DrawUpdateToggleCard(g, y, availW, "wu_toggle_reboot",
                T("Prevent Forced Auto-Restart While Signed In", "Oturum Açıkken Otomatik Yeniden Başlatmayı Engelle", "禁止用户登录期间强制自动重启"),
                T("Stops Windows Update from forcibly rebooting your PC while you are logged in.",
                  "Bilgisayarınız açıkken Windows Update'in habersiz yeniden başlatma yapmasını önler.",
                  "防止 Windows Update 在您挂机或工作期间未经允许自动重启电脑。"),
                UpdateState.PreventAutoReboot);

            y += Sf(14f);

            // ==================== 1-CLICK DNS SWITCHER ====================
            string adapTxt = !string.IsNullOrEmpty(DnsStatus.PrimaryAdapterName)
                ? string.Format(T("1-CLICK DNS SWITCHER   ·   ACTIVE ADAPTER: {0}  ({1})",
                                  "TEK TIK DNS DEĞİŞTİRİCİ   ·   AKTİF BAĞDAŞTIRICI: {0}  ({1})",
                                  "一键 DNS 切换   ·   当前活动网卡: {0}  ({1})"),
                                DnsStatus.PrimaryAdapterName, DnsStatus.CurrentDnsDisplay)
                : T("1-CLICK DNS SWITCHER (ADMIN REQUIRED)", "TEK TIK DNS DEĞİŞTİRİCİ (YÖNETİCİ GEREKLİ)", "一键 DNS 切换（需管理员权限）");
            DrawText(g, adapTxt, _fSection, ColCyan, new RectangleF(Sf(2f), y, availW, Sf(20f)), StringAlignment.Near);
            y += Sf(26f);

            int dCols = availW >= Sf(660f) ? 4 : 2;
            float dGap = Sf(8f);
            float dw = (availW - dGap * (dCols - 1)) / dCols;
            float dh = Sf(62f);

            for (int i = 0; i < DnsPresets.Count; i++)
            {
                int col = i % dCols;
                float x = col * (dw + dGap);
                RectangleF dr = new RectangleF(x, y, dw, dh);
                if (dr.Bottom >= -4 && dr.Top <= ClientSize.Height + 4)
                {
                    DnsPreset p = DnsPresets[i];
                    bool isAct = string.Equals(DnsStatus.ActivePresetId, p.Id, StringComparison.OrdinalIgnoreCase);
                    bool canClick = _isAdmin && !_isBusy && DnsStatus.ActiveAdapters.Count > 0;
                    bool hov = canClick && IsHovered("dns_apply", p);
                    DrawBox(g, dr, Sf(9f), hov ? ColCardHover : ColCard, isAct ? ColSafeBd : (hov ? ColBorderHover : ColBorder));
                    if (canClick) AddZone(dr, "dns_apply", p);

                    DrawText(g, p.GetName(_lang), _fTitle, isAct ? ColSafe : ColTextPrimary,
                        new RectangleF(dr.X + Sf(10f), dr.Y + Sf(7f), dr.Width - Sf(20f), Sf(18f)), StringAlignment.Near);
                    string ipLine = p.IsDhcp ? T("Automatic (DHCP)", "Otomatik (DHCP)", "自动获取 (DHCP)") : (p.PrimaryV4 + " / " + p.SecondaryV4);
                    DrawText(g, ipLine, _fSmall, isAct ? ColCyan : ColTextBody,
                        new RectangleF(dr.X + Sf(10f), dr.Y + Sf(26f), dr.Width - Sf(20f), Sf(15f)), StringAlignment.Near);
                    DrawText(g, p.GetTag(_lang), _fSmall, ColTextSecondary,
                        new RectangleF(dr.X + Sf(10f), dr.Y + Sf(41f), dr.Width - Sf(20f), Sf(15f)), StringAlignment.Near);
                }
                if (col == dCols - 1 || i == DnsPresets.Count - 1)
                    y += dh + Sf(8f);
            }

            return (int)Math.Ceiling(y + _scrollY + Sf(12f));
        }

        private float DrawUpdateToggleCard(Graphics g, float y, float availW, string key, string title, string desc, bool isOn)
        {
            float rowH = Sf(62f);
            RectangleF cr = new RectangleF(0, y, availW, rowH);
            bool hov = _isAdmin && !_isBusy && IsHovered(key, null);
            DrawBox(g, cr, Sf(9f), hov ? ColCardHover : ColCard, hov ? ColBorderHover : ColBorder);
            if (_isAdmin && !_isBusy) AddZone(cr, key, null);

            DrawText(g, title, _fTitle, ColTextPrimary, new RectangleF(cr.X + Sf(14f), cr.Y + Sf(10f), cr.Width - Sf(84f), Sf(20f)), StringAlignment.Near);
            DrawText(g, desc, _fSub, ColTextSecondary, new RectangleF(cr.X + Sf(14f), cr.Y + Sf(32f), cr.Width - Sf(84f), Sf(19f)), StringAlignment.Near);
            DrawToggleSwitch(g, new RectangleF(cr.Right - Sf(58f), cr.Y + (cr.Height - Sf(22f)) * 0.5f, Sf(42f), Sf(22f)), isOn, !_isAdmin);

            return y + rowH + Sf(8f);
        }

        // ==================== TAB 3: REPAIR & TOOLS ====================
        private int PaintRepairTab(Graphics g, float availW)
        {
            float y = -_scrollY;

            DrawText(g, T("OFFICIAL WINDOWS SYSTEM REPAIRS", "RESMİ WINDOWS SİSTEM ONARIM ARAÇLARI", "微软官方系统修复与维护工具"),
                _fSection, ColCyan, new RectangleF(Sf(2f), y, availW, Sf(20f)), StringAlignment.Near);
            y += Sf(26f);

            foreach (RepairActionItem rep in Repairs)
            {
                float rowH = Sf(62f);
                RectangleF cr = new RectangleF(0, y, availW, rowH);
                if (cr.Bottom >= -4 && cr.Top <= ClientSize.Height + 4)
                {
                    DrawBox(g, cr, Sf(9f), ColCard, ColBorder);

                    float tx = cr.X + Sf(14f);
                    float rightEdge = cr.Right - Sf(102f);
                    string title = rep.GetName(_lang);
                    SizeF tSz = MeasureTight(g, title, _fTitle);
                    float drawW = Math.Min(tSz.Width + Sf(4f), rightEdge - tx - Sf(80f));
                    DrawTextTight(g, title, _fTitle, ColTextPrimary, new RectangleF(tx, cr.Y + Sf(9f), drawW, Sf(20f)));

                    if (rep.RequiresAdmin)
                        DrawTag(g, tx + drawW + Sf(8f), cr.Y + Sf(11f), T("ADMIN", "YÖNETİCİ", "需管理员"), ColAmberBg, ColAmberBd, ColAmber, rightEdge);
                    else
                        DrawTag(g, tx + drawW + Sf(8f), cr.Y + Sf(11f), T("SAFE", "GÜVENLİ", "安全"), ColSafeBg, ColSafeBd, ColSafe, rightEdge);

                    DrawText(g, rep.GetDesc(_lang) + "  ·  " + rep.CommandHint, _fSub, ColTextSecondary,
                        new RectangleF(tx, cr.Y + Sf(33f), rightEdge - tx - Sf(8f), Sf(19f)), StringAlignment.Near);

                    bool canRun = !_isBusy && (!rep.RequiresAdmin || _isAdmin);
                    RectangleF btn = new RectangleF(cr.Right - Sf(90f), cr.Y + (cr.Height - Sf(28f)) * 0.5f, Sf(76f), Sf(28f));
                    DrawPillBtn(g, btn, T("Run", "Çalıştır", "运行"), IsHovered("repair_run", rep), false, canRun);
                    if (canRun) AddZone(btn, "repair_run", rep);
                }
                y += rowH + Sf(8f);
            }

            y += Sf(12f);
            DrawText(g, T("WINDOWS OPTIONAL FEATURES (ENABLE / DISABLE VIA OFFICIAL DISM · ADMIN)",
                "WINDOWS OPSİYONEL ÖZELLİKLERİ (RESMİ DISM İLE AÇ / KAPAT · YÖNETİCİ)",
                "WINDOWS 可选系统功能（通过官方 DISM 启用或关闭 · 需管理员）"),
                _fSection, ColCyan, new RectangleF(Sf(2f), y, availW, Sf(20f)), StringAlignment.Near);
            y += Sf(26f);

            foreach (WinFeatureItem feat in WinFeatures)
            {
                float fH = Sf(58f);
                RectangleF fr = new RectangleF(0, y, availW, fH);
                if (fr.Bottom >= -4 && fr.Top <= ClientSize.Height + 4)
                {
                    DrawBox(g, fr, Sf(9f), ColCard, ColBorder);
                    float tx = fr.X + Sf(14f);
                    float rightEdge = fr.Right - Sf(178f);
                    DrawText(g, feat.GetName(_lang), _fTitle, ColTextPrimary, new RectangleF(tx, fr.Y + Sf(8f), rightEdge - tx, Sf(19f)), StringAlignment.Near);
                    DrawText(g, feat.GetDesc(_lang) + "  ·  " + feat.FeatureHint, _fSub, ColTextSecondary, new RectangleF(tx, fr.Y + Sf(30f), rightEdge - tx, Sf(18f)), StringAlignment.Near);

                    bool canRun = !_isBusy && _isAdmin;
                    RectangleF bEn = new RectangleF(fr.Right - Sf(166f), fr.Y + (fr.Height - Sf(26f)) * 0.5f, Sf(76f), Sf(26f));
                    DrawPillBtn(g, bEn, T("Enable", "Etkinleştir", "启用"), IsHovered("feat_enable", feat), false, canRun);
                    if (canRun) AddZone(bEn, "feat_enable", feat);

                    RectangleF bDis = new RectangleF(fr.Right - Sf(84f), fr.Y + (fr.Height - Sf(26f)) * 0.5f, Sf(72f), Sf(26f));
                    DrawPillBtn(g, bDis, T("Disable", "Kapat", "关闭"), IsHovered("feat_disable", feat), false, canRun);
                    if (canRun) AddZone(bDis, "feat_disable", feat);
                }
                y += fH + Sf(8f);
            }

            y += Sf(12f);
            DrawText(g, T("CLASSIC WINDOWS CONTROL PANELS (15 · 1-CLICK LAUNCH)", "KLASİK WINDOWS YÖNETİM PANELLERİ (15 · TEK TIK)", "经典 Windows 管理面板快捷入口（15 项）"),
                _fSection, ColCyan, new RectangleF(Sf(2f), y, availW, Sf(20f)), StringAlignment.Near);
            y += Sf(26f);

            int cols = availW >= Sf(640f) ? 5 : 3;
            float gap = Sf(8f);
            float sw = (availW - gap * (cols - 1)) / cols;
            float sh = Sf(40f);

            for (int i = 0; i < Shortcuts.Count; i++)
            {
                int col = i % cols;
                float x = col * (sw + gap);
                RectangleF sr = new RectangleF(x, y, sw, sh);
                if (sr.Bottom >= -4 && sr.Top <= ClientSize.Height + 4)
                {
                    ControlPanelShortcut sc = Shortcuts[i];
                    bool hov = IsHovered("shortcut_open", sc);
                    DrawBox(g, sr, Sf(8f), hov ? ColBtnHover : ColCard, hov ? ColSafeBd : ColBorder);
                    AddZone(sr, "shortcut_open", sc);
                    DrawText(g, sc.GetName(_lang), _fBtn, hov ? ColTextPrimary : ColTextBody, sr, StringAlignment.Center);
                }
                if (col == cols - 1 || i == Shortcuts.Count - 1)
                    y += sh + Sf(8f);
            }

            return (int)Math.Ceiling(y + _scrollY + Sf(10f));
        }

        // ==================== TAB 4: ADVANCED (OPTIONAL EXPERT TOOLS) ====================
        private int PaintAdvancedTab(Graphics g, float availW)
        {
            float y = -_scrollY;

            // Optional expert warning banner
            RectangleF banner = new RectangleF(0, y, availW, Sf(36f));
            DrawBox(g, banner, Sf(8f), ColAmberBg, ColAmberBd);
            string banTxt = !_isAdmin
                ? T("Optional Expert Tools — Click 'Admin ↗' for Defender/Edge/Store controls. Safe HKCU Registry Cleanup works without Admin.",
                    "İsteğe Bağlı Uzman Araçları — Defender/Edge/Store için 'Yönetici ↗' tıklayın. Güvenli HKCU Kayıt Defteri Temizliği yönetici gerektirmez.",
                    "可选高级工具 — 管理 Defender / Edge / 应用商店需点击右上角「管理员 ↗」；安全注册表清理无需管理员权限。")
                : T("Optional Expert Tools — Excluded from Recommended Preset. Every action asks confirmation & offers 1-click Restore or .reg backup.",
                    "İsteğe Bağlı Uzman Araçları — Önerilen ayarlara dahil değildir. Her işlem onay ister ve tek tıkla geri yükleme / .reg yedeği sunar.",
                    "可选高级工具 — 不会随一键推荐自动开启；每项操作均需二次确认，并提供一键恢复或 .reg 自动备份。");
            DrawText(g, banTxt, _fSub, ColAmber, new RectangleF(banner.X + Sf(14f), banner.Y, banner.Width - Sf(28f), banner.Height), StringAlignment.Near);
            y += Sf(44f);

            float cardH = Sf(94f);

            // --- CARD 1: WINDOWS DEFENDER POLICY ---
            RectangleF c1 = new RectangleF(0, y, availW, cardH);
            if (c1.Bottom >= -4 && c1.Top <= ClientSize.Height + 4)
            {
                DrawBox(g, c1, Sf(10f), ColCard, ColBorder);
                float tx = c1.X + Sf(16f);
                float maxX = c1.Right - Sf(16f);
                string t1 = T("Windows Defender Real-Time Protection", "Windows Defender Gerçek Zamanlı Koruma", "Windows Defender 实时保护策略");
                float w1 = Math.Min(MeasureTight(g, t1, _fTitle).Width + Sf(4f), Sf(360f));
                DrawTextTight(g, t1, _fTitle, ColTextPrimary, new RectangleF(tx, c1.Y + Sf(9f), w1, Sf(19f)));
                float nx = DrawTag(g, tx + w1 + Sf(8f), c1.Y + Sf(10f), T("EXPERT", "UZMAN", "高级"), ColAmberBg, ColAmberBd, ColAmber, maxX);
                nx = DrawTag(g, nx, c1.Y + Sf(10f), T("ADMIN", "YÖNETİCİ", "需管理员"), ColAmberBg, ColAmberBd, ColAmber, maxX);

                if (AdvStatus.DefenderRealTimeEnabled)
                {
                    nx = DrawTag(g, nx, c1.Y + Sf(10f), T("ACTIVE", "AKTİF", "运行中"), ColSafeBg, ColSafeBd, ColSafe, maxX);
                    if (AdvStatus.DefenderTamperProtected)
                        DrawTag(g, nx, c1.Y + Sf(10f), T("TAMPER PROTECTION ON", "KURCALAMA KORUMASI AÇIK", "篡改保护已开启"), ColCyanBg, ColCyanBd, ColCyan, maxX);
                }
                else
                {
                    DrawTag(g, nx, c1.Y + Sf(10f), T("POLICY DISABLED", "DEVRE DIŞI", "策略已禁用"), ColDangerBg, ColDangerBd, ColDanger, maxX);
                }

                string d1 = T(
                    "Applies official Group Policy & MpPreference. Never bypasses Tamper Protection — turn it off in Windows Security first if active.",
                    "Resmi Grup İlkesi ve MpPreference kullanır. Kurcalama Koruması açıksa önce Windows Güvenliği'nden kapatmanız gerekir.",
                    "使用微软官方组策略与 MpPreference 控制；绝不绕过防篡改机制，若篡改保护开启请先在 Windows 安全中心手动关闭。");
                DrawText(g, d1, _fSub, ColTextSecondary, new RectangleF(tx, c1.Y + Sf(32f), c1.Width - Sf(32f), Sf(17f)), StringAlignment.Near);

                float by = c1.Bottom - Sf(36f);
                float bx = tx;
                RectangleF bDefDis = new RectangleF(bx, by, Sf(188f), Sf(26f));
                DrawDangerPillBtn(g, bDefDis, T("Disable Defender Policy", "Defender'ı Devre Dışı Bırak", "禁用 Defender 策略"), IsHovered("adv_def_disable", null), !_isBusy && _isAdmin);
                if (!_isBusy && _isAdmin) AddZone(bDefDis, "adv_def_disable", null);
                bx = bDefDis.Right + Sf(8f);

                RectangleF bDefRes = new RectangleF(bx, by, Sf(152f), Sf(26f));
                DrawPillBtn(g, bDefRes, T("Restore Defender", "Defender'ı Geri Aç", "恢复 Defender 保护"), IsHovered("adv_def_restore", null), !AdvStatus.DefenderRealTimeEnabled, !_isBusy && _isAdmin);
                if (!_isBusy && _isAdmin) AddZone(bDefRes, "adv_def_restore", null);
                bx = bDefRes.Right + Sf(8f);

                RectangleF bDefSec = new RectangleF(bx, by, Sf(166f), Sf(26f));
                DrawPillBtn(g, bDefSec, T("Windows Security ↗", "Windows Güvenliği ↗", "打开安全中心 ↗"), IsHovered("adv_def_security", null), false, true);
                AddZone(bDefSec, "adv_def_security", null);
            }
            y += cardH + Sf(8f);

            // --- CARD 2: MICROSOFT EDGE ---
            RectangleF c2 = new RectangleF(0, y, availW, cardH);
            if (c2.Bottom >= -4 && c2.Top <= ClientSize.Height + 4)
            {
                DrawBox(g, c2, Sf(10f), ColCard, ColBorder);
                float tx = c2.X + Sf(16f);
                float maxX = c2.Right - Sf(16f);
                string t2 = T("Microsoft Edge Browser", "Microsoft Edge Tarayıcısı", "Microsoft Edge 浏览器");
                float w2 = Math.Min(MeasureTight(g, t2, _fTitle).Width + Sf(4f), Sf(280f));
                DrawTextTight(g, t2, _fTitle, ColTextPrimary, new RectangleF(tx, c2.Y + Sf(9f), w2, Sf(19f)));
                float nx = DrawTag(g, tx + w2 + Sf(8f), c2.Y + Sf(10f), T("EXPERT", "UZMAN", "高级"), ColAmberBg, ColAmberBd, ColAmber, maxX);
                nx = DrawTag(g, nx, c2.Y + Sf(10f), T("ADMIN", "YÖNETİCİ", "需管理员"), ColAmberBg, ColAmberBd, ColAmber, maxX);

                if (AdvStatus.EdgeInstalled)
                {
                    string verTxt = !string.IsNullOrEmpty(AdvStatus.EdgeVersion) ? (" " + AdvStatus.EdgeVersion) : "";
                    DrawTag(g, nx, c2.Y + Sf(10f), T("INSTALLED", "KURULU", "已安装") + verTxt, ColCyanBg, ColCyanBd, ColCyan, maxX);
                }
                else
                {
                    DrawTag(g, nx, c2.Y + Sf(10f), T("REMOVED", "KALDIRILDI", "已卸载"), ColSafeBg, ColSafeBd, ColSafe, maxX);
                }

                string d2 = T(
                    "Uses Edge's official setup.exe / WinGet to remove or reinstall Microsoft Edge. Leaves Edge WebView2 Runtime 100% intact.",
                    "Edge'i resmi setup.exe / WinGet ile kaldırır veya yeniden kurar. Windows uygulamaları için Edge WebView2'ye kesinlikle dokunmaz.",
                    "调用微软官方安装程序 / WinGet 卸载或重装 Edge 浏览器，完整保留 Edge WebView2 运行时，不影响其他应用。");
                DrawText(g, d2, _fSub, ColTextSecondary, new RectangleF(tx, c2.Y + Sf(32f), c2.Width - Sf(32f), Sf(17f)), StringAlignment.Near);

                float by = c2.Bottom - Sf(36f);
                float bx = tx;
                bool canRemEdge = !_isBusy && _isAdmin && AdvStatus.EdgeInstalled;
                RectangleF bEdgeRem = new RectangleF(bx, by, Sf(156f), Sf(26f));
                DrawDangerPillBtn(g, bEdgeRem, T("Remove Edge", "Edge'i Kaldır", "卸载 Edge 浏览器"), IsHovered("adv_edge_remove", null), canRemEdge);
                if (canRemEdge) AddZone(bEdgeRem, "adv_edge_remove", null);
                bx = bEdgeRem.Right + Sf(8f);

                bool canInstEdge = !_isBusy && WingetOk;
                RectangleF bEdgeInst = new RectangleF(bx, by, Sf(186f), Sf(26f));
                DrawPillBtn(g, bEdgeInst, T("Reinstall Edge (WinGet)", "Edge'i Yeniden Kur", "重装 Edge (WinGet)"), IsHovered("adv_edge_reinstall", null), !AdvStatus.EdgeInstalled, canInstEdge);
                if (canInstEdge) AddZone(bEdgeInst, "adv_edge_reinstall", null);
            }
            y += cardH + Sf(8f);

            // --- CARD 3: MICROSOFT STORE ---
            RectangleF c3 = new RectangleF(0, y, availW, cardH);
            if (c3.Bottom >= -4 && c3.Top <= ClientSize.Height + 4)
            {
                DrawBox(g, c3, Sf(10f), ColCard, ColBorder);
                float tx = c3.X + Sf(16f);
                float maxX = c3.Right - Sf(16f);
                string t3 = T("Microsoft Store", "Microsoft Store (Mağaza)", "Microsoft Store 应用商店");
                float w3 = Math.Min(MeasureTight(g, t3, _fTitle).Width + Sf(4f), Sf(280f));
                DrawTextTight(g, t3, _fTitle, ColTextPrimary, new RectangleF(tx, c3.Y + Sf(9f), w3, Sf(19f)));
                float nx = DrawTag(g, tx + w3 + Sf(8f), c3.Y + Sf(10f), T("EXPERT", "UZMAN", "高级"), ColAmberBg, ColAmberBd, ColAmber, maxX);
                nx = DrawTag(g, nx, c3.Y + Sf(10f), T("ADMIN", "YÖNETİCİ", "需管理员"), ColAmberBg, ColAmberBd, ColAmber, maxX);

                if (AdvStatus.StoreInstalled)
                    DrawTag(g, nx, c3.Y + Sf(10f), T("INSTALLED", "KURULU", "已安装"), ColCyanBg, ColCyanBd, ColCyan, maxX);
                else
                    DrawTag(g, nx, c3.Y + Sf(10f), T("REMOVED", "KALDIRILDI", "已卸载"), ColSafeBg, ColSafeBd, ColSafe, maxX);

                string d3 = T(
                    "Removes Microsoft.WindowsStore via official Appx cmdlet or restores it cleanly with Microsoft's wsreset -i. WinGet stays intact.",
                    "Resmi Appx komutuyla Microsoft Store'u kaldırır veya wsreset -i ile eksiksiz geri kurar. WinGet zarar görmez.",
                    "通过官方 Appx 命令卸载微软商店，或使用微软内置 wsreset -i 一键重装；不影响 WinGet 软件包管理器。");
                DrawText(g, d3, _fSub, ColTextSecondary, new RectangleF(tx, c3.Y + Sf(32f), c3.Width - Sf(32f), Sf(17f)), StringAlignment.Near);

                float by = c3.Bottom - Sf(36f);
                float bx = tx;
                bool canRemSt = !_isBusy && _isAdmin && AdvStatus.StoreInstalled;
                RectangleF bStRem = new RectangleF(bx, by, Sf(156f), Sf(26f));
                DrawDangerPillBtn(g, bStRem, T("Remove Store", "Store'u Kaldır", "卸载微软商店"), IsHovered("adv_store_remove", null), canRemSt);
                if (canRemSt) AddZone(bStRem, "adv_store_remove", null);
                bx = bStRem.Right + Sf(8f);

                bool canInstSt = !_isBusy;
                RectangleF bStInst = new RectangleF(bx, by, Sf(198f), Sf(26f));
                DrawPillBtn(g, bStInst, T("Reinstall Store (wsreset -i)", "Store'u Geri Kur (wsreset)", "重装微软商店 (wsreset -i)"), IsHovered("adv_store_reinstall", null), !AdvStatus.StoreInstalled, canInstSt);
                if (canInstSt) AddZone(bStInst, "adv_store_reinstall", null);
            }
            y += cardH + Sf(8f);

            // --- CARD 4: SAFE BACKUP-FIRST REGISTRY CLEANUP ---
            float regH = Sf(114f);
            RectangleF c4 = new RectangleF(0, y, availW, regH);
            if (c4.Bottom >= -4 && c4.Top <= ClientSize.Height + 4)
            {
                DrawBox(g, c4, Sf(10f), ColCard, RegScan.Scanned && RegScan.TotalCount > 0 ? ColSafeBd : ColBorder);
                float tx = c4.X + Sf(16f);
                float maxX = c4.Right - Sf(16f);
                string t4 = T("Safe Registry Cleanup (HKCU)", "Güvenli Kayıt Defteri Temizliği (HKCU)", "安全注册表清理（自动 .reg 备份）");
                float w4 = Math.Min(MeasureTight(g, t4, _fTitle).Width + Sf(4f), Sf(340f));
                DrawTextTight(g, t4, _fTitle, ColTextPrimary, new RectangleF(tx, c4.Y + Sf(9f), w4, Sf(19f)));
                float nx = DrawTag(g, tx + w4 + Sf(8f), c4.Y + Sf(10f), T("SAFE", "GÜVENLİ", "安全"), ColSafeBg, ColSafeBd, ColSafe, maxX);
                nx = DrawTag(g, nx, c4.Y + Sf(10f), T("AUTO .REG BACKUP", "OTO .REG YEDEK", "自动 .reg 备份"), ColCyanBg, ColCyanBd, ColCyan, maxX);

                if (RegScan.Scanned)
                {
                    string cntTag = RegScan.TotalCount > 0
                        ? string.Format(T("{0} STALE FOUND", "{0} KALINTI", "发现 {0} 项"), RegScan.TotalCount)
                        : T("CLEAN", "TEMİZ", "已洁净");
                    DrawTag(g, nx, c4.Y + Sf(10f), cntTag,
                        RegScan.TotalCount > 0 ? ColAmberBg : ColSafeBg,
                        RegScan.TotalCount > 0 ? ColAmberBd : ColSafeBd,
                        RegScan.TotalCount > 0 ? ColAmber : ColSafe, maxX);
                }

                string d4 = T(
                    "Scans dead local .exe refs in MuiCache, AppCompat Store, AppSwitched & RunMRU/TypedPaths. Never touches CLSID, drivers, or USB drives.",
                    "MuiCache, AppCompat, AppSwitched silinmiş .exe kayıtları ve RunMRU/TypedPaths tarar. CLSID, sürücü veya harici disklere dokunmaz.",
                    "仅扫描 MuiCache、AppCompat、AppSwitched 失效本地程序残留及 RunMRU/TypedPaths 历史，绝不触碰 CLSID、驱动或系统服务。");
                DrawText(g, d4, _fSub, ColTextSecondary, new RectangleF(tx, c4.Y + Sf(32f), c4.Width - Sf(32f), Sf(17f)), StringAlignment.Near);

                string summary = RegScan.Scanned
                    ? string.Format(
                        T("Scan Result — MuiCache: {0}  ·  AppCompat: {1}  ·  AppSwitched: {2}  ·  RunMRU / TypedPaths: {3}   (Total: {4})",
                          "Tarama Sonucu — MuiCache: {0}  ·  AppCompat: {1}  ·  AppSwitched: {2}  ·  RunMRU / TypedPaths: {3}   (Toplam: {4})",
                          "扫描结果 — MuiCache: {0}  ·  AppCompat: {1}  ·  AppSwitched: {2}  ·  运行/路径历史: {3}   （共 {4} 项）"),
                        RegScan.MuiCacheCount, RegScan.AppCompatCount, RegScan.AppSwitchedCount, RegScan.MruCount, RegScan.TotalCount)
                    : T("Click 'Scan Registry' for a read-only check. 'Backup & Clean' saves a double-click restorable .reg file first.",
                        "Salt okunur tarama için 'Kayıt Defterini Tara' tıklayın. 'Yedekle ve Temizle' önce geri yüklenebilir .reg yedeği alır.",
                        "点击「扫描注册表」进行只读检查；「备份并清理」会先导出可双击还原的标准 .reg 备份文件。");
                DrawText(g, summary, _fSmall, RegScan.Scanned ? ColCyan : ColTextMuted,
                    new RectangleF(tx, c4.Y + Sf(52f), c4.Width - Sf(32f), Sf(16f)), StringAlignment.Near);

                float by = c4.Bottom - Sf(36f);
                float bx = tx;
                RectangleF bRegScan = new RectangleF(bx, by, Sf(148f), Sf(26f));
                DrawPillBtn(g, bRegScan, T("Scan Registry", "Kayıt Defterini Tara", "扫描注册表"), IsHovered("adv_reg_scan", null), false, !_isBusy);
                if (!_isBusy) AddZone(bRegScan, "adv_reg_scan", null);
                bx = bRegScan.Right + Sf(8f);

                bool canClean = !_isBusy && RegScan.Scanned && RegScan.TotalCount > 0;
                string clnLbl = RegScan.TotalCount > 0
                    ? string.Format(T("Backup & Clean ({0})", "Yedekle & Temizle ({0})", "备份并清理 ({0})"), RegScan.TotalCount)
                    : T("Backup & Clean", "Yedekle & Temizle", "备份并清理");
                RectangleF bRegClean = new RectangleF(bx, by, Sf(178f), Sf(26f));
                DrawPillBtn(g, bRegClean, clnLbl, IsHovered("adv_reg_clean", null), canClean, canClean);
                if (canClean) AddZone(bRegClean, "adv_reg_clean", null);
                bx = bRegClean.Right + Sf(8f);

                RectangleF bBackups = new RectangleF(bx, by, Sf(164f), Sf(26f));
                DrawPillBtn(g, bBackups, T("Open Backups Folder", "Yedek Klasörünü Aç", "打开备份目录"), IsHovered("adv_open_backups", null), false, true);
                AddZone(bBackups, "adv_open_backups", null);
            }
            y += regH + Sf(6f);

            return (int)Math.Ceiling(y + _scrollY + Sf(6f));
        }

        private void DrawDangerPillBtn(Graphics g, RectangleF r, string text, bool hovered, bool enabled)
        {
            Color bg, bd, fg;
            if (!enabled) { bg = ColCard; bd = Color.FromArgb(34, 42, 54); fg = ColTextMuted; }
            else { bg = hovered ? ColDangerHover : ColDangerBg; bd = hovered ? ColDanger : ColDangerBd; fg = ColDanger; }
            DrawBox(g, r, Sf(7f), bg, bd);
            DrawText(g, text, _fBtn, fg, r, StringAlignment.Center);
        }

        // ==================== SHARED DRAWING HELPERS ====================
        private void DrawCheckbox(Graphics g, RectangleF r, bool isChecked, Color activeColor)
        {
            if (isChecked)
            {
                DrawBox(g, r, Sf(4.5f), activeColor, activeColor);
                using (Pen p = new Pen(ColBg, Math.Max(1.7f, Sf(1.9f))))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                    g.DrawLines(p, new PointF[]
                    {
                        new PointF(r.X + r.Width * 0.22f, r.Y + r.Height * 0.52f),
                        new PointF(r.X + r.Width * 0.43f, r.Y + r.Height * 0.74f),
                        new PointF(r.X + r.Width * 0.78f, r.Y + r.Height * 0.28f)
                    });
                }
            }
            else
            {
                DrawBox(g, r, Sf(4.5f), ColBg, Color.FromArgb(72, 84, 102));
            }
        }

        private void DrawToggleSwitch(Graphics g, RectangleF r, bool isOn, bool locked)
        {
            Color trackBg = locked ? Color.FromArgb(24, 30, 38) : (isOn ? Color.FromArgb(18, 128, 92) : Color.FromArgb(36, 44, 56));
            Color trackBd = locked ? Color.FromArgb(42, 50, 62) : (isOn ? ColSafe : Color.FromArgb(62, 74, 92));
            DrawBox(g, r, r.Height * 0.5f, trackBg, trackBd);

            float pad = Sf(3f);
            float knobD = r.Height - pad * 2f;
            float knobX = isOn ? (r.Right - pad - knobD) : (r.X + pad);
            using (SolidBrush kb = new SolidBrush(locked ? ColTextMuted : Color.White))
                g.FillEllipse(kb, knobX, r.Y + pad, knobD, knobD);
        }

        private void DrawPillBtn(Graphics g, RectangleF r, string text, bool hovered, bool primary, bool enabled)
        {
            Color bg, bd, fg;
            if (!enabled) { bg = ColCard; bd = Color.FromArgb(34, 42, 54); fg = ColTextMuted; }
            else if (primary) { bg = hovered ? ColPrimaryHover : ColPrimaryBg; bd = ColSafe; fg = Color.White; }
            else { bg = hovered ? ColBtnHover : ColBtnBg; bd = hovered ? Color.FromArgb(78, 94, 116) : ColBtnBd; fg = ColTextBody; }

            DrawBox(g, r, Sf(7f), bg, bd);
            DrawText(g, text, _fBtn, fg, r, StringAlignment.Center);
        }

        private float DrawTag(Graphics g, float x, float y, string text, Color bg, Color bd, Color fg, float maxX)
        {
            SizeF sz = MeasureTight(g, text, _fBadge);
            float w = sz.Width + Sf(12f), h = Sf(16.5f);
            if (x + w > maxX) return maxX;
            RectangleF r = new RectangleF(x, y, w, h);
            DrawBox(g, r, Sf(4.5f), bg, bd);
            DrawText(g, text, _fBadge, fg, r, StringAlignment.Center);
            return x + w + Sf(6f);
        }

        private static SizeF MeasureTight(Graphics g, string text, Font font)
        {
            using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
            {
                sf.FormatFlags |= StringFormatFlags.NoWrap;
                return g.MeasureString(text, font, PointF.Empty, sf);
            }
        }

        private static void DrawTextTight(Graphics g, string text, Font font, Color color, RectangleF r)
        {
            using (SolidBrush b = new SolidBrush(color))
            using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
            {
                sf.Alignment = StringAlignment.Near; sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags |= StringFormatFlags.NoWrap; sf.Trimming = StringTrimming.EllipsisCharacter;
                g.DrawString(text, font, b, r, sf);
            }
        }

        private static void DrawText(Graphics g, string text, Font font, Color color, RectangleF r, StringAlignment align)
        {
            using (SolidBrush b = new SolidBrush(color))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = align; sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap; sf.Trimming = StringTrimming.EllipsisCharacter;
                g.DrawString(text, font, b, r, sf);
            }
        }

        private static void DrawTextWrap(Graphics g, string text, Font font, Color color, RectangleF r)
        {
            using (SolidBrush b = new SolidBrush(color))
                g.DrawString(text, font, b, r);
        }

        private static void DrawBox(Graphics g, RectangleF r, float rad, Color fill, Color border)
        {
            using (GraphicsPath p = Rounded(r, rad))
            using (SolidBrush fb = new SolidBrush(fill))
            using (Pen bp = new Pen(border, 1f)) { g.FillPath(fb, p); g.DrawPath(bp, p); }
        }

        private static GraphicsPath Rounded(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180f, 90f); p.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f); p.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            p.CloseFigure(); return p;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeFonts();
            base.Dispose(disposing);
        }
    }

    internal sealed class MainForm : Form
    {
        private const int LogicalW = 960;
        private const int LogicalH = 700;

        private static readonly Color ColBg = Color.FromArgb(13, 17, 23);
        private static readonly Color ColCard = Color.FromArgb(22, 27, 34);
        private static readonly Color ColBorder = Color.FromArgb(42, 51, 64);
        private static readonly Color ColDivider = Color.FromArgb(32, 40, 52);
        private static readonly Color ColTextPrimary = Color.FromArgb(240, 246, 252);
        private static readonly Color ColTextBody = Color.FromArgb(204, 212, 220);
        private static readonly Color ColTextSecondary = Color.FromArgb(139, 148, 158);
        private static readonly Color ColTextMuted = Color.FromArgb(110, 118, 129);
        private static readonly Color ColSafe = Color.FromArgb(52, 211, 153);
        private static readonly Color ColCyan = Color.FromArgb(56, 189, 248);
        private static readonly Color ColAmber = Color.FromArgb(251, 191, 36);
        private static readonly Color ColBtnNormal = Color.FromArgb(28, 35, 45);
        private static readonly Color ColBtnHover = Color.FromArgb(40, 49, 63);
        private static readonly Color ColBtnBd = Color.FromArgb(52, 63, 80);
        private static readonly Color ColPrimaryBg = Color.FromArgb(16, 124, 88);
        private static readonly Color ColPrimaryHover = Color.FromArgb(20, 148, 105);

        private readonly LaunchConfig _cfg;
        private readonly UserPrefs _prefs;
        private readonly ForgeContentPanel _content;
        private readonly TextBox _txtSearch;
        private readonly TextBox _txtConsole;
        private readonly Icon _appIcon;
        private readonly bool _isAdmin;
        private readonly string _osBadge;

        private float _scale = 1.0f;
        private int _activeTab;
        private bool _isBusy;
        private volatile bool _cancelFlag;
        private bool _consoleOpen;
        private float _progressRatio;
        private string _statusText = "";
        private bool _statusIsSuccess;

        private readonly List<ClickZone> _chromeZones = new List<ClickZone>();
        private string _hoverKey = "";

        private Font _fBrand;
        private Font _fSmallPill;
        private Font _fLangZh;
        private Font _fTab;
        private Font _fBtn;
        private Font _fStatus;
        private Font _fSearch;

        public MainForm(LaunchConfig cfg)
        {
            _cfg = cfg ?? new LaunchConfig();
            _prefs = _cfg.DemoMode ? new UserPrefs() : UserPrefs.Load();
            if (!string.IsNullOrEmpty(_cfg.ForceLang)) _prefs.Language = _cfg.ForceLang;
            _activeTab = _cfg.InitialTab;
            _isAdmin = _cfg.DemoMode ? true : WinNative.IsAdministrator();
            _osBadge = _cfg.DemoMode ? "Windows 11 · Build 26200" : WinNative.GetWindowsVersionBadge();

            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = ColBg;
            DoubleBuffered = true;
            KeyPreview = true;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);

            _appIcon = LoadAppIcon();
            if (_appIcon != null) Icon = _appIcon;

            _txtSearch = new TextBox();
            _txtSearch.BackColor = ColCard;
            _txtSearch.ForeColor = ColTextPrimary;
            _txtSearch.BorderStyle = BorderStyle.FixedSingle;
            _txtSearch.TextChanged += delegate { _content.SetAppSearch(_txtSearch.Text); };
            Controls.Add(_txtSearch);

            _content = new ForgeContentPanel();
            _content.ActionInvoked += OnContentAction;
            Controls.Add(_content);

            _txtConsole = new TextBox();
            _txtConsole.Multiline = true;
            _txtConsole.ReadOnly = true;
            _txtConsole.ScrollBars = ScrollBars.Vertical;
            _txtConsole.BackColor = Color.FromArgb(10, 14, 19);
            _txtConsole.ForeColor = Color.FromArgb(176, 206, 194);
            _txtConsole.BorderStyle = BorderStyle.FixedSingle;
            _txtConsole.Font = new Font("Consolas", 8.5f);
            _txtConsole.Visible = false;
            Controls.Add(_txtConsole);

            InitializeData();
            UpdateWindowTitle();
            UpdateDpiScale(GetInitialDpi());
            ClientSize = new Size(S(LogicalW), S(LogicalH));
            MinimumSize = new Size(S(840), S(580));

            if (!_cfg.DemoMode)
                StartBackgroundChecks();
        }

        private string Lang { get { return _prefs.Language ?? "en"; } }
        private string T(string en, string tr, string zh) { return Loc.T(Lang, en, tr, zh); }

        private static Icon LoadAppIcon()
        {
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { return null; }
        }

        private float GetInitialDpi()
        {
            using (Graphics g = CreateGraphics()) return g.DpiX > 0 ? g.DpiX : 96f;
        }

        private void InitializeData()
        {
            _content.Apps = WingetEngine.BuildVerifiedCatalog();
            _content.Tweaks = TweaksEngine.BuildCatalog();
            _content.BloatApps = DebloatEngine.BuildCatalog();
            _content.DnsPresets = DnsEngine.BuildPresets();
            _content.WinFeatures = UpdatesAndRepairEngine.BuildWindowsFeatures();
            _content.Repairs = UpdatesAndRepairEngine.BuildRepairActions();
            _content.Shortcuts = UpdatesAndRepairEngine.BuildLegacyShortcuts();

            if (_cfg.DemoMode)
            {
                _content.WingetOk = true;
                _content.WingetVersion = "v1.30";
                foreach (WingetAppItem a in _content.Apps)
                {
                    if (a.Id == "Google.Chrome" || a.Id == "Microsoft.VisualStudioCode" || a.Id == "Git.Git" || a.Id == "7zip.7zip")
                        a.IsInstalled = true;
                    if (a.Id == "Mozilla.Firefox" || a.Id == "OpenJS.NodeJS.LTS" || a.Id == "Notepad++.Notepad++")
                        a.Selected = true;
                }
                foreach (TweakDefinition tw in _content.Tweaks)
                {
                    bool on = tw.Id == "show_file_ext" || tw.Id == "dark_mode" || tw.Id == "classic_context_menu" || tw.Id == "disable_bing_search" || tw.Id == "enable_game_mode" || tw.Id == "disable_ad_id" || tw.Id == "services_manual";
                    tw.IsCurrentlyApplied = on;
                    tw.DesiredState = on;
                    if (tw.Id == "explorer_this_pc" || tw.Id == "taskbar_end_task")
                        tw.DesiredState = true;
                }
                _content.OneDriveInstalled = true;
                foreach (BloatAppItem b in _content.BloatApps)
                {
                    if (b.PackageName == "Clipchamp.Clipchamp" || b.PackageName == "Microsoft.WindowsFeedbackHub" || b.PackageName == "Microsoft.BingNews" || b.PackageName == "MSTeams" || b.PackageName == "Microsoft.OutlookForWindows" || b.PackageName == "Microsoft.GetHelp")
                        b.IsInstalled = true;
                    if (b.PackageName == "Clipchamp.Clipchamp" || b.PackageName == "Microsoft.BingNews" || b.PackageName == "MSTeams")
                        b.Selected = true;
                }
                _content.UpdateState = new UpdatePolicyState();
                _content.UpdateState.IsSecurityDeferred = true;
                _content.UpdateState.FeatureDeferDays = 365;
                _content.UpdateState.QualityDeferDays = 4;
                _content.UpdateState.ExcludeDrivers = true;
                _content.UpdateState.PreventAutoReboot = true;

                _content.DnsStatus = new LiveDnsStatus();
                _content.DnsStatus.ActiveAdapters.Add("Ethernet");
                _content.DnsStatus.PrimaryAdapterName = "Ethernet";
                _content.DnsStatus.CurrentDnsDisplay = "1.1.1.1, 1.0.0.1";
                _content.DnsStatus.ActivePresetId = "cloudflare";

                _content.AdvStatus = new AdvancedStatus();
                _content.AdvStatus.DefenderRealTimeEnabled = true;
                _content.AdvStatus.DefenderTamperProtected = true;
                _content.AdvStatus.DefenderChecked = true;
                _content.AdvStatus.EdgeInstalled = true;
                _content.AdvStatus.EdgeVersion = "158.0";
                _content.AdvStatus.StoreInstalled = true;
                _content.AdvStatus.StoreChecked = true;

                _content.RegScan = new RegistryScanResult();
                _content.RegScan.Scanned = true;
                _content.RegScan.MuiCacheCount = 19;
                _content.RegScan.AppCompatCount = 14;
                _content.RegScan.AppSwitchedCount = 7;
                _content.RegScan.MruCount = 6;
                for (int i = 0; i < 46; i++)
                    _content.RegScan.Entries.Add(new RegistryJunkEntry());

                _statusText = T(
                    "Ready · 60 verified apps · 40 reversible tweaks · Debloat · DNS · Features & Repairs · Optional Advanced tab.",
                    "Hazır · 60 doğrulanmış uygulama · 40 geri alınabilir ayar · Bloatware · DNS · Özellik/Onarım · Gelişmiş sekmesi.",
                    "就绪 · 60 款校验软件 · 40 项可还原优化 · 内置精简 · 一键 DNS · 系统功能与高级选项。");
            }
            else
            {
                TweaksEngine.RefreshLiveStates(_content.Tweaks);
                _content.OneDriveInstalled = DebloatEngine.IsOneDriveInstalled();
                _content.UpdateState = UpdatesAndRepairEngine.ReadUpdatePolicy();
                _content.DnsStatus = DnsEngine.ReadLiveDnsStatus(_content.DnsPresets);
                _content.AdvStatus = AdvancedEngine.ReadFastStatus();
                _statusText = T(
                    "Ready · Select any tab to install apps, tweak Windows, debloat, switch DNS, enable features, or run repairs.",
                    "Hazır · Uygulama kurmak, ayar yapmak, bloatware temizlemek, DNS veya özellik yönetmek için sekmeleri kullanın.",
                    "就绪 · 切换上方标签即可批量装机、系统优化、精简预装、一键 DNS、管理功能或修复系统。");
            }
        }

        private void StartBackgroundChecks()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                string ver;
                bool ok = WingetEngine.IsWingetAvailable(out ver);
                AdvancedStatus adv = AdvancedEngine.ReadFullStatus();
                DebloatEngine.ScanInstalledBloat(_content.BloatApps);
                SafeInvoke(delegate
                {
                    _content.WingetOk = ok;
                    _content.WingetVersion = ver;
                    _content.AdvStatus = adv;
                    _content.Invalidate();
                });
            });
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WinNative.ApplyDarkTitleBar(Handle, ColBg, ColTextPrimary);
            UpdateSearchPlaceholder();
            try
            {
                uint winDpi = WinNative.GetDpiForWindow(Handle);
                if (winDpi >= 72 && Math.Abs((winDpi / 96f) - _scale) > 0.01f)
                {
                    UpdateDpiScale(winDpi);
                    ClientSize = new Size(S(LogicalW), S(LogicalH));
                    MinimumSize = new Size(S(780), S(560));
                }
            }
            catch { }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WinNative.WM_DPICHANGED)
            {
                int newDpi = (int)(m.WParam.ToInt64() & 0xFFFF);
                if (newDpi >= 72) { UpdateDpiScale(newDpi); Invalidate(); }
            }
            base.WndProc(ref m);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutAll(); Invalidate(); }

        private int S(float v) { return (int)Math.Round(v * _scale); }
        private float Sf(float v) { return v * _scale; }
        private float PtToPx(float pt) { return pt * (96f / 72f) * _scale; }

        private void UpdateDpiScale(float dpi)
        {
            _scale = dpi / 96f;
            RebuildFonts();
            _content.Configure(_scale, Lang, _activeTab, _isAdmin, _isBusy);
            LayoutAll();
        }

        private void RebuildFonts()
        {
            DisposeFonts();
            string fam = Loc.FontFamily(Lang);
            _fBrand = new Font("Segoe UI", PtToPx(11.5f), FontStyle.Bold, GraphicsUnit.Pixel);
            _fSmallPill = new Font(fam, PtToPx(7.6f), FontStyle.Bold, GraphicsUnit.Pixel);
            _fLangZh = new Font("Microsoft YaHei UI", PtToPx(7.5f), FontStyle.Bold, GraphicsUnit.Pixel);
            _fTab = new Font(fam, PtToPx(8.8f), FontStyle.Bold, GraphicsUnit.Pixel);
            _fBtn = new Font(fam, PtToPx(8.3f), FontStyle.Bold, GraphicsUnit.Pixel);
            _fStatus = new Font(fam, PtToPx(8.4f), FontStyle.Regular, GraphicsUnit.Pixel);
            _fSearch = new Font(fam, PtToPx(8.8f), FontStyle.Regular, GraphicsUnit.Pixel);
            _txtSearch.Font = _fSearch;
        }

        private void DisposeFonts()
        {
            if (_fBrand != null) { _fBrand.Dispose(); _fBrand = null; }
            if (_fSmallPill != null) { _fSmallPill.Dispose(); _fSmallPill = null; }
            if (_fLangZh != null) { _fLangZh.Dispose(); _fLangZh = null; }
            if (_fTab != null) { _fTab.Dispose(); _fTab = null; }
            if (_fBtn != null) { _fBtn.Dispose(); _fBtn = null; }
            if (_fStatus != null) { _fStatus.Dispose(); _fStatus = null; }
            if (_fSearch != null) { _fSearch.Dispose(); _fSearch = null; }
        }

        private void UpdateSearchPlaceholder()
        {
            if (_txtSearch != null && !_txtSearch.IsDisposed)
                WinNative.SetCueBanner(_txtSearch.Handle, T("Search 60 apps...", "60 uygulama içinde ara...", "搜索 60 款软件..."));
        }

        private void UpdateWindowTitle()
        {
            Text = T("WinForge — Safe Windows Utility & Tweaker",
                "WinForge — Güvenli Windows Araç ve Optimizasyon Paketi",
                "WinForge — 安全透明的 Windows 装机优化与维护工具箱");
            UpdateSearchPlaceholder();
        }

        private void LayoutAll()
        {
            int w = ClientSize.Width;
            int h = ClientSize.Height;
            int pad = S(20f);

            int consoleH = _consoleOpen ? S(132f) : 0;
            int footerH = S(54f) + consoleH;

            bool showSearchBar = (_activeTab == 0);
            int contentTop = showSearchBar ? S(142f) : S(104f);

            _txtSearch.Visible = showSearchBar;
            if (showSearchBar)
                _txtSearch.SetBounds(pad, S(104f), S(236f), S(26f));

            int contentH = Math.Max(S(140f), h - contentTop - footerH - S(8f));
            _content.SetBounds(pad, contentTop, Math.Max(S(300f), w - pad * 2), contentH);

            _txtConsole.Visible = _consoleOpen;
            if (_consoleOpen)
                _txtConsole.SetBounds(pad, h - footerH + S(8f), Math.Max(S(300f), w - pad * 2), consoleH - S(12f));
        }

        private void AppendLog(string line)
        {
            SafeInvoke(delegate
            {
                if (_txtConsole.TextLength > 24000) _txtConsole.Clear();
                _txtConsole.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line + Environment.NewLine);
            });
        }

        private void SafeInvoke(MethodInvoker m)
        {
            try { if (!IsDisposed && IsHandleCreated) BeginInvoke(m); } catch { }
        }

        private void SetLanguage(string lang)
        {
            if (string.Equals(_prefs.Language, lang, StringComparison.OrdinalIgnoreCase)) return;
            _prefs.Language = lang;
            if (!_cfg.DemoMode) _prefs.Save();
            RebuildFonts();
            UpdateWindowTitle();
            _content.Configure(_scale, Lang, _activeTab, _isAdmin, _isBusy);
            Invalidate();
        }

        private void SwitchTab(int tab)
        {
            if (_activeTab == tab) return;
            _activeTab = tab;
            _content.Configure(_scale, Lang, _activeTab, _isAdmin, _isBusy);
            LayoutAll();
            Invalidate();
        }

        private void SetBusy(bool busy, string statusMsg, bool openConsole)
        {
            _isBusy = busy;
            _cancelFlag = false;
            if (!string.IsNullOrEmpty(statusMsg)) { _statusText = statusMsg; _statusIsSuccess = false; }
            if (openConsole && !_consoleOpen) { _consoleOpen = true; LayoutAll(); }
            _content.Configure(_scale, Lang, _activeTab, _isAdmin, _isBusy);
            Invalidate();
        }

        private void OnContentAction(string key, object payload)
        {
            if (key == "app_toggle")
            {
                WingetAppItem app = payload as WingetAppItem;
                if (app != null && !_isBusy) { app.Selected = !app.Selected; _content.Invalidate(); Invalidate(); }
            }
            else if (key == "tweak_toggle")
            {
                TweakDefinition tw = payload as TweakDefinition;
                if (tw != null && !_isBusy)
                {
                    if (tw.RequiresAdmin && !_isAdmin)
                    {
                        _statusText = T("This tweak modifies HKLM and requires Administrator mode — click 'Admin ↗' in the top bar.",
                            "Bu ayar HKLM değiştirir ve Yönetici izni ister — üstteki 'Yönetici ↗' butonuna tıklayın.",
                            "此项需要修改系统注册表 HKLM，请点击右上角「管理员 ↗」提权后再操作。");
                        _statusIsSuccess = false;
                        Invalidate();
                        return;
                    }
                    tw.DesiredState = !tw.DesiredState;
                    // Mutually exclusive Windows Power Plans
                    if (tw.DesiredState && (tw.Id == "high_perf_power" || tw.Id == "ultimate_perf_power"))
                    {
                        string otherId = tw.Id == "high_perf_power" ? "ultimate_perf_power" : "high_perf_power";
                        foreach (TweakDefinition o in _content.Tweaks)
                            if (o.Id == otherId) o.DesiredState = false;
                    }
                    _content.Invalidate();
                    Invalidate();
                }
            }
            else if (key == "tweak_preset")
            {
                foreach (TweakDefinition tw in _content.Tweaks)
                {
                    if (!tw.RequiresAdmin || _isAdmin)
                    {
                        if (tw.InRecommendedPreset) tw.DesiredState = true;
                        else if (tw.Id == "ultimate_perf_power") tw.DesiredState = tw.IsCurrentlyApplied;
                    }
                }
                _statusText = T("Desktop Preset selected (includes Hibernation Off & Services Manual) — review and click 'Apply Changes'.",
                    "Masaüstü Profili seçildi (Hazırda Beklet Kapalı & Servisler Manuel dahil) — kontrol edip 'Uygula' tıklayın.",
                    "已选择「台式机推荐」配置（含关闭休眠与服务手动化）— 核对后点击「应用更改」。");
                _statusIsSuccess = false;
                _content.Invalidate();
                Invalidate();
            }
            else if (key == "tweak_preset_laptop")
            {
                foreach (TweakDefinition tw in _content.Tweaks)
                {
                    if (!tw.RequiresAdmin || _isAdmin)
                    {
                        if (tw.InLaptopPreset) tw.DesiredState = true;
                        else if (tw.InRecommendedPreset) tw.DesiredState = tw.IsCurrentlyApplied; // Never leave Desktop-only Hibernation Off pending on Laptop!
                    }
                }
                _statusText = T("Laptop Preset selected (preserves Hibernation & battery sleep) — review and click 'Apply Changes'.",
                    "Dizüstü Profili seçildi (Hazırda Beklet ve pil tasarrufu korunur) — kontrol edip 'Uygula' tıklayın.",
                    "已选择「笔记本推荐」配置（保留休眠与省电模式）— 核对后点击「应用更改」。");
                _statusIsSuccess = false;
                _content.Invalidate();
                Invalidate();
            }
            else if (key == "bloat_toggle")
            {
                BloatAppItem item = payload as BloatAppItem;
                if (item != null && !_isBusy) { item.Selected = !item.Selected; _content.Invalidate(); Invalidate(); }
            }
            else if (key == "bloat_scan")
            {
                if (_cfg.DemoMode || _isBusy) return;
                RunAsyncTask(T("Scanning installed preinstalled Microsoft apps...", "Önyüklü Microsoft uygulamaları taranıyor...", "正在扫描本机预装的微软应用..."), false, delegate
                {
                    int n = DebloatEngine.ScanInstalledBloat(_content.BloatApps);
                    bool od = DebloatEngine.IsOneDriveInstalled();
                    AppendLog("[Debloat] Found " + n + " installed catalog Appx packages (OneDrive=" + od + ").");
                    SafeInvoke(delegate { _content.OneDriveInstalled = od; });
                }, T("Installed bloatware scan complete.", "Bloatware taraması tamamlandı.", "预装应用扫描完成。"));
            }
            else if (key == "bloat_select_safe")
            {
                int cnt = 0;
                foreach (BloatAppItem b in _content.BloatApps)
                {
                    if (b.DefaultSafeBloat && b.IsInstalled) { b.Selected = true; cnt++; }
                }
                _statusText = cnt > 0
                    ? string.Format(T("Selected {0} installed safe bloatware apps — click 'Remove Selected' to uninstall.", "Kurulu {0} güvenli bloatware seçildi — kaldırmak için 'Seçilenleri Kaldır' tıklayın.", "已勾选本机存在的 {0} 项常见预装应用 — 点击「卸载所选」即可清理。"), cnt)
                    : T("No default bloatware apps detected as installed on this PC.", "Bu bilgisayarda kurulu varsayılan bloatware tespit edilmedi.", "当前系统未检测到常见的多余预装应用。");
                _statusIsSuccess = false;
                _content.Invalidate(); Invalidate();
            }
            else if (key == "bloat_clear")
            {
                foreach (BloatAppItem b in _content.BloatApps) b.Selected = false;
                _content.Invalidate(); Invalidate();
            }
            else if (key == "bloat_remove")
            {
                if (_cfg.DemoMode || _isBusy) return;
                List<BloatAppItem> sel = new List<BloatAppItem>();
                foreach (BloatAppItem b in _content.BloatApps) if (b.Selected) sel.Add(b);
                if (sel.Count == 0) return;

                if (!ConfirmExpertAction(
                    T("Remove Selected Built-In Apps?", "Seçilen Önyüklü Uygulamalar Kaldırılsın mı?", "确认卸载所选预装应用？"),
                    string.Format(T("Remove {0} selected Microsoft Appx packages? You can reinstall any of them anytime from Microsoft Store.",
                                    "Seçilen {0} Microsoft Appx uygulaması kaldırılacak. İstediğiniz zaman Microsoft Store'dan geri kurabilirsiniz.",
                                    "将卸载所选的 {0} 款微软内置应用，随时可从微软商店重新安装。"), sel.Count)))
                    return;

                RunAsyncTask(T("Removing selected bloatware apps...", "Seçilen bloatware uygulamaları kaldırılıyor...", "正在卸载所选内置应用..."), true, delegate
                {
                    int rem, fail;
                    DebloatEngine.RemoveSelectedBloat(sel, AppendLog, delegate { return _cancelFlag || IsDisposed; }, out rem, out fail);
                    AppendLog("[Debloat] Finished: removed=" + rem + ", warnings=" + fail);
                }, T("Selected bloatware removal completed.", "Seçilen bloatware kaldırma işlemi tamamlandı.", "所选内置应用卸载完成。"));
            }
            else if (key == "onedrive_remove")
            {
                if (_cfg.DemoMode || _isBusy) return;
                if (!ConfirmExpertAction(
                    T("Remove Microsoft OneDrive?", "Microsoft OneDrive Kaldırılsın mı?", "确认卸载 Microsoft OneDrive？"),
                    T("Uninstalls OneDrive via official OneDriveSetup.exe (your cloud files on onedrive.com stay safe, and you can reinstall anytime via WinGet).",
                      "Resmi OneDriveSetup.exe ile OneDrive kaldırılır (buluttaki dosyalarınız güvendedir ve istediğiniz zaman WinGet ile geri kurulur).",
                      "将调用官方卸载程序移除 OneDrive（云端文件不受影响，随时可通过 WinGet 一键重装）。")))
                    return;

                RunAsyncTask(T("Removing Microsoft OneDrive...", "Microsoft OneDrive kaldırılıyor...", "正在卸载 Microsoft OneDrive..."), true, delegate
                {
                    DebloatEngine.RemoveOneDrive(AppendLog, delegate { return _cancelFlag || IsDisposed; });
                    bool still = DebloatEngine.IsOneDriveInstalled();
                    SafeInvoke(delegate { _content.OneDriveInstalled = still; });
                }, T("Microsoft OneDrive removed cleanly.", "Microsoft OneDrive kaldırıldı.", "Microsoft OneDrive 已卸载。"));
            }
            else if (key == "onedrive_reinstall")
            {
                if (_cfg.DemoMode || _isBusy) return;
                RunAsyncTask(T("Reinstalling Microsoft OneDrive via WinGet...", "Microsoft OneDrive WinGet ile yeniden kuruluyor...", "正在通过 WinGet 重装 OneDrive..."), true, delegate
                {
                    DebloatEngine.ReinstallOneDrive(AppendLog, delegate { return _cancelFlag || IsDisposed; });
                    bool now = DebloatEngine.IsOneDriveInstalled();
                    SafeInvoke(delegate { _content.OneDriveInstalled = now; });
                }, T("Microsoft OneDrive reinstall completed.", "Microsoft OneDrive yeniden kuruldu.", "Microsoft OneDrive 重装完成。"));
            }
            else if (key == "dns_apply")
            {
                DnsPreset preset = payload as DnsPreset;
                if (preset == null || _cfg.DemoMode || !_isAdmin || _isBusy) return;
                List<string> adapters = new List<string>(_content.DnsStatus.ActiveAdapters);
                SetBusy(true, T("Applying DNS: ", "DNS uygulanıyor: ", "正在切换 DNS: ") + preset.GetName(Lang) + "...", false);
                _progressRatio = 0.35f;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    bool ok = false;
                    try { ok = DnsEngine.ApplyDnsPreset(preset, adapters, AppendLog); } catch (Exception ex) { AppendLog("[ERROR] " + ex.Message); }
                    LiveDnsStatus st = DnsEngine.ReadLiveDnsStatus(_content.DnsPresets);
                    SafeInvoke(delegate
                    {
                        _isBusy = false; _progressRatio = 1f;
                        _content.DnsStatus = st;
                        _content.Configure(_scale, Lang, _activeTab, _isAdmin, false);
                        _statusIsSuccess = ok;
                        _statusText = ok
                            ? (T("DNS updated & flushed: ", "DNS güncellendi ve önbellek temizlendi: ", "DNS 已切换并刷新缓存：") + preset.GetName(Lang))
                            : T("DNS switch reported a warning — check Console for details.", "DNS değişikliği uyarı verdi — ayrıntılar için Konsola bakın.", "DNS 切换出现警告，请打开日志查看详情。");
                        Invalidate();
                    });
                });
            }
            else if (key == "feat_enable" || key == "feat_disable")
            {
                WinFeatureItem feat = payload as WinFeatureItem;
                if (feat == null || _cfg.DemoMode || !_isAdmin || _isBusy) return;
                bool en = (key == "feat_enable");
                string actWord = en ? T("Enabling: ", "Etkinleştiriliyor: ", "正在启用: ") : T("Disabling: ", "Kapatılıyor: ", "正在关闭: ");
                RunAsyncTask(actWord + feat.GetName(Lang) + "...", true, delegate
                {
                    if (en && feat.EnableAction != null) feat.EnableAction(AppendLog, delegate { return _cancelFlag || IsDisposed; });
                    else if (!en && feat.DisableAction != null) feat.DisableAction(AppendLog, delegate { return _cancelFlag || IsDisposed; });
                }, feat.GetName(Lang) + " — " + T("Completed (reboot may be required).", "Tamamlandı (yeniden başlatma gerekebilir).", "已完成（部分功能需重启生效）。"));
            }
            else if (key == "tweak_revert_all")
            {
                foreach (TweakDefinition tw in _content.Tweaks)
                {
                    if (!tw.RequiresAdmin || _isAdmin)
                        tw.DesiredState = tw.DefaultWindowsState;
                }
                _statusText = T("All accessible tweaks queued for Windows Default — click 'Apply Changes' to revert.",
                    "Tüm ayarlar Windows varsayılanına geri alınmak üzere işaretlendi — 'Uygula' ile onaylayın.",
                    "已将所有项标记为恢复 Windows 默认值 — 点击「应用更改」即可全部还原。");
                _statusIsSuccess = false;
                _content.Invalidate();
                Invalidate();
            }
            else if (key == "tweak_apply")
            {
                if (_cfg.DemoMode || _isBusy) return;
                ApplyTweaksAsync();
            }
            else if (key == "restart_explorer")
            {
                if (_cfg.DemoMode || _isBusy) return;
                RunAsyncTask(T("Restarting Windows Explorer...", "Windows Gezgini yenileniyor...", "正在重启 Windows 资源管理器..."), false, delegate
                {
                    WinNative.RestartExplorerSafely(AppendLog);
                }, T("Windows Explorer restarted.", "Windows Gezgini yenilendi.", "Windows 资源管理器已重启。"));
            }
            else if (key == "elevate")
            {
                if (!_cfg.DemoMode && WinNative.RestartAsAdministrator("--tab " + _activeTab))
                    Close();
            }
            else if (key == "wu_default")
            {
                if (_cfg.DemoMode || !_isAdmin || _isBusy) return;
                UpdatesAndRepairEngine.ApplyDefaultUpdates(AppendLog);
                _content.UpdateState = UpdatesAndRepairEngine.ReadUpdatePolicy();
                _statusText = T("Windows Update policy restored to Microsoft defaults.", "Windows Update varsayılan ayarlara döndürüldü.", "已恢复 Windows Update 微软默认策略。");
                _statusIsSuccess = true;
                _content.Invalidate(); Invalidate();
            }
            else if (key == "wu_security")
            {
                if (_cfg.DemoMode || !_isAdmin || _isBusy) return;
                UpdatesAndRepairEngine.ApplySecurityFocusedUpdates(AppendLog);
                _content.UpdateState = UpdatesAndRepairEngine.ReadUpdatePolicy();
                _statusText = T("Security-Focused Windows Update policy applied (Feature +365d · Quality +4d).", "Güvenlik odaklı Windows Update ilkesi uygulandı.", "已启用安全优先更新策略（大版本延迟 365 天 · 质量更新延迟 4 天）。");
                _statusIsSuccess = true;
                _content.Invalidate(); Invalidate();
            }
            else if (key == "wu_toggle_drivers")
            {
                if (_cfg.DemoMode || !_isAdmin || _isBusy) return;
                UpdatesAndRepairEngine.SetExcludeDrivers(!_content.UpdateState.ExcludeDrivers, AppendLog);
                _content.UpdateState = UpdatesAndRepairEngine.ReadUpdatePolicy();
                _statusText = T("Driver update policy updated.", "Sürücü güncelleme ilkesi güncellendi.", "驱动更新策略已更新。");
                _statusIsSuccess = true;
                _content.Invalidate(); Invalidate();
            }
            else if (key == "wu_toggle_reboot")
            {
                if (_cfg.DemoMode || !_isAdmin || _isBusy) return;
                UpdatesAndRepairEngine.SetPreventAutoReboot(!_content.UpdateState.PreventAutoReboot, AppendLog);
                _content.UpdateState = UpdatesAndRepairEngine.ReadUpdatePolicy();
                _statusText = T("Auto-reboot policy updated.", "Otomatik yeniden başlatma ilkesi güncellendi.", "自动重启策略已更新。");
                _statusIsSuccess = true;
                _content.Invalidate(); Invalidate();
            }
            else if (key == "repair_run")
            {
                RepairActionItem rep = payload as RepairActionItem;
                if (rep == null || _cfg.DemoMode || _isBusy) return;
                RunAsyncTask(T("Running: ", "Çalıştırılıyor: ", "正在运行: ") + rep.GetName(Lang), true, delegate
                {
                    rep.Execute(AppendLog, delegate { return _cancelFlag || IsDisposed; });
                }, rep.GetName(Lang) + " — " + T("Completed.", "Tamamlandı.", "已完成。"));
            }
            else if (key == "shortcut_open")
            {
                ControlPanelShortcut sc = payload as ControlPanelShortcut;
                if (sc != null && !_cfg.DemoMode)
                    UpdatesAndRepairEngine.LaunchShortcut(sc);
            }
            else if (key == "adv_def_security")
            {
                if (!_cfg.DemoMode) AdvancedEngine.OpenWindowsSecurity();
            }
            else if (key == "adv_open_backups")
            {
                if (!_cfg.DemoMode) AdvancedEngine.OpenBackupDirectory();
            }
            else if (key == "adv_def_disable")
            {
                if (_cfg.DemoMode || !_isAdmin || _isBusy) return;
                if (!ConfirmExpertAction(
                    T("Disable Windows Defender Policy?", "Windows Defender Devre Dışı Bırakılsın mı?", "确认禁用 Windows Defender 策略？"),
                    T("This disables Windows Defender Real-Time Protection via official Group Policy.\n\nOnly proceed if you use another antivirus or a dedicated test environment. You can restore it anytime with 1 click.\n\nContinue?",
                      "Bu işlem Windows Defender Gerçek Zamanlı Korumayı resmi Grup İlkesi ile devre dışı bırakır.\n\nYalnızca başka bir antivirüs kullanıyorsanız veya test ortamındaysanız devam edin. İstediğiniz zaman tek tıkla geri açabilirsiniz.\n\nDevam edilsin mi?",
                      "此操作将通过官方组策略禁用 Windows Defender 实时保护。\n\n仅建议在已安装其他杀毒软件或测试环境下使用，随时可一键恢复。\n\n是否继续？")))
                    return;

                SetBusy(true, T("Applying Windows Defender policy...", "Windows Defender politikası uygulanıyor...", "正在应用 Windows Defender 策略..."), true);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    bool blocked;
                    bool ok = AdvancedEngine.DisableDefenderPolicy(AppendLog, out blocked);
                    AdvancedStatus st = AdvancedEngine.ReadFullStatus();
                    SafeInvoke(delegate
                    {
                        _isBusy = false; _progressRatio = 1f;
                        _content.AdvStatus = st;
                        _content.Configure(_scale, Lang, _activeTab, _isAdmin, false);
                        if (blocked)
                        {
                            _statusIsSuccess = false;
                            _statusText = T("Blocked by Tamper Protection — turn OFF Tamper Protection in Windows Security first.",
                                "Kurcalama Koruması aktif — önce Windows Güvenliği'nden Kurcalama Korumasını kapatın.",
                                "已被防篡改保护阻止 — 请先在 Windows 安全中心关闭「篡改保护」后再试。");
                            if (MessageBox.Show(this,
                                T("Windows Security Tamper Protection is currently ON, so Windows blocks apps from changing Defender.\n\nOpen Windows Security settings now so you can toggle Tamper Protection manually?",
                                  "Windows Güvenliği Kurcalama Koruması şu an AÇIK olduğu için Windows bu değişikliği engelledi.\n\nKurcalama Korumasını manuel kapatmak için Windows Güvenliği açılsın mı?",
                                  "当前 Windows 安全中心「篡改保护」已开启，系统已阻止第三方修改 Defender。\n\n是否立即打开 Windows 安全中心以便手动关闭篡改保护？"),
                                T("Tamper Protection Active", "Kurcalama Koruması Açık", "篡改保护已开启"),
                                MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                            {
                                AdvancedEngine.OpenWindowsSecurity();
                            }
                        }
                        else
                        {
                            _statusIsSuccess = ok;
                            _statusText = ok
                                ? T("Windows Defender policy disabled (click 'Restore Defender' anytime to re-enable).", "Windows Defender politikası devre dışı bırakıldı (istediğiniz zaman geri açabilirsiniz).", "已禁用 Defender 实时保护策略（随时可点击「恢复 Defender 保护」还原）。")
                                : T("Could not update Defender policy (check Console).", "Defender politikası güncellenemedi (Konsola bakın).", "Defender 策略应用失败（请查看日志）。");
                        }
                        Invalidate();
                    });
                });
            }
            else if (key == "adv_def_restore")
            {
                if (_cfg.DemoMode || !_isAdmin || _isBusy) return;
                RunAsyncTask(T("Restoring Windows Defender protection...", "Windows Defender koruması geri yükleniyor...", "正在恢复 Windows Defender 实时保护..."), true, delegate
                {
                    AdvancedEngine.RestoreDefenderPolicy(AppendLog);
                    AdvancedStatus st = AdvancedEngine.ReadFullStatus();
                    SafeInvoke(delegate { _content.AdvStatus = st; });
                }, T("Windows Defender defaults restored.", "Windows Defender varsayılan koruması geri yüklendi.", "Windows Defender 默认保护已恢复。"));
            }
            else if (key == "adv_edge_remove")
            {
                if (_cfg.DemoMode || !_isAdmin || _isBusy) return;
                if (!ConfirmExpertAction(
                    T("Remove Microsoft Edge Browser?", "Microsoft Edge Kaldırılsın mı?", "确认卸载 Microsoft Edge？"),
                    T("Removes Microsoft Edge browser using the official installer.\n\nEdge WebView2 Runtime is NOT touched, and you can reinstall Edge anytime with 1 click via WinGet.\n\nContinue?",
                      "Microsoft Edge tarayıcısı resmi kaldırıcı ile kaldırılacak.\n\nEdge WebView2 Runtime kesinlikle korunur ve Edge'i istediğiniz zaman WinGet ile tek tıkla geri kurabilirsiniz.\n\nDevam edilsin mi?",
                      "将使用官方卸载程序移除 Microsoft Edge 浏览器。\n\n不会影响 Edge WebView2 运行时，且随时可通过 WinGet 一键重装。\n\n是否继续？")))
                    return;

                RunAsyncTask(T("Removing Microsoft Edge browser...", "Microsoft Edge kaldırılıyor...", "正在卸载 Microsoft Edge 浏览器..."), true, delegate
                {
                    AdvancedEngine.RemoveEdge(AppendLog, delegate { return _cancelFlag || IsDisposed; });
                    AdvancedStatus st = AdvancedEngine.ReadFullStatus();
                    SafeInvoke(delegate { _content.AdvStatus = st; });
                }, T("Microsoft Edge removal finished (see Console).", "Microsoft Edge kaldırma işlemi tamamlandı (Konsola bakın).", "Microsoft Edge 卸载流程已完成（详见日志）。"));
            }
            else if (key == "adv_edge_reinstall")
            {
                if (_cfg.DemoMode || _isBusy) return;
                RunAsyncTask(T("Reinstalling Microsoft Edge via WinGet...", "Microsoft Edge WinGet ile yeniden kuruluyor...", "正在通过 WinGet 重新安装 Microsoft Edge..."), true, delegate
                {
                    AdvancedEngine.ReinstallEdge(AppendLog, delegate { return _cancelFlag || IsDisposed; });
                    AdvancedStatus st = AdvancedEngine.ReadFullStatus();
                    SafeInvoke(delegate { _content.AdvStatus = st; });
                }, T("Microsoft Edge installation finished.", "Microsoft Edge kurulumu tamamlandı.", "Microsoft Edge 重新安装完成。"));
            }
            else if (key == "adv_store_remove")
            {
                if (_cfg.DemoMode || !_isAdmin || _isBusy) return;
                if (!ConfirmExpertAction(
                    T("Remove Microsoft Store?", "Microsoft Store Kaldırılsın mı?", "确认卸载 Microsoft Store？"),
                    T("Removes Microsoft.WindowsStore for the current user via official Appx cmdlet.\n\nWinGet (App Installer) remains intact, and you can reinstall Microsoft Store anytime with 1 click (wsreset -i).\n\nContinue?",
                      "Microsoft Store resmi Appx komutu ile kaldırılacak.\n\nWinGet etkilenmez ve Microsoft Store'u istediğiniz zaman tek tıkla (wsreset -i) geri kurabilirsiniz.\n\nDevam edilsin mi?",
                      "将通过官方 Appx 命令卸载微软商店。\n\nWinGet 保持正常可用，随时可点击「重装微软商店 (wsreset -i)」一键恢复。\n\n是否继续？")))
                    return;

                RunAsyncTask(T("Removing Microsoft Store...", "Microsoft Store kaldırılıyor...", "正在卸载 Microsoft Store..."), true, delegate
                {
                    AdvancedEngine.RemoveStore(AppendLog, delegate { return _cancelFlag || IsDisposed; });
                    AdvancedStatus st = AdvancedEngine.ReadFullStatus();
                    SafeInvoke(delegate { _content.AdvStatus = st; });
                }, T("Microsoft Store removed cleanly.", "Microsoft Store kaldırıldı.", "Microsoft Store 已卸载。"));
            }
            else if (key == "adv_store_reinstall")
            {
                if (_cfg.DemoMode || _isBusy) return;
                RunAsyncTask(T("Reinstalling Microsoft Store (wsreset -i)...", "Microsoft Store yeniden kuruluyor (wsreset -i)...", "正在通过 wsreset -i 重装微软商店..."), true, delegate
                {
                    AdvancedEngine.ReinstallStore(AppendLog, delegate { return _cancelFlag || IsDisposed; });
                    AdvancedStatus st = AdvancedEngine.ReadFullStatus();
                    SafeInvoke(delegate { _content.AdvStatus = st; });
                }, T("Microsoft Store reinstall triggered (finishes in 10-30s).", "Microsoft Store geri yükleme başlatıldı (10-30 sn içinde tamamlanır).", "已触发微软商店重装（10-30 秒内在后台完成）。"));
            }
            else if (key == "adv_reg_scan")
            {
                if (_cfg.DemoMode || _isBusy) return;
                RunAsyncTask(T("Scanning safe HKCU Registry keys (read-only)...", "Güvenli HKCU Kayıt Defteri anahtarları taranıyor...", "正在只读扫描安全 HKCU 注册表项..."), false, delegate
                {
                    RegistryScanResult scan = AdvancedEngine.ScanSafeRegistry();
                    AppendLog(string.Format("[Registry] Scan complete: {0} entries (MuiCache={1}, AppCompat={2}, AppSwitched={3}, MRU={4}).",
                        scan.TotalCount, scan.MuiCacheCount, scan.AppCompatCount, scan.AppSwitchedCount, scan.MruCount));
                    SafeInvoke(delegate { _content.RegScan = scan; });
                }, T("Safe Registry scan complete — review counts before cleaning.", "Kayıt Defteri taraması tamamlandı — temizlemeden önce sayıları inceleyin.", "安全注册表扫描完成 — 请核对数量后点击「备份并清理」。"));
            }
            else if (key == "adv_reg_clean")
            {
                if (_cfg.DemoMode || _isBusy || _content.RegScan == null || _content.RegScan.TotalCount == 0) return;
                int cnt = _content.RegScan.TotalCount;
                if (!ConfirmExpertAction(
                    T("Backup & Clean Registry?", "Kayıt Defteri Yedeklenip Temizlensin mi?", "确认备份并清理注册表？"),
                    string.Format(
                        T("WinForge will FIRST export a standard restorable .reg backup file to %APPDATA%\\WinForge\\backups\\, and THEN clean {0} verified stale HKCU entries.\n\nDouble-click the backup .reg file anytime to undo 100%.\n\nProceed?",
                          "WinForge ÖNCE %APPDATA%\\WinForge\\backups\\ klasörüne standart bir .reg yedek dosyası kaydedecek, ARDINDAN {0} doğrulanmış HKCU kalıntısını temizleyecek.\n\nYedek .reg dosyasına çift tıklayarak her şeyi %100 geri alabilirsiniz.\n\nDevam edilsin mi?",
                          "WinForge 将先导出标准可还原的 .reg 备份文件至 %APPDATA%\\WinForge\\backups\\，验证备份成功后再清理 {0} 项失效残留。\n\n随时双击备份 .reg 文件即可 100% 还原。\n\n是否继续？"),
                        cnt)))
                    return;

                RegistryScanResult targetScan = _content.RegScan;
                RunAsyncTask(T("Exporting .reg backup and cleaning safe HKCU entries...", ".reg yedeği alınıyor ve güvenli HKCU kayıtları temizleniyor...", "正在导出 .reg 备份并清理安全注册表残留..."), true, delegate
                {
                    string backupPath = System.IO.Path.Combine(
                        AdvancedEngine.GetBackupDirectory(),
                        "reg-cleanup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".reg");
                    int removed = AdvancedEngine.CleanScannedRegistry(Microsoft.Win32.Registry.CurrentUser, "HKEY_CURRENT_USER", targetScan, backupPath, AppendLog);
                    RegistryScanResult after = AdvancedEngine.ScanSafeRegistry();
                    after.LastBackupPath = targetScan.LastBackupPath;
                    SafeInvoke(delegate { _content.RegScan = after; });
                    AppendLog("[Registry] Remaining entries after clean: " + after.TotalCount + " (cleaned " + removed + ").");
                }, T("Registry backup saved & safe cleanup complete.", ".reg yedeği alındı ve güvenli temizlik tamamlandı.", "已完成 .reg 备份并安全清理注册表残留。"));
            }
        }

        private bool ConfirmExpertAction(string title, string message)
        {
            return MessageBox.Show(this, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private void ApplyTweaksAsync()
        {
            SetBusy(true, T("Creating backup snapshot and applying tweaks...", "Yedek alınıyor ve ayarlar uygulanıyor...", "正在自动备份并应用所选优化..."), false);
            ThreadPool.QueueUserWorkItem(delegate
            {
                TweakApplyResult res = TweaksEngine.ApplyPendingChanges(_content.Tweaks, _isAdmin, AppendLog);
                SafeInvoke(delegate
                {
                    _isBusy = false;
                    _progressRatio = 1f;
                    _content.Configure(_scale, Lang, _activeTab, _isAdmin, false);
                    _statusIsSuccess = (res.FailedCount == 0);
                    string msg = string.Format(
                        T("Done: {0} applied · {1} reverted · Backup saved.", "Tamamlandı: {0} uygulandı · {1} geri alındı · Yedek kaydedildi.", "完成：已应用 {0} 项 · 已还原 {1} 项 · 已自动备份。"),
                        res.AppliedCount, res.RevertedCount);
                    if (res.RecommendExplorerRestart)
                        msg += T(" (Tip: Click 'Restart Explorer' to refresh shell)", " (İpucu: 'Gezgini Yenile' ile anında yansıtın)", "（提示：点击「重启资源管理器」立即刷新界面）");
                    _statusText = msg;
                    Invalidate();
                });
            });
        }

        private void InstallSelectedAppsAsync()
        {
            List<WingetAppItem> sel = GetSelectedApps();
            if (sel.Count == 0 || _isBusy || _cfg.DemoMode) return;
            SetBusy(true, T("Installing selected apps via WinGet...", "Seçilen uygulamalar WinGet ile kuruluyor...", "正在通过 WinGet 批量安装所选应用..."), true);

            ThreadPool.QueueUserWorkItem(delegate
            {
                int okCnt, failCnt;
                WingetEngine.InstallSelected(
                    sel,
                    AppendLog,
                    delegate(int done, int total, string name)
                    {
                        SafeInvoke(delegate
                        {
                            _progressRatio = (float)done / Math.Max(1, total);
                            if (!string.IsNullOrEmpty(name))
                                _statusText = string.Format(T("Installing [{0}/{1}]: {2}...", "Kuruluyor [{0}/{1}]: {2}...", "正在安装 [{0}/{1}]: {2}..."), done + 1, total, name);
                            Invalidate();
                        });
                    },
                    delegate { return _cancelFlag || IsDisposed; },
                    out okCnt, out failCnt);

                SafeInvoke(delegate
                {
                    _isBusy = false; _progressRatio = 1f;
                    _content.Configure(_scale, Lang, _activeTab, _isAdmin, false);
                    _statusIsSuccess = (failCnt == 0);
                    _statusText = string.Format(
                        T("WinGet finished: {0} installed/up-to-date · {1} warnings (see Console).",
                          "WinGet tamamlandı: {0} kuruldu/güncel · {1} uyarı (Konsola bakın).",
                          "安装完成：{0} 个成功/已最新 · {1} 个提示（详见控制台日志）。"),
                        okCnt, failCnt);
                    Invalidate();
                });
            });
        }

        private void RunAsyncTask(string startMsg, bool openConsole, Action work, string doneMsg)
        {
            SetBusy(true, startMsg, openConsole);
            _progressRatio = 0.25f;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { work(); } catch (Exception ex) { AppendLog("[ERROR] " + ex.Message); }
                SafeInvoke(delegate
                {
                    _isBusy = false; _progressRatio = 1f;
                    _content.Configure(_scale, Lang, _activeTab, _isAdmin, false);
                    _statusText = doneMsg;
                    _statusIsSuccess = true;
                    Invalidate();
                });
            });
        }

        private List<WingetAppItem> GetSelectedApps()
        {
            List<WingetAppItem> r = new List<WingetAppItem>();
            foreach (WingetAppItem a in _content.Apps) if (a.Selected) r.Add(a);
            return r;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            string found = "";
            for (int i = _chromeZones.Count - 1; i >= 0; i--)
            {
                if (_chromeZones[i].Rect.Contains(e.Location)) { found = _chromeZones[i].ActionKey; break; }
            }
            if (found != _hoverKey)
            {
                _hoverKey = found;
                Cursor = !string.IsNullOrEmpty(found) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!string.IsNullOrEmpty(_hoverKey)) { _hoverKey = ""; Cursor = Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            for (int i = _chromeZones.Count - 1; i >= 0; i--)
            {
                if (_chromeZones[i].Rect.Contains(e.Location))
                {
                    HandleChromeClick(_chromeZones[i].ActionKey);
                    break;
                }
            }
        }

        private void HandleChromeClick(string key)
        {
            if (key == "lang_tr") SetLanguage("tr");
            else if (key == "lang_en") SetLanguage("en");
            else if (key == "lang_zh") SetLanguage("zh");
            else if (key == "tab_0") SwitchTab(0);
            else if (key == "tab_1") SwitchTab(1);
            else if (key == "tab_2") SwitchTab(2);
            else if (key == "tab_3") SwitchTab(3);
            else if (key == "tab_4") SwitchTab(4);
            else if (key == "tab_5") SwitchTab(5);
            else if (key == "cfg_export") ExportConfigProfile();
            else if (key == "cfg_import") ImportConfigProfile();
            else if (key == "elevate") { if (!_isAdmin && !_cfg.DemoMode && WinNative.RestartAsAdministrator("--tab " + _activeTab)) Close(); }
            else if (key == "toggle_console") { _consoleOpen = !_consoleOpen; LayoutAll(); Invalidate(); }
            else if (key == "cancel_task") { _cancelFlag = true; AppendLog("Cancelling current task..."); }
            else if (key == "quick_restore")
            {
                SwitchTab(4);
                if (!_cfg.DemoMode && !_isAdmin)
                {
                    _statusText = T("Creating a System Restore Point requires Administrator rights — click 'Admin ↗' in the top bar.",
                        "Sistem Geri Yükleme Noktası oluşturmak Yönetici izni gerektirir — üstteki 'Yönetici ↗' butonuna tıklayın.",
                        "创建系统还原点需要管理员权限 — 请点击右上角「管理员 ↗」提权后再操作。");
                    _statusIsSuccess = false;
                    Invalidate();
                }
                else if (!_cfg.DemoMode && _isAdmin && !_isBusy && _content.Repairs.Count > 0)
                {
                    OnContentAction("repair_run", _content.Repairs[0]);
                }
            }
            else if (key == "app_install") InstallSelectedAppsAsync();
            else if (key == "app_upgrade_all")
            {
                if (_cfg.DemoMode || _isBusy) return;
                RunAsyncTask(T("Upgrading all installed apps via WinGet...", "Tüm kurulu uygulamalar güncelleniyor...", "正在通过 WinGet 更新所有已安装软件..."), true, delegate
                {
                    WingetEngine.UpgradeAllApps(AppendLog, delegate { return _cancelFlag || IsDisposed; });
                }, T("WinGet upgrade check finished.", "WinGet güncelleme kontrolü tamamlandı.", "WinGet 全部软件更新完成。"));
            }
            else if (key == "app_detect")
            {
                if (_cfg.DemoMode || _isBusy) return;
                RunAsyncTask(T("Detecting installed apps via WinGet...", "Kurulu uygulamalar taranıyor...", "正在扫描本机已安装的软件..."), false, delegate
                {
                    int found = WingetEngine.DetectInstalledApps(_content.Apps, delegate { return _cancelFlag || IsDisposed; });
                    AppendLog("Detected " + found + " installed catalog apps.");
                }, T("Installed app detection complete.", "Kurulu uygulama taraması tamamlandı.", "已安装软件检测完成。"));
            }
            else if (key == "app_clear")
            {
                foreach (WingetAppItem a in _content.Apps) a.Selected = false;
                _content.Invalidate(); Invalidate();
            }
        }

        private void ExportConfigProfile()
        {
            if (_cfg.DemoMode || _isBusy) return;
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = T("Export WinForge Profile (JSON)", "WinForge Profilini Dışa Aktar (JSON)", "导出 WinForge 配置文件 (JSON)");
                dlg.Filter = "WinForge Config (*.json)|*.json";
                dlg.FileName = "winforge-profile.json";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        WinForgeConfigIo.ExportProfileToFile(dlg.FileName, _content.Apps, _content.Tweaks);
                        _statusText = T("Profile exported: ", "Profil dışa aktarıldı: ", "配置已导出至：") + dlg.FileName;
                        _statusIsSuccess = true;
                        AppendLog("[Config] Exported JSON profile -> " + dlg.FileName);
                        Invalidate();
                    }
                    catch (Exception ex) { AppendLog("[Config] Export error: " + ex.Message); }
                }
            }
        }

        private void ImportConfigProfile()
        {
            if (_cfg.DemoMode || _isBusy) return;
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = T("Import WinForge Profile (JSON)", "WinForge Profilini İçe Aktar (JSON)", "导入 WinForge 配置文件 (JSON)");
                dlg.Filter = "WinForge Config (*.json)|*.json|All Files (*.*)|*.*";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        int aCnt, tCnt;
                        if (WinForgeConfigIo.ImportProfileFromFile(dlg.FileName, _content.Apps, _content.Tweaks, _isAdmin, out aCnt, out tCnt))
                        {
                            _statusText = string.Format(
                                T("Profile imported: {0} apps selected · {1} tweaks queued (click Apply to execute).",
                                  "Profil içe aktarıldı: {0} uygulama seçildi · {1} ayar işaretlendi (Uygula ile onaylayın).",
                                  "已导入配置：勾选 {0} 款软件 · 标记 {1} 项优化（核对后点击应用生效）。"),
                                aCnt, tCnt);
                            _statusIsSuccess = true;
                            AppendLog("[Config] Imported JSON profile (" + aCnt + " apps, " + tCnt + " tweaks).");
                            _content.Invalidate(); Invalidate();
                        }
                    }
                    catch (Exception ex) { AppendLog("[Config] Import error: " + ex.Message); }
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!e.Control && !e.Alt && e.KeyCode == Keys.L && !_txtSearch.Focused)
            {
                if (Lang == "tr") SetLanguage("en");
                else if (Lang == "zh") SetLanguage("tr");
                else SetLanguage("zh");
                e.Handled = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(ColBg);
            _chromeZones.Clear();

            DrawTopHeader(g);
            DrawTabStrip(g);
            if (_activeTab == 0) DrawAppsToolbar(g);
            DrawFooterBar(g);
        }

        private void DrawTopHeader(Graphics g)
        {
            float pad = Sf(20f);
            float w = ClientSize.Width;

            // Forge hexagon / bolt emblem
            DrawForgeIcon(g, new RectangleF(pad, Sf(14f), Sf(20f), Sf(22f)));

            DrawStr(g, "WINFORGE", _fBrand, ColTextPrimary, new RectangleF(pad + Sf(27f), Sf(13f), Sf(106f), Sf(24f)), StringAlignment.Near);

            // OS Version badge
            RectangleF osRc = new RectangleF(pad + Sf(134f), Sf(15f), Sf(172f), Sf(22f));
            DrawBox(g, osRc, Sf(6f), ColCard, ColBorder);
            DrawStr(g, _osBadge, _fSmallPill, ColTextSecondary, osRc, StringAlignment.Center);

            // Right: Language 3-way pill + Admin pill
            RectangleF langPill = new RectangleF(w - pad - Sf(96f), Sf(14f), Sf(96f), Sf(23f));
            float wTr = Sf(29f), wEn = Sf(29f), wZh = langPill.Width - wTr - wEn;
            RectangleF rTr = new RectangleF(langPill.X, langPill.Y, wTr, langPill.Height);
            RectangleF rEn = new RectangleF(rTr.Right, langPill.Y, wEn, langPill.Height);
            RectangleF rZh = new RectangleF(rEn.Right, langPill.Y, wZh, langPill.Height);

            bool lHov = _hoverKey == "lang_tr" || _hoverKey == "lang_en" || _hoverKey == "lang_zh";
            DrawBox(g, langPill, Sf(6f), lHov ? ColBtnHover : ColCard, lHov ? Color.FromArgb(72, 85, 104) : ColBorder);
            DrawStr(g, "TR", _fSmallPill, Lang == "tr" ? ColSafe : (_hoverKey == "lang_tr" ? ColTextPrimary : ColTextMuted), rTr, StringAlignment.Center);
            DrawStr(g, "EN", _fSmallPill, Lang == "en" ? ColSafe : (_hoverKey == "lang_en" ? ColTextPrimary : ColTextMuted), rEn, StringAlignment.Center);
            DrawStr(g, "中文", _fLangZh, Lang == "zh" ? ColSafe : (_hoverKey == "lang_zh" ? ColTextPrimary : ColTextMuted), rZh, StringAlignment.Center);
            using (Pen sep = new Pen(ColDivider, 1f))
            {
                g.DrawLine(sep, rTr.Right, langPill.Y + Sf(4f), rTr.Right, langPill.Bottom - Sf(4f));
                g.DrawLine(sep, rEn.Right, langPill.Y + Sf(4f), rEn.Right, langPill.Bottom - Sf(4f));
            }
            _chromeZones.Add(new ClickZone(rTr, "lang_tr", null));
            _chromeZones.Add(new ClickZone(rEn, "lang_en", null));
            _chromeZones.Add(new ClickZone(rZh, "lang_zh", null));

            RectangleF admRc = new RectangleF(langPill.X - Sf(86f), Sf(14f), Sf(78f), Sf(23f));
            bool aHov = !_isAdmin && _hoverKey == "elevate";
            DrawBox(g, admRc, Sf(6f),
                _isAdmin ? Color.FromArgb(18, 46, 36) : (aHov ? ColBtnHover : ColCard),
                _isAdmin ? Color.FromArgb(38, 128, 96) : (aHov ? ColSafe : ColBorder));
            DrawStr(g, _isAdmin ? T("Admin", "Yönetici", "管理员") : T("Admin ↗", "Yönetici ↗", "管理员 ↗"),
                _fSmallPill, _isAdmin ? ColSafe : (aHov ? ColTextPrimary : ColTextSecondary), admRc, StringAlignment.Center);
            if (!_isAdmin) _chromeZones.Add(new ClickZone(admRc, "elevate", null));

            // Compact JSON Profile Export / Import pills
            RectangleF expRc = new RectangleF(admRc.X - Sf(76f), Sf(14f), Sf(68f), Sf(23f));
            bool eHov = !_isBusy && _hoverKey == "cfg_export";
            DrawBox(g, expRc, Sf(6f), eHov ? ColBtnHover : ColCard, eHov ? ColCyan : ColBorder);
            DrawStr(g, T("Export", "Dışa Aktar", "导出"), _fSmallPill, eHov ? ColTextPrimary : ColTextSecondary, expRc, StringAlignment.Center);
            if (!_isBusy) _chromeZones.Add(new ClickZone(expRc, "cfg_export", null));

            RectangleF impRc = new RectangleF(expRc.X - Sf(74f), Sf(14f), Sf(66f), Sf(23f));
            bool iHov = !_isBusy && _hoverKey == "cfg_import";
            DrawBox(g, impRc, Sf(6f), iHov ? ColBtnHover : ColCard, iHov ? ColCyan : ColBorder);
            DrawStr(g, T("Import", "İçe Aktar", "导入"), _fSmallPill, iHov ? ColTextPrimary : ColTextSecondary, impRc, StringAlignment.Center);
            if (!_isBusy) _chromeZones.Add(new ClickZone(impRc, "cfg_import", null));
        }

        private void DrawTabStrip(Graphics g)
        {
            float pad = Sf(20f);
            float y = Sf(50f);
            float h = Sf(36f);
            float w = ClientSize.Width;

            float rpW = Sf(148f);
            float trackW = Math.Min(Sf(756f), w - pad * 2f - rpW - Sf(12f));
            RectangleF track = new RectangleF(pad, y, trackW, h);
            DrawBox(g, track, Sf(9f), Color.FromArgb(16, 21, 28), ColDivider);

            string[] labels = new string[]
            {
                T("Apps (60)", "Uygulamalar", "软件安装"),
                T("Tweaks (40)", "Ayarlar (40)", "系统优化 (40)"),
                T("Debloat", "Bloatware", "内置精简"),
                T("Updates & DNS", "Güncelle/DNS", "更新与网络"),
                T("Features & Fix", "Özellik/Onarım", "功能与修复"),
                T("Advanced", "Gelişmiş", "高级选项")
            };

            float innerPad = Sf(3f);
            float tabW = (track.Width - innerPad * 7f) / 6f;
            for (int i = 0; i < 6; i++)
            {
                RectangleF tr = new RectangleF(track.X + innerPad + i * (tabW + innerPad), track.Y + innerPad, tabW, track.Height - innerPad * 2f);
                string key = "tab_" + i;
                bool active = (_activeTab == i);
                bool hov = (_hoverKey == key);
                bool isAdv = (i == 5);
                if (active)
                {
                    if (isAdv)
                        DrawBox(g, tr, Sf(7f), Color.FromArgb(48, 38, 18), Color.FromArgb(156, 108, 24));
                    else
                        DrawBox(g, tr, Sf(7f), Color.FromArgb(24, 46, 42), Color.FromArgb(42, 148, 112));
                }
                else if (hov)
                {
                    DrawBox(g, tr, Sf(7f), Color.FromArgb(26, 33, 43), ColDivider);
                }

                Color txtCol = active
                    ? (isAdv ? ColAmber : ColSafe)
                    : (hov ? ColTextPrimary : (isAdv ? Color.FromArgb(196, 164, 96) : ColTextSecondary));
                DrawStr(g, labels[i], _fTab, txtCol, tr, StringAlignment.Center);
                _chromeZones.Add(new ClickZone(tr, key, null));
            }

            // Quick Restore Point button on far right
            RectangleF rpRc = new RectangleF(w - pad - rpW, y + Sf(3f), rpW, Sf(30f));
            if (rpRc.X > track.Right + Sf(10f))
            {
                bool hov = _hoverKey == "quick_restore";
                DrawBox(g, rpRc, Sf(7f), hov ? ColBtnHover : ColCard, hov ? ColCyan : ColBorder);
                DrawStr(g, T("+ Restore Point", "+ Geri Yükleme", "+ 创建还原点"), _fBtn, hov ? ColTextPrimary : ColCyan, rpRc, StringAlignment.Center);
                _chromeZones.Add(new ClickZone(rpRc, "quick_restore", null));
            }
        }

        private void DrawAppsToolbar(Graphics g)
        {
            float pad = Sf(20f);
            float y = Sf(102f);
            float h = Sf(29f);
            float x = pad + Sf(246f);
            float w = ClientSize.Width;

            RectangleF rDet = new RectangleF(x, y, Sf(126f), h);
            DrawChromeBtn(g, rDet, "app_detect", T("Detect Installed", "Kuruluları Bul", "检测已安装"), false, !_isBusy);
            x = rDet.Right + Sf(8f);

            RectangleF rUpg = new RectangleF(x, y, Sf(116f), h);
            DrawChromeBtn(g, rUpg, "app_upgrade_all", T("Upgrade All", "Tümünü Güncelle", "一键全部更新"), false, !_isBusy && _content.WingetOk);
            x = rUpg.Right + Sf(8f);

            List<WingetAppItem> sel = GetSelectedApps();
            RectangleF rClr = new RectangleF(x, y, Sf(76f), h);
            DrawChromeBtn(g, rClr, "app_clear", T("Clear", "Temizle", "清空"), false, !_isBusy && sel.Count > 0);

            RectangleF rInst = new RectangleF(w - pad - Sf(176f), y, Sf(176f), h);
            string instTxt = sel.Count > 0
                ? string.Format(T("Install Selected ({0})", "Seçilenleri Kur ({0})", "安装所选 ({0})"), sel.Count)
                : T("Select Apps to Install", "Uygulama Seçin", "请勾选应用");
            DrawChromeBtn(g, rInst, "app_install", instTxt, sel.Count > 0, !_isBusy && _content.WingetOk && sel.Count > 0);
        }

        private void DrawFooterBar(Graphics g)
        {
            float pad = Sf(20f);
            float w = ClientSize.Width;
            float h = ClientSize.Height;
            float barTop = h - Sf(54f);
            float dividerTop = _consoleOpen ? (h - Sf(186f)) : barTop;

            using (Pen p = new Pen(ColDivider, 1f))
                g.DrawLine(p, 0, dividerTop, w, dividerTop);

            // Right: Console toggle + optional Cancel button
            RectangleF rLog = new RectangleF(w - pad - Sf(106f), barTop + Sf(12f), Sf(106f), Sf(30f));
            DrawChromeBtn(g, rLog, "toggle_console",
                _consoleOpen ? T("Console ▾", "Konsol ▾", "日志 ▾") : T("Console ▴", "Konsol ▴", "日志 ▴"),
                false, true);

            float statusMaxX = rLog.X - Sf(12f);
            if (_isBusy)
            {
                RectangleF rStop = new RectangleF(rLog.X - Sf(82f), barTop + Sf(12f), Sf(74f), Sf(30f));
                DrawChromeBtn(g, rStop, "cancel_task", T("Stop", "Durdur", "停止"), false, true);
                statusMaxX = rStop.X - Sf(12f);

                DrawStr(g, _statusText, _fStatus, ColTextBody, new RectangleF(pad, barTop + Sf(8f), statusMaxX - pad, Sf(18f)), StringAlignment.Near);
                RectangleF track = new RectangleF(pad, barTop + Sf(32f), Math.Min(Sf(320f), statusMaxX - pad), Sf(6f));
                using (GraphicsPath tp = Rounded(track, Sf(3f)))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(30, 38, 48))) g.FillPath(tb, tp);
                float fw = Math.Max(Sf(16f), track.Width * Math.Min(1f, Math.Max(0.12f, _progressRatio)));
                using (GraphicsPath fp = Rounded(new RectangleF(track.X, track.Y, fw, track.Height), Sf(3f)))
                using (SolidBrush fb = new SolidBrush(ColSafe)) g.FillPath(fb, fp);
            }
            else
            {
                DrawStr(g, _statusText, _fStatus, _statusIsSuccess ? ColSafe : ColTextSecondary,
                    new RectangleF(pad, barTop + Sf(10f), Math.Max(Sf(100f), statusMaxX - pad), Sf(34f)), StringAlignment.Near);
            }
        }

        private void DrawChromeBtn(Graphics g, RectangleF r, string key, string text, bool primary, bool enabled)
        {
            bool hov = enabled && _hoverKey == key;
            Color bg, bd, fg;
            if (!enabled) { bg = ColCard; bd = ColDivider; fg = ColTextMuted; }
            else if (primary) { bg = hov ? ColPrimaryHover : ColPrimaryBg; bd = ColSafe; fg = Color.White; }
            else { bg = hov ? ColBtnHover : ColBtnNormal; bd = hov ? Color.FromArgb(74, 88, 108) : ColBtnBd; fg = ColTextBody; }

            DrawBox(g, r, Sf(7f), bg, bd);
            DrawStr(g, text, _fBtn, fg, r, StringAlignment.Center);
            if (enabled) _chromeZones.Add(new ClickZone(r, key, null));
        }

        private static void DrawForgeIcon(Graphics g, RectangleF r)
        {
            PointF[] hex = new PointF[]
            {
                new PointF(r.X + r.Width * 0.50f, r.Y),
                new PointF(r.Right, r.Y + r.Height * 0.25f),
                new PointF(r.Right, r.Y + r.Height * 0.75f),
                new PointF(r.X + r.Width * 0.50f, r.Bottom),
                new PointF(r.X, r.Y + r.Height * 0.75f),
                new PointF(r.X, r.Y + r.Height * 0.25f)
            };
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(40, ColCyan)))
            using (Pen stroke = new Pen(ColCyan, 1.5f)) { stroke.LineJoin = LineJoin.Round; g.FillPolygon(fill, hex); g.DrawPolygon(stroke, hex); }

            PointF[] bolt = new PointF[]
            {
                new PointF(r.X + r.Width * 0.56f, r.Y + r.Height * 0.20f),
                new PointF(r.X + r.Width * 0.26f, r.Y + r.Height * 0.54f),
                new PointF(r.X + r.Width * 0.50f, r.Y + r.Height * 0.54f),
                new PointF(r.X + r.Width * 0.42f, r.Y + r.Height * 0.82f),
                new PointF(r.X + r.Width * 0.74f, r.Y + r.Height * 0.46f),
                new PointF(r.X + r.Width * 0.50f, r.Y + r.Height * 0.46f)
            };
            using (SolidBrush b = new SolidBrush(ColSafe)) g.FillPolygon(b, bolt);
        }

        private static void DrawStr(Graphics g, string text, Font font, Color color, RectangleF r, StringAlignment align)
        {
            using (SolidBrush b = new SolidBrush(color))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = align; sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap; sf.Trimming = StringTrimming.EllipsisCharacter;
                g.DrawString(text, font, b, r, sf);
            }
        }

        private static void DrawBox(Graphics g, RectangleF r, float rad, Color fill, Color border)
        {
            using (GraphicsPath p = Rounded(r, rad))
            using (SolidBrush fb = new SolidBrush(fill))
            using (Pen bp = new Pen(border, 1f)) { g.FillPath(fb, p); g.DrawPath(bp, p); }
        }

        private static GraphicsPath Rounded(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180f, 90f); p.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f); p.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            p.CloseFigure(); return p;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cancelFlag = true;
            if (_appIcon != null) _appIcon.Dispose();
            DisposeFonts();
            base.OnFormClosing(e);
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            WinNative.EnableDpiAwareness();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(LaunchConfig.Parse(args)));
        }
    }
}
