using UnityEngine;

public class ModelFrameAnimator : MonoBehaviour
{
    [SerializeField] private Transform[] frames;
    [SerializeField, Min(1f)] private float framesPerSecond = 12f;
    [SerializeField] private bool randomStartFrame = true;

    private int currentFrame;
    private float timer;

    private void Awake()
    {
        CollectFramesIfNeeded();
    }

    private void OnEnable()
    {
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
        frames = new Transform[childCount];
        for (int i = 0; i < childCount; i++)
        {
            frames[i] = transform.GetChild(i);
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
}
