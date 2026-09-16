using KingdomIdle.UGUI;
using UnityEngine;

namespace KingdomIdle.Combat
{
    /// <summary>Six short voices; close simultaneous impacts share the mix instead of piling up.</summary>
    public sealed class CombatAudio : MonoBehaviour
    {
        private static CombatAudio _instance;
        private CombatAudioPalette _palette;
        private readonly AudioSource[] _voices=new AudioSource[6];
        private readonly SoundChannel[] _channels=new SoundChannel[6];
        private readonly float[] _gains=new float[6],_lastCue=new float[6];
        private float _lastAny=-1;
        private int _sequence;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(_instance!=null)return;
            var palette=Resources.Load<CombatAudioPalette>("CombatAudioPalette");if(palette==null)return;
            _instance=new GameObject("CombatAudio").AddComponent<CombatAudio>();
            _instance._palette=palette;DontDestroyOnLoad(_instance.gameObject);
        }
        private void Awake()
        {
            for(int i=0;i<_voices.Length;i++)
            {
                var voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;voice.priority=180;
                _voices[i]=voice;_lastCue[i]=-1;
            }
            GameAudioSettings.Changed+=RefreshVolumes;
        }
        private void OnDestroy(){GameAudioSettings.Changed-=RefreshVolumes;if(_instance==this)_instance=null;}
        private void RefreshVolumes(){for(int i=0;i<_voices.Length;i++)_voices[i].volume=_gains[i]*GameAudioSettings.Gain(_channels[i]);}
        public static void PlayerImpact(Player player, bool projectile=false)
        {
            if(player==null)return;
            var cue=projectile?CombatSoundCue.Magic:player.playerStatus.JobName switch {
                "Knight" or "Elite_Knight"=>CombatSoundCue.Steel,_=>CombatSoundCue.Thrust};
            Play(cue,SoundChannel.RoyalGuard,player.transform.position);
        }
        public static void Play(CombatSoundCue cue,SoundChannel channel,Vector3 position)
        {
            var owner=_instance;
            if(owner==null || GameAudioSettings.Muted || GameAudioSettings.Gain(channel)<=0)return;
            float now=Time.unscaledTime;
            if(now-owner._lastAny<.065f || now-owner._lastCue[(int)cue]<.12f)return;
            int active=0,available=-1;
            for(int i=0;i<owner._voices.Length;i++)
            {
                if(!owner._voices[i].isPlaying){if(available<0)available=i;}
                else if(owner._channels[i]==channel)active++;
            }
            if(available<0 || active >= (channel==SoundChannel.Monsters?3:2))return;
            var clip=owner._palette.Clip(cue);if(clip==null)return;
            owner._lastAny=owner._lastCue[(int)cue]=now;
            var source=owner._voices[available];owner._channels[available]=channel;
            owner._gains[available]=channel==SoundChannel.Monsters?.34f:.52f;
            source.clip=clip;source.pitch=1+((owner._sequence++%5)-2)*.018f;
            source.panStereo=Mathf.Clamp(position.x/5,-.25f,.25f);
            source.volume=owner._gains[available]*GameAudioSettings.Gain(channel);source.Play();
        }
    }
}
