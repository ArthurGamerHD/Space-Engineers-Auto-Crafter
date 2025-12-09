using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    public partial class Program
    {
       
        public class Blueprint
        {
            public string Name;
            public string DisplayName;

            public Dictionary<string, double> Prerequisites;
            public string Result;

            public MyDefinitionId BlueprintId;

            public Blueprint(MyItemType item, MyDefinitionId blueprintId)
            {
                Name = blueprintId.ToString();
                DisplayName = blueprintId.SubtypeName;
                Result = item.ToString();
                BlueprintId = blueprintId;
            }
        }
    }
}