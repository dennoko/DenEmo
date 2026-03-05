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
            string[,] patternsLatin = { { "_L", "_R" }, { ".L", ".R" }, { "-L", "-R" }, { " L", " R" } };
            string[,] patternsKanji = { { "_左", "_右" }, { ".左", ".右" }, { "-左", "-右" }, { " 左", " 右" } };
            
            if (n.EndsWith("(L)", StringComparison.OrdinalIgnoreCase)) { baseName = n.Substring(0, n.Length - 3).TrimEnd(); side = LRSide.L; return true; }
            if (n.EndsWith("(R)", StringComparison.OrdinalIgnoreCase)) { baseName = n.Substring(0, n.Length - 3).TrimEnd(); side = LRSide.R; return true; }
            if (n.EndsWith("(左)")) { baseName = n.Substring(0, n.Length - 3).TrimEnd(); side = LRSide.L; return true; }
            if (n.EndsWith("(右)")) { baseName = n.Substring(0, n.Length - 3).TrimEnd(); side = LRSide.R; return true; }
            if (n.EndsWith("（左）")) { baseName = n.Substring(0, n.Length - 3).TrimEnd(); side = LRSide.L; return true; }
            if (n.EndsWith("（右）")) { baseName = n.Substring(0, n.Length - 3).TrimEnd(); side = LRSide.R; return true; }
            
            for (int i = 0; i < patternsLatin.GetLength(0); i++)
            {
                string l = patternsLatin[i, 0];
                string r = patternsLatin[i, 1];
                if (n.EndsWith(l, StringComparison.OrdinalIgnoreCase)) { baseName = n.Substring(0, n.Length - l.Length); side = LRSide.L; return true; }
                if (n.EndsWith(r, StringComparison.OrdinalIgnoreCase)) { baseName = n.Substring(0, n.Length - r.Length); side = LRSide.R; return true; }
            }
            
            for (int i = 0; i < patternsKanji.GetLength(0); i++)
            {
                string l = patternsKanji[i, 0];
                string r = patternsKanji[i, 1];
                if (n.EndsWith(l)) { baseName = n.Substring(0, n.Length - l.Length); side = LRSide.L; return true; }
                if (n.EndsWith(r)) { baseName = n.Substring(0, n.Length - r.Length); side = LRSide.R; return true; }
            }
            
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
