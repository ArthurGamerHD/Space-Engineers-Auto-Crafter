using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    public partial class Program
    {
        static Dictionary<string, Vector2> _fontSizeCache = new Dictionary<string, Vector2>();
        static StringBuilder _stringBuilderBuffer = new StringBuilder();

        static Vector2 GetSizeInPixel(string text, string font, float fontSize, IMyTextSurface surface)
        {
            Vector2 size;
            var key = text + font + fontSize;
            if (_fontSizeCache.TryGetValue(key, out size)) return size;
            _stringBuilderBuffer.Clear();
            _stringBuilderBuffer.Append(text);
            size = surface.MeasureStringInPixels(_stringBuilderBuffer, font, fontSize);
            _fontSizeCache[key] = size;
            return size;
        }
    }
}