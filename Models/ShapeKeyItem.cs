using UnityEngine;

namespace DenEmo.Models
{
    public class ShapeKeyItem
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public float Value { get; set; }
        
        // Settings and Filters
        public bool IsIncluded { get; set; }
        public bool IsVrcShape { get; set; }
        public bool IsLipSyncShape { get; set; }
        
        // UI State
        public bool IsVisible { get; set; }

        public ShapeKeyItem(int index, string name, float initialValue)
        {
            Index = index;
            Name = name;
            Value = initialValue;
            IsIncluded = true;
            IsVrcShape = false;
            IsLipSyncShape = false;
            IsVisible = true;
        }
    }
}
