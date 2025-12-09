using System.Text;
using Sandbox.ModAPI.Interfaces;

namespace IngameScript
{
    public partial class Program
    {
                
#if DEBUG
        void Dump()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var keyValuePair in _stock)
            {
                sb.AppendLine();
                sb.AppendLine(keyValuePair.Key + ": ");
                sb.AppendLine("   -" + $"{keyValuePair.Value.NaturalName}: {keyValuePair.Value.Amount}");
                sb.AppendLine("   -" + keyValuePair.Value.Sprite);
                if (keyValuePair.Value.Blueprint != null)
                {
                    sb.AppendLine("   -" + keyValuePair.Value.Blueprint.Name);
                    foreach (var prerequisite in keyValuePair.Value.Blueprint.Prerequisites)
                    {
                        sb.AppendLine("      -" + prerequisite.Value + "x " + prerequisite.Key);
                    }
                }
                else
                {
                    sb.AppendLine("   - (No Blueprint)");
                }
            }

            Me.GetSurface(0).WriteText(sb);
        }


        void DumpBlueprints()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var keyValuePair in _definitionPerRecipe)
            {
                sb.AppendLine();
                sb.AppendLine(keyValuePair.Key + ": ");
                if (keyValuePair.Value != null)
                {
                    foreach (var producer in keyValuePair.Value)
                    {
                        sb.AppendLine("      -" + producer);
                    }
                }
                else
                {
                    sb.AppendLine("   - (No Producer)");
                }
            }

            Me.GetSurface(0).WriteText(sb);
        }

        void DumpCompanion() => Me.GetSurface(0).WriteText(Me.GetProperty("GetBlueprints").As<string>()?.GetValue(Me) ?? "error");
#endif
    }
}