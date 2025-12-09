using System;

namespace IngameScript
{
    [Flags]
    public enum ItemTypes
    {
        None = 0,
        Ore = 1,
        Ingot = 2,
        Component = 4,
        AmmoMagazine = 8,
        PhysicalGunObject = 16,
        SeedItem = 32,
        ConsumableItem = 128,
    }
}