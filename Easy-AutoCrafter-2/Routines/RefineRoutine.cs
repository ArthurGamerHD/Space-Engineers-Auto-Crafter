using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRage;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    public partial class Program
    {
        readonly Dictionary<string, double> _refineAllocation = new Dictionary<string, double>();

        public IEnumerator<bool> RefineRoutine()
        {
            if (_craftRequests.Count <= 0)
            {
                _debugCraft?.WriteText("No Pending Requests");
                yield break;
            }

            _currentCraftSb.AppendLine("Current Crafting:");

            yield return true;

            foreach (var request in _refineRequests)
            {
                if (request.CurrentAssigned == null)
                {
                    _currentCraftSb.Append($" -{request.Amount:0} " + request.Item.NaturalName);

                    if (request.Blueprint.Prerequisites != null)
                    {
                        _currentCraftSb.AppendLine(":");
                        _predictedCost.Clear();

                        foreach (var item in request.Blueprint.Prerequisites)
                        {
                            _predictedCost[item.Key] = item.Value * request.Amount / _assemblerEfficiency;
                            Item stockItem = GetOrAddItem(item.Key);
                            _currentCraftSb.AppendLine(
                                $"    -{MetricFormat(item.Value * request.Amount)}/{MetricFormat(stockItem.Amount)}" +
                                ": " + stockItem.NaturalName);
                        }
                    }

                    _currentCraftSb.AppendLine("");

                    var maximum = request.Amount;

                    if (_predictedCost.Any(a => !_stock.ContainsKey(a.Key) || a.Value > _stock[a.Key].Amount))
                    {
                        foreach (var item in _predictedCost)
                        {
                            Item value;
                            double required = 0;
                            if (!_stock.TryGetValue(item.Key, out value)
                                || !(request.Blueprint?.Prerequisites?.TryGetValue(item.Key, out required) ?? false)
                                || value.Amount < required)
                            {
                                maximum = 0; // some requirement is missing 
                                break;
                            }

                            maximum = Math.Min(maximum, (int)(value.Amount / required));
                        }

                        if (maximum <= 0)
                            continue; // too expensive to even craft a single item
                    }

                    var available = 
                        _refineries.Where(refinery => !refinery.IsBusy &&
                        refinery.CanUseBlueprint(request.Blueprint.BlueprintId)).ToArray();

                    if (!available.Any())
                    {
                        available = _refineries.Where(refinery =>
                            refinery.CanUseBlueprint(request.Blueprint.BlueprintId)).ToArray();
                    }

                    foreach (var refinery in available)
                    {
                        bool success = false;
                        
                        if (refinery.InputInventory.ItemCount != 0)
                        {
                            _itemsBuffer.Clear();
                            refinery.InputInventory.GetItems(_itemsBuffer);
                            foreach (var item in _itemsBuffer)
                                if (!request.Blueprint.Prerequisites.ContainsKey(item.Type.ToString()))
                                    foreach (var inventory in _perInventoryStorage.Where(a =>
                                                 (a.Value.AllowedTypes & ItemTypes.Ore) != 0 ||
                                                 a.Value.ItemsRequest.ContainsKey(item.Type.ToString())))
                                        if (refinery.InputInventory.TransferItemTo(inventory.Key, item))
                                            break;
                        }

                        success |= MoveItemToRefinery(request.Item, refinery);

                        if (success)
                        {
                            _currentOperations++;
                            refinery.Request = request;
                            request.CurrentAssigned = refinery;
                            break;
                        }
                    }
                }
            }

            _currentCraftString = _currentCraftSb.ToString();
            _currentCraftSb.Clear();
        }
        
        bool MoveItemToRefinery(Item request, ManagedRefinery refinery)
        {
            bool success = false;
            foreach (var inventory in 
                     _perInventoryStorage.Where(a => 
                         request.Blueprint.Prerequisites.Any(b =>
                             a.Value.Items.ContainsKey(b.Key) && !
                                 // steal from friends is nasty
                                 (a.Value.Owner is IMyRefinery && a.Key.GetItemAt(0)?.Type.ToString() == b.Key))))
            {
                _itemsBuffer.Clear();
                inventory.Key.GetItems(_itemsBuffer);
                foreach (var item in _itemsBuffer)
                    if (request.Blueprint.Prerequisites.ContainsKey(item.Type.ToString()))
                        success |= refinery.InputInventory.TransferItemFrom(inventory.Key, item,
                            1000);
            }
            
            return success;
        }
    }
}