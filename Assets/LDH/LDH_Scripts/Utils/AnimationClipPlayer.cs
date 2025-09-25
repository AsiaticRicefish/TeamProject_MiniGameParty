using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LDH_Util
{
    public static class AnimationClipPlayer
    {
        public static (PlayableGraph, AnimationClipPlayable) Play(AnimationClip clip, Animator animator, float speed = 1f)
        {
            var graph = PlayableGraph.Create("OneShot");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            var playable = AnimationClipPlayable.Create(graph, clip);
            
            playable.SetSpeed(speed);
            
            var output = AnimationPlayableOutput.Create(graph, "out", animator);
            output.SetSourcePlayable(playable);
            graph.Play();
            return (graph, playable); // 필요 시 Stop/Destroy
        }
    }
}