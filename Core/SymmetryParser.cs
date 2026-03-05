using System;

namespace DenEmo.Core
{
    public enum LRSide { None, L, R }

    public class LRShapeParseResult
    {
        public string BaseName { get; set; }
        public LRSide Side { get; set; }
    }

    public static class SymmetryParser
    {
        public static bool TryParseLRSuffix(string name, out string baseName, out LRSide side)
        {
            baseName = name;
            side = LRSide.None;
            if (string.IsNullOrEmpty(name)) return false;
            
            string n = name.Trim();

            // 1. 括弧形式の判定
            // (L), (R), (left), (right), [left], [right] 等 (大文字小文字無視)
            string[] leftBrackets = { "(L)", "(left)", "[L]", "[left]", "(左)", "（左）" };
            string[] rightBrackets = { "(R)", "(right)", "[R]", "[right]", "(右)", "（右）" };

            foreach (var lb in leftBrackets)
            {
                if (n.EndsWith(lb, StringComparison.OrdinalIgnoreCase))
                {
                    baseName = n.Substring(0, n.Length - lb.Length).TrimEnd();
                    side = LRSide.L;
                    return true;
                }
            }
            foreach (var rb in rightBrackets)
            {
                if (n.EndsWith(rb, StringComparison.OrdinalIgnoreCase))
                {
                    baseName = n.Substring(0, n.Length - rb.Length).TrimEnd();
                    side = LRSide.R;
                    return true;
                }
            }

            // 2. セパレータ + キーワード形式の判定
            // _L, .left, -Right,  LEFT 等
            string[] separators = { "_", ".", "-", " " };
            string[] leftKeywords = { "L", "left" };
            string[] rightKeywords = { "R", "right" };

            foreach (var sep in separators)
            {
                foreach (var kw in leftKeywords)
                {
                    string pattern = sep + kw;
                    if (n.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        baseName = n.Substring(0, n.Length - pattern.Length);
                        side = LRSide.L;
                        return true;
                    }
                }
                foreach (var kw in rightKeywords)
                {
                    string pattern = sep + kw;
                    if (n.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        baseName = n.Substring(0, n.Length - pattern.Length);
                        side = LRSide.R;
                        return true;
                    }
                }
            }

            // 日本語セパレータ形式 (左, 右)
            string[] jpSeparators = { "_", ".", "-", " " };
            foreach (var sep in jpSeparators)
            {
                if (n.EndsWith(sep + "左")) { baseName = n.Substring(0, n.Length - 2); side = LRSide.L; return true; }
                if (n.EndsWith(sep + "右")) { baseName = n.Substring(0, n.Length - 2); side = LRSide.R; return true; }
            }

            // 3. セパレータなし接尾辞形式 (キャメルケース等)
            // EyeBlinkLeft, EyeBlinkRight
            if (n.EndsWith("Left", StringComparison.Ordinal)) { baseName = n.Substring(0, n.Length - 4); side = LRSide.L; return true; }
            if (n.EndsWith("Right", StringComparison.Ordinal)) { baseName = n.Substring(0, n.Length - 5); side = LRSide.R; return true; }
            if (n.EndsWith("LEFT", StringComparison.Ordinal)) { baseName = n.Substring(0, n.Length - 4); side = LRSide.L; return true; }
            if (n.EndsWith("RIGHT", StringComparison.Ordinal)) { baseName = n.Substring(0, n.Length - 5); side = LRSide.R; return true; }
            
            return false;
        }

        public static string GetSymmetryDisplayName(string name)
        {
            if (TryParseLRSuffix(name, out var baseName, out var side))
            {
                if (side != LRSide.None) return baseName + "_LR";
            }
            return name;
        }
    }
}
