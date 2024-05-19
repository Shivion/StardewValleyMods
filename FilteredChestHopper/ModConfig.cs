
using GenericModConfigMenu;

namespace FilteredChestHopper
{
    internal class ModConfig
    {
        public bool CompareQuality { get; set; } = false;
        public bool CompareQuantity { get; set; } = false;
        public int TransferInterval { get; set; } = 60;
    }
}
