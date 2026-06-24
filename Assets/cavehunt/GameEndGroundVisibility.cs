using System.Collections.Generic;
using UnityEngine;

public static class GameEndGroundVisibility
{
    private struct RendererState
    {
        public Renderer Renderer;
        public bool WasEnabled;
    }

    private static readonly List<RendererState> hiddenRenderers = new List<RendererState>();
    private static bool hidden;

    public static void HideGround()
    {
        if (hidden) return;

        hiddenRenderers.Clear();
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !ShouldHide(renderer.transform)) continue;

            hiddenRenderers.Add(new RendererState
            {
                Renderer = renderer,
                WasEnabled = renderer.enabled
            });
            renderer.enabled = false;
        }

        hidden = true;
    }

    public static void RestoreGround()
    {
        if (!hidden) return;

        for (int i = 0; i < hiddenRenderers.Count; i++)
        {
            RendererState state = hiddenRenderers[i];
            if (state.Renderer != null)
            {
                state.Renderer.enabled = state.WasEnabled;
            }
        }

        hiddenRenderers.Clear();
        hidden = false;
    }

    private static bool ShouldHide(Transform candidate)
    {
        Transform current = candidate;
        while (current != null)
        {
            string name = current.name.ToLowerInvariant();
            if (name.Contains("ground fog")) return true;
            if (name.Contains("ground mesh")) return true;
            if (name.Contains("floor")) return true;
            if (name.Contains("boden")) return true;
            current = current.parent;
        }

        return false;
    }
}
