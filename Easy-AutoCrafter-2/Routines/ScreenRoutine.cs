using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    public partial class Program
    {
        public IEnumerator<bool> ScreenRoutine()
        {
            while (true)
            {
                try
                {
                    foreach (var cache in ScreenWidgetBase.Cache) cache.Value.Clear();

                    for (int i = 0; i < _widgets.Count; i++)
                    {
                        var current = _widgets[i];
                        current.Draw();
                    }
                }
                catch (Exception e)
                {
                    MyLog.Log(LogLevel.Error, "Screen Thread Error: " + e);
                    yield break;
                }

                yield return true;
            }
        }
    }
}