using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;

namespace IngameScript
{
    public partial class Program
    {
        public abstract class ProductionRequestBase : IComparable<ProductionRequestBase>, IVisualItem
        {
            public string Sprite => Item.Sprite;
            public string NaturalName => Item.NaturalName;
            public string Description => $"{MetricFormat(Current)}/{MetricFormat(Amount)} Crafted";
            
            public event Action<ProductionRequestBase> OnClosed;
            
            public bool Closed => Current >= Amount;

            public Item Item { get; protected set; }
            public Blueprint Blueprint { get; protected set; }
            public abstract int Amount { get; }
            
            public virtual int Missing => Amount - Current;
            
            static List<MyProductionItem> _queueCache = new List<MyProductionItem>();
            
            MyProductionItem? _currentProductionItem;

            int _current;

            public virtual int Current
            {
                get { return _current; }
                set
                {
                    var delta = value - _current;
                    _current = value;
                    CurrentCrafting -= delta;
                    
                    if (!Closed) 
                        return;
                    
                    CurrentAssigned = null;
                    OnClosed?.Invoke(this);
                    OnClosed = null;
                }
            }

            public ManagedProductionBlock CurrentAssigned {get; set;}

            public MyProductionItem? CurrentProductionItem
            {
                get
                {
                    _queueCache.Clear();
                    if (CurrentAssigned == null)
                        return null;

                    CurrentAssigned.GetQueue(_queueCache);
                    return _currentProductionItem = _queueCache.FirstOrDefault(a => a.BlueprintId == Item?.Blueprint?.BlueprintId);
                }
            }
            
            public MyProductionItem? ItemBeingProduced
            {
                get
                {
                    var pending = NestedRequests?.FirstOrDefault(a => a?.ItemBeingProduced != null);
                    return CurrentProductionItem ?? pending?.ItemBeingProduced;
                }
            }

            public List<ProductionRequestBase> NestedRequests { get; set; }
            public int CurrentCrafting { get; set; }

            public int CompareTo(ProductionRequestBase other) => Amount.CompareTo(other.Amount);
        }
    }
}