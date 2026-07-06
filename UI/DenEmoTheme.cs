using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DenEmo.UI
{
    /// <summary>
    /// dennokoworks フローティングデザインシステムのテーマ定義。
    /// すべての GUIStyle を new GUIStyle() から構築し（EditorStyles 非継承）、
    /// 全 state の色を固定することで Unity のライト/ダークテーマに依存しない外観を保証する。
    /// OnGUI 先頭で Initialize() → PushEditorTheme()、finally で PopEditorTheme() を呼ぶこと。
    /// </summary>
    internal static class DenEmoTheme
    {
        // ─── Colors ──────────────────────────────────────────────────────────

        public static readonly Color Surface0 = Hex(0x121212);
        public static readonly Color Surface1 = Hex(0x1e1e1e);
        public static readonly Color Surface2 = Hex(0x2c2c2c);
        public static readonly Color Outline  = Hex(0x3a3a3a);

        public static readonly Color TextPrimary   = Hex(0xffffff);
        public static readonly Color TextSecondary = Hex(0xcccccc);
        public static readonly Color TextTertiary  = Hex(0xaaaaaa);
        public static readonly Color TextDisabled  = Hex(0x555555);

        public static readonly Color SemanticError   = Hex(0x9b1b30);
        public static readonly Color SemanticWarning = Hex(0xffb74d);
        public static readonly Color SemanticSuccess = Hex(0x4caf50);
        public static readonly Color SemanticInfo    = Hex(0x64b5f6);

        public static readonly Color Accent         = Color.white;
        public static readonly Color HoverOverlay   = new Color(1f, 1f, 1f, 0.05f);
        public static readonly Color FavoriteActive = new Color(1f, 0.85f, 0.1f, 1f);
        public static readonly Color RecordingRed   = new Color(0.95f, 0.25f, 0.25f, 1f);

        // ─── Cached Textures ─────────────────────────────────────────────────

        private static Texture2D _texSurface0;
        private static Texture2D _texSurface1;
        private static Texture2D _texSurface2;
        private static Texture2D _texCard;
        private static Texture2D _texAccentCard;
        private static Texture2D _texRowHover;
        private static Texture2D _texSearchField;

        // 生成したすべてのテクスチャを追跡してドメインリロード前に確実に破棄する
        private static readonly List<Texture2D> _allTextures = new List<Texture2D>();

        // ─── Styles ──────────────────────────────────────────────────────────

        private static bool _initialized;
        private static bool _lastIsProSkin;

        public static GUIStyle CardStyle        { get; private set; }
        public static GUIStyle CardOuterStyle   { get; private set; }
        public static GUIStyle ToolbarStyle     { get; private set; }
        public static GUIStyle RowStyle         { get; private set; }
        public static GUIStyle RowHoverStyle    { get; private set; }
        public static GUIStyle SearchTextFieldStyle  { get; private set; }

        public static GUIStyle TitleStyle            { get; private set; }
        public static GUIStyle SectionHeaderStyle    { get; private set; }
        public static GUIStyle ToggleSectionOnStyle  { get; private set; }
        public static GUIStyle ToggleSectionOffStyle { get; private set; }
        public static GUIStyle SecondaryTextStyle    { get; private set; }
        public static GUIStyle CaptionStyle          { get; private set; }
        public static GUIStyle GroupLabelStyle       { get; private set; }

        // タイムライン用の小型ラベル（毎フレーム new GUIStyle しないためテーマ側で保持）
        public static GUIStyle SmallLabelStyle       { get; private set; } // 9px 目盛りラベル
        public static GUIStyle TinyLabelStyle        { get; private set; } // 8px 秒ラベル
        public static GUIStyle HoverTipStyle         { get; private set; } // ホバーツールチップ（白文字）
        public static GUIStyle KeyDiamondStyle       { get; private set; } // ◆（非カレント）
        public static GUIStyle KeyDiamondActiveStyle { get; private set; } // ◆（カレントフレーム上）
        public static GUIStyle CenterCaptionStyle    { get; private set; } // 中央寄せキャプション（＝ ハンドル等）

        public static GUIStyle ActionButtonStyle    { get; private set; }
        public static GUIStyle SecondaryButtonStyle { get; private set; }
        public static GUIStyle MiniButtonStyle      { get; private set; }
        public static GUIStyle ChipOnStyle          { get; private set; }
        public static GUIStyle ChipOffStyle         { get; private set; }
        public static GUIStyle FavOnStyle           { get; private set; }
        public static GUIStyle FavOffStyle          { get; private set; }

        public static GUIStyle StatusInfoStyle    { get; private set; }
        public static GUIStyle StatusSuccessStyle { get; private set; }
        public static GUIStyle StatusWarningStyle { get; private set; }
        public static GUIStyle StatusErrorStyle   { get; private set; }

        // ─────────────────────────────────────────────────────────────────────

        public static void Initialize()
        {
            bool currentProSkin = EditorGUIUtility.isProSkin;
            if (_initialized && _lastIsProSkin != currentProSkin)
            {
                DisposeTextures();
            }
            _lastIsProSkin = currentProSkin;

            // テクスチャはドメインリロードで破棄されるので毎回チェック
            EnsureTextures();
            if (_initialized) return;
            _initialized = true;
            BuildStyles();
        }

        private static void EnsureTextures()
        {
            if (!_texSurface0)   _texSurface0   = MakeTex(Surface0);
            if (!_texSurface1)   _texSurface1   = MakeTex(Surface1);
            if (!_texSurface2)   _texSurface2   = MakeTex(Surface2);
            if (!_texCard)       _texCard       = MakeBorderedTex(Surface1, Outline);
            if (!_texAccentCard) _texAccentCard = MakeBorderedTex(Surface2, Outline);
            if (!_texRowHover)   _texRowHover   = MakeTex(Surface2);
            if (!_texSearchField) _texSearchField = MakeBorderedTex(Surface2, Hex(0x5a5a5a));

            // テクスチャが再生成された場合はスタイルも再構築
            if (_initialized && (CardStyle == null || CardStyle.normal.background == null))
            {
                _initialized = false;
            }
        }

        private static void BuildStyles()
        {
            // ── Containers ──────────────────────────────────────────────────

            CardStyle = new GUIStyle();
            CardStyle.normal.background = _texCard;
            CardStyle.border  = new RectOffset(1, 1, 1, 1);
            CardStyle.padding = new RectOffset(10, 10, 8, 8);
            CardStyle.margin  = new RectOffset(6, 6, 10, 10);

            CardOuterStyle = new GUIStyle();
            CardOuterStyle.normal.background = _texCard;
            CardOuterStyle.border  = new RectOffset(1, 1, 1, 1);
            CardOuterStyle.padding = new RectOffset(0, 0, 0, 0);
            CardOuterStyle.margin  = new RectOffset(6, 6, 4, 4);

            ToolbarStyle = new GUIStyle();
            ToolbarStyle.normal.background = _texSurface2;
            ToolbarStyle.padding = new RectOffset(8, 8, 5, 5);
            ToolbarStyle.margin  = new RectOffset(0, 0, 0, 0);

            RowStyle = new GUIStyle();
            RowStyle.normal.background = _texSurface1;
            RowStyle.padding = new RectOffset(4, 4, 2, 2);
            RowStyle.margin  = new RectOffset(0, 0, 0, 0);

            RowHoverStyle = new GUIStyle();
            RowHoverStyle.normal.background = _texRowHover;
            RowHoverStyle.padding = new RectOffset(4, 4, 2, 2);
            RowHoverStyle.margin  = new RectOffset(0, 0, 0, 0);

            // ── Typography ───────────────────────────────────────────────────
            // すべて new GUIStyle() から構築し、FixAllTextColors で全 state を固定する。

            TitleStyle = new GUIStyle();
            TitleStyle.fontStyle = FontStyle.Bold;
            TitleStyle.fontSize  = 14;
            TitleStyle.alignment = TextAnchor.MiddleLeft;
            FixAllTextColors(TitleStyle, TextPrimary);

            SectionHeaderStyle = new GUIStyle();
            SectionHeaderStyle.fontStyle = FontStyle.Bold;
            SectionHeaderStyle.fontSize  = 10;
            SectionHeaderStyle.margin    = new RectOffset(0, 0, 0, 2);
            FixAllTextColors(SectionHeaderStyle, TextTertiary);

            ToggleSectionOnStyle = new GUIStyle();
            ToggleSectionOnStyle.fontStyle = FontStyle.Bold;
            ToggleSectionOnStyle.fontSize  = 10;
            ToggleSectionOnStyle.margin    = new RectOffset(0, 0, 0, 2);
            FixAllTextColors(ToggleSectionOnStyle, TextPrimary);

            ToggleSectionOffStyle = new GUIStyle();
            ToggleSectionOffStyle.fontStyle = FontStyle.Bold;
            ToggleSectionOffStyle.fontSize  = 10;
            ToggleSectionOffStyle.margin    = new RectOffset(0, 0, 0, 2);
            FixAllTextColors(ToggleSectionOffStyle, TextTertiary);

            SecondaryTextStyle = new GUIStyle();
            SecondaryTextStyle.fontSize  = 11;
            SecondaryTextStyle.wordWrap  = true;
            SecondaryTextStyle.alignment = TextAnchor.MiddleLeft;
            SecondaryTextStyle.padding   = new RectOffset(2, 2, 1, 2);
            FixAllTextColors(SecondaryTextStyle, TextSecondary);

            CaptionStyle = new GUIStyle();
            CaptionStyle.fontSize  = 10;
            CaptionStyle.alignment = TextAnchor.MiddleLeft;
            CaptionStyle.padding   = new RectOffset(2, 2, 1, 2);
            FixAllTextColors(CaptionStyle, TextTertiary);

            GroupLabelStyle = new GUIStyle();
            GroupLabelStyle.fontStyle = FontStyle.Bold;
            GroupLabelStyle.fontSize  = 11;
            GroupLabelStyle.alignment = TextAnchor.MiddleLeft;
            FixAllTextColors(GroupLabelStyle, TextSecondary);

            SmallLabelStyle = new GUIStyle();
            SmallLabelStyle.fontSize  = 9;
            SmallLabelStyle.alignment = TextAnchor.UpperLeft;
            FixAllTextColors(SmallLabelStyle, TextTertiary);

            TinyLabelStyle = new GUIStyle();
            TinyLabelStyle.fontSize  = 8;
            TinyLabelStyle.alignment = TextAnchor.UpperLeft;
            FixAllTextColors(TinyLabelStyle, TextTertiary);

            HoverTipStyle = new GUIStyle();
            HoverTipStyle.fontSize  = 9;
            HoverTipStyle.padding   = new RectOffset(2, 2, 1, 1);
            HoverTipStyle.alignment = TextAnchor.MiddleLeft;
            FixAllTextColors(HoverTipStyle, Color.white);

            KeyDiamondStyle = new GUIStyle();
            KeyDiamondStyle.fontSize  = 10;
            KeyDiamondStyle.alignment = TextAnchor.MiddleCenter;
            FixAllTextColors(KeyDiamondStyle, SemanticInfo);

            KeyDiamondActiveStyle = new GUIStyle();
            KeyDiamondActiveStyle.fontSize  = 10;
            KeyDiamondActiveStyle.alignment = TextAnchor.MiddleCenter;
            FixAllTextColors(KeyDiamondActiveStyle, Color.white);

            CenterCaptionStyle = new GUIStyle();
            CenterCaptionStyle.fontSize  = 10;
            CenterCaptionStyle.alignment = TextAnchor.MiddleCenter;
            FixAllTextColors(CenterCaptionStyle, TextSecondary);

            // ── Buttons ──────────────────────────────────────────────────────
            // GUI.skin.button / EditorStyles.miniButton を継承すると角丸・グラデーション・
            // scaledBackgrounds が混入するため、new GUIStyle() から全プロパティを明示構築する。

            ActionButtonStyle = new GUIStyle();
            ActionButtonStyle.normal.background  = _texAccentCard;
            ActionButtonStyle.hover.background   = MakeTex(Color.Lerp(Surface2, Color.white, 0.07f));
            ActionButtonStyle.active.background  = MakeTex(Color.Lerp(Surface2, Color.white, 0.15f));
            ActionButtonStyle.border       = new RectOffset(1, 1, 1, 1);
            ActionButtonStyle.margin       = new RectOffset(4, 4, 2, 2);
            ActionButtonStyle.padding      = new RectOffset(6, 6, 3, 3);
            ActionButtonStyle.fontSize     = 12;
            ActionButtonStyle.fontStyle    = FontStyle.Bold;
            ActionButtonStyle.fixedHeight  = 32;
            ActionButtonStyle.alignment    = TextAnchor.MiddleCenter;
            ActionButtonStyle.stretchWidth = true;
            FixAllTextColors(ActionButtonStyle, TextPrimary);

            SecondaryButtonStyle = new GUIStyle();
            SecondaryButtonStyle.normal.background = MakeBorderedTex(Surface1, Outline);
            SecondaryButtonStyle.hover.background  = _texAccentCard;
            SecondaryButtonStyle.active.background = MakeTex(Color.Lerp(Surface1, Color.white, 0.10f));
            SecondaryButtonStyle.border       = new RectOffset(1, 1, 1, 1);
            SecondaryButtonStyle.margin       = new RectOffset(4, 4, 2, 2);
            SecondaryButtonStyle.padding      = new RectOffset(6, 6, 3, 3);
            SecondaryButtonStyle.fontSize     = 11;
            SecondaryButtonStyle.fixedHeight  = 26;
            SecondaryButtonStyle.alignment    = TextAnchor.MiddleCenter;
            SecondaryButtonStyle.stretchWidth = true;
            SecondaryButtonStyle.normal.textColor    = TextSecondary;
            SecondaryButtonStyle.hover.textColor     = TextPrimary;
            SecondaryButtonStyle.active.textColor    = TextPrimary;
            SecondaryButtonStyle.focused.textColor   = TextSecondary;
            SecondaryButtonStyle.onNormal.textColor  = TextSecondary;
            SecondaryButtonStyle.onHover.textColor   = TextPrimary;
            SecondaryButtonStyle.onActive.textColor  = TextPrimary;
            SecondaryButtonStyle.onFocused.textColor = TextSecondary;

            // fixedHeight は 0 のまま（呼び出し側が GUILayout.Height で制御する）
            MiniButtonStyle = new GUIStyle();
            MiniButtonStyle.normal.background = _texAccentCard;
            MiniButtonStyle.hover.background  = MakeTex(Color.Lerp(Surface2, Color.white, 0.10f));
            MiniButtonStyle.active.background = MakeTex(Color.Lerp(Surface2, Color.white, 0.18f));
            MiniButtonStyle.border    = new RectOffset(1, 1, 1, 1);
            MiniButtonStyle.margin    = new RectOffset(2, 2, 1, 1);
            MiniButtonStyle.padding   = new RectOffset(4, 4, 2, 3);
            MiniButtonStyle.fontSize  = 10;
            MiniButtonStyle.alignment = TextAnchor.MiddleCenter;
            MiniButtonStyle.normal.textColor    = TextTertiary;
            MiniButtonStyle.hover.textColor     = TextSecondary;
            MiniButtonStyle.active.textColor    = TextPrimary;
            MiniButtonStyle.focused.textColor   = TextTertiary;
            MiniButtonStyle.onNormal.textColor  = TextPrimary;
            MiniButtonStyle.onHover.textColor   = TextPrimary;
            MiniButtonStyle.onActive.textColor  = TextPrimary;
            MiniButtonStyle.onFocused.textColor = TextPrimary;

            // フィルターチップ（トグルボタン）
            ChipOnStyle = new GUIStyle();
            ChipOnStyle.normal.background  = MakeBorderedTex(Surface2, Accent);
            ChipOnStyle.hover.background   = MakeBorderedTex(Color.Lerp(Surface2, Accent, 0.1f), Accent);
            ChipOnStyle.active.background  = MakeBorderedTex(Color.Lerp(Surface2, Accent, 0.2f), Accent);
            ChipOnStyle.border      = new RectOffset(1, 1, 1, 1);
            ChipOnStyle.fontSize    = 10;
            ChipOnStyle.fixedHeight = 20;
            ChipOnStyle.padding     = new RectOffset(6, 6, 2, 2);
            ChipOnStyle.margin      = new RectOffset(2, 2, 1, 1);
            ChipOnStyle.alignment   = TextAnchor.MiddleCenter;
            FixAllTextColors(ChipOnStyle, TextPrimary);

            ChipOffStyle = new GUIStyle();
            ChipOffStyle.normal.background = MakeBorderedTex(Surface1, Outline);
            ChipOffStyle.hover.background  = MakeBorderedTex(Surface2, Outline);
            ChipOffStyle.active.background = MakeTex(Color.Lerp(Surface1, Color.white, 0.10f));
            ChipOffStyle.border      = new RectOffset(1, 1, 1, 1);
            ChipOffStyle.fontSize    = 10;
            ChipOffStyle.fixedHeight = 20;
            ChipOffStyle.padding     = new RectOffset(6, 6, 2, 2);
            ChipOffStyle.margin      = new RectOffset(2, 2, 1, 1);
            ChipOffStyle.alignment   = TextAnchor.MiddleCenter;
            ChipOffStyle.normal.textColor    = TextTertiary;
            ChipOffStyle.hover.textColor     = TextSecondary;
            ChipOffStyle.active.textColor    = TextPrimary;
            ChipOffStyle.focused.textColor   = TextTertiary;
            ChipOffStyle.onNormal.textColor  = TextSecondary;
            ChipOffStyle.onHover.textColor   = TextSecondary;
            ChipOffStyle.onActive.textColor  = TextPrimary;
            ChipOffStyle.onFocused.textColor = TextTertiary;

            // お気に入り星ボタン（背景なし・文字色のみ）
            FavOnStyle = new GUIStyle();
            FavOnStyle.fontSize  = 13;
            FavOnStyle.alignment = TextAnchor.MiddleCenter;
            FavOnStyle.padding   = new RectOffset(0, 0, 0, 0);
            FixAllTextColors(FavOnStyle, FavoriteActive);
            FavOnStyle.hover.textColor = new Color(1f, 0.95f, 0.4f, 1f);

            FavOffStyle = new GUIStyle();
            FavOffStyle.fontSize  = 13;
            FavOffStyle.alignment = TextAnchor.MiddleCenter;
            FavOffStyle.padding   = new RectOffset(0, 0, 0, 0);
            FixAllTextColors(FavOffStyle, TextDisabled);
            FavOffStyle.hover.textColor = new Color(0.7f, 0.7f, 0.7f, 1f);

            // ── Status Bar ───────────────────────────────────────────────────

            var statusBase = new GUIStyle();
            statusBase.border    = new RectOffset(1, 1, 1, 1);
            statusBase.padding   = new RectOffset(10, 10, 5, 5);
            statusBase.margin    = new RectOffset(0, 0, 0, 0);
            statusBase.fontSize  = 11;
            statusBase.wordWrap  = true;
            statusBase.alignment = TextAnchor.MiddleLeft;

            StatusInfoStyle = new GUIStyle(statusBase);
            StatusInfoStyle.normal.background = _texSurface1;
            FixAllTextColors(StatusInfoStyle, TextSecondary);

            StatusSuccessStyle = new GUIStyle(statusBase);
            StatusSuccessStyle.normal.background = MakeTex(Color.Lerp(Surface1, SemanticSuccess, 0.25f));
            FixAllTextColors(StatusSuccessStyle, SemanticSuccess);

            StatusWarningStyle = new GUIStyle(statusBase);
            StatusWarningStyle.normal.background = MakeTex(Color.Lerp(Surface1, SemanticWarning, 0.20f));
            FixAllTextColors(StatusWarningStyle, SemanticWarning);

            StatusErrorStyle = new GUIStyle(statusBase);
            StatusErrorStyle.normal.background = MakeTex(Color.Lerp(Surface1, SemanticError, 0.50f));
            FixAllTextColors(StatusErrorStyle, new Color(1f, 0.65f, 0.65f));

            // ─── Search Text Field Style ──────────────────────────────────────
            SearchTextFieldStyle = new GUIStyle(EditorStyles.textField);
            FixAllStateBackgrounds(SearchTextFieldStyle, _texSearchField);
            SearchTextFieldStyle.border = new RectOffset(1, 1, 1, 1);
            SearchTextFieldStyle.padding = new RectOffset(6, 6, 3, 3);
            FixAllTextColors(SearchTextFieldStyle, TextPrimary);
        }

        public static GUIStyle GetStatusStyle(int level)
        {
            return level switch
            {
                1 => StatusSuccessStyle,
                2 => StatusWarningStyle,
                3 => StatusErrorStyle,
                _ => StatusInfoStyle,
            };
        }

        // ─── Editor Style Override（ライト/ダーク両対応） ─────────────────────
        // EditorGUILayout.Toggle / ObjectField / Slider 等の組み込みコントロールは
        // EditorStyles を直接参照するため、OnGUI スコープ内でのみ一時上書きする。

        private static bool _overrideActive;
        public static bool IsOverrideActive => _overrideActive;

        private static Color _backupCursorColor;
        private static Color _backupSelectionColor;
        private static bool _settingsBackupActive;

        private class GUIStyleBackup
        {
            private readonly GUIStyle _style;
            private readonly Color _normal, _hover, _active, _focused;
            private readonly Color _onNormal, _onHover, _onActive, _onFocused;
            private readonly Texture2D _normalBg, _hoverBg, _activeBg, _focusedBg;
            private readonly Texture2D _onNormalBg, _onHoverBg, _onActiveBg, _onFocusedBg;
            private readonly RectOffset _border;
            private readonly RectOffset _padding;

            public GUIStyleBackup(GUIStyle style)
            {
                _style     = style;
                _normal    = style.normal.textColor;
                _hover     = style.hover.textColor;
                _active    = style.active.textColor;
                _focused   = style.focused.textColor;
                _onNormal  = style.onNormal.textColor;
                _onHover   = style.onHover.textColor;
                _onActive  = style.onActive.textColor;
                _onFocused = style.onFocused.textColor;

                _normalBg    = style.normal.background;
                _hoverBg     = style.hover.background;
                _activeBg    = style.active.background;
                _focusedBg   = style.focused.background;
                _onNormalBg  = style.onNormal.background;
                _onHoverBg   = style.onHover.background;
                _onActiveBg  = style.onActive.background;
                _onFocusedBg = style.onFocused.background;

                _border  = new RectOffset(style.border.left,  style.border.right,  style.border.top,  style.border.bottom);
                _padding = new RectOffset(style.padding.left, style.padding.right, style.padding.top, style.padding.bottom);
            }

            public void Restore()
            {
                _style.normal.textColor    = _normal;
                _style.hover.textColor     = _hover;
                _style.active.textColor    = _active;
                _style.focused.textColor   = _focused;
                _style.onNormal.textColor  = _onNormal;
                _style.onHover.textColor   = _onHover;
                _style.onActive.textColor  = _onActive;
                _style.onFocused.textColor = _onFocused;

                _style.normal.background    = _normalBg;
                _style.hover.background     = _hoverBg;
                _style.active.background    = _activeBg;
                _style.focused.background   = _focusedBg;
                _style.onNormal.background  = _onNormalBg;
                _style.onHover.background   = _onHoverBg;
                _style.onActive.background  = _onActiveBg;
                _style.onFocused.background = _onFocusedBg;

                _style.border  = _border;
                _style.padding = _padding;
            }
        }

        private static GUIStyleBackup[] _backups;

        /// <summary>
        /// OnGUI 先頭（Initialize の直後）で呼ぶ。EditorStyles をテーマ色に一時上書きする。
        /// 必ず finally ブロックで PopEditorTheme() を呼ぶこと。
        /// </summary>
        public static void PushEditorTheme()
        {
            if (_overrideActive) return; // 入れ子（PopupWindow 等）の二重 Push を防ぐ
            _overrideActive = true;

            // バックアップはドメインリロードで消えるため都度確認
            if (_backups == null)
            {
                _backups = new[]
                {
                    new GUIStyleBackup(EditorStyles.label),
                    new GUIStyleBackup(EditorStyles.objectField),
                    new GUIStyleBackup(EditorStyles.numberField),
                    new GUIStyleBackup(EditorStyles.textField),
                    new GUIStyleBackup(EditorStyles.popup),
                    new GUIStyleBackup(EditorStyles.toggle),
                    new GUIStyleBackup(GUI.skin.textField),
                    new GUIStyleBackup(GUI.skin.label),
                };
            }

            if (!_settingsBackupActive)
            {
                _backupCursorColor = GUI.skin.settings.cursorColor;
                _backupSelectionColor = GUI.skin.settings.selectionColor;
                _settingsBackupActive = true;
            }

            FixAllTextColors(EditorStyles.label,       TextSecondary);
            FixAllTextColors(EditorStyles.objectField, TextSecondary);
            FixAllTextColors(EditorStyles.numberField, TextSecondary);
            FixAllTextColors(EditorStyles.textField,   TextSecondary);
            FixAllTextColors(EditorStyles.popup,       TextSecondary);
            FixAllTextColors(EditorStyles.toggle,      TextSecondary);
            FixAllTextColors(GUI.skin.textField,       TextSecondary);
            FixAllTextColors(GUI.skin.label,           TextSecondary);

            // 入力欄の背景を全 state でダーク色＋ボーダーに固定（ホバー・フォーカス時の白背景リーク防止）
            FixAllStateBackgrounds(EditorStyles.objectField, _texSearchField);
            EditorStyles.objectField.border = new RectOffset(1, 1, 1, 1);

            FixAllStateBackgrounds(EditorStyles.numberField, _texSearchField);
            EditorStyles.numberField.border = new RectOffset(1, 1, 1, 1);

            FixAllStateBackgrounds(EditorStyles.textField,   _texSearchField);
            EditorStyles.textField.border = new RectOffset(1, 1, 1, 1);
            EditorStyles.textField.padding = new RectOffset(6, 6, 3, 3);

            FixAllStateBackgrounds(GUI.skin.textField,       _texSearchField);
            GUI.skin.textField.border = new RectOffset(1, 1, 1, 1);
            GUI.skin.textField.padding = new RectOffset(6, 6, 3, 3);

            GUI.skin.settings.cursorColor = TextPrimary;
            GUI.skin.settings.selectionColor = new Color(1f, 1f, 1f, 0.25f);

            // ポップアップは枠線付きテクスチャ + 1px 境界で縞ノイズを防ぐ
            FixAllStateBackgrounds(EditorStyles.popup, _texCard);
            EditorStyles.popup.border  = new RectOffset(1, 1, 1, 1);
            EditorStyles.popup.padding = new RectOffset(6, 18, 4, 4);
        }

        /// <summary>OnGUI 末尾の finally ブロックで必ず呼ぶ。EditorStyles を元へ復元する。</summary>
        public static void PopEditorTheme()
        {
            if (!_overrideActive) return;
            _overrideActive = false;

            if (_backups != null)
                foreach (var b in _backups)
                    b.Restore();

            if (_settingsBackupActive)
            {
                GUI.skin.settings.cursorColor = _backupCursorColor;
                GUI.skin.settings.selectionColor = _backupSelectionColor;
                _settingsBackupActive = false;
            }
        }

        /// <summary>
        /// Popup の背景上書きで消えるドロップダウン矢印（▼）を直前のコントロール右端に重ね描きする。
        /// EditorGUILayout.Popup の直後に呼ぶ。
        /// </summary>
        public static void DrawPopupArrowOverlay()
        {
            if (!_overrideActive) return;
            if (Event.current.type != EventType.Repaint) return;
            Rect rect = GUILayoutUtility.GetLastRect();
            var arrowRect = new Rect(rect.xMax - 14, rect.y + (rect.height - 12) * 0.5f, 12, 12);
            GUI.Label(arrowRect, "▼", SmallLabelStyle);
        }

        // ─── Style Utilities ─────────────────────────────────────────────────

        /// <summary>全 state の textColor を同一色に固定する（onNormal がトグル ON 状態に使われる点に注意）。</summary>
        internal static void FixAllTextColors(GUIStyle style, Color color)
        {
            style.normal.textColor    = color;
            style.hover.textColor     = color;
            style.active.textColor    = color;
            style.focused.textColor   = color;
            style.onNormal.textColor  = color;
            style.onHover.textColor   = color;
            style.onActive.textColor  = color;
            style.onFocused.textColor = color;
        }

        private static void FixAllStateBackgrounds(GUIStyle style, Texture2D tex)
        {
            style.normal.background    = tex;
            style.hover.background     = tex;
            style.active.background    = tex;
            style.focused.background   = tex;
            style.onNormal.background  = tex;
            style.onHover.background   = tex;
            style.onActive.background  = tex;
            style.onFocused.background = tex;
        }

        // ─── Separator ───────────────────────────────────────────────────────

        public static void DrawSeparator(int marginBottom = 4)
        {
            var rect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, Outline);
            if (marginBottom > 0) EditorGUILayout.Space(marginBottom);
        }

        // ─── Section Helper ──────────────────────────────────────────────────

        public static void BeginSection(string title)
        {
            GUILayout.BeginVertical(CardStyle);
            GUILayout.Label(title, SectionHeaderStyle);
            DrawSeparator(4);
        }

        public static void EndSection()
        {
            GUILayout.EndVertical();
        }

        // ─── Texture Utilities ───────────────────────────────────────────────

        internal static Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            _allTextures.Add(tex);
            return tex;
        }

        internal static Texture2D MakeBorderedTex(Color fillColor, Color borderColor)
        {
            const int size = 3;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y,
                        (x == 0 || x == size - 1 || y == 0 || y == size - 1)
                            ? borderColor
                            : fillColor);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            tex.hideFlags  = HideFlags.HideAndDontSave;
            _allTextures.Add(tex);
            return tex;
        }

        // ドメインリロード前に全テクスチャを破棄する（DenEmoThemeCleanupから呼ばれる）
        internal static void DisposeTextures()
        {
            // EditorStyles を上書きしたまま破棄するとエディタ全体が壊れるため先に復元
            PopEditorTheme();

            foreach (var tex in _allTextures)
                if (tex != null) Object.DestroyImmediate(tex);
            _allTextures.Clear();

            _texSurface0   = null;
            _texSurface1   = null;
            _texSurface2   = null;
            _texCard       = null;
            _texAccentCard = null;
            _texRowHover   = null;
            _texSearchField = null;
            _initialized   = false;
            _backups       = null;
        }

        private static Color Hex(int rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >>  8) & 0xFF) / 255f,
            ( rgb        & 0xFF) / 255f);
    }

    [InitializeOnLoad]
    internal static class DenEmoThemeCleanup
    {
        static DenEmoThemeCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DenEmoTheme.DisposeTextures;
        }
    }
}
