using UnityEngine;

namespace LastLight.World
{
    /// <summary>Gun sayaci ve saat. Gece basinca goruntude uyari verir.</summary>
    public sealed class WorldHUD : MonoBehaviour
    {
        void OnGUI()
        {
            var cycle = DayNightCycle.Instance;
            if (cycle == null) return;

            var box = new Rect(Screen.width - 210, 12, 198, 52);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(box.x + 10, box.y + 6, 190, 20),
                      $"Gun {cycle.DayNumber}   {cycle.ClockText()}");

            GUI.color = cycle.IsNight ? new Color(1f, 0.45f, 0.40f) : new Color(0.6f, 0.9f, 0.6f);
            GUI.Label(new Rect(box.x + 10, box.y + 26, 190, 20),
                      cycle.IsNight ? "GECE - isik yak" : $"Gunduz   (gecilen gece: {cycle.NightsSurvived})");

            GUI.color = Color.white;
        }
    }
}
