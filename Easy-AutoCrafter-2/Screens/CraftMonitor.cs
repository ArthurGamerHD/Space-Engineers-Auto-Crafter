using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    public partial class Program : MyGridProgram
    {
        public class CraftMonitor : ScreenWidgetBase
        {
            public readonly IMyTerminalBlock Parent;
            List<RectangleF> _rects = new List<RectangleF>();
            Dictionary<IVisualItem, bool> _requestItems = new Dictionary<IVisualItem, bool>();
            Program _program;
            int _currentRow = 0;
            float[] _currentCursorPosY;
            float _rowWidth;

            public CraftMonitor(IMyTextSurface surface, IMyTerminalBlock parent, Program program) : base(surface)
            {
                SetupDrawSurface(surface);
                Parent = parent;
                _program = program;
                _currentRow = 0;
                var maxRows = (int)Math.Round(ViewArea.Width / 512, MidpointRounding.AwayFromZero);
                _currentCursorPosY = new float [maxRows];
                _rowWidth = ViewArea.Width / maxRows;
            }

            public override string CacheKey => nameof(CraftMonitor) + ViewArea;

            public override void Draw()
            {
                List<MySprite> sprites;

                if (!Cache.TryGetValue(CacheKey, out sprites))
                {
                    sprites = new List<MySprite>();
                    Cache[CacheKey] = sprites;
                }

                var frame = Surface.DrawFrame();
                if (sprites.Any())
                {
                    frame.AddRange(sprites);
                    frame.Dispose();
                    return;
                }

                for (int i = 0; i < _currentCursorPosY.Length; i++)
                {
                    _currentCursorPosY[i] = ViewArea.Y;
                }


                foreach (var request in _program.GetAllRequests())
                {
                    var craftRequest = request as CraftRequest;

                    _currentRow = _currentCursorPosY.Length > 1 ? FindIndexOfSmallest(_currentCursorPosY) : 0;
                    var x = ViewArea.X + _rowWidth * _currentRow;
                    if (craftRequest != null)
                        DrawCraftTree(sprites, craftRequest,
                            new RectangleF(x, _currentCursorPosY[_currentRow], _rowWidth,
                                ViewArea.Height - (_currentCursorPosY[_currentRow] - ViewArea.Y)));
                }

                frame.AddRange(sprites);
                frame.Dispose();
            }


            void DrawCraftTree(List<MySprite> sprites, ProductionRequestBase request, RectangleF viewArea)
            {
                _requestItems.Clear();
                GetRequestItems(request);
                int columns = (int)((Math.Min(ViewArea.Width, 512) / ViewArea.Height + 0.3) * 3);
                _rects.Clear();
                var itemCount = _requestItems.Count;
                var margin = 6f;

                int rows = (int)Math.Ceiling(itemCount / (float)columns);
                var contentPadding = 8;
                float totalHeight =
                    contentPadding * 2 +
                    rows * 64 +
                    (rows - 1) * margin;

                RectangleF fitted = CreateFittedViewArea(
                    viewArea,
                    totalHeight,
                    4
                );
                var rects = DrawRectangle(fitted, FrameActive, 1f, .05f);

                sprites.AddRange(rects.Shadow(4f, Frame));
                sprites.AddRange(rects);

                _currentCursorPosY[_currentRow] += totalHeight + (margin * 3);

                CreateGridRects(fitted, margin, contentPadding, itemCount, ref _rects, columns);
                CreateSpritesFromRects(_rects, sprites, new Color(150, 142, 131), .15f);

                var index = 0;
                foreach (var keypair in _requestItems)
                {
                    var type = keypair.Key;
                    var rect = _rects[index];
                    bool active = (type as ProductionRequestBase)?.CurrentProductionItem != null;
                    bool primary = type is CraftRequest;


                    var color = !keypair.Value ? Error
                        : active ? primary ? PrimaryActive : SecondaryActive
                        : primary ? Primary : Secondary;

                    rects = DrawRectangle(rect, color);
                    sprites.AddRange(rects.Shadow(3, Frame));
                    sprites.AddRange(rects);

                    RectangleF iconRect;
                    RectangleF textRect;
                    RectangleF descriptionRect;

                    SubdivideTile(rect, out iconRect, out textRect, out descriptionRect, 4f, 4f);

                    sprites.Add(new MySprite(0, type.Sprite, iconRect.Center, iconRect.Size));

                    if (active)
                    {
                        var degrees = AnimationFrame % 36;
                        var activeBadgeRectangle = new RectangleF(iconRect.Center, iconRect.Size / 2);

                        DrawCog(sprites, activeBadgeRectangle.Center - new Vector2(activeBadgeRectangle.Width, 0),
                            activeBadgeRectangle.Width / 64f,
                            MathHelper.ToRadians(degrees * 10)
                            , primary ? SecondaryActive : PrimaryActive
                            , primary ? Secondary : Primary);
                    }


                    sprites.Add(MySprite.CreateClipRect(textRect.ToRectangle()));

                    var name = type.NaturalName;
                    Vector2 size = GetSizeInPixel(name, "White", 1, Surface);

                    float minProportion = Math.Min(textRect.Width / size.X, textRect.Height / size.Y);
                    float fontSize = minProportion;

                    float renderedHeight = size.Y * fontSize;
                    Vector2 pos = textRect.Center;
                    pos.Y -= renderedHeight * 0.5f;

                    var text = new MySprite(
                        (SpriteType)2,
                        type.NaturalName,
                        pos,
                        null,
                        TextColor,
                        "White",
                        TextAlignment.CENTER,
                        fontSize * .95f
                    );

                    sprites.Add(text.Shadow(text.RotationOrScale, Color.Black));
                    sprites.Add(text);
                    sprites.Add(MySprite.CreateClipRect(descriptionRect.ToRectangle()));
                    sprites.Add(new MySprite((SpriteType)2, type.Description.Replace(' ', '\n'),
                        descriptionRect.Center - new Vector2(0, descriptionRect.Height / 3), null,
                        TextColor, "White", TextAlignment.CENTER, .4f));
                    sprites.Add(MySprite.CreateClearClipRect());

                    index++;
                }
            }

            void GetRequestItems(ProductionRequestBase request)
            {
                if (!(request is CraftRequest) && request.Closed)
                    return;

                _requestItems[request] = true;

                if (request.NestedRequests != null)
                {
                    foreach (var prerequisite in request.Blueprint.Prerequisites.Where(b => request.NestedRequests.All(a => !b.Key.Equals(a.Item.FullName))))
                    {
                        Item item;
                        if (_program._stock.TryGetValue(prerequisite.Key, out item))
                            _requestItems[item] = item.Amount >= prerequisite.Value * request.Missing;
                    }

                    foreach (var item in request.NestedRequests)
                        GetRequestItems(item);
                }
                else
                    foreach (var prerequisite in request.Blueprint.Prerequisites)
                    {
                        Item item;
                        if (_program._stock.TryGetValue(prerequisite.Key, out item))
                            _requestItems[item] = item.Amount >= prerequisite.Value * request.Missing;
                    }
            }
        }

        static int FindIndexOfSmallest(float[] array)
        {
            float smallestValue = array[0];
            int smallestIndex = 0;

            for (int i = 1; i < array.Length; i++)
            {
                if (array[i] < smallestValue)
                {
                    smallestValue = array[i];
                    smallestIndex = i;
                }
            }

            return smallestIndex;
        }
    }
}