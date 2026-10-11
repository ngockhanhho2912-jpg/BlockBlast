using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlockBlast
{
    public enum Ease { Linear, OutQuad, InQuad, OutBack, OutCubic }

    /// <summary>Minimal coroutine tween runner. Tweens stop when their owner is destroyed;
    /// starting a tween with the same (owner, key) replaces the running one.</summary>
    public sealed class Tween : MonoBehaviour
    {
        static Tween host;
        readonly Dictionary<(int, string), Coroutine> running = new Dictionary<(int, string), Coroutine>();

        static Tween Host
        {
            get
            {
                if (!host)
                {
                    var go = new GameObject("[Tween]");
                    DontDestroyOnLoad(go);
                    host = go.AddComponent<Tween>();
                }
                return host;
            }
        }

        public static void Run(UnityEngine.Object owner, string key, float duration, Action<float> step,
            Ease ease = Ease.OutQuad, float delay = 0f, Action done = null)
        {
            var h = Host;
            var id = (owner ? owner.GetInstanceID() : 0, key);
            if (key != null && h.running.TryGetValue(id, out var old) && old != null) h.StopCoroutine(old);
            var co = h.StartCoroutine(h.Play(owner, id, key != null, duration, step, ease, delay, done));
            if (key != null) h.running[id] = co;
        }

        public static void Kill(UnityEngine.Object owner, string key)
        {
            if (!host || !owner) return;
            var id = (owner.GetInstanceID(), key);
            if (host.running.TryGetValue(id, out var co) && co != null) host.StopCoroutine(co);
            host.running.Remove(id);
        }

        public static void Delay(float seconds, Action action) => Run(null, null, seconds, null, Ease.Linear, 0, action);

        IEnumerator Play(UnityEngine.Object owner, (int, string) id, bool keyed, float duration, Action<float> step,
            Ease ease, float delay, Action done)
        {
            bool guarded = owner;
            if (delay > 0) yield return new WaitForSecondsRealtime(delay);
            float t = 0;
            while (t < duration)
            {
                if (guarded && !owner) yield break;
                step?.Invoke(Evaluate(ease, t / duration));
                yield return null;
                t += Time.unscaledDeltaTime;
            }
            if (guarded && !owner) yield break;
            step?.Invoke(1f);
            if (keyed) running.Remove(id);
            done?.Invoke();
        }

        public static float Evaluate(Ease ease, float t)
        {
            switch (ease)
            {
                case Ease.OutQuad: return 1 - (1 - t) * (1 - t);
                case Ease.InQuad: return t * t;
                case Ease.OutCubic: return 1 - Mathf.Pow(1 - t, 3);
                case Ease.OutBack:
                    const float c1 = 1.70158f, c3 = c1 + 1;
                    return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
                default: return t;
            }
        }

        // ---- common helpers ----

        public static void Scale(Transform t, Vector3 from, Vector3 to, float duration, Ease ease = Ease.OutBack,
            float delay = 0f, Action done = null)
        {
            t.localScale = from;
            Run(t, "scale", duration, k => t.localScale = Vector3.LerpUnclamped(from, to, k), ease, delay, done);
        }

        public static void Move(RectTransform t, Vector2 to, float duration, Ease ease = Ease.OutQuad, Action done = null)
        {
            Vector2 from = t.anchoredPosition;
            Run(t, "move", duration, k => t.anchoredPosition = Vector2.LerpUnclamped(from, to, k), ease, 0, done);
        }

        public static void Fade(CanvasGroup g, float to, float duration, float delay = 0f, Action done = null)
        {
            float from = g.alpha;
            Run(g, "fade", duration, k => g.alpha = Mathf.Lerp(from, to, k), Ease.Linear, delay, done);
        }
    }
}
