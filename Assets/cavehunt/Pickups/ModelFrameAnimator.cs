using UnityEngine;

public class ModelFrameAnimator : MonoBehaviour
{
    [SerializeField] private Transform[] frames;
    [SerializeField, Min(1f)] private float framesPerSecond = 12f;
    [SerializeField] private bool randomStartFrame = true;
    [SerializeField] private string[] excludedChildNames = { "Grenade" };
    [SerializeField] private bool detachExcludedChildrenToParent = true;

    private int currentFrame;
    private float timer;

    private void Awake()
    {
        DetachExcludedChildren();
        CollectFramesIfNeeded();
    }

    private void OnEnable()
    {
        DetachExcludedChildren();
        CollectFramesIfNeeded();
        currentFrame = randomStartFrame && frames.Length > 0 ? Random.Range(0, frames.Length) : 0;
        timer = 0f;
        ApplyFrame();
    }

    private void Update()
    {
        if (frames == null || frames.Length <= 1) return;

        timer += Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);
        while (timer >= frameDuration)
        {
            timer -= frameDuration;
            currentFrame = (currentFrame + 1) % frames.Length;
            ApplyFrame();
        }
    }

    private void CollectFramesIfNeeded()
    {
        if (frames != null && frames.Length > 0) return;

        int childCount = transform.childCount;
        int frameCount = 0;
        for (int i = 0; i < childCount; i++)
        {
            if (!ShouldExclude(transform.GetChild(i)))
            {
                frameCount++;
            }
        }

        frames = new Transform[frameCount];
        int frameIndex = 0;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (ShouldExclude(child)) continue;

            frames[frameIndex] = child;
            frameIndex++;
        }
    }

    private void ApplyFrame()
    {
        if (frames == null || frames.Length == 0) return;

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
            {
                frames[i].gameObject.SetActive(i == currentFrame);
            }
        }
    }

    private bool ShouldExclude(Transform child)
    {
        if (child == null || excludedChildNames == null) return false;

        for (int i = 0; i < excludedChildNames.Length; i++)
        {
            string excludedName = excludedChildNames[i];
            if (!string.IsNullOrEmpty(excludedName) && child.name == excludedName)
            {
                return true;
            }
        }

        return false;
    }

    private void DetachExcludedChildren()
    {
        if (!detachExcludedChildrenToParent || transform.parent == null) return;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (ShouldExclude(child))
            {
                child.SetParent(transform.parent, true);
            }
        }
    }
}
