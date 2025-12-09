using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    public partial class Program
    {
        public static readonly List<MySprite> SpritesBuffer = new List<MySprite>();

        public abstract class ScreenWidgetBase
        {
            protected static Color
                PrimaryActive = new Color(41, 114, 150),
                Primary = new Color(31, 61, 87),
                SecondaryActive = new Color(82, 88, 110),
                Secondary = new Color(48, 54, 76),
                Error = new Color(255, 104, 83),
                TextColor = new Color(179, 185, 207),
                FrameActive = new Color(32, 34, 39),
                Frame = new Color(28, 28, 26),
                Background = Color.Black;
            
            public static readonly Dictionary<string, List<MySprite>> Cache = new Dictionary<string, List<MySprite>>();
            public readonly IMyTextSurface Surface;
            public readonly RectangleF ViewArea;

            public abstract string CacheKey { get; }

            protected ScreenWidgetBase(IMyTextSurface surface)
            {
                Surface = surface;
                Vector2 screenSize = surface.SurfaceSize;
                Vector2 textureSize = surface.TextureSize;

                Vector2 viewPos = (textureSize - screenSize) / 2f;

                ViewArea = new RectangleF(viewPos.X, viewPos.Y, screenSize.X, screenSize.Y);
            }

            protected static void SetupDrawSurface(IMyTextSurface surface)
            {
                surface.ContentType = (ContentType)3; //ContentType.SCRIPT
                surface.Script = "";
            }

            public abstract void Draw();

            public static RectangleF CreateFittedViewArea(
                RectangleF originalViewArea,
                float totalContentHeight,
                float outerPadding)
            {
                float x = originalViewArea.X + outerPadding;
                float y = originalViewArea.Y + outerPadding;
                float width = originalViewArea.Width - outerPadding * 2f;
                float height = totalContentHeight + outerPadding * 2f;

                return new RectangleF(x, y, width, height);
            }

            protected static void CreateGridRects(
                RectangleF viewArea,
                float margin,
                float padding,
                int itemCount,
                ref List<RectangleF> rects,
                int columns = 3,
                float itemHeight = 64f)
            {
                float colWidth = (viewArea.Width - padding * 2f - margin * (columns - 1)) / columns;

                for (int i = 0; i < itemCount; i++)
                {
                    int col = i % columns;
                    int row = i / columns;

                    rects.Add(new RectangleF((viewArea.X + padding) + col * (colWidth + margin),
                        (viewArea.Y + padding) + row * (itemHeight + margin),
                        colWidth,
                        itemHeight));
                }
            }

            protected static void CreateSpritesFromRects(List<RectangleF> rects, List<MySprite> sprites,
                Color? color = null, float borderPercentage = 0)
            {
                if (color == null)
                    color = Color.Gray;

                foreach (var rect in rects)
                    if (borderPercentage == 0)
                        sprites.Add(new MySprite(0, "SquareSimple", rect.Center, rect.Size, color));
                    else
                        sprites.AddRange(DrawRectangle(rect, color.Value, 1f, borderPercentage));
            }

            protected static MySprite[] DrawRectangle(RectangleF rectangle, Color color, float finalScale = 1f,
                float borderPercentage = 0.15f)
            {
                SpritesBuffer.Clear();
                Vector2 fullSize = rectangle.Size * finalScale;
                Vector2 half = fullSize * 0.5f;

                float r = Math.Min(rectangle.Width, rectangle.Height) * borderPercentage / 2f * finalScale;
                Vector2 coreSize = new Vector2(
                    fullSize.X - 2f * r,
                    fullSize.Y - 2f * r
                );

                MySprite tx = new MySprite(0, "SquareSimple", rectangle.Center, coreSize, color);

                SpritesBuffer.Add(tx);

                Vector2 cornerSize = new Vector2(r * 2f, r * 2f);

                Vector2 center = rectangle.Center;

                MySprite corner = tx;
                corner.Data = "Circle";
                corner.Size = cornerSize;

                // corners
                corner.Position = center + new Vector2(-half.X + r, -half.Y + r);
                SpritesBuffer.Add(corner);

                corner.Position = center + new Vector2(half.X - r, -half.Y + r);
                SpritesBuffer.Add(corner);

                corner.Position = center + new Vector2(-half.X + r, half.Y - r);
                SpritesBuffer.Add(corner);

                corner.Position = center + new Vector2(half.X - r, half.Y - r);
                SpritesBuffer.Add(corner);

                // edges
                MySprite edge = tx;
                edge.Data = tx.Data;

                Vector2 horizontalEdgeSize = new Vector2(fullSize.X - 2f * r, 2f * r);
                Vector2 verticalEdgeSize = new Vector2(2f * r, fullSize.Y - 2f * r);

                // top
                edge.Size = horizontalEdgeSize;
                edge.Position = center + new Vector2(0, -half.Y + r);
                SpritesBuffer.Add(edge);

                // bottom
                edge.Position = center + new Vector2(0, half.Y - r);
                SpritesBuffer.Add(edge);

                // left
                edge.Size = verticalEdgeSize;
                edge.Position = center + new Vector2(-half.X + r, 0);
                SpritesBuffer.Add(edge);

                // Right
                edge.Position = center + new Vector2(half.X - r, 0);
                SpritesBuffer.Add(edge);

                return SpritesBuffer.ToArray();
            }

            public static void SubdivideTile(
                RectangleF outer,
                out RectangleF iconRect,
                out RectangleF titleRect,
                out RectangleF descriptionRect,
                float margin,
                float padding)
            {
                outer = new RectangleF(outer.X + padding, outer.Y + padding, outer.Width - padding * 2,
                    outer.Height - padding * 2);

                float h = outer.Height;
                float w = outer.Width;

                float imgW = h * .9f;
                float y = outer.Y + (outer.Height - imgW) / 2;

                // Left Icon Area
                iconRect = new RectangleF(
                    outer.X,
                    y,
                    imgW,
                    imgW
                );

                // Right Panel
                float rightX = outer.X + imgW + margin;
                float rightW = w - imgW - margin;

                // Vertical split
                float titleH = h * 0.40f;
                float descH = h - titleH;

                titleRect = new RectangleF(
                    rightX,
                    outer.Y,
                    rightW,
                    titleH
                );

                descriptionRect = new RectangleF(
                    rightX,
                    outer.Y + titleH,
                    rightW,
                    descH
                );
            }


            public static void DrawAssembler(List<MySprite> frame, Vector2 centerPos, float scale = 1)
            {
                var yellow = new Color(255, 255, 0, 255);
                var v050 = new Vector2(0, 50);
                var assemblerFrame = new MySprite(0, SQUARE_SIMPLE, centerPos,
                    new Vector2(100, 200) * scale, new Color(180, 180, 180, 255));
                var greeble = new[]
                {
                    new MySprite(0, "Grid", new Vector2(),
                        new Vector2(100, 200), Color.White),
                    new MySprite(0, SQUARE_SIMPLE, v050,
                        new Vector2(64), new Color(96, 96, 96, 255)),
                    new MySprite(0, "LCD_Economy_Clear", new Vector2(0, -50),
                        new Vector2(80), Color.White),
                    new MySprite(0, "SquareHollow", v050,
                        new Vector2(40, 40), yellow),
                    new MySprite(0, SQUARE_SIMPLE, new Vector2(0, 48),
                        new Vector2(60, 2), yellow),
                    new MySprite(0, SQUARE_SIMPLE, new Vector2(0, 52),
                        new Vector2(60, 2), yellow),
                    new MySprite(0, "SquareHollow", v050,
                        new Vector2(64, 64), yellow)
                };

                var header = new MySprite(SpriteType.TEXT, "ASSEMBLER", centerPos, null, Color.White, "Debug",
                    TextAlignment.CENTER, 0.4f * scale);

                frame.Add(assemblerFrame.Shadow(2 * scale, Color.Black));
                frame.Add(assemblerFrame);

                frame.AddRange(greeble.Select(a =>
                    new MySprite(0, a.Data, a.Position * scale + centerPos, a.Size * scale, a.Color)));
                frame.Add(header.Shadow(0.4f * scale * 2));
                frame.Add(header);
            }

            public static void DrawCog(List<MySprite> frame, Vector2 centerPos, float scale = 1, float rotation = 0, Color? color = null,  Color? backgroundColor = null)
            {
                color = color ?? Color.White;
                backgroundColor = backgroundColor ?? Color.Black;
                var bigCog = new MySprite(0, GEAR,
                    new Vector2(0, -15) * scale + centerPos, new Vector2(64, 64) * scale,
                    color,
                    null, TextAlignment.CENTER, rotation);
                var smallCog = new MySprite(0, GEAR,
                    new Vector2(-20, 15) * scale + centerPos, new Vector2(32, 32) * scale,
                    color, null, TextAlignment.CENTER, rotation * -2);
                frame.Add(bigCog.Shadow(2 * scale, backgroundColor));
                frame.Add(smallCog.Shadow(2 * scale, backgroundColor));
                frame.Add(bigCog);
                frame.Add(smallCog);
            }
        }
    }
}