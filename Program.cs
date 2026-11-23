using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("WinForge — Safe Windows Setup, Tweaks & Utility")]
[assembly: AssemblyDescription("Lightweight, zero-dependency, 100% reversible Windows 10/11 utility for WinGet apps, privacy/UI tweaks, Windows Update policies, and official system repairs (EN/TR/zh-CN).")]
[assembly: AssemblyProduct("WinForge")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

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
                    if (int.TryParse(args[++i], out t) && t >= 0 && t <= 3) cfg.InitialTab = t;
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
        public UpdatePolicyState UpdateState = new UpdatePolicyState();
        public List<RepairActionItem> Repairs = new List<RepairActionItem>();
        public List<ControlPanelShortcut> Shortcuts = new List<ControlPanelShortcut>();
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
            else if (_activeTab == 2) _contentHeight = PaintUpdatesTab(g, availW);
            else _contentHeight = PaintRepairTab(g, availW);

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
                AppGroup.Media, AppGroup.Communication, AppGroup.GamingRuntimes
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

            // Sub-toolbar inside Tweaks tab
            float tbH = Sf(30f);
            float bx = 0f;
            RectangleF rPreset = new RectangleF(bx, y, Sf(156f), tbH);
            DrawPillBtn(g, rPreset, T("Recommended Preset", "Önerilen Ayarlar", "一键推荐配置"), IsHovered("tweak_preset", null), false, !_isBusy);
            if (!_isBusy) AddZone(rPreset, "tweak_preset", null);
            bx = rPreset.Right + Sf(8f);

            RectangleF rRevert = new RectangleF(bx, y, Sf(148f), tbH);
            DrawPillBtn(g, rRevert, T("Revert All Defaults", "Varsayılana Dön", "全部恢复默认"), IsHovered("tweak_revert_all", null), false, !_isBusy);
            if (!_isBusy) AddZone(rRevert, "tweak_revert_all", null);
            bx = rRevert.Right + Sf(8f);

            RectangleF rExp = new RectangleF(bx, y, Sf(134f), tbH);
            DrawPillBtn(g, rExp, T("Restart Explorer", "Gezgini Yenile", "重启资源管理器"), IsHovered("restart_explorer", null), false, !_isBusy);
            if (!_isBusy) AddZone(rExp, "restart_explorer", null);

            // Count pending changes
            int pending = 0;
            foreach (TweakDefinition t in Tweaks)
                if (t.DesiredState != t.IsCurrentlyApplied) pending++;

            RectangleF rApply = new RectangleF(availW - Sf(178f), y, Sf(178f), tbH);
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
                if (cat == TweakCategory.UiExplorer) secName = T("UI & FILE EXPLORER (ZERO ADMIN NEEDED)", "ARAYÜZ VE DOSYA GEZGİNİ (YÖNETİCİ GEREKTİRMEZ)", "界面与资源管理器（无需管理员权限）");
                else if (cat == TweakCategory.Privacy) secName = T("PRIVACY & TELEMETRY (100% REVERSIBLE)", "GİZLİLİK VE TELEMETRİ (%100 GERİ ALINABİLİR)", "隐私保护与遥测精简（100% 可还原）");
                else secName = T("PERFORMANCE & POWER", "PERFORMANS VE GÜÇ", "性能与电源优化");

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

        // ==================== TAB 2: WINDOWS UPDATE ====================
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
            DrawText(g, T("CLASSIC WINDOWS CONTROL PANELS (1-CLICK LAUNCH)", "KLASİK WINDOWS YÖNETİM PANELLERİ (TEK TIK)", "经典 Windows 管理面板快捷入口"),
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
        private const int LogicalW = 900;
        private const int LogicalH = 668;

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
        private bool _cancelFlag;
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
            MinimumSize = new Size(S(780), S(560));

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
            _content.Repairs = UpdatesAndRepairEngine.BuildRepairActions();
            _content.Shortcuts = UpdatesAndRepairEngine.BuildLegacyShortcuts();

            if (_cfg.DemoMode)
            {
                _content.WingetOk = true;
                _content.WingetVersion = "v1.30";
                // Mark a few realistic apps installed/selected for clean promo showcase
                foreach (WingetAppItem a in _content.Apps)
                {
                    if (a.Id == "Google.Chrome" || a.Id == "Microsoft.VisualStudioCode" || a.Id == "Git.Git" || a.Id == "7zip.7zip")
                        a.IsInstalled = true;
                    if (a.Id == "Mozilla.Firefox" || a.Id == "OpenJS.NodeJS.LTS" || a.Id == "Notepad++.Notepad++")
                        a.Selected = true;
                }
                foreach (TweakDefinition tw in _content.Tweaks)
                {
                    bool on = tw.Id == "show_file_ext" || tw.Id == "dark_mode" || tw.Id == "classic_context_menu" || tw.Id == "disable_bing_search" || tw.Id == "enable_game_mode" || tw.Id == "disable_ad_id";
                    tw.IsCurrentlyApplied = on;
                    tw.DesiredState = on;
                    if (tw.Id == "explorer_this_pc" || tw.Id == "taskbar_end_task")
                        tw.DesiredState = true;
                }
                _content.UpdateState = new UpdatePolicyState();
                _content.UpdateState.IsSecurityDeferred = true;
                _content.UpdateState.FeatureDeferDays = 365;
                _content.UpdateState.QualityDeferDays = 4;
                _content.UpdateState.ExcludeDrivers = true;
                _content.UpdateState.PreventAutoReboot = true;

                _statusText = T(
                    "Ready · All tweaks are live-inspected and 100% reversible with automatic snapshot backups.",
                    "Hazır · Tüm ayarlar canlı okunur ve otomatik yedekleme ile %100 geri alınabilir.",
                    "就绪 · 所有优化项均实时读取系统状态，修改前自动备份且 100% 可还原。");
            }
            else
            {
                TweaksEngine.RefreshLiveStates(_content.Tweaks);
                _content.UpdateState = UpdatesAndRepairEngine.ReadUpdatePolicy();
                _statusText = T(
                    "Ready · Click any tab to install apps, customize reversible tweaks, manage updates, or run repairs.",
                    "Hazır · Uygulama kurmak, ayarları düzenlemek veya sistem onarmak için sekmeleri kullanın.",
                    "就绪 · 切换上方标签即可批量装机、配置可还原优化、管理更新策略或修复系统。");
            }
        }

        private void StartBackgroundChecks()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                string ver;
                bool ok = WingetEngine.IsWingetAvailable(out ver);
                SafeInvoke(delegate
                {
                    _content.WingetOk = ok;
                    _content.WingetVersion = ver;
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
            _txtSearch.Font = new Font(fam, PtToPx(8.8f), FontStyle.Regular, GraphicsUnit.Pixel);
        }

        private void DisposeFonts()
        {
            if (_fBrand != null) { _fBrand.Dispose(); _fBrand = null; }
            if (_fSmallPill != null) { _fSmallPill.Dispose(); _fSmallPill = null; }
            if (_fLangZh != null) { _fLangZh.Dispose(); _fLangZh = null; }
            if (_fTab != null) { _fTab.Dispose(); _fTab = null; }
            if (_fBtn != null) { _fBtn.Dispose(); _fBtn = null; }
            if (_fStatus != null) { _fStatus.Dispose(); _fStatus = null; }
        }

        private void UpdateSearchPlaceholder()
        {
            if (_txtSearch != null && !_txtSearch.IsDisposed)
                WinNative.SetCueBanner(_txtSearch.Handle, T("Search 36 apps...", "36 uygulama içinde ara...", "搜索 36 款软件..."));
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
                    _content.Invalidate();
                    Invalidate();
                }
            }
            else if (key == "tweak_preset")
            {
                foreach (TweakDefinition tw in _content.Tweaks)
                {
                    if (tw.InRecommendedPreset && (!tw.RequiresAdmin || _isAdmin))
                        tw.DesiredState = true;
                }
                _statusText = T("Recommended safe preset selected — review toggles and click 'Apply Changes' to execute.",
                    "Önerilen güvenli ayarlar işaretlendi — kontrol edip 'Uygula' butonuna tıklayın.",
                    "已勾选推荐安全优化项 — 请核对开关后点击「应用更改」生效。");
                _statusIsSuccess = false;
                _content.Invalidate();
                Invalidate();
            }
            else if (key == "tweak_revert_all")
            {
                foreach (TweakDefinition tw in _content.Tweaks)
                {
                    if (!tw.RequiresAdmin || _isAdmin)
                        tw.DesiredState = false;
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
            else if (key == "elevate") { if (!_isAdmin && !_cfg.DemoMode && WinNative.RestartAsAdministrator("--tab " + _activeTab)) Close(); }
            else if (key == "toggle_console") { _consoleOpen = !_consoleOpen; LayoutAll(); Invalidate(); }
            else if (key == "cancel_task") { _cancelFlag = true; AppendLog("Cancelling current task..."); }
            else if (key == "quick_restore")
            {
                SwitchTab(3);
                if (!_cfg.DemoMode && _isAdmin && !_isBusy && _content.Repairs.Count > 0)
                    OnContentAction("repair_run", _content.Repairs[0]);
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

            RectangleF admRc = new RectangleF(langPill.X - Sf(88f), Sf(14f), Sf(80f), Sf(23f));
            bool aHov = !_isAdmin && _hoverKey == "elevate";
            DrawBox(g, admRc, Sf(6f),
                _isAdmin ? Color.FromArgb(18, 46, 36) : (aHov ? ColBtnHover : ColCard),
                _isAdmin ? Color.FromArgb(38, 128, 96) : (aHov ? ColSafe : ColBorder));
            DrawStr(g, _isAdmin ? T("Admin", "Yönetici", "管理员") : T("Admin ↗", "Yönetici ↗", "管理员 ↗"),
                _fSmallPill, _isAdmin ? ColSafe : (aHov ? ColTextPrimary : ColTextSecondary), admRc, StringAlignment.Center);
            if (!_isAdmin) _chromeZones.Add(new ClickZone(admRc, "elevate", null));
        }

        private void DrawTabStrip(Graphics g)
        {
            float pad = Sf(20f);
            float y = Sf(50f);
            float h = Sf(36f);
            float w = ClientSize.Width;

            RectangleF track = new RectangleF(pad, y, Sf(568f), h);
            DrawBox(g, track, Sf(9f), Color.FromArgb(16, 21, 28), ColDivider);

            string[] labels = new string[]
            {
                T("Apps (WinGet)", "Uygulamalar", "软件安装"),
                T("Tweaks (21)", "Ayarlar (21)", "系统优化 (21)"),
                T("Windows Update", "Güncellemeler", "系统更新"),
                T("Repair & Tools", "Onarım ve Araçlar", "修复与工具")
            };

            float innerPad = Sf(3f);
            float tabW = (track.Width - innerPad * 5f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                RectangleF tr = new RectangleF(track.X + innerPad + i * (tabW + innerPad), track.Y + innerPad, tabW, track.Height - innerPad * 2f);
                string key = "tab_" + i;
                bool active = (_activeTab == i);
                bool hov = (_hoverKey == key);
                if (active)
                    DrawBox(g, tr, Sf(7f), Color.FromArgb(24, 46, 42), Color.FromArgb(42, 148, 112));
                else if (hov)
                    DrawBox(g, tr, Sf(7f), Color.FromArgb(26, 33, 43), ColDivider);

                DrawStr(g, labels[i], _fTab, active ? ColSafe : (hov ? ColTextPrimary : ColTextSecondary), tr, StringAlignment.Center);
                _chromeZones.Add(new ClickZone(tr, key, null));
            }

            // Quick Restore Point button on far right
            RectangleF rpRc = new RectangleF(w - pad - Sf(158f), y + Sf(3f), Sf(158f), Sf(30f));
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
