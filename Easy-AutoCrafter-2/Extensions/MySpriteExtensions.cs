using VRage.Game.GUI.TextPanel;
using Color = VRageMath.Color;

namespace IngameScript
{
    public static class MySpriteExtensions
    {
        public static MySprite Shadow(this MySprite sprite, float offset, Color? color = null)
        {
            color = color ?? sprite.Color.Invert();
            return new MySprite(sprite.Type,
                sprite.Data,
                sprite.Position + offset,
                sprite.Size,
                color,
                sprite.FontId,
                sprite.Alignment,
                sprite.RotationOrScale);
        }
        
        public static MySprite[] Shadow(this MySprite[] sprites, float offset, Color? color = null)
        {
            Program.SpritesBuffer.Clear();
            foreach (var sprite in sprites)
            {
                color = color ?? sprite.Color.Invert();
                Program.SpritesBuffer.Add(new MySprite(sprite.Type,
                    sprite.Data,
                    sprite.Position + offset,
                    sprite.Size,
                    color,
                    sprite.FontId,
                    sprite.Alignment,
                    sprite.RotationOrScale));;
            }
            return Program.SpritesBuffer.ToArray();
        }
    }
}