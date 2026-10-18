using UnityEngine;

namespace SoulsMaze
{
    /// <summary>讓火光閃爍。</summary>
    public class Flicker : MonoBehaviour
    {
        public float baseIntensity = 2f;
        public float speed = 3f;
        Light l;
        float seed;

        void Awake()
        {
            l = GetComponent<Light>();
            seed = Random.value * 100f;
            if (l) baseIntensity = l.intensity;
        }

        void Update()
        {
            if (!l) return;
            l.intensity = baseIntensity * (0.8f + 0.4f * Mathf.PerlinNoise(seed, Time.time * speed));
        }
    }
}
