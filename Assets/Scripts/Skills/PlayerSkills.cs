using LastLight.World;
using UnityEngine;

namespace LastLight.Skills
{
    /// <summary>
    /// Beceri durumunu tasir. Arayuzu GameMenuController cizer (Tab ekrani).
    /// Puan gecilen gece sayisindan geliyor.
    /// </summary>
    public sealed class PlayerSkills : MonoBehaviour
    {
        public SkillState State { get; } = new();

        public static PlayerSkills Instance { get; private set; }

        void Awake() => Instance = this;

        void Update()
        {
            var cycle = DayNightCycle.Instance;
            if (cycle != null) State.SyncPoints(cycle.NightsSurvived);

        }

    }
}
