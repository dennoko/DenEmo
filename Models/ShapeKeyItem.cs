using UnityEngine;

namespace DenEmo.Models
{
    public class ShapeKeyItem
    {
        public int    Index          { get; set; }
        public string Name           { get; set; }
        public float  Value          { get; set; }

        public bool IsIncluded    { get; set; }
        public bool IsVrcShape    { get; set; }
        public bool IsLipSyncShape{ get; set; }
        public bool IsFavorite    { get; set; }

        public bool IsVisible     { get; set; }

        public ShapeKeyItem(int index, string name, float initialValue)
        {
            Index          = index;
            Name           = name;
            Value          = initialValue;
            IsIncluded     = true;
            IsVrcShape     = false;
            IsLipSyncShape = false;
            IsFavorite     = false;
            IsVisible      = true;
        }
    }
}
