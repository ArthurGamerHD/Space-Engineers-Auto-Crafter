using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    public partial class Program
    {
        readonly Dictionary<string, double> _countingCacheItems = new Dictionary<string, double>();
        readonly Dictionary<string, double> _countingDelta = new Dictionary<string, double>();

        public IEnumerator<bool> CountingRoutine()
        {
            foreach (var item in _stock.Keys)
                _stock[item].Amount = 0;

            _countingDelta.Clear();

            Dictionary<IMyInventory, Inventory> perInventoryStorage = new Dictionary<IMyInventory, Inventory>();

            foreach (var container in _containers)
            {
                for (int i = 0; i < container.InventoryCount; ++i)
                {
                    var inventory = container.GetInventory(i);

                    Inventory value;
                    if (_perInventoryStorage.TryGetValue(inventory, out value))
                        perInventoryStorage[inventory] = value;
                    else
                        perInventoryStorage[inventory] = new Inventory(inventory, this);

                    if (inventory.ItemCount <= 0)
                    {
                        perInventoryStorage[inventory].Items.Clear();
                        continue;
                    }

                    _itemsBuffer.Clear();
                    _countingCacheItems.Clear();

                    inventory.GetItems(_itemsBuffer);
                    foreach (var key in perInventoryStorage[inventory].Items.Keys)
                        _countingCacheItems[key] = 0;

                    foreach (var item in _itemsBuffer)
                    {
                        string key = item.Type.ToString();

                        double amount = (double)item.Amount;

                        if (!_countingCacheItems.ContainsKey(key))
                            _countingCacheItems[key] = 0;

                        _countingCacheItems[key] += amount;
                    }

                    perInventoryStorage[inventory].Delta.Clear();
                    foreach (var newItem in _countingCacheItems)
                    {
                        var key = newItem.Key;
                        double currentAmount;
                        perInventoryStorage[inventory].Items.TryGetValue(key, out currentAmount);
                        double delta = newItem.Value - currentAmount;
                        perInventoryStorage[inventory].Items[newItem.Key] = newItem.Value;
                        var stockItem = GetOrAddItem(key);
                        stockItem.Amount += newItem.Value;
                        perInventoryStorage[inventory].Delta[key] = delta;

                        if (delta == 0)
                            continue;

                        if (!_countingDelta.ContainsKey(newItem.Key))
                            _countingDelta[newItem.Key] = 0;

                        _countingDelta[newItem.Key] += delta;
                    }

                    yield return true;
                }
            }

            _perInventoryStorage = perInventoryStorage;

            if (_debugItems != null)
            {
                _debugSb.Clear();
                if (_countingDelta.Any())
                {
                    _debugSb.AppendLine("-- Delta --");

                    foreach (var item in _countingDelta)
                    {
                        _debugSb.Append("  -" + _stock[item.Key].NaturalName + ":");
                        _debugSb.AppendLine($"{(item.Value > 0 ? " +" : " ")}{item.Value}");
                    }
                }

                _debugSb.AppendLine("-- Total --");
                foreach (var item in _stock)
                {
                    double delta;
                    _countingDelta.TryGetValue(item.Key, out delta);

                    _debugSb.Append("  -" + item.Value.NaturalName +
                                    $": {item.Value.Amount}");

                    _debugSb.AppendLine(delta == 0 ? "" : $" ({(delta > 0 ? "+" : "")}{delta})");
                    if(item.Key.Length > CHARACTERS_TO_SKIP)
                        _debugSb.AppendLine("   " + item.Key.Substring(CHARACTERS_TO_SKIP));
                }

                _debugSb.AppendLine("\n-- Per Inventory--");

                foreach (var item in _perInventoryStorage)
                {
                    if (item.Value.ItemsRequest.Count == 0 && item.Value.Items.Count == 0)
                        continue;

                    _debugSb.AppendLine(item.Value.CustomName);
                    if (item.Value.Items.Count != 0)
                    {
                        _debugSb.AppendLine(" Current:");
                        foreach (var itemKeyPair in item.Value.Items)
                        {
                            var delta = item.Value.Delta[itemKeyPair.Key];
                            var value = itemKeyPair.Value;
                            _debugSb.AppendLine("  -" + _stock[itemKeyPair.Key].NaturalName +
                                                $": {value} ({(delta > 0 ? "+" : "")}{delta})");
                            if(itemKeyPair.Key.Length > CHARACTERS_TO_SKIP)
                                _debugSb.AppendLine("   " + itemKeyPair.Key.Substring(CHARACTERS_TO_SKIP));
                        }
                    }

                    if (item.Value.ItemsRequest.Count != 0)
                    {
                        _debugSb.AppendLine(" Requests:");
                        foreach (var itemsItem in item.Value.ItemsRequest)
                        {
                            if(itemsItem.Key.Length > CHARACTERS_TO_SKIP)
                                _debugSb.AppendLine("  -" + itemsItem.Key.Substring(CHARACTERS_TO_SKIP) + ":" + itemsItem.Value);
                        }
                    }

                    _debugSb.AppendLine();
                }

                _debugItems.WriteText(_debugSb);
            }
        }
    }
}