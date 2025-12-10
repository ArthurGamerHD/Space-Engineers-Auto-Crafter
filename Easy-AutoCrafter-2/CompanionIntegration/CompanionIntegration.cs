using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    public partial class Program
    {
        
        public IEnumerator<bool> GetItemsFromCompanion()
        {
            try
            {
                var sb = new StringBuilder();

                _lock = true;
                sb.AppendLine(NAME + "\nFound Companion Mod");
                Echo(sb.ToString());
                var text = Me.GetProperty("GetBlueprints").As<string>()?.GetValue(Me) ?? RAW_COMPANION_DATA;
                MyLog.Log(LogLevel.Debug, "Data received from Companion Mod:\n" + text);

                MyIni ini = new MyIni();
                if (!ini.TryParse(text))
                {
                    Echo("ERROR: Fail to Parse Companion from Mod!");
                    yield break;
                }

                int version = ini.Get("Meta", "Version").ToInt32();
                string[] types = ini.Get("Meta", "Types").ToString().Split(',');
                string[] ores = ini.Get("Meta", "Ores").ToString().Split(',');
                string[] ingots = ini.Get("Meta", "Ingots").ToString().Split(',');
                
                var debugSb = new StringBuilder();
                const int BATCH_SIZE = 25;
                for (var index = 0; index < types.Length; index++)
                {
                    var type = types[index];
                    if(index % BATCH_SIZE == 0)
                        Echo(sb + $"\nLoading Translation...\n ({index + 1}/{types.Length} {type})");
                    
                    _iniKeys.Clear();
                    ini.GetKeys($"Translation_{index}", _iniKeys);
                    foreach (var key in _iniKeys)
                    {
                        var itemKey = OB + type + "/" + key.Name;
                        var localizedName = ini.Get(key).ToString().Replace("%s", key.Name)
                            .Replace("%m", "Meal Pack")
                            .Replace("%p", "Prototech");

                        var item = GetOrAddItem(itemKey);
                        item.NaturalName = localizedName;
                        debugSb.AppendLine(itemKey + "=" + localizedName);
                    }

                    yield return true;
                }

                MyLog.Log(LogLevel.Debug, "Translations Loaded from Companion Mod\n" + debugSb);

                yield return true;
                List<string> names = new List<string>();
                ini.GetSections(names);

                var db = names.Where(name =>
                    ini.ContainsKey(name, "O") &&
                    (ini.ContainsKey(name, "I") ||
                     ini.ContainsKey(name, "R"))).ToArray();

                sb.Append($"Testing recipes in {_productionBlockPerDefinition.Count} Production Blocks");
                _definitionPerRecipe.Clear();
                debugSb.Clear();
                for (var index = 0; index < db.Length; index++)
                {
                    var name = db[index];
                    MyDefinitionId id;
                    MyDefinitionId.TryParse("BlueprintDefinition", name, out id);

                    var defs = new List<string>();
                    foreach (var pb in _productionBlockPerDefinition)
                    {
                        bool canUse = pb.Value.FirstOrDefault()?.CanUseBlueprint(id) ?? false;
                        if (canUse)
                            defs.Add(pb.Value.First().BlockDefinition.ToString());
                    }
                    if(index % BATCH_SIZE == 0)
                        Echo(sb + $"\nParsing Recipe...\n{index + 1}/{names.Count}\n({id})");

                    _definitionPerRecipe[name] = defs;

                    var bpid = id.ToString().Substring(36).Trim();
                    if(_ignoredBlueprintsList.Contains(bpid))
                        continue;
                    
                    if (!defs.Any()) 
                        debugSb.AppendLine("    " + bpid);

                    Dictionary<MyItemType, double> prerequisites = new Dictionary<MyItemType, double>();
                    Dictionary<MyItemType, double> production = new Dictionary<MyItemType, double>();
                    if (ini.ContainsKey(name, "R")) // Raw Ore to Ingot
                    {
                        var rawOre = ini.Get(name, "R").ToString().Split(',');
                        foreach (var o in rawOre)
                        {
                            int oreIndex;
                            double amount;
                            var a = o.Split(':');
                            if (a.Length != 2
                                || !int.TryParse(a[0], out oreIndex)
                                || oreIndex == -1
                                || ores.Length <= oreIndex
                                || !double.TryParse(a[1], out amount))
                                continue;
                            var type = new MyItemType("MyObjectBuilder_Ore", ores[oreIndex]);
                            if (type.ToString().Length == 0)
                            {
                                MyLog.Log(LogLevel.Error, $"Fail to parse item Ore/{type}");
                                continue;
                            }

                            prerequisites.Add(type, amount);
                        }
                    }

                    else if (ini.ContainsKey(name, "I"))
                    {
                        var inputs = ini.Get(name, "I").ToString().Split(',');
                        foreach (var o in inputs)
                        {
                            string itemName;
                            string typeName;
                            double amount;
                            var a = o.Split(':');
                            if (a.Length != 2 || !double.TryParse(a[1], out amount))
                                continue;

                            int typeIndex;
                            if (!a[0].Contains('/'))
                            {
                                if (!int.TryParse(a[0], out typeIndex) || ores.Length <= typeIndex || typeIndex == -1)
                                    continue;
                                typeName = "Ingot";
                                itemName = ingots[typeIndex];
                            }
                            else
                            {
                                var b = a[0].Split('/');
                                if (!int.TryParse(b[0], out typeIndex) || types.Length <= typeIndex || b.Length != 2 ||
                                    typeIndex == -1)
                                    continue;
                                typeName = types[typeIndex];
                                itemName = b[1];
                            }


                            var type = new MyItemType($"MyObjectBuilder_{typeName}", itemName);
                            if (type.ToString().Length == 0)
                            {
                                MyLog.Log(LogLevel.Error, $"Fail to parse item {typeName}/{itemName}");
                                continue;
                            }

                            prerequisites.Add(type, amount);
                        }
                    }

                    var outputs = ini.Get(name, "O").ToString().Split(',');
                    foreach (var o in outputs)
                    {
                        string itemName;
                        string typeName;
                        double amount;
                        var a = o.Split(':');
                        if (a.Length != 2 || !double.TryParse(a[1], out amount))
                            continue;

                        int typeIndex;
                        if (!a[0].Contains('/'))
                        {
                            if (!int.TryParse(a[0], out typeIndex) || ores.Length <= typeIndex || typeIndex == -1)
                                continue;
                            typeName = "Ingot";
                            itemName = ingots[typeIndex];
                        }
                        else
                        {
                            var b = a[0].Split('/');
                            if (!int.TryParse(b[0], out typeIndex) || types.Length <= typeIndex || b.Length != 2 ||
                                typeIndex == -1)
                                continue;
                            typeName = types[typeIndex];
                            itemName = b[1];
                        }

                        var type = new MyItemType($"MyObjectBuilder_{typeName}", itemName);
                        if (type.ToString().Length == 0)
                        {
                            MyLog.Log(LogLevel.Error, $"Fail to parse item {typeName}/{itemName}");
                            continue;
                        }

                        production.Add(type, amount);
                    }
                    
                    if (production.Count == 1)
                    {
                        var output = production.First();
                        var key = output.Key.ToString();
                        Item value;
                        if (!_stock.TryGetValue(key, out value))
                        {
                            value = new Item(key, this);
                            _stock[key] = value;
                        }

                        value.Blueprint = new Blueprint(output.Key, id)
                        {
                            Prerequisites = new Dictionary<string, double>()
                        };

                        foreach (var prerequisite in prerequisites)
                            value.Blueprint.Prerequisites[prerequisite.Key.ToString()] = prerequisite.Value;
                    }

                    var rqSb = new StringBuilder();

                    foreach (var item in prerequisites)
                    {
                        rqSb.AppendLine("   -" + item.Value + "x " + item.Key);
                    }

                    foreach (var item in production)
                    {
                        rqSb.AppendLine("   +" + item.Value + "x " + item.Key);
                    }

                    MyLog.Log(LogLevel.Debug, $"{name}\n" + rqSb);

                    if(index % BATCH_SIZE == 0)
                        yield return true;
                }

                var debug = debugSb.ToString();
                if (!string.IsNullOrWhiteSpace(debug))
                {
                    MyLog.Log(LogLevel.Info,
                        "Some Blueprints failed Validation\nan adequate production block is missing or the blueprint is invalid:\n" +
                        debug);
                }

                Echo(sb.ToString());
            }
            finally
            {
                _lock = false;
            }
        }

        readonly List<string> _ignoredBlueprintsList = new List<string>()
        {
            "ScrapIngotToIronIngot",
            "ScrapToIronIngot",
            "StoneOreToIngot_Deconstruction",
            "IceToOxygen",
            "OxygenBottlesRefill",
            "HydrogenBottlesRefill",
            "ZoneChip",
            "EngineerPlushie",
            "SabiroidPlushie",
            "PrototechScrap",
            "PrototechFrame",
        };
    }
}