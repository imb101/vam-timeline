using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class Curves : MonoBehaviour
    {
        private const int _maxCurves = 9;

        private readonly CurvesStyle _style = CurvesStyle.Default();
        private readonly RectTransform _scrubberLineRect;
        private readonly GameObject _noCurves;
        private readonly GameObject _linesContainer;
        private readonly IList<ICurveAnimationTarget> _targets = new List<ICurveAnimationTarget>();
        private readonly IList<CurvesLines> _lines = new List<CurvesLines>();
        private AtomAnimationEditContext _animationEditContext;
        private float _lastClipTime;

        public Curves()
        {
            CreateBackground(_style.BackgroundColor);

            var image = gameObject.AddComponent<Image>();
            image.color = Color.yellow;
            image.raycastTarget = false;

            var mask = gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            _noCurves = CreateNoCurvesText();

            _linesContainer = CreateLinesContainer();
            _scrubberLineRect = CreateScrubber(transform);
        }

        private GameObject CreateBackground(Color color)
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();

            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return go;
        }

        private RectTransform CreateScrubber(Transform parent)
        {
            var go = new GameObject("Scrubber");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();
            rect.offsetMin = new Vector2(_style.Padding, 0f);
            rect.offsetMax = new Vector2(-_style.Padding, 0f);

            var line = new GameObject("Scrubber Line");
            line.transform.SetParent(go.transform, false);

            var lineRect = line.AddComponent<RectTransform>();
            lineRect.StretchCenter();
            lineRect.sizeDelta = new Vector2(_style.ScrubberSize, 0);

            var image = line.AddComponent<Image>();
            image.color = _style.ScrubberColor;
            image.raycastTarget = false;

            return lineRect;
        }

        public GameObject CreateLinesContainer()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();

            var group = go.AddComponent<VerticalLayoutGroup>();
            group.childForceExpandHeight = true;

            return go;
        }

        private CurvesLines CreateCurvesLines(GameObject parent, Color color, string label)
        {
            var go = new GameObject();
            go.transform.SetParent(parent.transform, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1;
            layout.flexibleHeight = 1;

            var linesContainer = new GameObject();
            linesContainer.transform.SetParent(go.transform, false);

            var linesRect = linesContainer.AddComponent<RectTransform>();
            linesRect.StretchParent();

            var lines = linesContainer.AddComponent<CurvesLines>();
            lines.style = _style;
            lines.raycastTarget = false;

            var textContainer = new GameObject();
            textContainer.transform.SetParent(go.transform, false);

            var textRect = textContainer.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 0);
            textRect.anchoredPosition = new Vector2(5f, 5f);
            textRect.pivot = new Vector2(0, 0);
            textRect.sizeDelta = new Vector2(0, 30);

            var textText = textContainer.AddComponent<Text>();
            textText.raycastTarget = false;
            textText.alignment = TextAnchor.LowerLeft;
            textText.font = _style.Font;
            textText.color = Color.Lerp(color, Color.white, .9f);
            textText.text = label;
            textText.fontSize = 20;

            return lines;
        }

        private GameObject CreateNoCurvesText()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();
            rect.offsetMin = new Vector2(20f, 0);
            rect.offsetMax = new Vector2(-20f, 0);

            var text = go.AddComponent<Text>();
            text.text = "Select targets in the dope sheet to see their curves";
            text.font = _style.Font;
            text.fontSize = 28;
            text.color = _style.FontColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;

            return go;
        }

        public void Bind(AtomAnimationEditContext animationEditContext)
        {
            if (_animationEditContext != null) throw new InvalidOperationException("Cannot bind to animation twice");
            _animationEditContext = animationEditContext;
            _animationEditContext.animation.animatables.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            _animationEditContext.onScrubberRangeChanged.AddListener(OnScrubberRangeChanged);
            OnTargetsSelectionChanged();
        }

        private void OnScrubberRangeChanged(AtomAnimationEditContext.ScrubberRangeChangedEventArgs args)
        {
            _lastClipTime = -1;
            foreach (var l in _lines)
            {
                l.rangeBegin = args.scrubberRange.rangeBegin;
                l.rangeDuration = args.scrubberRange.rangeDuration;
                l.SetVerticesDirty();
            }
        }

        private void OnTargetsSelectionChanged()
        {
            Bind(_animationEditContext.current.GetAllCurveTargets().Count() == 1
                ? _animationEditContext.current.GetAllCurveTargets().ToList()
                : _animationEditContext.GetSelectedTargets().OfType<ICurveAnimationTarget>().ToList()
            );
        }

        private void Bind(IList<ICurveAnimationTarget> targets)
        {
            Unbind();

            if ((targets?.Count ?? 0) > 0)
            {
                foreach (var target in targets)
                {
                    BindCurves(target);
                }
                _noCurves.SetActive(false);
                _scrubberLineRect.transform.parent.gameObject.SetActive(true);
                _lastClipTime = -1f;
            }
            else
            {
                _noCurves.SetActive(true);
                _scrubberLineRect.transform.parent.gameObject.SetActive(false);
            }
        }

        private void BindCurves(ICurveAnimationTarget target)
        {
            _targets.Add(target);
            target.onAnimationKeyframesRebuilt.AddListener(OnAnimationKeyframesRebuilt);
            var freeControllerV3AnimationTarget = target as FreeControllerV3AnimationTarget;
            if (freeControllerV3AnimationTarget != null)
            {
                if (_lines.Count > _maxCurves - 3) return;
#if(!HIDE_CURVES_POS)
                BindCurve(freeControllerV3AnimationTarget.x, _style.CurveLineColorX, $"{target.GetShortName()} x");
                BindCurve(freeControllerV3AnimationTarget.y, _style.CurveLineColorY, $"{target.GetShortName()} y");
                BindCurve(freeControllerV3AnimationTarget.z, _style.CurveLineColorZ, $"{target.GetShortName()} z");
#endif
#if(SHOW_CURVES_ROT)
                // To display rotation as euler angles, we have to build custom curves. But it's not that useful.
                var rotVXCurve = new BezierAnimationCurve(freeControllerV3AnimationTarget.rotX.length);
                var rotVYCurve = new BezierAnimationCurve(freeControllerV3AnimationTarget.rotX.length);
                var rotVZCurve = new BezierAnimationCurve(freeControllerV3AnimationTarget.rotX.length);
                ConvertQuaternionCurvesToEuleur(rotVXCurve, rotVYCurve, rotVZCurve, freeControllerV3AnimationTarget);
                BindCurve(rotVXCurve, new Color(1.0f, 0.8f, 0.8f), $"{target.GetShortName()} rot x");
                BindCurve(rotVYCurve, new Color(0.8f, 1.0f, 0.8f), $"{target.GetShortName()} rot y");
                BindCurve(rotVZCurve, new Color(0.8f, 0.8f, 1.0f), $"{target.GetShortName()} rot z");
                target.onAnimationKeyframesRebuilt.AddListener(() => ConvertQuaternionCurvesToEuleur(rotVXCurve, rotVYCurve, rotVZCurve, freeControllerV3AnimationTarget));
#endif
                target.onAnimationKeyframesRebuilt.AddListener(OnAnimationKeyframesRebuilt);
                return;
            }

            var floatAnimationTarget = target as JSONStorableFloatAnimationTarget;
            if (floatAnimationTarget != null)
            {
                if (_lines.Count > _maxCurves - 1) return;
                BindCurve(floatAnimationTarget.value, _style.CurveLineColorFloat, target.GetShortName());
                // ReSharper disable once RedundantJumpStatement
                return;
            }
        }

#if(SHOW_CURVES_ROT)
        private static void ConvertQuaternionCurvesToEuleur(BezierAnimationCurve rotVXCurve, BezierAnimationCurve rotVYCurve, BezierAnimationCurve rotVZCurve, FreeControllerV3AnimationTarget freeControllerV3AnimationTarget)
        {
            rotVXCurve.keys.Clear();
            rotVYCurve.keys.Clear();
            rotVZCurve.keys.Clear();
            for (var time = 0; time < freeControllerV3AnimationTarget.rotW.length; time++)
            {
                var keyX = freeControllerV3AnimationTarget.rotX.keys[time];
                var keyY = freeControllerV3AnimationTarget.rotY.keys[time];
                var keyZ = freeControllerV3AnimationTarget.rotZ.keys[time];
                var keyW = freeControllerV3AnimationTarget.rotW.keys[time];
                var rot = new Quaternion(
                    keyX.value,
                    keyY.value,
                    keyZ.value,
                    keyW.value
                );
                var eulerAngles = rot.eulerAngles;
                rotVXCurve.keys.Add(new BezierKeyframe(keyW.time, eulerAngles.x, keyW.curveType));
                rotVYCurve.keys.Add(new BezierKeyframe(keyW.time, eulerAngles.y, keyW.curveType));
                rotVZCurve.keys.Add(new BezierKeyframe(keyW.time, eulerAngles.z, keyW.curveType));
            }
        }
#endif

        private void BindCurve(BezierAnimationCurve lead, Color color, string label)
        {
            var lines = CreateCurvesLines(_linesContainer, color, label);
            _lines.Add(lines);
            lines.rangeBegin = _animationEditContext.scrubberRange.rangeBegin;
            lines.rangeDuration = _animationEditContext.scrubberRange.rangeDuration;
            lines.AddCurve(color, lead);
            lines.SetVerticesDirty();
        }

        private void Unbind()
        {
            foreach (var t in _targets)
                t.onAnimationKeyframesRebuilt.RemoveListener(OnAnimationKeyframesRebuilt);
            _targets.Clear();
            foreach (var l in _lines)
                Destroy(l.gameObject.transform.parent.gameObject);
            _lines.Clear();
        }

        private void OnAnimationKeyframesRebuilt()
        {
            StartCoroutine(DrawCurveLinesDeferred());
        }

        private IEnumerator DrawCurveLinesDeferred()
        {
            // Allow curve refresh;
            yield return 0;
            yield return 0;
            DrawCurveLinesImmediate();
        }

        private void DrawCurveLinesImmediate()
        {
            foreach (var l in _lines)
            {
                l.SetVerticesDirty();
            }
        }

        public void Update()
        {
            if (_animationEditContext == null) return;
            if (_animationEditContext.clipTime == _lastClipTime) return;
            if (_animationEditContext.scrubberRange.rangeDuration == 0) return;
            if (UIPerformance.ShouldSkip(UIPerformance.HighFrequency)) return;

            _lastClipTime = _animationEditContext.clipTime;
            var ratio = (_animationEditContext.clipTime - _animationEditContext.scrubberRange.rangeBegin) / _animationEditContext.scrubberRange.rangeDuration;
            // TODO: The line could just be a vertice instead of moving the layout
            _scrubberLineRect.anchorMin = new Vector2(ratio, 0);
            _scrubberLineRect.anchorMax = new Vector2(ratio, 1);
        }

        public void OnDestroy()
        {
            if (_animationEditContext != null)
            {
                _animationEditContext.animation.animatables.onTargetsSelectionChanged.RemoveListener(OnTargetsSelectionChanged);
                _animationEditContext.onScrubberRangeChanged.AddListener(OnScrubberRangeChanged);
            }

            foreach (var t in _targets)
                t.onAnimationKeyframesRebuilt.RemoveListener(OnAnimationKeyframesRebuilt);
        }
    }
}
