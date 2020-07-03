using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class CurveTypePopup : MonoBehaviour
    {
        private const string _noKeyframeCurveType = "(No Keyframe)";

        private readonly HashSet<string> _curveTypes = new HashSet<string>();

        public static CurveTypePopup Create(VamPrefabFactory prefabFactory)
        {
            var curveTypeJSON = new JSONStorableStringChooser(StorableNames.ChangeCurve, CurveTypeValues.DisplayCurveTypes, "", "Change Curve");
            var curveTypeUI = prefabFactory.CreateScrollablePopup(curveTypeJSON);
            curveTypeUI.popupPanelHeight = 300f;

            var curveTypePopup = curveTypeUI.gameObject.AddComponent<CurveTypePopup>();
            curveTypePopup.curveTypeJSON = curveTypeJSON;
            curveTypePopup.curveTypeUI = curveTypeUI;

            return curveTypePopup;
        }

        public JSONStorableStringChooser curveTypeJSON;
        public UIDynamicPopup curveTypeUI;
        private AtomAnimation _animation;
        private AtomAnimationClip _current => _animation.current;
        private bool _listening;

        public void Bind(AtomAnimation animation)
        {
            _animation = animation;
            curveTypeJSON.setCallbackFunction = ChangeCurve;
            OnEnable();
        }

        private void ChangeCurve(string curveType)
        {
            if (_animation.isPlaying) return;

            if (string.IsNullOrEmpty(curveType) || curveType.StartsWith("("))
            {
                RefreshCurrentCurveType(_animation.clipTime);
                return;
            }
            float time = _animation.clipTime.Snap();

            foreach (var target in _current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>())
                target.ChangeCurve(time, curveType);

            RefreshCurrentCurveType(_animation.clipTime);
        }

        private void RefreshCurrentCurveType(float currentClipTime)
        {
            if (curveTypeJSON == null) return;

            var time = currentClipTime.Snap();
            var ms = time.ToMilliseconds();
            _curveTypes.Clear();
            foreach (var target in _current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>())
            {
                KeyframeSettings v;
                if (!target.settings.TryGetValue(ms, out v)) continue;
                _curveTypes.Add(v.curveType);
            }
            if (_curveTypes.Count == 0)
            {
                curveTypeJSON.valNoCallback = _noKeyframeCurveType;
                curveTypeUI.popup.topButton.interactable = false;
            }
            else if (_curveTypes.Count == 1)
            {
                curveTypeJSON.valNoCallback = _curveTypes.First().ToString();
                curveTypeUI.popup.topButton.interactable = true;
            }
            else
            {
                curveTypeJSON.valNoCallback = "(" + string.Join("/", _curveTypes.ToArray()) + ")";
                curveTypeUI.popup.topButton.interactable = true;
            }
        }

        private void OnTimeChanged(AtomAnimation.TimeChangedEventArgs args)
        {
            RefreshCurrentCurveType(args.currentClipTime);
        }

        private void OnTargetsSelectionChanged()
        {
            RefreshCurrentCurveType(_animation.clipTime);
        }

        private void OnAnimationRebuilt()
        {
            RefreshCurrentCurveType(_animation.clipTime);
        }

        public void OnEnable()
        {
            if (_listening || _animation == null) return;
            _listening = true;
            _animation.onTimeChanged.AddListener(OnTimeChanged);
            _animation.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            _animation.onAnimationRebuilt.AddListener(OnAnimationRebuilt);
            OnTimeChanged(_animation.timeArgs);
        }

        public void OnDisable()
        {
            if (!_listening || _animation == null) return;
            _animation.onTimeChanged.RemoveListener(OnTimeChanged);
            _animation.onTargetsSelectionChanged.RemoveListener(OnTargetsSelectionChanged);
            _animation.onAnimationRebuilt.RemoveListener(OnAnimationRebuilt);
            _listening = false;
        }
    }
}
