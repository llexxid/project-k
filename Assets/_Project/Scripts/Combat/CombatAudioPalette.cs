using UnityEngine;

namespace KingdomIdle.Combat
{
    public enum CombatSoundCue { Steel, Heavy, Thrust, Throw, Magic, Whip }
    public sealed class CombatAudioPalette : ScriptableObject
    {
        public AudioClip steel, heavy, thrust, throwing, magic, whip;
        public AudioClip Clip(CombatSoundCue cue) => cue switch {
            CombatSoundCue.Steel=>steel,CombatSoundCue.Heavy=>heavy,CombatSoundCue.Thrust=>thrust,
            CombatSoundCue.Throw=>throwing,CombatSoundCue.Magic=>magic,_=>whip };
    }
}
