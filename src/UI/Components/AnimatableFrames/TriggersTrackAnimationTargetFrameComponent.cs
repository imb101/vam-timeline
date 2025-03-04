using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class TriggersTrackAnimationTargetFrameComponent : AnimationTargetFrameComponentBase<TriggersTrackAnimationTarget>, TriggerHandler
    {
        public Transform popupParent;

        private UIDynamicButton _editTriggersButton;
        private CustomTrigger _trigger;

        protected override void CreateCustom()
        {
            if (!expanded) StartCoroutine(ToggleExpandedDeferred());
        }

        private IEnumerator ToggleExpandedDeferred()
        {
            yield return 0;
            if (!expanded) ToggleExpanded();
        }

        public override void SetTime(float time, bool stopped)
        {
            base.SetTime(time, stopped);

            if (stopped)
                SyncEditButton(time);
        }

        private void SyncEditButton(float time)
        {
            if (!ReferenceEquals(_trigger, null) && _trigger.startTime != time)
                CloseTriggersPanel();

            CustomTrigger trigger;
            var ms = plugin.animationEditContext.clipTime.ToMilliseconds();
            if (target.triggersMap.TryGetValue(ms, out trigger))
            {
                valueText.text = string.IsNullOrEmpty(trigger.displayName) ? "Has Triggers" : trigger.displayName;
                if (_editTriggersButton != null) _editTriggersButton.label = "Edit Triggers";
            }
            else
            {
                valueText.text = "-";
                if (_editTriggersButton != null) _editTriggersButton.label = "Create Trigger";
            }
        }

        protected override void ToggleKeyframeImpl(float time, bool on, bool mustBeOn)
        {
            if (on)
            {
                GetOrCreateTriggerAtCurrentTime();
            }
            else
            {
                target.DeleteFrame(time);
            }
            SyncEditButton(time);
        }

        protected override void CreateExpandPanel(RectTransform container)
        {
            var group = container.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 4f;
            group.padding = new RectOffset(8, 8, 8, 8);
            group.childAlignment = TextAnchor.MiddleCenter;

            _editTriggersButton = CreateExpandButton(
                group.transform,
                target.triggersMap.ContainsKey(plugin.animationEditContext.clipTime.ToMilliseconds()) ? "Edit Triggers" : "Create Trigger",
                EditTriggers);
        }

        private void EditTriggers()
        {
            if (!plugin.animationEditContext.CanEdit()) return;

            _trigger = GetOrCreateTriggerAtCurrentTime();

            _trigger.handler = this;
            _trigger.triggerActionsParent = popupParent;
            _trigger.atom = plugin.containingAtom;
            _trigger.InitTriggerUI();
            // TODO: Because everything is protected/private in VaM, I cannot use CheckMissingReceiver
            _trigger.OpenTriggerActionsPanel();
            // When already open but in the wrong parent:
            _trigger.SetPanelParent(popupParent);
        }

        private CustomTrigger GetOrCreateTriggerAtCurrentTime()
        {
            CustomTrigger trigger;
            var ms = plugin.animationEditContext.clipTime.Snap().ToMilliseconds();
            if (!target.triggersMap.TryGetValue(ms, out trigger))
            {
                trigger = new CustomTrigger();
                target.SetKeyframe(ms, trigger);
            }
            return trigger;
        }

        public void OnDisable()
        {
            CloseTriggersPanel();
        }

        private void CloseTriggersPanel()
        {
            if (_trigger == null) return;
            _trigger.ClosePanel();
            _trigger = null;
        }

        #region Trigger handler

        void TriggerHandler.RemoveTrigger(Trigger t)
        {
            throw new NotImplementedException();
        }

        void TriggerHandler.DuplicateTrigger(Trigger t)
        {
            throw new NotImplementedException();
        }

        RectTransform TriggerHandler.CreateTriggerActionsUI()
        {
            return Instantiate(VamPrefabFactory.triggerActionsPrefab);
        }

        RectTransform TriggerHandler.CreateTriggerActionMiniUI()
        {
            return Instantiate(VamPrefabFactory.triggerActionMiniPrefab);
        }

        RectTransform TriggerHandler.CreateTriggerActionDiscreteUI()
        {
            return Instantiate(VamPrefabFactory.triggerActionDiscretePrefab);
        }

        RectTransform TriggerHandler.CreateTriggerActionTransitionUI()
        {
            return Instantiate(VamPrefabFactory.triggerActionTransitionPrefab);
        }

        void TriggerHandler.RemoveTriggerActionUI(RectTransform rt)
        {
            if (rt != null) Destroy(rt.gameObject);
        }

        #endregion
    }
}
