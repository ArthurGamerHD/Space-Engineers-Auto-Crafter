using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    public partial class Program
    {
        public abstract class ManagedProductionBlock
        {
            public abstract float Efficiency { get; protected set; }
            public abstract IMyProductionBlock ProductionBlock { get; }
            
            public ProductionRequestBase Request { get; set; }
            
            public IMyInventory InputInventory => ProductionBlock.InputInventory;
            
            public virtual bool IsBusy => Request != null && !Request.Closed;
            
            public bool CanUseBlueprint(MyDefinitionId blueprintBlueprintId) => ProductionBlock.CanUseBlueprint(blueprintBlueprintId);
            
            public void AddQueueItem(MyDefinitionId blueprintBlueprintId, double maximum) => ProductionBlock.AddQueueItem(blueprintBlueprintId, maximum);

            public void GetQueue(List<MyProductionItem> queueCache) => ProductionBlock.GetQueue(queueCache);

            public virtual void Tick(Program program)
            {
                if (Request?.Closed ?? false)
                    Request = null;
            }
        }

        public class ManagedAssembler : ManagedProductionBlock
        {
            public override float Efficiency { get; protected set; } = 0;

            public ManagedAssembler(IMyAssembler assembler)
            {
                Assembler = assembler;
                assembler.ClearQueue();
                assembler.CooperativeMode = false;
            }

            public override IMyProductionBlock ProductionBlock => Assembler;

            public IMyAssembler Assembler { get; }
            public bool IsQueueEmpty => Assembler.IsQueueEmpty;

            public void CalculateProductivity(Dictionary<string, float> upgrades)
            {
                var upgradableBlock = Assembler as IMyUpgradableBlock;
                if (upgradableBlock == null)
                    return;

                upgradableBlock.FillUpgradesDictionary(upgrades);
                Efficiency = upgrades["Productivity"];
            }
        }

        public class ManagedRefinery : ManagedProductionBlock
        {
            public ManagedRefinery(IMyRefinery refinery)
            {
                Refinery = refinery;
            }

            public override float Efficiency { get; protected set; }
            public override IMyProductionBlock ProductionBlock => Refinery;

            public IMyRefinery Refinery { get; }
            public override bool IsBusy => InputInventory.VolumeFillFactor > 0.01f || base.IsBusy;

            public void CalculateEffectiveness(Dictionary<string, float> upgrades)
            {
                var upgradableBlock = Refinery as IMyUpgradableBlock;
                if (upgradableBlock == null)
                    return;

                upgradableBlock.FillUpgradesDictionary(upgrades);
                Efficiency = upgrades["Effectiveness"];
            }

            public override void Tick(Program program)
            {
                base.Tick(program);

                Refinery.UseConveyorSystem = !IsBusy;
                
                if(Request != null && Refinery.InputInventory.VolumeFillFactor < 0.01f)
                    program.MoveItemToRefinery(Request.Item, this);
            }
        }

        class ProductionComparer : IComparer<ManagedProductionBlock>
        {
            public static ProductionComparer Default = new ProductionComparer();
            
            public int Compare(ManagedProductionBlock x, ManagedProductionBlock y)
            {
                if (x != null && y != null)
                {
                    var cmp = y.Efficiency.CompareTo(x.Efficiency);
                    if (cmp != 0) 
                        return cmp;
                }

                return string.Compare(x?.ProductionBlock.CustomName, y?.ProductionBlock.CustomName, StringComparison.Ordinal);
            }
        }
    }
}