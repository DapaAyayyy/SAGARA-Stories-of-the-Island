using UnityEngine;
using UnityEngine.UIElements;

namespace Sagara.DermagaV2
{
    internal sealed class DermagaMinimap
    {
        private const float AtlasSpan = 136f;
        private const float VisibleSpan = 34f;
        private const int ShadeSegments = 40;
        private static readonly Vector2 AtlasNorthWest = new Vector2(-90f, 89f);

        // Key cozy island landmarks (world X, Z) rendered subtly on the Genshin-style minimap
        private static readonly Vector2 CottageWorld = new Vector2(-8.1f, 11.0f);
        private static readonly Vector2 DockWorld = new Vector2(4.2f, 2.2f);

        private readonly VisualElement root;
        private readonly VisualElement face;
        private readonly VisualElement artwork;
        private readonly VisualElement ink;
        private readonly VisualElement bezel;
        private readonly Texture2D damagedMap;
        private readonly Texture2D repairedMap;
        private bool usingRepaired;
        private Vector2 playerPosition;
        private Vector2 heading = Vector2.up;
        private Vector2 goal;
        private Vector2 goalDirection = Vector2.up;
        private float pixelsPerMeter = 4f;
        private bool hasGoal;
        private bool outOfRange;

        internal DermagaMinimap(VisualElement parent, Texture2D map, Texture2D repaired = null)
        {
            damagedMap = map;
            repairedMap = repaired;
            root = Layer(parent, "coast-minimap");
            Layer(root, "coast-minimap-shadow");
            face = Layer(root, "coast-minimap-face");
            artwork = Layer(face, "coast-minimap-art");
            if (map)
            {
                artwork.style.backgroundImage = new StyleBackground(map);
            }
            var shade = Layer(face, "coast-minimap-shade");
            shade.generateVisualContent += DrawFalloff;
            ink = Layer(face, "coast-minimap-ink");
            ink.generateVisualContent += DrawMarkers;
            bezel = Layer(root, "coast-minimap-dial");
            bezel.generateVisualContent += DrawBezel;
            var north = new Label("N") { pickingMode = PickingMode.Ignore };
            north.AddToClassList("coast-minimap-north");
            root.Add(north);
        }

        internal void SetDockRepaired(bool repaired)
        {
            if (usingRepaired == repaired)
            {
                return;
            }
            usingRepaired = repaired;
            Texture2D chosen = repaired && repairedMap ? repairedMap : damagedMap;
            if (chosen)
            {
                artwork.style.backgroundImage = new StyleBackground(chosen);
            }
        }

        internal void Update(bool visible, Transform player, Transform target)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            float diameter = face.layout.width;
            if (!visible || !player || diameter < 1f || float.IsNaN(diameter))
            {
                return;
            }

            Vector3 position = player.position;
            Vector3 forward = player.forward;
            playerPosition = new Vector2(position.x, position.z);
            Vector2 rawHeading = new Vector2(forward.x, -forward.z);
            if (rawHeading.sqrMagnitude > .0001f)
            {
                heading = rawHeading.normalized;
            }

            pixelsPerMeter = diameter / VisibleSpan;
            float atlasSize = AtlasSpan * pixelsPerMeter;
            var origin = new Vector2(playerPosition.x - AtlasNorthWest.x, AtlasNorthWest.y - playerPosition.y);
            artwork.style.width = atlasSize;
            artwork.style.height = atlasSize;
            artwork.style.left = diameter * .5f - origin.x * pixelsPerMeter;
            artwork.style.top = diameter * .5f - origin.y * pixelsPerMeter;

            hasGoal = target;
            if (hasGoal)
            {
                Vector3 delta = target.position - position;
                Vector2 offset = new Vector2(delta.x, -delta.z) * pixelsPerMeter;
                float innerRadius = diameter * .5f - 11f;
                outOfRange = offset.sqrMagnitude > innerRadius * innerRadius;
                goalDirection = offset.sqrMagnitude > .001f ? offset.normalized : Vector2.up;
                goal = Vector2.ClampMagnitude(offset, innerRadius);
            }
            ink.MarkDirtyRepaint();
        }

        private static void DrawFalloff(MeshGenerationContext context)
        {
            Rect bounds = context.visualElement.contentRect;
            Vector2 center = bounds.center;
            float radius = Mathf.Min(bounds.width, bounds.height) * .5f;
            if (radius <= 0f)
            {
                return;
            }

            // Genshin-style soft translucent feathered rim vignette (light & airy, never muddy black)
            var mesh = context.Allocate(ShadeSegments * 2, ShadeSegments * 6);
            var inside = new Color32(16, 44, 58, 0);
            var edge = new Color32(14, 38, 52, 92);
            for (int i = 0; i < ShadeSegments; i++)
            {
                float angle = 2f * Mathf.PI * i / ShadeSegments;
                Vector2 unit = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 inner = center + unit * (radius * .68f);
                Vector2 outer = center + unit * radius;
                mesh.SetNextVertex(new Vertex { position = new Vector3(inner.x, inner.y, Vertex.nearZ), tint = inside });
                mesh.SetNextVertex(new Vertex { position = new Vector3(outer.x, outer.y, Vertex.nearZ), tint = edge });
            }
            for (int i = 0; i < ShadeSegments; i++)
            {
                ushort inner = (ushort)(2 * i);
                ushort next = (ushort)(2 * ((i + 1) % ShadeSegments));
                mesh.SetNextIndex(inner);
                mesh.SetNextIndex((ushort)(inner + 1));
                mesh.SetNextIndex((ushort)(next + 1));
                mesh.SetNextIndex(inner);
                mesh.SetNextIndex((ushort)(next + 1));
                mesh.SetNextIndex(next);
            }
        }

        private static void DrawBezel(MeshGenerationContext context)
        {
            Rect bounds = context.visualElement.contentRect;
            Vector2 center = bounds.center;
            float radius = Mathf.Min(bounds.width, bounds.height) * .5f - 1.5f;
            if (radius <= 4f)
            {
                return;
            }

            var painter = context.painter2D;
            // Subtle warm ivory-gold outer ring (Genshin compass craftsmanship)
            painter.strokeColor = new Color(1f, .96f, .86f, .78f);
            painter.lineWidth = 1.6f;
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Stroke();

            // Delicate inner hairline ring
            painter.strokeColor = new Color(.94f, .82f, .52f, .38f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.Arc(center, radius - 3.5f, 0f, 360f);
            painter.Stroke();

            // Subtle E / S / W compass dots on the rim
            painter.fillColor = new Color(1f, .95f, .82f, .82f);
            float[] tickAngles = { 0f, 90f, 180f };
            foreach (float deg in tickAngles)
            {
                float rad = deg * Mathf.Deg2Rad;
                Vector2 pt = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
                painter.BeginPath();
                painter.Arc(pt, 2.0f, 0f, 360f);
                painter.Fill();
            }
        }

        private void DrawMarkers(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            Rect rect = ink.contentRect;
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * .5f;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;

            // 1. Subtle cozy landmark pips (Pak Darma cottage & Dermaga pier)
            DrawLandmark(painter, center, radius, CottageWorld, new Color(0.88f, 0.44f, 0.28f, 0.92f));
            DrawLandmark(painter, center, radius, DockWorld, new Color(0.24f, 0.66f, 0.68f, 0.92f));

            // 2. Genshin-style soft translucent player view cone
            Vector2 side = new Vector2(-heading.y, heading.x);
            painter.fillColor = new Color(0.55f, 0.94f, 1.0f, 0.22f);
            painter.BeginPath();
            painter.MoveTo(center);
            painter.LineTo(center + heading * 24f + side * 14f);
            painter.LineTo(center + heading * 27f);
            painter.LineTo(center + heading * 24f - side * 14f);
            painter.ClosePath();
            painter.Fill();

            // 3. Animated Genshin golden double-diamond quest pin
            if (hasGoal)
            {
                Vector2 at = center + goal;
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.2f);

                // Soft glowing pulse ring
                painter.strokeColor = new Color(1f, .86f, .42f, .24f + .34f * (1f - pulse));
                painter.lineWidth = 1.6f;
                painter.BeginPath();
                painter.Arc(at, 6.5f + pulse * 4.2f, 0f, 360f);
                painter.Stroke();

                // Directional golden chevron when quest target is beyond minimap radius
                if (outOfRange)
                {
                    Vector2 tip = at + goalDirection * 7.5f;
                    Vector2 gSide = new Vector2(-goalDirection.y, goalDirection.x);
                    painter.fillColor = new Color(1f, .90f, .52f, 1f);
                    painter.strokeColor = new Color(.35f, .22f, .07f, .92f);
                    painter.lineWidth = 1.1f;
                    painter.BeginPath();
                    painter.MoveTo(tip);
                    painter.LineTo(at + goalDirection * 2.5f + gSide * 4.2f);
                    painter.LineTo(at + goalDirection * 2.5f - gSide * 4.2f);
                    painter.ClosePath();
                    painter.Fill();
                    painter.Stroke();
                }

                // Outer warm-gold diamond
                float s = outOfRange ? 4.8f : 6.0f;
                painter.fillColor = new Color(0.98f, 0.77f, 0.28f, 1f);
                painter.strokeColor = new Color(0.32f, 0.20f, 0.06f, 0.95f);
                painter.lineWidth = 1.4f;
                DrawDiamond(painter, at, s);
                painter.Fill();
                painter.Stroke();

                // Luminous cream-white core diamond
                painter.fillColor = new Color(1f, 0.98f, 0.88f, 1f);
                DrawDiamond(painter, at, s * 0.46f);
                painter.Fill();
            }

            // 4. Crisp Genshin cyan-white player arrow with drop shadow
            Vector2 shadowOffset = new Vector2(0f, 1.4f);
            painter.fillColor = new Color(0.04f, 0.16f, 0.22f, 0.55f);
            DrawPlayerArrow(painter, center + shadowOffset, heading, side, 8.6f, 5.4f);
            painter.Fill();

            painter.fillColor = new Color(0.38f, 0.92f, 1.0f, 1f);
            painter.strokeColor = new Color(1f, 1f, 1f, 0.98f);
            painter.lineWidth = 1.65f;
            DrawPlayerArrow(painter, center, heading, side, 8.0f, 5.0f);
            painter.Fill();
            painter.Stroke();
        }

        private void DrawLandmark(Painter2D painter, Vector2 center, float radius, Vector2 worldXZ, Color accent)
        {
            Vector2 delta = worldXZ - playerPosition;
            Vector2 offset = new Vector2(delta.x, -delta.y) * pixelsPerMeter;
            if (offset.sqrMagnitude > (radius - 9f) * (radius - 9f))
            {
                return;
            }
            Vector2 p = center + offset;
            painter.fillColor = new Color(1f, 0.98f, 0.92f, 0.92f);
            painter.BeginPath();
            painter.Arc(p, 4.2f, 0f, 360f);
            painter.Fill();
            painter.fillColor = accent;
            painter.BeginPath();
            painter.Arc(p, 2.6f, 0f, 360f);
            painter.Fill();
        }

        private static void DrawDiamond(Painter2D painter, Vector2 at, float size)
        {
            painter.BeginPath();
            painter.MoveTo(at + Vector2.up * size);
            painter.LineTo(at + Vector2.right * size);
            painter.LineTo(at - Vector2.up * size);
            painter.LineTo(at - Vector2.right * size);
            painter.ClosePath();
        }

        private static void DrawPlayerArrow(Painter2D painter, Vector2 c, Vector2 fwd, Vector2 side, float tipLen, float wing)
        {
            painter.BeginPath();
            painter.MoveTo(c + fwd * tipLen);
            painter.LineTo(c - fwd * (tipLen * .58f) + side * wing);
            painter.LineTo(c - fwd * (tipLen * .22f));
            painter.LineTo(c - fwd * (tipLen * .58f) - side * wing);
            painter.ClosePath();
        }

        private static VisualElement Layer(VisualElement parent, string styleClass)
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.AddToClassList(styleClass);
            parent.Add(layer);
            return layer;
        }
    }
}
