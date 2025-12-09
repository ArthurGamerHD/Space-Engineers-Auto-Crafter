using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;

namespace IngameScript
{
    public partial class Program
    {
        class CraftRequest : ProductionRequestBase
        {
            int _current;

            public CraftRequest(int amount, Item item)
            {
                Amount = amount;
                Item = item;
                Blueprint = item.Blueprint;
            }

            public override int Amount { get; }
        }

        class SubCraftRequest : ProductionRequestBase
        {
            public SubCraftRequest(ProductionRequestBase parent, Item item)
            {
                Item = item;
                Blueprint = item.Blueprint;
                _parent = parent;
            }

            ProductionRequestBase _parent;

            public override int Amount => (int)Math.Round(
                _parent.Blueprint.Prerequisites[Item.FullName] * _parent.Amount,
                MidpointRounding.AwayFromZero);
        }
    }
}