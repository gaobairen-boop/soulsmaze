using UnityEngine;

namespace SoulsMaze
{
    /// <summary>火龍噴火的火球粒子（純視覺，傷害由 Enemy 判定）。</summary>
    public class FireParticle : MonoBehaviour
    {
        static Material mat;
        Vector3 vel;
        float life, age;

        public static void Spawn(Vector3 pos, Vector3 velocity)
        {
            if (mat == null) mat = Util.Mat(new Color(1f, 0.5f, 0.1f), 0f, new Color(8f, 3f, 0.5f));
            var go = Util.Prim(PrimitiveType.Sphere, "Fire", null, pos, Vector3.one * 0.3f, mat, false);
            var f = go.AddComponent<FireParticle>();
            f.vel = velocity;
            f.life = Random.Range(0.45f, 0.65f);
        }

        void Update()
        {
            age += Time.deltaTime;
            transform.position += vel * Time.deltaTime;
            vel += Vector3.up * 2f * Time.deltaTime;
            float t = age / life;
            transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.5f, t) * (1f - t * t * 0.7f);
            if (age >= life) Destroy(gameObject);
        }
    }
}
