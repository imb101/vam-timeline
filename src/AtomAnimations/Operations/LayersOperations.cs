﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class LayersOperations
    {
        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;

        public LayersOperations(AtomAnimation animation, AtomAnimationClip clip)
        {
            _animation = animation;
            _clip = clip;
        }

        public AtomAnimationClip Add()
        {
            return _animation.CreateClip(GetNewLayerName(), GetNewAnimationName());
        }

        public void SplitLayer(List<IAtomAnimationTarget> targets)
        {
            var layerName = GetSplitLayerName(_clip.animationLayer, _animation.index.ByLayer().Select(c => c[0].animationLayer).ToList());
            foreach (var sourceClip in _animation.index.ByLayer(_clip.animationLayer).ToList())
            {
                var newClip = _animation.CreateClip(layerName, sourceClip.animationName);
                sourceClip.CopySettingsTo(newClip);
                newClip.animationLayer = layerName;
                foreach (var t in sourceClip.GetAllTargets().Where(t => targets.Any(t.TargetsSameAs)).ToList())
                {
                    sourceClip.Remove(t);
                    newClip.Add(t);
                }
            }
        }

        public string GetNewLayerName()
        {
            var layers = new HashSet<string>(_animation.clips.Select(c => c.animationLayer));
            for (var i = 1; i < 999; i++)
            {
                var layerName = "Layer " + i;
                if (!layers.Contains(layerName)) return layerName;
            }
            return Guid.NewGuid().ToString();
        }

        private string GetNewAnimationName()
        {
            for (var i = _animation.clips.Count + 1; i < 999; i++)
            {
                var animationName = "Anim " + i;
                if (_animation.clips.All(c => c.animationName != animationName)) return animationName;
            }
            return Guid.NewGuid().ToString();
        }

        private static string GetSplitLayerName(string sourceLayerName, IList<string> list)
        {
            for (var i = 1; i < 999; i++)
            {
                var animationName = $"{sourceLayerName} (Split {i})";
                if (list.All(n => n != animationName)) return animationName;
            }
            return Guid.NewGuid().ToString();
        }
    }
}
