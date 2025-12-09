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
using Sandbox.Definitions;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;
using VRageMath;

namespace IngameScript
{
    public partial class Program : MyGridProgram
    {
        bool _companion;
        int _lastOperations = 1, _currentOperations, _operations;
        double _throttle;
        
        Dictionary<string, float> _upgrades = new Dictionary<string, float>();
        bool _ran = false;

        ScreenWidgetBase _screenSaverWidgetBase;

        readonly Dictionary<string, List<IMyProductionBlock>> _productionBlockPerDefinition =
            new Dictionary<string, List<IMyProductionBlock>>();

        readonly Dictionary<string, List<string>> _definitionPerRecipe = new Dictionary<string, List<string>>();

        SortedSet<ManagedAssembler> _assemblers = new SortedSet<ManagedAssembler>(ProductionComparer.Default);
        SortedSet<ManagedRefinery> _refineries = new SortedSet<ManagedRefinery>(ProductionComparer.Default);
        List<ManagedProductionBlock> _tempProductionBlocks = new List<ManagedProductionBlock>();

        List<IMyTerminalBlock> _containers = new List<IMyTerminalBlock>();
        List<ScreenWidgetBase> _widgets = new List<ScreenWidgetBase>();


        MyIni _ini = new MyIni();
        SortedDictionary<string, Item> _stock = new SortedDictionary<string, Item>();
        SortedDictionary<string, double> _predictedCost = new SortedDictionary<string, double>();
        SortedDictionary<string, string> _translation = new SortedDictionary<string, string>();
        readonly List<MyInventoryItem> _itemsBuffer = new List<MyInventoryItem>();

        Operations _runningOperation;

        readonly StringBuilder _currentCraftSb = new StringBuilder(), _debugSb = new StringBuilder();
        string _currentCraftString;

        double _assemblerEfficiency = 1;

        Dictionary<IMyInventory, Inventory> _perInventoryStorage = new Dictionary<IMyInventory, Inventory>();

        List<ProductionRequestBase> _craftRequests = new List<ProductionRequestBase>();
        List<ProductionRequestBase> _refineRequests = new List<ProductionRequestBase>();
        IEnumerator<bool> _stateMachine;
        IEnumerator<bool> _screenStateMachine;

        int _rebuildCounter;
        int _rebuildDelay;
        int _delayCounter;
        int _delay;

        bool _translateEnabled; // Enable translate feature globally
        bool _rebuild;

        List<MyIniKey> _translationKeys = new List<MyIniKey>();

        IMyTextPanel _debugRequest, _debugCraft, _debugSort, _debugItems, _debugLog;

        bool HasAnyDebugScreen => _debugItems != null || _debugSort != null || _debugCraft != null ||
                                  _debugRequest != null || _debugLog != null;

        readonly Dictionary<string, ItemTypes> _typesMap = new Dictionary<string, ItemTypes>()
        {
            { "GasContainerObject", ItemTypes.PhysicalGunObject },
            { "OxygenContainerObject", ItemTypes.PhysicalGunObject },
        };

        readonly StringBuilder _widgetNames= new StringBuilder();

        bool _lock;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10 | UpdateFrequency.Update100 | UpdateFrequency.Once;

            foreach (var name in Enum.GetNames(typeof(Widgets))) 
                _widgetNames.Append(name + ", ");
            
            ReadConfig();
            UpdateGrid();
            _screenStateMachine = ScreenRoutine();
            //ParseBlueprintDataBase();

            _screenSaverWidgetBase = new ScreenSaver(Me.GetSurface(0));
        }

        public void Main(string argument, UpdateType updateSource)
        {
            try
            {
                _ran = false;
                if (_debugLog != null && MyLog.HasNewLogs)
                {
                    var newString = "";
                    foreach (var s in MyLog.Flush().Where(a => a.Item1 > LogLevel.Debug).Select(MyLog.AsLog))
                        newString = s + "\n\n" + newString;

                    if (!string.IsNullOrEmpty(newString))
                    {
                        var oldBuffer = new StringBuilder();
                        _debugLog.ReadText(oldBuffer);
                        _debugLog.WriteText(newString + oldBuffer);
                    }
                }

                Run(argument, updateSource);

                if (_stateMachine != null)
                {
                    Runtime.UpdateFrequency |= UpdateFrequency.Once;
                }
            }
            catch (Exception e)
            {
                MyLog.Log(LogLevel.Error, e.ToString());
                Runtime.UpdateFrequency = UpdateFrequency.Once;
            }
        }

        public void Run(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Terminal) == UpdateType.Terminal)
                UpdateGrid();
            
            if (!string.IsNullOrEmpty(argument))
                HandleArgument(argument);

            if ((updateSource & UpdateType.Once) == UpdateType.Once)
                DoOnce();

            if (_lock)
                return;

            if ((updateSource & UpdateType.Update1) == UpdateType.Update1)
                DoEveryTick();

            if ((updateSource & UpdateType.Update10) == UpdateType.Update10)
                DoEvery10Ticks();

            if ((updateSource & UpdateType.Update100) == UpdateType.Update100)
                DoEvery100Ticks();
        }

        void HandleArgument(string argument)
        {
            if (argument == "count" && _stateMachine == null)
            {
                _stateMachine = CountingRoutine();
                RunStateMachine();
            }

            switch (argument.ToLower())
            {
                case "rebuild":
                    _rebuild = true;
                    break;
                case "clear":
                    _craftRequests.Clear();
                    _refineRequests.Clear();
                    break;
#if DEBUG
                case "dumpstock":
                    Dump();
                    break;
                case "dumpblueprints":
                    DumpBlueprints();
                    break;
                case "dumpcompanion":
                    DumpCompanion();
                    break;
#endif
            }
        }


        public void DoOnce() => RunStateMachine();

        public void DoEveryTick() => RunStateMachine();

        public void DoEvery10Ticks()
        {
            if (_lastOperations > 0)
                RunOperations();

            UpdateScreens();
        }

        public void DoEvery100Ticks() => RunOperations();

        public void RunOperations()
        {
            if(_ran)
                return;

            _ran = true;
            Echo(NAME);

            if (_delayCounter >= _delay && _stateMachine == null)
            {
                if (_rebuild)
                {
                    _rebuild = false;
                    ReadConfig();
                    UpdateGrid();
                }

                _operations += _currentOperations;

                switch (_runningOperation)
                {
                    case Operations.Counting:
                        _stateMachine = CountingRoutine();
                        break;
                    case Operations.Parsing:
                        _stateMachine = QuotaRoutine();
                        break;
                    case Operations.Requests:
                        _stateMachine = RequestRoutine();
                        break;
                    case Operations.Crafting:
                        _stateMachine = CraftRoutine();
                        break;
                    case Operations.Refine:
                        _stateMachine = RefineRoutine();
                        break;
                    case Operations.Sorting:
                        _stateMachine = SortRoutine();
                        break;
                }

                _runningOperation++;

                if (_runningOperation > Operations.Sorting)
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
            Echo("Recipes Known: " + _stock.Count(a => a.Value.Blueprint != null) + $" ({_definitionPerRecipe.Count})");
            Echo("Assemblers: " + _assemblers.Count);
            Echo("Actions Count: " + _lastOperations);
            Echo("Widgets Count: " + _widgets.Count + $" ({ScreenWidgetBase.Cache.Count} Distinct)");
            Echo($"Delay: {_delayCounter}/{_delay} ");
            Echo("Running Operation: " + _runningOperation);

            if (HasAnyDebugScreen)
            {
                Echo("");
                Echo("Debug Output: ");
                Echo($" - Request: {_debugRequest?.CustomName ?? UNSET}");
                Echo($" - Craft: {_debugCraft?.CustomName ?? UNSET}");
                Echo($" - Sort: {_debugSort?.CustomName ?? UNSET}");
                Echo($" - Items: {_debugItems?.CustomName ?? UNSET}");
                Echo($" - Log: {_debugLog?.CustomName ?? UNSET}");
            }

            Echo("");
            Echo(_currentCraftString);
        }

        public void Save()
        {
        }

        public void RunStateMachine()
        {
            int i = 0;
            int target = (int)((Runtime.MaxInstructionCount - 500f) * _throttle);
            while (Runtime.CurrentInstructionCount < target && i < MAX_YIELD_PER_CYCLE)
            {
                if (_stateMachine != null)
                {
                    bool hasMoreSteps = _stateMachine.MoveNext();

                    if (!hasMoreSteps)
                    {
                        _stateMachine.Dispose();
                        _stateMachine = null;
                        break;
                    }

                    if (_lock)
                        return;
                }

                i++;
            }
        }
        
        void UpdateScreens()
        {
            _screenSaverWidgetBase.Draw();
            AnimationFrame++;
            
            int i = 0;
            int target = (int)((Runtime.MaxInstructionCount - 500f) * _throttle);
            while (Runtime.CurrentInstructionCount < target && i < MAX_YIELD_PER_CYCLE)
            {
                if (_screenStateMachine != null)
                {
                    bool hasMoreSteps = _screenStateMachine.MoveNext();
                    bool finishedCycle = _screenStateMachine.Current;
                    
                    if(finishedCycle)
                        break;
                    
                    if (!hasMoreSteps)
                    {
                        MyLog.Log(LogLevel.Error, "Screen Thread Ended Prematurely");
                        _screenStateMachine.Dispose();
                        _screenStateMachine = ScreenRoutine();
                        break;
                    }
                }

                i++;
            }
        }

        const int MAX_YIELD_PER_CYCLE = 50;

        public void UpdateGrid()
        {
            var getBlueprintFromCompanion = Me.GetProperty("GetBlueprints");
            if (getBlueprintFromCompanion != null)
            {
                _companion = true;
            }

            Echo($"Companion Mod: {(_companion ? "" : "Not ")}Found");
            _stateMachine = GetItemsFromCompanion();

            _currentCraftSb.Clear();
            _containers.Clear();
            _tempProductionBlocks.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(_containers, block =>
            {
                if (!block.IsSameConstructAs(Me) || block == Me)
                    return false;

                var productionBlock = block as IMyProductionBlock;
                if (productionBlock != null)
                    CatalogProductionBlock(productionBlock);

                var panel = block as IMyTextPanel;
                if (panel != null)
                {
                    if (panel.CustomData.ToLower().Contains($"[{SETTINGS_GENERAL.ToLower()}]"))
                        AddWidget(panel);
                    
                    var line = panel.CustomData.Split('\n').First().ToLower();
                    if (!line.StartsWith($"[{DEBUG}") || !line.EndsWith($"]"))
                        return false;

                    var id = line.Substring(DEBUG.Length + 2, line.Length - DEBUG.Length - 3);
                    switch (id)
                    {
                        case "request":
                            _debugRequest = panel;
                            break;
                        case "craft":
                            _debugCraft = panel;
                            break;
                        case "sort":
                            _debugSort = panel;
                            break;
                        case "items":
                            _debugItems = panel;
                            break;
                        case "log":
                            _debugLog = panel;
                            break;
                        default:
                            return false;
                    }

                    panel.ContentType = ContentType.TEXT_AND_IMAGE;
                }

                var provider = block as IMyTextSurfaceProvider;
                if (provider != null && ((IMyTerminalBlock)provider).CustomData.ToLower().Contains($"[{SETTINGS_GENERAL.ToLower()}]"))
                    AddWidget(provider as IMyTerminalBlock);

                return block.HasInventory & block.ShowInInventory;
            });

            _assemblers.Clear();
            _refineries.Clear();
            foreach (var productionBlock in _tempProductionBlocks)
            {
                if(productionBlock is ManagedAssembler)
                    _assemblers.Add(productionBlock as ManagedAssembler);
                else if(productionBlock is ManagedRefinery)
                    _refineries.Add(productionBlock as ManagedRefinery);
            }
            
            _tempProductionBlocks.Clear();
        }

        void AddWidget(IMyTerminalBlock panel)
        {
            _ini.TryParse(panel.CustomData);

            var provider = panel as IMyTextSurfaceProvider;
            var max = 0;
            if (provider != null)
                max = provider.SurfaceCount;

            IMyTextSurface surface = panel as IMyTextSurface;
            for (int i = 0; i < max; i++)
            {
                var setting = "Screen";
                if (provider != null)
                    setting += i;
                
                var sf = _ini.Get(SETTINGS_GENERAL, setting).ToString(i == 0 ? nameof(Widgets.CraftMonitor) : nameof(Widgets.None));

                Widgets widget;
                if (!Enum.TryParse(sf, out widget))
                {
                    int widgetInt;
                    if (!int.TryParse(sf, out widgetInt))
                        return;

                    widget = (Widgets)widgetInt;
                }

                switch (widget)
                {
                    case Widgets.CraftMonitor:
                        surface = surface ?? provider?.GetSurface(i);
                        var existing = _widgets.FirstOrDefault(a => a.Surface == surface);
                        if (existing is CraftMonitor) 
                            break;
                        if(existing != null)
                            _widgets.Remove(existing);

                        _widgets.Add(new CraftMonitor(surface, panel, this));
                        break;
                }
                
                _ini.Set(SETTINGS_GENERAL, $"Screen{i}", widget.ToString());

                if(i == 0)
                    _ini.SetComment(SETTINGS_GENERAL, $"Screen{i}", $"The current Widget to be shown on this screen\nAllowed Widgets: {_widgetNames}");
            }

            panel.CustomData = _ini.ToString();
        }

        public IEnumerator<bool> LoadStorage()
        {
            _ini.TryParse(Storage);
            yield break;
        }

        Item GetOrAddItem(string itemName)
        {
            Item item;
            if (!_stock.TryGetValue(itemName, out item))
            {
                item = new Item(itemName, this);
                _stock[itemName] = item;

                string translation;
                item.NaturalName = _translation.TryGetValue(item.KeyString, out translation)
                    ? translation
                    : item.Name;
            }

            return item;
        }

        void ReadConfig()
        {
            if (_ini.TryParse(Me.CustomData))
            {
                _assemblerEfficiency = _ini.Get(SETTINGS_GENERAL, "AssemblerEfficiency").ToDouble(1);
                _delay = _ini.Get(SETTINGS_GENERAL, "delay").ToInt32(1);
                _rebuildDelay = _ini.Get(SETTINGS_GENERAL, "rebuildDelay").ToInt32(200);
                _throttle = _ini.Get(SETTINGS_GENERAL, "throttle").ToInt16(10) / 100f;
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
                _ini.SetComment(SETTINGS_GENERAL, "AssemblerEfficiency",
                    "Defines the assembler efficiency, Only used if [Blueprints] is set");
                _ini.Set(SETTINGS_GENERAL, "throttle", (int)(_throttle * 100f));
                _ini.SetComment(SETTINGS_GENERAL, "throttle",
                    $"Defines how much % of the {Runtime.MaxInstructionCount} instructions the script should use (default 10%)");
                _ini.Set(SETTINGS_GENERAL, "delay", _delay);
                _ini.SetComment(SETTINGS_GENERAL, "delay",
                    "Defines the delay between tasks (sorting, crafting, moving items, etc)");
                _ini.Set(SETTINGS_GENERAL, "rebuildDelay", _rebuildDelay);
                _ini.SetComment(SETTINGS_GENERAL, "rebuildDelay",
                    "Defines the delay between scanning for new blocks (Cargo, Assemblers, etc)");
                Me.CustomData = _ini.ToString(true);
            }
        }
        
        public void CatalogProductionBlock(IMyProductionBlock productionBlock)
        {
            List<IMyProductionBlock> blocks;
            var def = productionBlock.BlockDefinition.ToString();
            if (!_productionBlockPerDefinition.TryGetValue(def, out blocks))
            {
                blocks = new List<IMyProductionBlock>();
                _productionBlockPerDefinition[def] = blocks;
            }

            blocks.Add(productionBlock);

            _upgrades.Clear();
            
            var assembler = productionBlock as IMyAssembler;
            if (assembler != null)
            {
                var managed = _assemblers.FirstOrDefault(a => a.Assembler == assembler) ?? new ManagedAssembler(assembler);
                managed.CalculateProductivity(_upgrades);
                _tempProductionBlocks.Add(managed);
            }

            var refinery = productionBlock as IMyRefinery;
            if (refinery != null)
            {
                var managed = _refineries.FirstOrDefault(a => a.Refinery == refinery) ?? new ManagedRefinery(refinery);
                managed.CalculateEffectiveness(_upgrades);
                _tempProductionBlocks.Add(managed);
            }
        }
    }
}