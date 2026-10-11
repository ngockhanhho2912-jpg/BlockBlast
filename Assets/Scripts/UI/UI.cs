using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlockBlast
{
    /// <summary>Small factory helpers for building uGUI hierarchies from code.</summary>
    public static class UI
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        /// <summary>Anchor to a normalized rect of the parent (x/y from bottom-left, Unity convention).</summary>
        public static RectTransform Anchor(this RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Img(Transform parent, string name, Sprite sprite, Color? color = null, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color ?? Color.white;
            img.raycastTarget = raycast;
            if (sprite && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        public static Image Rounded(Transform parent, string name, int radius, Color color, bool raycast = false)
            => Img(parent, name, Art.RoundedRect(radius), color, raycast);

        public static Text Txt(Transform parent, string name, string text, int size, Color color,
            TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Art.Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static T Outline<T>(this T g, Color color, float distance) where T : Graphic
        {
            var o = g.gameObject.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(distance, -distance);
            return g;
        }

        public static T Shadow<T>(this T g, Color color, Vector2 offset) where T : Graphic
        {
            var s = g.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            s.effectColor = color;
            s.effectDistance = offset;
            return g;
        }

        public static Button Btn(Transform parent, string name, Sprite sprite, Action onClick, string label = null,
            int labelSize = 56)
        {
            var img = Img(parent, name, sprite, null, true);
            img.preserveAspect = sprite && sprite.border == Vector4.zero;
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() =>
            {
                Sfx.Play("click");
                onClick?.Invoke();
            });
            img.gameObject.AddComponent<PressScale>();
            if (label != null)
            {
                var t = Txt(img.transform, "Label", label, labelSize, Color.white);
                t.rectTransform.Stretch();
                t.rectTransform.offsetMin = new Vector2(0, 8); // sit above the button's bottom lip
                t.Shadow(new Color(0, 0, 0, .35f), new Vector2(0, -4));
            }
            return b;
        }

        /// <summary>Full-screen image that keeps its aspect and covers the parent (like CSS background-size: cover).</summary>
        public static Image CoverImage(Transform parent, string name, Sprite sprite)
        {
            var img = Img(parent, name, sprite);
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(.5f, .5f);
            var fit = img.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = sprite ? sprite.rect.width / sprite.rect.height : .5f;
            return img;
        }
    }

    /// <summary>Squash feedback on press for any clickable graphic.</summary>
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public void OnPointerDown(PointerEventData e) =>
            Tween.Run(transform, "press", .08f, k => transform.localScale = Vector3.one * Mathf.Lerp(1, .92f, k));

        public void OnPointerUp(PointerEventData e) =>
            Tween.Run(transform, "press", .18f, k => transform.localScale = Vector3.one * Mathf.LerpUnclamped(.92f, 1, k), Ease.OutBack);
    }

    /// <summary>Keeps a RectTransform inside Screen.safeArea (notches, home indicator).</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        Rect last;

        void Update()
        {
            var sa = Screen.safeArea;
            if (sa == last || Screen.width == 0) return;
            last = sa;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
