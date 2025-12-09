using System;
using System.Collections.Generic;
using System.Linq;
using VRage;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    public partial class Program
    {
        
        public IEnumerator<bool> SortRoutine()
        {
            Dictionary<ItemTypes, List<Inventory>> perTypeStorages = new Dictionary<ItemTypes, List<Inventory>>();

            IEnumerable<Inventory> sortableInventory = _perInventoryStorage.Values.Where(a => a.Sort).ToArray();

            List<MyInventoryItem> items = new List<MyInventoryItem>();

            foreach (var item in Enum.GetValues(typeof(ItemTypes)))
            {
                if ((ItemTypes)item == ItemTypes.None)
                    continue;

                perTypeStorages[(ItemTypes)item] = new List<Inventory>();
            }

            yield return true;

            foreach (var inventory in sortableInventory)
            {
                foreach (var type in perTypeStorages.Keys.Where(type => inventory.AllowedTypes.HasFlag(type)))
                    perTypeStorages[type].Add(inventory);
            }

            yield return true;

            foreach (var list in perTypeStorages.Values)
            {
                list.Sort((a, b) => a.CompareTo(b));
            }

            yield return true;

            foreach (var inventory in sortableInventory)
            {
                items.Clear();
                inventory.MyInventory.GetItems(items);
                foreach (var item in items)
                {
                    var move = item.Amount;
                    var itemFullType = item.Type.ToString();

                    var itemType = itemFullType.Split('_')[1].Split('/')[0];

                    ItemTypes type;
                    if (!Enum.TryParse(itemType, out type) && !_typesMap.TryGetValue(itemType, out type))
                        continue;


                    var targets = perTypeStorages[type]
                        .Where(a => a.MyInventory.CanTransferItemTo(inventory.MyInventory, item.Type)).ToList();


                    if (inventory.AllowedTypes.HasFlag(type))
                    {
                        double totalMass = 0;

                        foreach (var target in targets)
                        {
                            totalMass += (double)target.MyInventory.CurrentMass;
                        }

                        totalMass /= targets.Count;

                        if ((double)inventory.MyInventory.CurrentMass < totalMass * 1.05d)
                            continue;
                    }

                    int keep;
                    if (inventory.ItemsRequest.TryGetValue(itemFullType, out keep))
                        move -= keep;

                    if (move > 0)
                    {
                        move = (MyFixedPoint)Math.Min((double)item.Amount, 10000d);

                        targets.Sort((a, b) =>
                            a.MyInventory.CurrentMass.ToIntSafe().CompareTo(b.MyInventory.CurrentMass.ToIntSafe()));

                        foreach (var dstInventory in targets)
                        {
                            if (dstInventory.MyInventory.TransferItemFrom(inventory.MyInventory, item, move))
                            {
                                _currentOperations++;
                                _currentCraftSb.AppendLine(
                                    $"Moved {move} {item.Type.SubtypeId} {item.Type.TypeId.Split('_').Last()} from {inventory.CustomName} to {dstInventory.CustomName}");
                                break;
                            }
                        }

                        yield return true;
                    }
                }

                yield return true;
            }

            yield return true;

            if (_debugSort != null)
            {
                _debugSb.Clear();

                foreach (var item in sortableInventory)
                {
                    if (item.AllowedTypes == ItemTypes.None)
                        continue;

                    _debugSb.AppendLine(item.CustomName);
                    _debugSb.AppendLine("  -" + item.AllowedTypes);
                    _debugSb.AppendLine();
                }

                if (_debugSb.Length == 0)
                    _debugSb.AppendLine("Sorting not setup");

                _debugSort.WriteText(_debugSb);
            }
        }
    }
}