using UnityEngine;

namespace Sagara.Dermaga.Fishing
{
    [DisallowMultipleComponent]
    public sealed class FishingCatchCard : MonoBehaviour
    {
        // Result card bindings.
        public FishingUiTheme theme;
        public FishLandingPresentation landing;
        public SessionFishVisuals visuals;
        public FishingSession session;

        public Rect Bounds => new Rect(450, 720, 1020, 340);

        public void Draw(FishingUiPainter draw)
        {
            if (landing == null || !landing.ReadyForConfirmation || session.LastResult == null)
            {
                return;
            }

            var r = Bounds;
            draw.Illustrated(r, true);
            var accent = theme.Rarity(session.LastResult.rarity);
            var entry = visuals.catalog.Find(session.Selected);
            draw.Text(
                new Rect(r.x + 200, r.y + 55, 620, 47),
                "CATATAN TANGKAPAN",
                theme.teal,
                3,
                TextAnchor.MiddleCenter
            );

            if (entry != null && entry.portrait != null)
            {
                draw.Icon(new Rect(r.x + 80, r.y + 98, 315, 180), entry.portrait, Color.white);
            }

            draw.Text(
                new Rect(r.x + 415, r.y + 117, 500, 48),
                entry == null ? "Ikan tertangkap" : entry.displayName,
                theme.teal,
                3,
                TextAnchor.MiddleCenter
            );
            draw.Text(
                new Rect(r.x + 440, r.y + 166, 450, 40),
                "✦ " + session.LastResult.rarity + " ✦",
                accent,
                2,
                TextAnchor.MiddleCenter
            );
            draw.Text(
                new Rect(r.x + 415, r.y + 210, 500, 43),
                "Skor " + session.LastResult.score + " dari 100",
                theme.wood,
                4,
                TextAnchor.MiddleCenter
            );
            draw.Text(
                new Rect(r.x + 805, r.y + 94, 160, 31),
                "DERMAGA",
                theme.ivory,
                1,
                TextAnchor.MiddleCenter
            );

            var button = new Rect(r.x + 300, r.y + 273, 420, 48);
            draw.Text(button, "Lanjutkan  [Enter]", theme.ivory, 2, TextAnchor.MiddleCenter);

            if (GUI.Button(button, GUIContent.none, GUIStyle.none))
            {
                landing.Acknowledge();
            }
        }
    }
}
