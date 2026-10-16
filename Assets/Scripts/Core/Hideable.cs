using System;
using System.Collections.Generic;
using UnityEngine;

// Hides an object without deactivating it: its renderers, canvases, colliders and 2D bodies are
// switched off, but the GameObject stays active. NGO only handles active objects in places
// (despawn on shutdown, late-join synchronization of disabled behaviours), so spawned objects
// hidden with SetActive(false) were left behind or half synchronized.
// Local to each peer: whatever hides an object does it on every machine (RPC or replicated state).
// Added at runtime by On(), so no prefab needs it.
[DisallowMultipleComponent]
public class Hideable : MonoBehaviour
{
    // Only what this component switched off is switched back on: anything the game had already
    // disabled stays as it was.
    private readonly List<Renderer> _renderers = new();
    private readonly List<Canvas> _canvases = new();
    private readonly List<Collider2D> _colliders = new();
    private readonly List<Rigidbody2D> _bodies = new();

    public bool IsVisible { get; private set; } = true;

    // Raised after each change, with the new visibility.
    public event Action<bool> VisibilityChanged;

    public static Hideable On(Component component)
    {
        return On(component.gameObject);
    }

    public static Hideable On(GameObject target)
    {
        return target.TryGetComponent(out Hideable hideable) ? hideable : target.AddComponent<Hideable>();
    }

    public void SetVisible(bool visible)
    {
        if (visible == IsVisible)
            return;

        IsVisible = visible;

        if (visible)
            ShowAgain();
        else
            Hide();

        VisibilityChanged?.Invoke(visible);
    }

    private void Hide()
    {
        // Inactive children included: one shown while hidden (an indicator) must stay invisible.
        foreach (Renderer rendererComponent in GetComponentsInChildren<Renderer>(true))
            Disable(rendererComponent, _renderers);

        foreach (Canvas canvas in GetComponentsInChildren<Canvas>(true))
            Disable(canvas, _canvases);

        foreach (Collider2D colliderComponent in GetComponentsInChildren<Collider2D>(true))
            Disable(colliderComponent, _colliders);

        foreach (Rigidbody2D body in GetComponentsInChildren<Rigidbody2D>(true))
        {
            if (!body.simulated)
                continue;

            // Frozen where it is: it must not shoot off with its old speed when shown again.
            if (body.bodyType == RigidbodyType2D.Dynamic)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            body.simulated = false;
            _bodies.Add(body);
        }
    }

    private void ShowAgain()
    {
        Restore(_renderers);
        Restore(_canvases);
        Restore(_colliders);

        foreach (Rigidbody2D body in _bodies)
        {
            if (body != null)
                body.simulated = true;
        }
        _bodies.Clear();
    }

    private static void Disable<T>(T component, List<T> disabled) where T : Behaviour
    {
        if (!component.enabled)
            return;

        component.enabled = false;
        disabled.Add(component);
    }

    private static void Restore<T>(List<T> disabled) where T : Behaviour
    {
        foreach (T component in disabled)
        {
            if (component != null)
                component.enabled = true;
        }
        disabled.Clear();
    }

    // Renderer is not a Behaviour: same thing, through its own "enabled".
    private static void Disable(Renderer component, List<Renderer> disabled)
    {
        if (!component.enabled)
            return;

        component.enabled = false;
        disabled.Add(component);
    }

    private static void Restore(List<Renderer> disabled)
    {
        foreach (Renderer component in disabled)
        {
            if (component != null)
                component.enabled = true;
        }
        disabled.Clear();
    }
}