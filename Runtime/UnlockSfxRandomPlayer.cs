using UnityEngine;

namespace UnlockSfx
{
    /// <summary>
    /// Plays a random clip from a set, avoiding immediate repeats and adding a
    /// little pitch variance so retriggers don't sound mechanical — the runtime
    /// equivalent of Godot's AudioStreamRandomizer. UnlockSFX builds a prefab with
    /// this component (clips pre-assigned) when you generate a variation bank; drop
    /// the prefab into a scene and call <see cref="Play"/>.
    ///
    /// This is a normal public component (no internal engine APIs), so it works in
    /// builds and across Unity versions.
    /// </summary>
    [AddComponentMenu("Audio/UnlockSFX Random Player")]
    [RequireComponent(typeof(AudioSource))]
    public class UnlockSfxRandomPlayer : MonoBehaviour
    {
        [Tooltip("The variation clips to choose from.")]
        public AudioClip[] clips;

        [Tooltip("Never play the same clip twice in a row (when there's more than one).")]
        public bool avoidRepeat = true;

        [Tooltip("Random pitch wobble per play, as a fraction (0.08 = ±8%).")]
        [Range(0f, 0.5f)]
        public float pitchVariance = 0.08f;

        [Tooltip("Play a random clip automatically when this object awakes.")]
        public bool playOnAwake;

        AudioSource _source;
        int _lastIndex = -1;

        void Awake()
        {
            _source = GetComponent<AudioSource>();
            if (playOnAwake) Play();
        }

        /// <summary>Play a random clip (non-repeating) with a touch of pitch variance.</summary>
        public void Play()
        {
            if (clips == null || clips.Length == 0) return;
            if (_source == null) _source = GetComponent<AudioSource>();

            int index = PickIndex();
            _lastIndex = index;

            _source.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
            _source.PlayOneShot(clips[index]);
        }

        int PickIndex()
        {
            if (clips.Length == 1) return 0;
            if (!avoidRepeat) return Random.Range(0, clips.Length);

            int index;
            do { index = Random.Range(0, clips.Length); }
            while (index == _lastIndex);
            return index;
        }
    }
}
