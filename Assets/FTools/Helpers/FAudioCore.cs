using System.Collections.Generic;
using FTools.Cache;
using FUnityCommon;
using UnityEngine;
using UnityEngine.Audio;

namespace FTools.Helpers
{
    public class FAudioCore : FSingletonMono<FAudioCore>
    {
        private const string DEFAULT_PATH = "Audio";

        [Header("Mixer")]
        public AudioMixer audioMixer;
        public string mixerBGMVolume = "BGM_Vol";
        public string mixerSfxVolume = "SFX_Vol";

        [Header("池配置")]
        public int maxSfxSources = 12;
        public float sfxCooldown = 0.02f;  // 防止单位时间内音效被 spam

        [Header("缓存")]
        public int maxClipCache = 32;

        private readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>();
        private readonly LinkedList<string> _lruList = new LinkedList<string>(); // LRU

        [Header("AudioSources")]
        private AudioSource _bgmSource;
        private readonly List<AudioSource> _sfxSources = new List<AudioSource>();
        private readonly Dictionary<string, float> _lastPlayTime = new Dictionary<string, float>();

        [Header("音量管理")]
        private float _bgmVolume = 1f;
        private float _sfxVolume = 1f;
        private bool _muteBGM = false;
        private bool _muteSfx = false;

        protected override void OnAwake()
        {
            InitBGMSource();
            InitAudioFromLocal();
        }

        private void InitBGMSource()
        {
            _bgmSource = GetComponent<AudioSource>();
            if (_bgmSource == null)
                _bgmSource = gameObject.AddComponent<AudioSource>();

            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            if (audioMixer != null)
            {
                _bgmSource.outputAudioMixerGroup = audioMixer.FindMatchingGroups("BGM")[0];
            }
        }

        private void InitAudioFromLocal()
        {
            var audioAmount = FCacheManager.GetCache(ECacheType.Local).Load<float>("AudioAmount", 1f);
            
            var musicAmount = FCacheManager.GetCache(ECacheType.Local).Load<float>("MusicAmount", 1f);
            
            

            SetBGMVolume(musicAmount);
            SetSfxVolume(audioAmount);
            // if (_bgmSource) _bgmSource.volume = 1f;

        }

        // =====================================================
        // LRU Clip Cache
        // =====================================================
        private AudioClip LoadClip(string path)
        {
            if (_clipCache.TryGetValue(path, out var cached))
            {
                TouchLRU(path);
                return cached;
            }

            var clip = Resources.Load<AudioClip>($"{DEFAULT_PATH}/{path}");
            if (clip == null)
            {
                Debug.LogError($"[FAudioCore] AudioClip not found: {DEFAULT_PATH}/{path}");
                return null;
            }

            if (_clipCache.Count >= maxClipCache)
            {
                // 移除最久未使用的 Clip（LRU）
                string oldest = _lruList.Last.Value;
                _lruList.RemoveLast();
                _clipCache.Remove(oldest);
            }

            _clipCache[path] = clip;
            _lruList.AddFirst(path);

            return clip;
        }

        private void TouchLRU(string key)
        {
            _lruList.Remove(key);
            _lruList.AddFirst(key);
        }

        // =====================================================
        // BGM（音乐）
        // =====================================================
        public void PlayBGM(string bgmName, float fadeTime = 0.5f)
        {
            var clip = LoadClip($"BGM/{bgmName}");
            if (clip == null) return;
            StartCoroutine(FadeBGM(clip, fadeTime));
        }

        private System.Collections.IEnumerator FadeBGM(AudioClip newClip, float fadeTime)
        {
            // 淡出：从当前 BGM_Vol → -80dB
            audioMixer.GetFloat(mixerBGMVolume, out float currentDB);

            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                float v = Mathf.Lerp(currentDB, -80f, t / fadeTime); 
                audioMixer.SetFloat(mixerBGMVolume, v);
                yield return null;
            }

            _bgmSource.clip = newClip;
            _bgmSource.Play();

            float targetDB = Mathf.Log10(Mathf.Max(_bgmVolume, 0.001f)) * 20;

            t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                float v = Mathf.Lerp(-80f, targetDB, t / fadeTime);
                audioMixer.SetFloat(mixerBGMVolume, v);
                yield return null;
            }

            audioMixer.SetFloat(mixerBGMVolume, targetDB);
        }

        public void StopBGM()
        {
            _bgmSource.Stop();
        }

        public void SetBGMVolume(float v)
        {
            _bgmVolume = v;

            if (audioMixer)
            {
                audioMixer.SetFloat(mixerBGMVolume, Mathf.Log10(Mathf.Max(v, 0.001f)) * 20);
            }
            else _bgmSource.volume = v;
        }

 

        public void MuteBGM(bool mute)
        {
            _muteBGM = mute;
            _bgmSource.mute = mute;
        }

        // =====================================================
        // SFX（音效）
        // =====================================================
        public void PlaySfx(string sfxName)
        {
            var clip = LoadClip($"SFX/{sfxName}");
            if (clip == null) return;

            if (CheckCooldown(sfxName))
                return;

            var source = GetSfxSource();
            source.clip = clip;
            source.volume = _sfxVolume;
            source.Play();
        }

        public void PlaySfxOneShot(string sfxName)
        {
            var clip = LoadClip($"SFX/{sfxName}");
            if (clip == null) return;

            if (CheckCooldown(sfxName))
                return;

            var source = GetSfxSource();
            source.PlayOneShot(clip, _sfxVolume);
        }

        private bool CheckCooldown(string key)
        {
            if (!_lastPlayTime.TryGetValue(key, out float last))
            {
                _lastPlayTime[key] = Time.time;
                return false;
            }

            if (Time.time - last < sfxCooldown)
                return true;

            _lastPlayTime[key] = Time.time;
            return false;
        }

        private AudioSource GetSfxSource()
        {
            foreach (var s in _sfxSources)
            {
                if (!s.isPlaying)
                    return s;
            }

            if (_sfxSources.Count < maxSfxSources)
            {
                var newSource = gameObject.AddComponent<AudioSource>();
                newSource.playOnAwake = false;
                newSource.loop = false;
                newSource.mute = _muteSfx;
                if (audioMixer != null)
                {
                    var outputGroup = audioMixer.FindMatchingGroups("Sfx");
                    if (outputGroup.Length > 0)
                    {
                        newSource.outputAudioMixerGroup = outputGroup[0];
                    }
                }
                _sfxSources.Add(newSource);
                return newSource;
            }

            // 池满 → 使用最旧的、已完成播放的 source
            return _sfxSources[0];
        }

        public void SetSfxVolume(float v)
        {
            _sfxVolume = v;

            if (audioMixer)
                audioMixer.SetFloat(mixerSfxVolume, Mathf.Log10(Mathf.Max(v, 0.001f)) * 20);
            else
                foreach (var s in _sfxSources)
                    s.volume = v;
        }

        public void MuteSfx(bool mute)
        {
            _muteSfx = mute;
            foreach (var s in _sfxSources)
                s.mute = mute;
        }

 
        public void ClearCache()
        {
            _clipCache.Clear();
            _lruList.Clear();
        }
    }
}
