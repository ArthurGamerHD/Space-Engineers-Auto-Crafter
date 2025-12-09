using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    public partial class Program : MyGridProgram
    {
        
        static public int AnimationFrame;
        
        public class ScreenSaver : ScreenWidgetBase
        {
            List<MySprite> _mySurfaceStaticScene;

            float _scale;

            public ScreenSaver(IMyTextSurface surface) : base(surface)
            {
                SetupDrawSurface(surface);
                _scale = surface.SurfaceSize.Y / surface.SurfaceSize.X * 2;
            }

            public override string CacheKey => "Self";

            public override void Draw()
            {
                var frame = Surface.DrawFrame();

                var degrees = AnimationFrame % 36;

                if (degrees == 0)
                {
                    //Reset surface buffer in multiplayer
                    frame.Add(MySprite.CreateText(" ", "Debug", Color.Transparent));
                    AnimationFrame = 0;
                }

                if (_mySurfaceStaticScene == null)
                {
                    _mySurfaceStaticScene = new List<MySprite>();
                    DrawAssembler(_mySurfaceStaticScene, Surface.TextureSize / 2, _scale);
                }

                frame.AddRange(_mySurfaceStaticScene);
                
                SpritesBuffer.Clear();
                DrawCog(SpritesBuffer, Surface.TextureSize / 2 - (new Vector2(0, 45) * _scale), _scale, MathHelper.ToRadians(degrees * 10));
                frame.AddRange(SpritesBuffer);
                frame.Dispose();
            }
        }
    }
}