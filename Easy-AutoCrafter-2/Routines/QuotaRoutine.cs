using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace IngameScript
{
    public partial class Program
    {
        Dictionary<string, int> _quotaRequestsCache = new Dictionary<string, int>();
        List<ProductionRequestBase> _validRequestsBuffer = new List<ProductionRequestBase>();

        List<ProductionRequestBase> GetAllRequests(string filter = null)
        {
            _validRequestsBuffer.Clear();
            _validRequestsBuffer.AddRange(filter == null ? _craftRequests : _craftRequests.Where(a => a.Item.FullName == filter));
            _validRequestsBuffer.AddRange(filter == null ? _refineRequests : _refineRequests.Where(a => a.Item.FullName == filter));
            return _validRequestsBuffer;
        }
        
        void FulfillRequests(string item, double value)
        {
            var remainder = value; ;
            foreach (var refineRequest in GetAllRequests(item))
            {
                if (remainder <= 0)
                    break;

                var amount = Math.Min(remainder, refineRequest.Amount - refineRequest.Current);
                refineRequest.Current += (int)amount;
                remainder -= amount;
            }
        }

        public IEnumerator<bool> QuotaRoutine()
        {
            _quotaRequestsCache.Clear();
            foreach (var inventory in _perInventoryStorage)
            {
                inventory.Value.UpdateFilters();
                foreach (var item in inventory.Value.ItemsRequest)
                {
                    if (!_quotaRequestsCache.ContainsKey(item.Key))
                        _quotaRequestsCache[item.Key] = 0;

                    _quotaRequestsCache[item.Key] = item.Value;
                }

                yield return true;
            }

            if (_debugRequest != null)
            {
                _debugSb.Clear();
                foreach (var request in _quotaRequestsCache)
                    if (request.Key.Length > CHARACTERS_TO_SKIP)
                        _debugSb.AppendLine(request.Key.Substring(CHARACTERS_TO_SKIP) + ": " + request.Value);

                _debugRequest.WriteText(_debugSb);
            }

            StringBuilder debugSb = null;
            if (_debugCraft != null)
                debugSb = new StringBuilder($"Crafting:{_craftRequests.Count}\n" +
                                            $"Refining:{_refineRequests.Count}\n" +
                                            $"-- New Requests --\n");


            Item stockItem;

            foreach (var item in _quotaRequestsCache)
            {
                if (!_stock.TryGetValue(item.Key, out stockItem))
                    continue;

                if (stockItem.Amount >= item.Value)
                    continue;

                if (HasPendingRequest(stockItem))
                    continue;

                if (item.Key.Length > CHARACTERS_TO_SKIP)
                    debugSb?.Append($"{item.Value}x {item.Key.Substring(CHARACTERS_TO_SKIP)}: ");
                stockItem = _stock[item.Key];
                if (stockItem.Blueprint == null)
                {
                    debugSb?.AppendLine($"No Recipe Found!");
                    continue;
                }

                var isRefine = stockItem.Type == "Ingot";
                var request = new CraftRequest((int)Math.Round(item.Value - stockItem.Amount, MidpointRounding.AwayFromZero), stockItem);

                if (isRefine)
                    AddRefineRequest(request);
                else
                    AddCraftRequest(request);


                HandleDependenciesRecursively(request);

                debugSb?.Append($"Using Recipe {stockItem.Blueprint.DisplayName}");
                debugSb?.AppendLine();

                yield return true;
            }

            foreach (var item in _countingDelta)
            {
                FulfillRequests(item.Key, item.Value);
            }
            yield return true;

            
            if (_debugCraft != null)
            {
                debugSb?.AppendLine("-- Requests --");
                WriteCrafts(_craftRequests, debugSb);
                WriteCrafts(_refineRequests, debugSb);
                _debugCraft.WriteText(debugSb?.ToString());
            }
        }

        bool HasPendingRequest(Item stockItem)
        {
            if (_craftRequests.Count == 0 && _refineRequests.Count == 0)
                return false;

            return _craftRequests.Any(a => a.Item.FullName == stockItem.FullName) ||
                   _refineRequests.Any(a => a.Item.FullName == stockItem.FullName);
        }

        void WriteCrafts(IEnumerable<ProductionRequestBase> requests, StringBuilder debugSb)
        {
            foreach (var request in requests)
            {
                if (request is SubCraftRequest)
                    continue;


                debugSb?.AppendLine($"Using Recipe {request.Item.Blueprint.DisplayName}");
                DebugDisplayRecipeRecursively(request, debugSb);

                var bp = request.ItemBeingProduced?.BlueprintId.ToString();
                if (!(bp != null && !string.IsNullOrEmpty(bp) && bp.Length > 36))
                    continue;
                debugSb?.AppendLine($"Crafting now: {request.ItemBeingProduced?.Amount}" +
                                    $" {bp.Substring("MyObjectBuilder_BlueprintDefinition/".Length)}");
            }
        }

        void DebugDisplayRecipeRecursively(ProductionRequestBase request, StringBuilder debugSb, string indent = " >")
        {
            indent = indent.PadLeft(indent.Length + 3);
            debugSb.AppendLine(indent + $"{request.Current}/{request.Amount}x " + request.Blueprint?.DisplayName);

            if (request.NestedRequests != null)
                foreach (var blueprintNestedRequest in request.NestedRequests)
                {
                    if (blueprintNestedRequest.Blueprint == null)
                    {
                        MyLog.Log(LogLevel.Warning, "Invalid blueprint nested request");
                        continue;
                    }

                    DebugDisplayRecipeRecursively(blueprintNestedRequest, debugSb, indent + "  ");
                }
            else
            {
                if (request.Blueprint?.Prerequisites != null)
                    foreach (var prerequisite in request.Blueprint.Prerequisites)
                    {
                        Item value;
                        if (_stock.TryGetValue(prerequisite.Key, out value))
                            debugSb.AppendLine("  " + indent + $"{value.Amount}/{prerequisite.Value * request.Missing}x " + value.NaturalName);
                        else
                            debugSb.AppendLine("  " + indent + $"<none>/{prerequisite.Value * request.Missing}x " + prerequisite);
                    }
            }
        }

        void HandleDependenciesRecursively(ProductionRequestBase request)
        {
            if (request?.Blueprint?.Prerequisites == null)
                return;

            foreach (var prerequisite in request.Blueprint.Prerequisites)
            {
                Item requiredItem;
                var total = prerequisite.Value * request.Missing;
                if (_stock.TryGetValue(prerequisite.Key, out requiredItem) && requiredItem.Amount < total)
                {
                    var missing = total - Math.Max(requiredItem.Amount, 0);
                    
                    if (requiredItem.Blueprint == null)
                        continue;
                    
                    if(missing <= 0)
                        continue;

                    if (request.NestedRequests == null)
                        request.NestedRequests = new List<ProductionRequestBase>();

                    var isRefine = requiredItem.Type == "Ingot";
                    var subRequest = new SubCraftRequest(request, requiredItem);
                    if (isRefine)
                        AddRefineRequest(subRequest);
                    else
                        AddCraftRequest(subRequest);

                    subRequest.Current = (int)(total - missing);
                    request.NestedRequests.Add(subRequest);
                    HandleDependenciesRecursively(subRequest);
                }
            }
        }

        void OnCrafted(ProductionRequestBase craftRequest) => _craftRequests.Remove(craftRequest);
        void OnRefined(ProductionRequestBase refineRequest) => _refineRequests.Remove(refineRequest);

        void AddCraftRequest(ProductionRequestBase craftRequest)
        {
            _craftRequests.Add(craftRequest);
            craftRequest.OnClosed += OnCrafted;
        }

        void AddRefineRequest(ProductionRequestBase craftRequest)
        {
            _refineRequests.Add(craftRequest);
            craftRequest.OnClosed += OnRefined;
        }
    }
}