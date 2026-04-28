using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace DenEmo.UI
{
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

        public static readonly Color Accent       = Color.white;
        public static readonly Color HoverOverlay = new Color(1f, 1f, 1f, 0.05f);
        public static readonly Color FavoriteActive = new Color(1f, 0.85f, 0.1f, 1f);

        // ─── Cached Textures ─────────────────────────────────────────────────

        private static Texture2D _texSurface0;
        private static Texture2D _texSurface1;
        private static Texture2D _texSurface2;
        private static Texture2D _texCard;
        private static Texture2D _texAccentCard;
        private static Texture2D _texRowHover;

        // 生成したすべてのテクスチャを追跡してドメインリロード前に確実に破棄する
        private static readonly List<Texture2D> _allTextures = new List<Texture2D>();

        // ─── Styles ──────────────────────────────────────────────────────────

        private static bool _initialized;

        public static GUIStyle CardStyle        { get; private set; }
        public static GUIStyle CardOuterStyle   { get; private set; }
        public static GUIStyle ToolbarStyle     { get; private set; }
        public static GUIStyle RowStyle         { get; private set; }
        public static GUIStyle RowHoverStyle    { get; private set; }

        public static GUIStyle TitleStyle            { get; private set; }
        public static GUIStyle SectionHeaderStyle    { get; private set; }
        public static GUIStyle ToggleSectionOnStyle  { get; private set; }
        public static GUIStyle ToggleSectionOffStyle { get; private set; }
        public static GUIStyle SecondaryTextStyle    { get; private set; }
        public static GUIStyle CaptionStyle          { get; private set; }
        public static GUIStyle GroupLabelStyle       { get; private set; }

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
            RowStyle.margin  = new RectOffset(0, 0, 1, 1);

            RowHoverStyle = new GUIStyle();
            RowHoverStyle.normal.background = _texRowHover;
            RowHoverStyle.padding = new RectOffset(4, 4, 2, 2);
            RowHoverStyle.margin  = new RectOffset(0, 0, 1, 1);

            // ── Typography ───────────────────────────────────────────────────

            TitleStyle = new GUIStyle(EditorStyles.boldLabel);
            TitleStyle.fontSize = 14;
            TitleStyle.normal.textColor = TextPrimary;

            SectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
            SectionHeaderStyle.fontSize = 10;
            SectionHeaderStyle.normal.textColor = TextTertiary;
            SectionHeaderStyle.margin = new RectOffset(0, 0, 0, 2);

            ToggleSectionOnStyle = new GUIStyle(EditorStyles.boldLabel);
            ToggleSectionOnStyle.fontSize = 10;
            ToggleSectionOnStyle.normal.textColor = TextPrimary;

            ToggleSectionOffStyle = new GUIStyle(EditorStyles.boldLabel);
            ToggleSectionOffStyle.fontSize = 10;
            ToggleSectionOffStyle.normal.textColor = TextTertiary;

            SecondaryTextStyle = new GUIStyle(EditorStyles.label);
            SecondaryTextStyle.normal.textColor = TextSecondary;
            SecondaryTextStyle.wordWrap = true;

            CaptionStyle = new GUIStyle(EditorStyles.miniLabel);
            CaptionStyle.normal.textColor = TextTertiary;

            GroupLabelStyle = new GUIStyle(EditorStyles.boldLabel);
            GroupLabelStyle.fontSize = 11;
            GroupLabelStyle.normal.textColor = TextSecondary;

            // ── Buttons ──────────────────────────────────────────────────────

            ActionButtonStyle = new GUIStyle();
            ActionButtonStyle.normal.background  = _texAccentCard;
            ActionButtonStyle.normal.textColor   = TextPrimary;
            ActionButtonStyle.hover.background   = MakeTex(Color.Lerp(Surface2, Color.white, 0.07f));
            ActionButtonStyle.hover.textColor    = TextPrimary;
            ActionButtonStyle.active.background  = MakeTex(Color.Lerp(Surface2, Color.white, 0.15f));
            ActionButtonStyle.active.textColor   = TextPrimary;
            ActionButtonStyle.border     = new RectOffset(1, 1, 1, 1);
            ActionButtonStyle.fontSize   = 12;
            ActionButtonStyle.fontStyle  = FontStyle.Bold;
            ActionButtonStyle.fixedHeight = 32;
            ActionButtonStyle.alignment  = TextAnchor.MiddleCenter;

            SecondaryButtonStyle = new GUIStyle();
            SecondaryButtonStyle.normal.background = MakeBorderedTex(Surface1, Outline);
            SecondaryButtonStyle.normal.textColor  = TextSecondary;
            SecondaryButtonStyle.hover.background  = _texAccentCard;
            SecondaryButtonStyle.hover.textColor   = TextPrimary;
            SecondaryButtonStyle.active.background = MakeTex(Color.Lerp(Surface1, Color.white, 0.10f));
            SecondaryButtonStyle.active.textColor  = TextPrimary;
            SecondaryButtonStyle.border     = new RectOffset(1, 1, 1, 1);
            SecondaryButtonStyle.fontSize   = 11;
            SecondaryButtonStyle.fixedHeight = 26;
            SecondaryButtonStyle.alignment  = TextAnchor.MiddleCenter;

            MiniButtonStyle = new GUIStyle(EditorStyles.miniButton);
            MiniButtonStyle.normal.textColor  = TextTertiary;
            MiniButtonStyle.hover.textColor   = TextSecondary;
            MiniButtonStyle.active.textColor  = TextPrimary;

            // フィルターチップ（トグルボタン）
            ChipOnStyle = new GUIStyle();
            ChipOnStyle.normal.background  = MakeBorderedTex(Surface2, Accent);
            ChipOnStyle.normal.textColor   = TextPrimary;
            ChipOnStyle.hover.background   = MakeBorderedTex(Color.Lerp(Surface2, Accent, 0.1f), Accent);
            ChipOnStyle.hover.textColor    = TextPrimary;
            ChipOnStyle.active.background  = MakeBorderedTex(Color.Lerp(Surface2, Accent, 0.2f), Accent);
            ChipOnStyle.active.textColor   = TextPrimary;
            ChipOnStyle.border     = new RectOffset(1, 1, 1, 1);
            ChipOnStyle.fontSize   = 10;
            ChipOnStyle.fixedHeight = 20;
            ChipOnStyle.padding    = new RectOffset(6, 6, 2, 2);
            ChipOnStyle.alignment  = TextAnchor.MiddleCenter;

            ChipOffStyle = new GUIStyle();
            ChipOffStyle.normal.background  = MakeBorderedTex(Surface1, Outline);
            ChipOffStyle.normal.textColor   = TextTertiary;
            ChipOffStyle.hover.background   = MakeBorderedTex(Surface2, Outline);
            ChipOffStyle.hover.textColor    = TextSecondary;
            ChipOffStyle.active.background  = MakeTex(Color.Lerp(Surface1, Color.white, 0.10f));
            ChipOffStyle.active.textColor   = TextPrimary;
            ChipOffStyle.border     = new RectOffset(1, 1, 1, 1);
            ChipOffStyle.fontSize   = 10;
            ChipOffStyle.fixedHeight = 20;
            ChipOffStyle.padding    = new RectOffset(6, 6, 2, 2);
            ChipOffStyle.alignment  = TextAnchor.MiddleCenter;

            // お気に入り星ボタン
            FavOnStyle = new GUIStyle(EditorStyles.label);
            FavOnStyle.normal.textColor = FavoriteActive;
            FavOnStyle.hover.textColor  = new Color(1f, 0.95f, 0.4f, 1f);
            FavOnStyle.fontSize   = 13;
            FavOnStyle.alignment  = TextAnchor.MiddleCenter;
            FavOnStyle.padding    = new RectOffset(0, 0, 0, 0);

            FavOffStyle = new GUIStyle(EditorStyles.label);
            FavOffStyle.normal.textColor = TextDisabled;
            FavOffStyle.hover.textColor  = new Color(0.7f, 0.7f, 0.7f, 1f);
            FavOffStyle.fontSize   = 13;
            FavOffStyle.alignment  = TextAnchor.MiddleCenter;
            FavOffStyle.padding    = new RectOffset(0, 0, 0, 0);

            // ── Status Bar ───────────────────────────────────────────────────

            var statusBase = new GUIStyle();
            statusBase.border  = new RectOffset(1, 1, 1, 1);
            statusBase.padding = new RectOffset(10, 10, 5, 5);
            statusBase.margin  = new RectOffset(0, 0, 0, 0);
            statusBase.fontSize = 11;

            StatusInfoStyle = new GUIStyle(statusBase);
            StatusInfoStyle.normal.background = _texSurface1;
            StatusInfoStyle.normal.textColor  = TextSecondary;

            StatusSuccessStyle = new GUIStyle(statusBase);
            StatusSuccessStyle.normal.background = MakeTex(Color.Lerp(Surface1, SemanticSuccess, 0.25f));
            StatusSuccessStyle.normal.textColor  = SemanticSuccess;

            StatusWarningStyle = new GUIStyle(statusBase);
            StatusWarningStyle.normal.background = MakeTex(Color.Lerp(Surface1, SemanticWarning, 0.20f));
            StatusWarningStyle.normal.textColor  = SemanticWarning;

            StatusErrorStyle = new GUIStyle(statusBase);
            StatusErrorStyle.normal.background = MakeTex(Color.Lerp(Surface1, SemanticError, 0.50f));
            StatusErrorStyle.normal.textColor  = new Color(1f, 0.65f, 0.65f);
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

        private static Texture2D MakeTex(Color color)
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
            foreach (var tex in _allTextures)
                if (tex != null) Object.DestroyImmediate(tex);
            _allTextures.Clear();

            _texSurface0   = null;
            _texSurface1   = null;
            _texSurface2   = null;
            _texCard       = null;
            _texAccentCard = null;
            _texRowHover   = null;
            _initialized   = false;
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
