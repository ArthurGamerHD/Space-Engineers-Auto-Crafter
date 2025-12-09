using System;
using System.Collections.Generic;
using System.Linq;
using VRage;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    public partial class Program
    {
        
        public IEnumerator<bool> RequestRoutine()
        {
            foreach (var current in _perInventoryStorage.Values)
            {
                foreach (var request in current.ItemsRequest)
                {
                    double amount = request.Value;
                    double currentCount;
                    current.Items.TryGetValue(request.Key, out currentCount);

                    amount -= currentCount;
                    if (amount <= 0)
                        continue;

                    var type = MyItemType.Parse(request.Key);

                    var available =
                        _perInventoryStorage.Values
                            .Where(a => a != current &&
                                        a.Sort && a.Items.ContainsKey(request.Key) &&
                                        (!a.ItemsRequest.ContainsKey(request.Key) ||
                                         a.ItemsRequest[request.Key] < a.Items[request.Key]) &&
                                        a.MyInventory.IsConnectedTo(current.MyInventory) &&
                                        a.MyInventory.CanTransferItemTo(current.MyInventory, type) &&
                                        // ReSharper disable once AccessToModifiedClosure, this is intended
                                        a.MyInventory.CanItemsBeAdded((MyFixedPoint)amount, type));

                    foreach (var inventory in available)
                    {
                        var attempts = 0;
                        while (amount > 0 && attempts <= 10)
                        {
                            attempts++;
                            var item = inventory.MyInventory.FindItem(type);

                            if (item == null)
                                break;

                            int reserved;
                            inventory.ItemsRequest.TryGetValue(request.Key, out reserved);
                            var transferBatch = Math.Min(amount, item.Value.Amount.ToIntSafe() - reserved);

                            if (!inventory.MyInventory.TransferItemTo(current.MyInventory, item.Value,
                                    (MyFixedPoint)transferBatch))
                            {
                                _currentOperations++;
                                break;
                            }


                            inventory.Items[request.Key] -= transferBatch;
                            amount -= transferBatch;
                        }

                        yield return true;
                    }
                }
            }
        }
    }
}