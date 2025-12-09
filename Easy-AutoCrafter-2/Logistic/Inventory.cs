using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    public partial class Program
    {
        
        class Inventory : IComparable<Inventory>
        {
            public readonly Dictionary<string, double> Items = new Dictionary<string, double>();
            public readonly Dictionary<string, double> Delta = new Dictionary<string, double>();
            public readonly Dictionary<string, int> ItemsRequest = new Dictionary<string, int>();

            public string CustomName => _owner.CustomName;

            public int Priority { get; private set; } = 0;

            public bool Sort { get; private set; } = false;

            public ItemTypes AllowedTypes = ItemTypes.None;

            public IMyInventory MyInventory { get; private set; }

            IMyTerminalBlock _owner;
            public IMyTerminalBlock Owner => _owner;

            public Inventory(IMyInventory inventory)
            {
                _owner = (IMyTerminalBlock)inventory.Owner;
                MyInventory = inventory;
                UpdateFilters();
            }

            public void UpdateFilters()
            {
                ItemsRequest.Clear();

                var ini = new MyIni();

                var block = _owner as IMyProductionBlock;
                if (block != null)
                    Sort = block.OutputInventory == MyInventory;

                if (!(_owner is IMyCargoContainer) &&
                    !(_owner is IMyShipConnector) && // why do they try to steal every item from my assemblers?
                    !(_owner is IMyConveyorSorter))
                    return;

                Sort = true;
                ini.TryParse(_owner.CustomData);

                Priority = ini.Get(SETTINGS_SORTER, "priority").ToInt32();

                var allowedTypes = ItemTypes.None;

                if (ini.ContainsKey(SETTINGS_SORTER, "filter"))
                {
                    var filters = ini.Get(SETTINGS_SORTER, "filter").ToString("None").Split(',');
                    foreach (var filter in filters)
                    {
                        ItemTypes type;
                        if (Enum.TryParse(filter, out type))
                        {
                            allowedTypes |= type;
                        }
                    }
                }

                AllowedTypes = allowedTypes;

                List<MyIniKey> keys = new List<MyIniKey>();

                ini.GetKeys("requests", keys);

                foreach (var key in keys)
                {
                    try
                    {
                        var type = key.Name.StartsWith(OB) ? key.Name : OB + key.Name;
                        MyItemType.Parse(type);
                        ItemsRequest.Add(type, ini.Get(key).ToInt32());
                    }
                    catch
                    {
                        _owner.CustomData = _owner.CustomData.Replace(key.Name, "#Invalid Blueprint:\n#" + key.Name);
                    }
                }
            }

            public int CompareTo(Inventory other) => other.Priority.CompareTo(Priority);
        }
    }
}