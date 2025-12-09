using System.Text;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    public static class MyIniExtensions
    {
        public static string ToString(this MyIni ini, bool addEmptyLines)
        {
            if(!addEmptyLines)
                return ini.ToString();
            
            var sb = new StringBuilder();
            var lines = ini.ToString().Split('\n');

            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (line.StartsWith(";") && !lines[index-1].StartsWith(";")) 
                    sb.AppendLine();

                sb.AppendLine(line);
            }

            return sb.ToString();
        }
    }
}