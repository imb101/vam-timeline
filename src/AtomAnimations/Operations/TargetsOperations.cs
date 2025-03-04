﻿using System.Linq;

namespace VamTimeline
{
    public class TargetsOperations
    {
        private readonly Atom _containingAtom;
        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;

        public TargetsOperations(Atom containingAtom, AtomAnimation animation, AtomAnimationClip clip)
        {
            _containingAtom = containingAtom;
            _animation = animation;
            _clip = clip;
        }

        public FreeControllerV3AnimationTarget Add(FreeControllerV3 fc)
        {
            bool subscene = false;
            if (_containingAtom.type == "SubScene")
            { subscene = true; if (fc == null || fc.containingAtom.containingSubScene.containingAtom != _containingAtom) return null; }            
            else
            { if (fc == null || fc.containingAtom != _containingAtom) return null; }

            var target = _clip.targetControllers.FirstOrDefault(t => t.animatableRef.Targets(fc));
            if (target != null) return target;
            foreach (var clip in _animation.index.ByLayer(_clip.animationLayer))
            {
                var t = clip.Add(_animation.animatables.GetOrCreateController(fc, subscene));
                if (t == null) continue;
                t.SetKeyframeToCurrent(0f);
                t.SetKeyframeToCurrent(clip.animationLength);
                if (clip == _clip) target = t;
            }
            return target;
        }

        public void AddSelectedController()
        {
            var selected = SuperController.singleton.GetSelectedController();

            if (_containingAtom.type == "SubScene")
            { if (selected == null || selected.containingAtom.containingSubScene.containingAtom != _containingAtom) return; }
            else
            { if (selected == null || selected.containingAtom != _containingAtom) return; }
                        
            if (_animation.index.ByController().Any(kvp => kvp.Key.Targets(selected))) return;
            Add(selected);
        }
    }
}
