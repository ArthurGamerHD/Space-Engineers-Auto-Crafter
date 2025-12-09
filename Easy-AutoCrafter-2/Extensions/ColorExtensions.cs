using VRageMath;

namespace IngameScript
{
    public static class ColorExtensions
    {
        public static Color Invert(this Color color)
        {
            return new Color(255 - color.R, 255 - color.G, 255 - color.B);
        }

        public static Color Invert(this Color? color)
        {
            if (color == null)
                color = Color.White;
            return Invert(color.Value);
        }
    }
}