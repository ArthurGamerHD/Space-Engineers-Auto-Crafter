using VRage.Game.GUI.TextPanel;
using VRageMath;
using Color = VRageMath.Color;

namespace IngameScript
{
    public static class RectangleExtensions
    {
        public static Rectangle ToRectangle(this RectangleF rectangle) => new Rectangle((int)rectangle.X, (int)rectangle.Y, (int)rectangle.Width, (int)rectangle.Height);
    }
}