using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    public partial class Program : MyGridProgram
    {
        const string BP = "BlueprintDefinition";
        const string COMP_PREFIX = "Component";
        const string OB = "MyObjectBuilder_";
        const string INGOT = "Ingot";
        const string GUN_TYPE = OB + "PhysicalGunObject";
        const string AMMO_TYPE = OB + "AmmoMagazine";
        const string SEED_ITEM = OB + "SeedItem";

        const string SETTINGS_GENERAL = "Autocrafter";
        const string SETTINGS_SORTER = "Sorter";
        const string SETTINGS_BLUEPRINT = "Blueprints";
        const string DEBUG = "debug-autocrafter";
        const string UNSET = "<Not Set>";

        const string VERSION = "Version 0.0.3";
        const int CHARACTERS_TO_SKIP = 16; // same as OB.Length;

        int _lastOperations, _currentOperations, _operations;

        readonly Dictionary<string, int> _offset = new Dictionary<string, int>
            // Some items have a hardcoded position on the grid on the definition name, normally is offset by
            // its own tier + other tools on the same grid
            // ex: Grinder tier 3 has Position003,
            // Welder tier 3, has (grinder * 4) + (handdrill * 4) + its own tier, then Position0110
            {
                { "HandDrill", 4 },
                { "Welder", 8 },
                { "FullAuto", 1 },
                { "Elite", 2 },
                { "Automatic", 3 },
                { "RapidFire", 4 },
                { "Precise", 5 },
                { "Ultimate", 6 },
                { "Basic", 7 },
                { "Advanced", 8 },
                { "Canvas", 2 },
                { "Hydrogen", 1 },
                { "Auto", 8 },
                { "Missile", 9 },
                { "Medium", 10 },
                { "LargeCalibre", 11 },
                { "Small", 12 },
                { "LargeRailgun", 13 },
            };

        readonly Dictionary<string, int> _fireworksOffset = new Dictionary<string, int>
            //  Fireworks have its offset based on the color
            {
                { "Blue", 7 },
                { "Green", 71 },
                { "Red", 72 },
                { "Yellow", 73 },
                { "Pink", 74 },
                { "Rainbow", 75 }
            };

        readonly Dictionary<string, string> _periodicTable = new Dictionary<string, string>()
            // Custom data of a block has a limit on how much text it can handle,
            // so to import a large database of recipes, reduce the element name to only 2 digits
            {
                { "Fe", "Iron" },
                { "Ni", "Nickel" },
                { "Co", "Cobalt" },
                { "Si", "Silicon" },
                { "Ag", "Silver" },
                { "Au", "Gold" },
                { "Pt", "Platinum" },
                { "Mg", "Magnesium" },
                { "U", "Uranium" },
            };

        readonly Dictionary<string, ItemTypes> _typesMap = new Dictionary<string, ItemTypes>()
        {
            { "GasContainerObject", ItemTypes.PhysicalGunObject },
            { "OxygenContainerObject", ItemTypes.PhysicalGunObject },
        };

        readonly List<string> _seeds = new List<string>()
        {
            "Fruit",
            "Grain",
            "Vegetables",
            "Mushrooms"
        };

        const string SUBTYPE_FOR_SPORES = "Mushrooms"; // Some seeds are not called "Seeds", but "Spores"


        const string GEAR = "Textures\\FactionLogo\\Others\\OtherIcon_22.dds";

        // Corner of the Shame (some items recipes name are simply too complex to "guess")
        const string ELITE_PISTOL_ITEM = "ElitePistolItem";
        const string ELITE_PISTOL_ITEM_BP = "Position0030_EliteAutoPistol";
        const string FLARE_GUN_ITEM = "FlareGunItem";
        const string FLARE_GUN_ITEM_BP = "Position0005_FlareGun";
        const string FLARE_CLIP_ITEM = "FlareClip";
        const string FLARE_CLIP_ITEM_BP = "Position0005_FlareGunMagazine";

        const string FIREWORKS_BOX_ITEM = "FireworksBox";

        void DrawAssembler(List<MySprite> frame, Vector2 centerPos, float scale = 1f, float rotation = 0f)
        {
            frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f, 0f) * scale + centerPos,
                new Vector2(100f, 200f) * scale, new Color(192, 192, 192, 255)));
            frame.Add(new MySprite(SpriteType.TEXTURE, "Grid", new Vector2(0f, 0f) * scale + centerPos,
                new Vector2(100f, 200f) * scale, new Color(255, 255, 255, 255)));
            frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f, 50f) * scale + centerPos,
                new Vector2(64f, 64f) * scale, new Color(128, 128, 128, 255)));
            frame.Add(new MySprite(SpriteType.TEXTURE, "LCD_Economy_Clear", new Vector2(0f, -50f) * scale + centerPos,
                new Vector2(80f, 80f) * scale, new Color(255, 255, 255, 255)));
            frame.Add(new MySprite(SpriteType.TEXTURE, "SquareHollow", new Vector2(0f, 50f) * scale + centerPos,
                new Vector2(40f, 40f) * scale, new Color(255, 255, 0, 255)));
            frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f, 48f) * scale + centerPos,
                new Vector2(60f, 2f) * scale, new Color(255, 255, 0, 255)));
            frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f, 52f) * scale + centerPos,
                new Vector2(60f, 2f) * scale, new Color(255, 255, 0, 255)));
            frame.Add(new MySprite(SpriteType.TEXTURE, "SquareHollow", new Vector2(0f, 50f) * scale + centerPos,
                new Vector2(64f, 64f) * scale, new Color(255, 255, 0, 255)));
            frame.Add(new MySprite(SpriteType.TEXT, $"Assembler {_assemblerEfficiency}x",
                new Vector2(-30f, 0f) * scale + centerPos, null, new Color(255, 255, 255, 255), "Debug",
                TextAlignment.LEFT, 0.4f * scale));
        }

        void DrawAssemblerOverlay(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f, float rotation = 0f)
        {
            frame.Add(new MySprite(SpriteType.TEXTURE, GEAR,
                new Vector2(0f, -60f) * scale + centerPos, new Vector2(64f, 64f) * scale, new Color(255, 255, 255, 255),
                null, TextAlignment.CENTER, rotation));
            frame.Add(new MySprite(SpriteType.TEXTURE, GEAR,
                new Vector2(-20f, -30f) * scale + centerPos, new Vector2(32f, 32f) * scale,
                new Color(255, 255, 255, 255), null, TextAlignment.CENTER, rotation * -2));
        }

        public Dictionary<string, object> AssemblerTypes = new Dictionary<string, object>();

        List<IMyTerminalBlock> _blocks = new List<IMyTerminalBlock>();

        List<IMyAssembler> _assemblers = new List<IMyAssembler>();

        List<IMyTerminalBlock> _containers = new List<IMyTerminalBlock>();


        MyIni _ini = new MyIni();
        SortedDictionary<string, Item> _stock = new SortedDictionary<string, Item>();
        SortedDictionary<string, double> _predictedCost = new SortedDictionary<string, double>();
        SortedDictionary<string, string> _translation = new SortedDictionary<string, string>();

        Operations _runningOperation;

        readonly StringBuilder _currentCraftSb = new StringBuilder(), _debugSb = new StringBuilder();

        double _assemblerEfficiency = 1;

        Dictionary<IMyInventory, Inventory> _perInventoryStorage = new Dictionary<IMyInventory, Inventory>();

        Dictionary<string, int> _craftRequest = new Dictionary<string, int>();

        readonly IMyTextSurface _mySurface;
        List<MySprite> _mySurfaceStaticScene;

        readonly List<MyInventoryItem> _items = new List<MyInventoryItem>();
        IEnumerator<bool> _stateMachine;

        int _rebuildCounter;
        int _rebuildDelay;
        int _delayCounter;
        int _delay;

        bool _translateEnabled; // Enable translate feature globally
        bool _rebuild;
        bool _clear;

        int _animationFrame;

        List<MyIniKey> _translationKeys = new List<MyIniKey>();

        IMyTextPanel _debugRequest, _debugCraft, _debugSort, _debugItems;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10 | UpdateFrequency.Update100 | UpdateFrequency.Once;
            ReadConfig();

            GetBlocks();
            ParseBlueprintDataBase();

            SetupDrawSurface(Me.GetSurface(0));

            _mySurface = Me.GetSurface(0);
        }

        public void SetupDrawSurface(IMyTextSurface surface)
        {
            // Draw background color
            surface.ScriptBackgroundColor = new Color(0, 0, 0, 255);

            // Set content type
            surface.ContentType = ContentType.SCRIPT;

            // Set script to none
            surface.Script = "";
        }


        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Once) == UpdateType.Once)
            {
                RunStateMachine();
            }

            if ((updateSource & UpdateType.Update10) == UpdateType.Update10)
            {
                var frame = _mySurface.DrawFrame();

                _animationFrame++;
                if (_animationFrame > 36)
                {
                    //Reset surface buffer in multiplayer
                    frame.Add(MySprite.CreateText(" ", "Debug", Color.Transparent));
                    _animationFrame = 0;
                }

                if (_mySurfaceStaticScene == null)
                {
                    _mySurfaceStaticScene = new List<MySprite>();
                    DrawAssembler(_mySurfaceStaticScene, _mySurface.TextureSize / 2, 2.2f,
                        MathHelper.ToRadians(_animationFrame * 10));
                }

                frame.AddRange(_mySurfaceStaticScene);
                DrawAssemblerOverlay(frame, _mySurface.TextureSize / 2, 2.2f,
                    MathHelper.ToRadians(_animationFrame * 10));

                frame.Dispose();
            }

            if ((updateSource & UpdateType.Update100) == UpdateType.Update100)
            {
                if (_delayCounter >= _delay && _stateMachine == null)
                {
                    if (_rebuild)
                    {
                        _rebuild = false;
                        ReadConfig();
                        GetBlocks();
                    }

                    if (_clear)
                    {
                        _clear = false;
                        _stateMachine = RemoveEmptyItems();
                    }

                    _operations += _currentOperations;

                    switch (_runningOperation)
                    {
                        case Operations.Counting:
                            _stateMachine = CountItems();
                            break;
                        case Operations.Parsing:
                            _stateMachine = UpdateRequests();
                            break;
                        case Operations.Requests:
                            _stateMachine = HandleRequests();
                            break;
                        case Operations.Crafting:
                            _stateMachine = HandleCraft();
                            break;
                        case Operations.Sorting:
                            _stateMachine = HandleSorting();
                            break;
                    }

                    _runningOperation++;

                    if ((int)_runningOperation > 5)
                    {
                        _lastOperations = _operations;
                        _operations = 0;
                        _runningOperation = Operations.Counting;
                        ReadConfig();
                    }

                    _delayCounter = 0;
                    RunStateMachine();
                }
                else
                {
                    ++_delayCounter;
                }

                if (_rebuildCounter > _rebuildDelay)
                {
                    _rebuild = true;
                    _rebuildCounter = 0;
                }
                else
                {
                    _rebuildCounter++;
                }

                Echo("Items tracked: " + _stock.Count);
                Echo("Recipes Known: " + _stock.Count(a => a.Value.Blueprint != null));
                Echo("Actions Count: " + _lastOperations);
                Echo($"Delay: {_delayCounter}/{_delay} ");
                Echo("Running Operation: " + _runningOperation);
                
                if (_debugItems != null || _debugSort != null || _debugCraft != null || _debugRequest != null)
                {
                    Echo("");
                    Echo("Debug Output: ");
                    Echo($" - Request: {_debugRequest?.CustomName ?? UNSET}");
                    Echo($" - Craft: {_debugCraft?.CustomName ?? UNSET}");
                    Echo($" - Sort: {_debugSort?.CustomName ?? UNSET}");
                    Echo($" - Items: {_debugItems?.CustomName ?? UNSET}");
                }
                
                Echo(_currentCraftSb.ToString());
            }

            if (argument == "count" && _stateMachine == null)
            {
                _stateMachine = CountItems();
                RunStateMachine();
            }

            switch (argument)
            {
                case "rebuild":
                    _rebuild = true;
                    break;
                case "clear":
                    _clear = true;
                    break;
            }
        }

        public void Save()
        {
        }

        public void RunStateMachine()
        {
            if (_stateMachine != null)
            {
                bool hasMoreSteps = _stateMachine.MoveNext();

                if (hasMoreSteps)
                {
                    Runtime.UpdateFrequency |= UpdateFrequency.Once;
                }
                else
                {
                    _stateMachine.Dispose();
                    _stateMachine = null;
                }
            }
        }

        public void GetBlocks()
        {
            _currentCraftSb.Clear();
            _containers.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(_containers, block =>
            {
                if (!block.IsSameConstructAs(Me))
                    return false;
                if (block is IMyAssembler)
                    InitAssembler((IMyAssembler)block);
                if (block is IMyTextPanel)
                {
                    var line = block.CustomData.Split('\n').First().ToLower();
                    if (!line.StartsWith($"[{DEBUG}") || !line.EndsWith($"]"))
                        return false;

                    var id = line.Substring(DEBUG.Length + 2, line.Length - DEBUG.Length - 3);
                    switch (id)
                    {
                        case "request":
                            _debugRequest = block as IMyTextPanel;
                            break;
                        case "craft":
                            _debugCraft = block as IMyTextPanel;
                            break;
                        case "sort":
                            _debugSort = block as IMyTextPanel;
                            break;
                        case "items":
                            _debugItems = block as IMyTextPanel;
                            break;
                        default:
                            return false;
                    }
                       
                    (block as IMyTextPanel).ContentType = ContentType.TEXT_AND_IMAGE;
                }

                return block.HasInventory & block.ShowInInventory;
            });
        }

        public IEnumerator<bool> LoadStorage()
        {
            _ini.TryParse(Storage);
            yield break;
        }

        public void AddItem(string itemName)
        {
            Item newItem = new Item(itemName, this);
            string translation;
            newItem.NaturalName = _translation.TryGetValue(newItem.KeyString, out translation)
                ? translation
                : newItem.Name;
            _stock.Add(itemName, newItem);
        }

        void ReadConfig()
        {
            if (_ini.TryParse(Me.CustomData))
            {
                _assemblerEfficiency = _ini.Get(SETTINGS_GENERAL, "AssemblerEfficiency").ToDouble(1);
                _delay = _ini.Get(SETTINGS_GENERAL, "delay").ToInt32(3);
                _rebuildDelay = _ini.Get(SETTINGS_GENERAL, "rebuildDelay").ToInt32(300);
                _translateEnabled = _ini.ContainsSection("translation");
                if (_translateEnabled)
                {
                    _translationKeys.Clear();
                    _ini.GetKeys("translation", _translationKeys);
                    foreach (var key in _translationKeys)
                    {
                        var lowerkey = key.Name.ToLower();
                        if (_translation.ContainsKey(lowerkey))
                            _translation[lowerkey] = _ini.Get(key).ToString();
                        else
                            _translation.Add(lowerkey, _ini.Get(key).ToString());
                    }
                }

                if (_ini.ContainsSection(SETTINGS_GENERAL))
                    return;

                _ini.Set(SETTINGS_GENERAL, "AssemblerEfficiency", _assemblerEfficiency);
                _ini.SetComment(SETTINGS_GENERAL, "AssemblerEfficiency","Defines the assembler efficiency, Only used if [Blueprints] is set");
                _ini.Set(SETTINGS_GENERAL, "delay", _delay);
                _ini.SetComment(SETTINGS_GENERAL, "delay","Defines the delay between tasks (sorting, crafting, moving items, etc)");
                _ini.Set(SETTINGS_GENERAL, "rebuildDelay", _rebuildDelay);
                _ini.SetComment(SETTINGS_GENERAL, "rebuildDelay","Defines the delay between scanning for new blocks (Cargo, Assemblers, etc)");
                Me.CustomData = _ini.ToString().Replace(";", "\n;");
            }
        }

        public IEnumerator<bool> CountItems()
        {
            foreach (var item in _stock.Keys)
                _stock[item].Amount = 0;

            Dictionary<IMyInventory, Inventory> perInventoryStorage = new Dictionary<IMyInventory, Inventory>();

            foreach (var container in _containers)
            {
                for (int i = 0; i < container.InventoryCount; ++i)
                {
                    var inventory = container.GetInventory(i);

                    Inventory value;
                    if (_perInventoryStorage.TryGetValue(inventory, out value))
                    {
                        perInventoryStorage[inventory] = value;
                    }
                    else
                        perInventoryStorage[inventory] = new Inventory(inventory);

                    if (inventory.ItemCount <= 0)
                    {
                        perInventoryStorage[inventory].Items.Clear();
                        continue;
                    }


                    _items.Clear();
                    inventory.GetItems(_items);
                    foreach (var item in _items)
                    {
                        string key = item.Type.ToString();
                        if (!_stock.ContainsKey(key))
                            AddItem(key);

                        var amount = item.Amount.ToIntSafe();

                        if (!perInventoryStorage[inventory].Items.ContainsKey(key))
                            perInventoryStorage[inventory].Items[key] = 0;

                        perInventoryStorage[inventory].Items[key] = amount;
                        _stock[key].Amount += amount;
                    }

                    yield return true;
                }
            }

            _perInventoryStorage = perInventoryStorage;

            if (_debugItems != null)
            {
                _debugSb.Clear();
                foreach (var item in _perInventoryStorage)
                {
                    if (item.Value.ItemsRequest.Count == 0 && item.Value.Items.Count == 0)
                        continue;

                    _debugSb.AppendLine(item.Value.CustomName);
                    if (item.Value.Items.Count != 0)
                    {
                        _debugSb.AppendLine(" Current:");
                        foreach (var itemsItem in item.Value.Items)
                        {
                            _debugSb.AppendLine("  -" + itemsItem.Key.Substring(CHARACTERS_TO_SKIP) + ":" + itemsItem.Value);
                        }
                    }

                    if (item.Value.ItemsRequest.Count != 0)
                    {
                        _debugSb.AppendLine(" Requests:");
                        foreach (var itemsItem in item.Value.ItemsRequest)
                        {
                            _debugSb.AppendLine("  -" + itemsItem.Key.Substring(CHARACTERS_TO_SKIP) + ":" + itemsItem.Value);
                        }
                    }

                    _debugSb.AppendLine();
                }

                _debugItems.WriteText(_debugSb);
            }
        }

        public IEnumerator<bool> UpdateRequests()
        {
            _craftRequest.Clear();
            yield return true;
            foreach (var inventory in _perInventoryStorage)
            {
                inventory.Value.UpdateFilters();
                foreach (var item in inventory.Value.ItemsRequest)
                {
                    if (!_craftRequest.ContainsKey(item.Key))
                    {
                        _craftRequest[item.Key] = 0;
                    }

                    _craftRequest[item.Key] += item.Value;
                }

                yield return true;
            }

            if (_debugRequest != null)
            {
                _debugSb.Clear();
                foreach (var request in _craftRequest)
                    _debugSb.AppendLine(request.Key.Substring(CHARACTERS_TO_SKIP) + ": " + request.Value);

                _debugRequest.WriteText(_debugSb);
            }
        }

        public IEnumerator<bool> HandleRequests()
        {
            foreach (var current in _perInventoryStorage.Values)
            {
                foreach (var request in current.ItemsRequest)
                {
                    int amount = request.Value;
                    int currentCount;
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
                                        a.MyInventory.CanItemsBeAdded(amount, type));

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

        public IEnumerator<bool> HandleCraft()
        {
            _currentCraftSb.Clear();
            if (_craftRequest.Count <= 0)
            {
                _debugCraft?.WriteText("No Pending Requests");
                yield break;
            }

            StringBuilder debugSb = null;
            if (_debugItems != null)
                debugSb = new StringBuilder();

            var hasCraft = false;
            var failCraft = false;

            var requests = new List<CraftRequest>();

            foreach (var item in _craftRequest.Where(item =>
                         _stock.ContainsKey(item.Key) && _stock[item.Key].Amount < item.Value))
            {
                debugSb?.Append($"{item.Value}x {item.Key.Substring(CHARACTERS_TO_SKIP)}: ");

                if (_stock[item.Key].Blueprint == null)
                {
                    failCraft = true;
                    debugSb?.Append($"No Recipe Found!");
                    continue;
                }

                requests.Add(new CraftRequest(item.Value - _stock[item.Key].Amount, _stock[item.Key]));
                debugSb?.Append($"Using Recipe {_stock[item.Key].Blueprint.DisplayName}");
                debugSb?.AppendLine();
                hasCraft = true;
            }

            if (!hasCraft)
            {
                _debugCraft?.WriteText((failCraft)
                    ? "Unable to Process Pending Requests!"
                    : "All Requests Fulfilled!" + "\n" + debugSb);
                yield break;
            }


            _currentCraftSb.AppendLine("Current Crafting:");

            requests.Sort((a, b) => a.CompareTo(b));

            var activeAssemblers = _assemblers.Where(a => !a.IsQueueEmpty && a.Mode == MyAssemblerMode.Assembly);
            List<MyProductionItem> blueprintQueue = new List<MyProductionItem>();
            foreach (var assembler in activeAssemblers)
            {
                List<MyProductionItem> queue = new List<MyProductionItem>();
                assembler.GetQueue(queue);
                blueprintQueue.AddRange(queue);
            }

            foreach (var request in blueprintQueue.Select(productionItem =>
                         requests.FirstOrDefault(a =>
                             a.Blueprint != null && a.Blueprint.BlueprintId == productionItem.BlueprintId)))
                requests.Remove(request); // don't try to craft the same item twice

            yield return true;

            var sortedAssemblers = new List<MyTuple<IMyAssembler, float>>();

            foreach (var assembler in _assemblers.Where(a => a.IsQueueEmpty && a.Mode == MyAssemblerMode.Assembly))
            {
                var upgradableBlock = assembler as IMyUpgradableBlock;
                if (upgradableBlock == null)
                {
                    sortedAssemblers.Add(new MyTuple<IMyAssembler, float>(assembler, 0));
                    continue;
                }

                Dictionary<string, float> upgrades = new Dictionary<string, float>();
                upgradableBlock.FillUpgradesDictionary(upgrades);
                sortedAssemblers.Add(new MyTuple<IMyAssembler, float>(assembler, upgrades["Productivity"]));

                //sortedAssemblers.Add(new MyTuple<IMyAssembler, float>(assembler, 1)); //Falback to 1
            }

            sortedAssemblers.Sort((a, b) => b.Item2.CompareTo(a.Item2));

            foreach (var request in requests)
            {
                _currentCraftSb.Append(" -" + request.Item.Name);

                if (request.Blueprint.Prerequisites != null)
                {
                    _currentCraftSb.Append(": ");
                    _predictedCost.Clear();

                    foreach (var item in request.Blueprint.Prerequisites)
                    {
                        _predictedCost[item.Key] = item.Value * request.Amount;
                        _currentCraftSb.Append($"{item.Value:0.00}" + ":" + item.Key.Split('/')[1] + " ");
                    }
                }

                _currentCraftSb.AppendLine("");

                var maximum = request.Amount;

                if (_predictedCost.Any(a => !_stock.ContainsKey(a.Key) || a.Value > _stock[a.Key].Amount))
                {
                    foreach (var item in _predictedCost)
                    {
                        Item value;
                        double required;
                        if (!_stock.TryGetValue(item.Key, out value)
                            || !request.Blueprint.Prerequisites.TryGetValue(item.Key, out required)
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

                var available = sortedAssemblers.Where(assembler =>
                    assembler.Item1.CanUseBlueprint(request.Blueprint.BlueprintId) &&
                    assembler.Item1.IsQueueEmpty).ToArray();

                if (!available.Any())
                {
                    available = sortedAssemblers.Where(assembler =>
                        assembler.Item1.CanUseBlueprint(request.Blueprint.BlueprintId)).ToArray();
                }

                foreach (var assembler in available)
                {
                    _currentOperations++;
                    assembler.Item1.AddQueueItem(request.Blueprint.BlueprintId, (double)maximum);

                    foreach (var cost in _predictedCost)
                    {
                        _stock[cost.Key].Amount -= (int)(cost.Value + 0.5);
                    }

                    break;
                }
            }

            yield return true;

            _debugCraft.WriteText(debugSb);
        }

        public IEnumerator<bool> HandleSorting()
        {
            Dictionary<ItemTypes, List<Inventory>> perTypeStorages = new Dictionary<ItemTypes, List<Inventory>>();

            IEnumerable<Inventory> sortableInventory = _perInventoryStorage.Values.Where(a => a.Sort).ToArray();

            List<MyInventoryItem> items = new List<MyInventoryItem>();

            foreach (var item in Enum.GetValues(typeof(ItemTypes)))
            {
                if ((ItemTypes)item == ItemTypes.None)
                    continue;

                perTypeStorages[(ItemTypes)item] = new List<Inventory>();
            }

            yield return true;

            foreach (var inventory in sortableInventory)
            {
                foreach (var type in perTypeStorages.Keys.Where(type => inventory.AllowedTypes.HasFlag(type)))
                    perTypeStorages[type].Add(inventory);
            }

            yield return true;

            foreach (var list in perTypeStorages.Values)
            {
                list.Sort((a, b) => a.CompareTo(b));
            }

            yield return true;

            foreach (var inventory in sortableInventory)
            {
                items.Clear();
                inventory.MyInventory.GetItems(items);
                foreach (var item in items)
                {
                    var move = item.Amount;
                    var itemFullType = item.Type.ToString();

                    var itemType = itemFullType.Split('_')[1].Split('/')[0];

                    ItemTypes type;
                    if (!Enum.TryParse(itemType, out type) &&  !_typesMap.TryGetValue(itemType, out type))
                        continue;


                    var targets = perTypeStorages[type]
                        .Where(a => a.MyInventory.CanTransferItemTo(inventory.MyInventory, item.Type)).ToList();


                    if (inventory.AllowedTypes.HasFlag(type))
                    {
                        double totalMass = 0;

                        foreach (var target in targets)
                        {
                            totalMass += (double)target.MyInventory.CurrentMass;
                        }

                        totalMass /= targets.Count;

                        if ((double)inventory.MyInventory.CurrentMass < totalMass * 1.05d)
                            continue;
                    }

                    int keep;
                    if (inventory.ItemsRequest.TryGetValue(itemFullType, out keep))
                        move -= keep;

                    if (move > 0)
                    {
                        move = (MyFixedPoint)Math.Min((double)item.Amount, 10000d);

                        targets.Sort((a, b) =>
                            a.MyInventory.CurrentMass.ToIntSafe().CompareTo(b.MyInventory.CurrentMass.ToIntSafe()));

                        foreach (var dstInventory in targets)
                        {
                            if (dstInventory.MyInventory.TransferItemFrom(inventory.MyInventory, item, move))
                            {
                                _currentOperations++;
                                _currentCraftSb.AppendLine(
                                    $"Moved {move} {item.Type.SubtypeId} {item.Type.TypeId.Split('_').Last()} from {inventory.CustomName} to {dstInventory.CustomName}");
                                break;
                            }
                        }

                        yield return true;
                    }
                }

                yield return true;
            }

            yield return true;

            if (_debugSort != null)
            {
                _debugSb.Clear();

                foreach (var item in sortableInventory)
                {
                    if(item.AllowedTypes == ItemTypes.None)
                        continue;

                    _debugSb.AppendLine(item.CustomName);
                    _debugSb.AppendLine("  -" + item.AllowedTypes);
                    _debugSb.AppendLine();
                }

                if (_debugSb.Length == 0) 
                    _debugSb.AppendLine("Sorting not setup");

                _debugSort.WriteText(_debugSb);
            }
        }

        public IEnumerator<bool> RemoveEmptyItems()
        {
            var keys = _stock.Keys.ToList();

            for (int i = _stock.Keys.Count - 1; i >= 0; i--)
            {
                if (_stock[keys[i]].Amount == 0)
                {
                    _stock.Remove(keys[i]);
                    yield return true;
                }
            }
        }

        public void InitAssembler(IMyAssembler assembler)
        {
            _assemblers.Add(assembler);
            var items = new List<MyItemType>();
            if (AssemblerTypes.ContainsKey(assembler.BlockDefinition.SubtypeId))
                return;

            items.Clear();
            assembler.OutputInventory.GetAcceptedItems(items);

            Dictionary<string, MyDefinitionId> blueprints = new Dictionary<string, MyDefinitionId>();

            foreach (var item in items)
            {
                MyDefinitionId id;
                if (MyDefinitionId.TryParse(BP, item.SubtypeId, out id) &&
                    assembler.CanUseBlueprint(id) || // not a regular item
                    MyDefinitionId.TryParse(BP, item.SubtypeId + COMP_PREFIX, out id) &&
                    assembler.CanUseBlueprint(id) || // not a component
                    ParseSpecialNames(item, assembler, out id)) //if that failed, I don't know what that is
                {
                    var key = item.TypeId + "/" + item.SubtypeId;

                    if (!_stock.ContainsKey(key))
                        AddItem(key);

                    _stock[key].Blueprint = new ItemBlueprint(item, id);
                    blueprints[item.SubtypeId] = item;
                }
            }

            AssemblerTypes[assembler.BlockDefinition.SubtypeId] = blueprints;

            items.Clear();
        }

        bool ParseSpecialNames(MyItemType item, IMyAssembler assembler, out MyDefinitionId id) // this WILL break sometime in the future
        {
            string name = item.SubtypeId;
            int offset = _offset.FirstOrDefault(a => item.SubtypeId.StartsWith(a.Key)).Value;
            int tier = 1;
            switch (item.TypeId)
            {
                case GUN_TYPE:
                    switch (item.SubtypeId)
                    {
                        case ELITE_PISTOL_ITEM:
                            return MyDefinitionId.TryParse(BP, ELITE_PISTOL_ITEM_BP, out id) &&
                                   assembler.CanUseBlueprint(id);
                        case FLARE_GUN_ITEM:
                            return MyDefinitionId.TryParse(BP, FLARE_GUN_ITEM_BP, out id) &&
                                   assembler.CanUseBlueprint(id);

                        default:
                            name = item.SubtypeId.Replace("Item", "");
                            var tierString = item.SubtypeId.Replace("Item", "").Last().ToString();
                            if (!int.TryParse(tierString, out tier))
                                tier = 1;
                            break;
                    }

                    break;

                case AMMO_TYPE:
                    if (item.SubtypeId.StartsWith(FIREWORKS_BOX_ITEM))
                    {
                        var color = item.SubtypeId.Replace(FIREWORKS_BOX_ITEM, "");
                        return MyDefinitionId.TryParse(BP,
                                   "Position000" + _fireworksOffset[color] + "_" + item.SubtypeId,
                                   out id) &&
                               assembler.CanUseBlueprint(id);
                    }

                    if (item.SubtypeId.StartsWith(FLARE_CLIP_ITEM))
                    {
                        return MyDefinitionId.TryParse(BP, FLARE_CLIP_ITEM_BP, out id) &&
                               assembler.CanUseBlueprint(id);
                    }

                    break;

                case SEED_ITEM:
                    string prefix = "Seeds_";

                    if (item.SubtypeId == SUBTYPE_FOR_SPORES)
                        prefix = "Spores_";

                    name = prefix + item.SubtypeId;
                    tier = _seeds.IndexOf(item.SubtypeId) + 1;
                    break;
            }

            var candidateString = $"Position{tier + offset:D3}0_" + name;
            return MyDefinitionId.TryParse(BP, candidateString, out id) &&
                   assembler.CanUseBlueprint(id);
        }

        void ParseBlueprintDataBase()
        {
            if (!_ini.TryParse(Me.CustomData))
                return;

            var keys = new List<MyIniKey>();
            _ini.GetKeys(SETTINGS_BLUEPRINT, keys);

            foreach (var key in keys)
            {
                //Assuming that every item has an exclusive subtype id for the assemblers
                var item = _stock.FirstOrDefault(a => a.Key.EndsWith(key.Name)).Value;

                if (item?.Blueprint == null)
                    continue;

                var items = new Dictionary<string, double>();
                var value = _ini.Get(key).ToString();
                var prerequisites = value.Split(' ');

                foreach (var prerequisite in prerequisites)
                {
                    var stg = prerequisite.Split(':');
                    if (stg.Length != 2)
                        return;
                    string name;
                    if (!_periodicTable.TryGetValue(stg[1], out name))
                        name = stg[1]; //prototech stuff

                    double amount;
                    double.TryParse(stg[0], out amount);
                    items[OB + INGOT + "/" + name] = amount / _assemblerEfficiency;
                }

                item.Blueprint.Prerequisites = items;
            }
        }


        public class ItemBlueprint
        {
            public string Name;
            public string DisplayName;

            public Dictionary<string, double> Prerequisites;
            public string Result;

            public MyDefinitionId BlueprintId;

            public ItemBlueprint(MyItemType item, MyDefinitionId blueprintId)
            {
                Name = blueprintId.ToString();
                DisplayName = blueprintId.SubtypeName;
                Result = item.ToString();
                BlueprintId = blueprintId;
            }
        }


        class Item
        {
            public Item(string itemType, Program program, int amount = 0)
            {
                var temp = itemType.Split('/');
                Sprite = itemType;
                Name = NaturalName = temp[1];
                ItemType = temp[0];
                Amount = amount;
                KeyString = program._translateEnabled ? itemType.Substring(CHARACTERS_TO_SKIP).ToLower() : "";
            }

            public string KeyString;
            public int Amount;
            public string Sprite;
            public string Name;
            public string ItemType;
            public string NaturalName;
            public ItemBlueprint Blueprint { get; set; }
        }

        class CraftRequest : IComparable<CraftRequest>
        {
            public CraftRequest(int amount, Item item)
            {
                Amount = amount;
                Item = item;
                Blueprint = item.Blueprint;
            }

            public Item Item;
            public ItemBlueprint Blueprint;
            public int Amount;

            public int CompareTo(CraftRequest other) => Amount.CompareTo(other.Amount);
        }

        class Inventory : IComparable<Inventory>
        {
            public readonly Dictionary<string, int> Items = new Dictionary<string, int>();
            public readonly Dictionary<string, int> ItemsRequest = new Dictionary<string, int>();

            public string CustomName => _owner.CustomName;

            public int Priority { get; private set; } = 0;

            public bool Sort { get; private set; } = false;

            public ItemTypes AllowedTypes = ItemTypes.None;

            public IMyInventory MyInventory { get; private set; }

            IMyTerminalBlock _owner;

            public Inventory(IMyInventory inventory)
            {
                _owner = (IMyTerminalBlock)inventory.Owner;
                MyInventory = inventory;
                UpdateFilters();
            }

            public void UpdateFilters()
            {
                ItemsRequest.Clear();

                var ini = new MyIni();

                var block = _owner as IMyProductionBlock;
                if (block != null)
                    Sort = block.OutputInventory == MyInventory;

                if (!(_owner is IMyCargoContainer) &&
                    !(_owner is IMyShipConnector) && // why do they try to steal every item from my assemblers?
                    !(_owner is IMyConveyorSorter))
                    return;

                Sort = true;
                ini.TryParse(_owner.CustomData);

                Priority = ini.Get(SETTINGS_SORTER, "priority").ToInt32();

                var allowedTypes = ItemTypes.None;

                if (ini.ContainsKey(SETTINGS_SORTER, "filter"))
                {
                    var filters = ini.Get(SETTINGS_SORTER, "filter").ToString("None").Split(',');
                    foreach (var filter in filters)
                    {
                        ItemTypes type;
                        if (Enum.TryParse(filter, out type))
                        {
                            allowedTypes |= type;
                        }
                    }
                }

                AllowedTypes = allowedTypes;

                List<MyIniKey> keys = new List<MyIniKey>();

                ini.GetKeys("requests", keys);

                foreach (var key in keys)
                {
                    try
                    {
                        var type = key.Name.StartsWith(OB) ? key.Name : OB + key.Name;
                        MyItemType.Parse(type);
                        ItemsRequest.Add(type, ini.Get(key).ToInt32());
                    }
                    catch
                    {
                        _owner.CustomData = _owner.CustomData.Replace(key.Name, "#Invalid Blueprint:\n#" + key.Name);
                    }
                }
            }

            public int CompareTo(Inventory other) => other.Priority.CompareTo(Priority);
        }


        [Flags]
        public enum ItemTypes
        {
            None = 0,
            Ore = 1,
            Ingot = 2,
            Component = 4,
            AmmoMagazine = 8,
            PhysicalGunObject = 16,
            SeedItem = 32,
            ConsumableItem = 128,
        }

        public enum Operations
        {
            Initializing,
            Counting,
            Parsing,
            Requests,
            Crafting,
            Sorting
        }
    }
}