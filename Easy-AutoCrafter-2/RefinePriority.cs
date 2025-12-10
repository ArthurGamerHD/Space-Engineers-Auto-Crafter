using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    public partial class Program
    {
        const string REFINE_KEY = "Auto-Refine";

        Dictionary<string, float> _refinePriority = new Dictionary<string, float>()
        {
            { "Iron", 0.310f },
            { "Silicon", 0.310f },
            { "Nickel", 0.177f },
            { "Cobalt", 0.133f },
            { "Silver", 0.044f },
            { "Stone", 0.006f },
            { "Gold", 0.004f },
            { "Uranium", 0.004f },
            { "Magnesium", 0.003f },
            { "Platinum", 0.002f }
        };

        void LoadRefinePrioritySettings(MyIni ini = null)
        {
            if (ini == null)
            {
                ini = _ini;
                if(!_ini.TryParse(Me.CustomData))
                    return;
            }

            foreach (var ore in _refinePriority)
                if (!ini.ContainsKey(REFINE_KEY, ore.Key))
                    ini.Set(REFINE_KEY, ore.Key, ore.Value);
            
            _iniKeys.Clear();
            ini.GetKeys(REFINE_KEY, _iniKeys);

            foreach (var key in _iniKeys)
                _refinePriority[key.Name] = ini.Get(key).ToSingle();
        }
    }
}