using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using IMyProgrammableBlock = Sandbox.ModAPI.IMyProgrammableBlock;
using IMyShipConnector = Sandbox.ModAPI.IMyShipConnector;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;

namespace Easy_AutoCrafter_2_Companion
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MyProgrammableBlock), useEntityUpdate: true)]
    public class PBInterface : MyGameLogicComponent
    {
        const string SETTINGS_GENERAL = "Autocrafter";

        static bool _initialized;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            DoOnce();
        }

        public static void DoOnce()
        {
            if (_initialized)
                return;

            CreateProperty();
            _initialized = true;
        }

        static bool CanDrop(IMyTerminalBlock b)
        {
            var connector = b as IMyShipConnector;
            return connector != null && CanDrop(connector);
        }

        static bool CanDrop(IMyShipConnector connector)
        {
            return connector.HasInventory && !connector.GetInventory(0).Empty() &&
                   !((connector.Status != MyShipConnectorStatus.Unconnected || !connector.IsFunctional ||
                      connector.Closed));
        }

        static void CreateProperty()
        {
            var a = MyAPIGateway.TerminalControls.CreateProperty<string, IMyProgrammableBlock>("GetBlueprints");
            a.Enabled = b => true;
            a.Getter = Getter;
            a.Setter = Setter;
            MyAPIGateway.TerminalControls.AddControl<IMyProgrammableBlock>(a);
        }

        static void Setter(IMyTerminalBlock arg1, string arg2)
        {
        }

        static string Getter(IMyTerminalBlock arg)
        {
            var currentOre = 0;
            var currentType = 0;
            var currentIngot = 0;
            List<string> types = new List<string>();
            List<string> ores = new List<string>();
            List<string> ingots = new List<string>();

            MyIni ini = new MyIni();
            int version = 1;
            ini.TryParse(arg.CustomData);
            version = ini.Get("SETTINGS_GENERAL", "Version").ToInt32(1);

            var allItems = MyDefinitionManager.Static.GetAllDefinitions();

            ini.Clear();
            ini.Set("Meta", "Types", "placeholder");
            ini.Set("Meta", "Ores", "placeholder");
            ini.Set("Meta", "Ingots", "placeholder");

            foreach (var def in allItems)
            {
                if (def is MyPhysicalItemDefinition)
                {
                    var ob = def.Id;


                    var type = ob.TypeId.ToString().Split('_')[1].Replace("Definition", "").Trim();
                    var name = ob.SubtypeId.String;
                    if (type == "TreeObject" || name == "Organic" || name.StartsWith("GoodAI"))
                        continue;

                    if (type == "Ore")
                    {
                        ores.Add(name);
                        currentOre = (currentOre + 1);
                    }

                    if (type == "Ingot")
                    {
                        ingots.Add(name);
                        currentIngot = (currentIngot + 1);
                    }

                    if (!types.Contains(type))
                    {
                        types.Add(type);
                        currentType = (currentType + 1);
                    }

                    type = types.IndexOf(type).ToString();
                    var dn = def.DisplayNameText ?? "Unknown";
                    dn = dn.Replace(name, "%s").Replace("Meal Pack", "%m").Replace("Prototech", "%p");
                    try
                    {
                        if (dn != "@")
                            ini.Set($"Translation_{type}", name, dn);
                    }
                    catch (Exception e)
                    {
                        MyLog.Default.WriteLine(e);
                    }
                }
            }

            var recipes = MyDefinitionManager.Static.GetBlueprintDefinitions();
            foreach (var bp in recipes)
            {
                try
                {
                    var bpName = bp.Id.SubtypeId.ToString();
                    var result = bp.Results[0];
                    if (bpName.Contains("/"))
                        continue;

                    var rSType = result.Id.TypeId.ToString().Split('_')[1].Replace("Definition", "").Trim();

                    StringBuilder sb = new StringBuilder();
                    if (rSType == "Ingot")
                    {
                        ini.Set(bpName, "Ore", "");
                        
                        foreach (var results in bp.Results)
                            sb.Append($"{ingots.IndexOf(results.Id.SubtypeId.ToString())}: {results.Amount},");
                        ini.Set(bpName, "O", sb.ToString());
                        sb.Clear();

                        foreach (var prerequisite in bp.Prerequisites)
                            sb.Append($"{ores.IndexOf(prerequisite.Id.SubtypeId.ToString())}: {prerequisite.Amount},");
                        ini.Set(bpName, "R", sb.ToString());
                    }
                    else
                    {
                        foreach (var results in bp.Results)
                        {
                            var type = results.Id.ToString().Substring(16);
                            sb.Append($"{(!type.StartsWith("Ingot") ? type.Replace(type.Split('/')[0], types.IndexOf(type.Split('/')[0]).ToString()) : ingots.IndexOf(results.Id.SubtypeId.ToString()).ToString())}: {results.Amount},");
                        }

                        ini.Set(bpName, "O", sb.ToString().Substring(0, sb.Length - 1));

                        sb.Clear();
                        foreach (var prerequisite in bp.Prerequisites)
                        {
                            var type = prerequisite.Id.ToString().Substring(16);
                            sb.Append($"{(!type.StartsWith("Ingot") ? type.Replace(type.Split('/')[0], types.IndexOf(type.Split('/')[0]).ToString()) :  ingots.IndexOf(prerequisite.Id.SubtypeId.ToString()).ToString())}: {prerequisite.Amount},");
                        }

                        ini.Set(bpName, "I", sb.ToString().Substring(0, sb.Length - 1));
                    }
                }
                catch (Exception e)
                {
                    MyLog.Default.WriteLine(bp.Id.SubtypeId + ": " + e);
                }
            }

            {
                var sb = new StringBuilder();
                foreach (var bp in ores)
                    sb.Append(bp + ",");
                ini.Set("Meta", "Ores", sb.ToString());
                sb.Clear();

                foreach (var bp in ingots)
                    sb.Append(bp + ",");
                ini.Set("Meta", "Ingots", sb.ToString());
                sb.Clear();

                foreach (var bp in types)
                    sb.Append(bp + ",");
                ini.Set("Meta", "types", sb.ToString());
                sb.Clear();
            }


            return ini.ToString();
        }
    }
}