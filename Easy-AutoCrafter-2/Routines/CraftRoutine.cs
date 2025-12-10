using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRage;

namespace IngameScript
{
    public partial class Program
    {
        readonly Dictionary<string, double> _craftAllocation = new Dictionary<string, double>();

        public IEnumerator<bool> CraftRoutine()
        {
            if (_craftRequests.Count <= 0)
            {
                _debugCraft?.WriteText("No Pending Requests");
                yield break;
            }

            _currentCraftSb.AppendLine("Current Crafting:");

            yield return true;

            foreach (var request in _craftRequests)
            {
                // Prevents multiples craft order of the same item
                if (request.Closed || request.CurrentCrafting > 0)
                    continue;

                var maximum = request.Missing;

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

                if (request.CurrentAssigned == null)
                {
                    var available = _assemblers.Where(assembler =>
                        assembler.Assembler.IsQueueEmpty &&
                        assembler.Assembler.Mode == MyAssemblerMode.Assembly &&
                        assembler.CanUseBlueprint(request.Blueprint.BlueprintId) &&
                        assembler.IsQueueEmpty).ToArray();

                    if (!available.Any())
                    {
                        available = _assemblers.Where(assembler =>
                            assembler.CanUseBlueprint(request.Blueprint.BlueprintId)).ToArray();
                    }

                    foreach (var assembler in available)
                    {
                        _currentOperations++;
                        if (request.Blueprint == null)
                            throw new Exception("Blueprint is null");

                        assembler.AddQueueItem(request.Blueprint.BlueprintId, maximum);
                        request.CurrentCrafting += maximum;
                        request.CurrentAssigned = assembler;
                        break;
                    }
                }
                else
                {
                    request.CurrentAssigned.AddQueueItem(request.Blueprint.BlueprintId, maximum);
                    request.CurrentCrafting += maximum;
                }
            }

            _currentCraftString = _currentCraftSb.ToString();
            _currentCraftSb.Clear();
        }
    }
}