using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CoffeeManiaVPN.Helpers;

public static class SmoothScrollHelper
{
    private const double SmoothTimeMs = 220;
    private const double StopThreshold = 0.15;

    private static readonly Dictionary<ScrollViewer, ScrollAnimation> Animations = new();

    private sealed class ScrollAnimation
    {
        public EventHandler? Handler { get; set; }
        public double TargetOffset { get; set; }
        public long LastFrameTicks { get; set; }
    }

    public static void ScrollBy(ScrollViewer viewer, double delta)
    {
        if (viewer.ScrollableHeight <= 0)
            return;

        if (MotionPreferences.SimplifiedAnimations)
        {
            viewer.ScrollToVerticalOffset(Math.Clamp(viewer.VerticalOffset + delta, 0, viewer.ScrollableHeight));
            return;
        }

        if (!Animations.TryGetValue(viewer, out var animation))
        {
            animation = new ScrollAnimation
            {
                TargetOffset = viewer.VerticalOffset,
                LastFrameTicks = Environment.TickCount64
            };

            EventHandler handler = (_, _) => OnRendering(viewer, animation);
            animation.Handler = handler;
            Animations[viewer] = animation;
            CompositionTarget.Rendering += handler;
        }

        animation.TargetOffset = Math.Clamp(animation.TargetOffset + delta, 0, viewer.ScrollableHeight);
    }

    private static void OnRendering(ScrollViewer viewer, ScrollAnimation animation)
    {
        var now = Environment.TickCount64;
        var deltaMs = Math.Clamp(now - animation.LastFrameTicks, 1, 32);
        animation.LastFrameTicks = now;

        var current = viewer.VerticalOffset;
        var target = animation.TargetOffset;
        var delta = target - current;

        if (Math.Abs(delta) <= StopThreshold)
        {
            viewer.ScrollToVerticalOffset(target);
            Stop(viewer, animation);
            return;
        }

        var factor = 1.0 - Math.Exp(-deltaMs / SmoothTimeMs);
        viewer.ScrollToVerticalOffset(current + delta * factor);
    }

    private static void Stop(ScrollViewer viewer, ScrollAnimation animation)
    {
        if (animation.Handler is EventHandler handler)
            CompositionTarget.Rendering -= handler;

        Animations.Remove(viewer);
    }
}
