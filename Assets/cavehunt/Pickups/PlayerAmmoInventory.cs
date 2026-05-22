using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerAmmoInventory : MonoBehaviour
{
    [Header("Ammo")]
    [SerializeField] private AmmoType currentAmmo = AmmoType.Normal;
    [SerializeField, Min(0)] private int fireAmmo;
    [SerializeField, Min(0)] private int grenadeAmmo;
    [SerializeField, Min(0)] private int airAmmo;

    [Header("Testing Input")]
    [SerializeField] private bool allowKeyboardSwitching = true;

    public AmmoType CurrentAmmo => HasAmmo(currentAmmo) ? currentAmmo : AmmoType.Normal;

    private void Awake()
    {
        if (GetComponent<AmmoHud>() == null)
        {
            gameObject.AddComponent<AmmoHud>();
        }
    }

    private void Update()
    {
        if (!allowKeyboardSwitching || Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            SelectAmmo(AmmoType.Normal);
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            SelectAmmo(AmmoType.Fire);
        }
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            SelectAmmo(AmmoType.Grenade);
        }
        else if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            SelectAmmo(AmmoType.Air);
        }
        else if (Keyboard.current.tabKey.wasPressedThisFrame || Keyboard.current.qKey.wasPressedThisFrame)
        {
            CycleNextAmmo();
        }
    }

    public void AddAmmo(AmmoType ammoType, int amount, bool switchToAmmo = true)
    {
        if (ammoType == AmmoType.Normal) return;

        int safeAmount = Mathf.Max(1, amount);
        switch (ammoType)
        {
            case AmmoType.Fire:
                fireAmmo += safeAmount;
                break;
            case AmmoType.Grenade:
                grenadeAmmo += safeAmount;
                break;
            case AmmoType.Air:
                airAmmo += safeAmount;
                break;
        }

        if (switchToAmmo)
        {
            currentAmmo = ammoType;
        }

        Debug.Log($"Picked up {safeAmount} {ammoType} ammo. Current ammo: {CurrentAmmo}");
    }

    public bool SelectAmmo(AmmoType ammoType)
    {
        if (!HasAmmo(ammoType)) return false;

        currentAmmo = ammoType;
        Debug.Log($"Selected ammo: {CurrentAmmo}");
        return true;
    }

    public void CycleNextAmmo()
    {
        AmmoType[] order =
        {
            AmmoType.Normal,
            AmmoType.Fire,
            AmmoType.Grenade,
            AmmoType.Air
        };

        int startIndex = 0;
        for (int i = 0; i < order.Length; i++)
        {
            if (order[i] == CurrentAmmo)
            {
                startIndex = i;
                break;
            }
        }

        for (int offset = 1; offset <= order.Length; offset++)
        {
            AmmoType candidate = order[(startIndex + offset) % order.Length];
            if (SelectAmmo(candidate))
            {
                return;
            }
        }
    }

    public AmmoType ConsumeCurrentShot()
    {
        AmmoType ammoType = CurrentAmmo;
        if (ammoType == AmmoType.Normal) return AmmoType.Normal;

        switch (ammoType)
        {
            case AmmoType.Fire:
                fireAmmo = Mathf.Max(0, fireAmmo - 1);
                break;
            case AmmoType.Grenade:
                grenadeAmmo = Mathf.Max(0, grenadeAmmo - 1);
                break;
            case AmmoType.Air:
                airAmmo = Mathf.Max(0, airAmmo - 1);
                break;
        }

        if (!HasAmmo(ammoType))
        {
            currentAmmo = AmmoType.Normal;
        }

        return ammoType;
    }

    public bool HasAmmo(AmmoType ammoType)
    {
        return ammoType switch
        {
            AmmoType.Normal => true,
            AmmoType.Fire => fireAmmo > 0,
            AmmoType.Grenade => grenadeAmmo > 0,
            AmmoType.Air => airAmmo > 0,
            _ => false
        };
    }

    public int GetAmmoCount(AmmoType ammoType)
    {
        return ammoType switch
        {
            AmmoType.Normal => int.MaxValue,
            AmmoType.Fire => fireAmmo,
            AmmoType.Grenade => grenadeAmmo,
            AmmoType.Air => airAmmo,
            _ => 0
        };
    }
}

[RequireComponent(typeof(PlayerAmmoInventory))]
public class AmmoHud : MonoBehaviour
{
    [SerializeField] private float hudDistance = 1.1f;
    [SerializeField] private Vector2 localOffset = new Vector2(0f, -0.34f);
    [SerializeField] private Vector2 canvasSize = new Vector2(480f, 92f);
    [SerializeField] private Vector2 slotSize = new Vector2(92f, 78f);
    [SerializeField] private float slotGap = 16f;

    private static readonly AmmoType[] DisplayOrder =
    {
        AmmoType.Normal,
        AmmoType.Fire,
        AmmoType.Grenade,
        AmmoType.Air
    };

    private PlayerAmmoInventory inventory;
    private Canvas canvas;
    private RectTransform canvasRect;
    private AmmoSlot[] slots;
    private Font hudFont;

    private struct AmmoSlot
    {
        public AmmoType AmmoType;
        public Image Background;
        public Image ActiveMarker;
        public Text Icon;
        public Text Count;
    }

    private void Awake()
    {
        inventory = GetComponent<PlayerAmmoInventory>();
        hudFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (hudFont == null)
        {
            hudFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }

    private void Start()
    {
        BuildHud();
    }

    private void LateUpdate()
    {
        EnsureCameraParent();
        UpdateHud();
    }

    private void BuildHud()
    {
        if (canvas != null) return;

        GameObject root = new GameObject("Ammo HUD");
        root.transform.SetParent(transform, false);

        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 30;

        canvasRect = root.GetComponent<RectTransform>();
        canvasRect.sizeDelta = canvasSize;
        canvasRect.localScale = Vector3.one * 0.00145f;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        root.AddComponent<GraphicRaycaster>();

        slots = new AmmoSlot[DisplayOrder.Length];
        float totalWidth = DisplayOrder.Length * slotSize.x + (DisplayOrder.Length - 1) * slotGap;
        float startX = -totalWidth * 0.5f + slotSize.x * 0.5f;

        for (int i = 0; i < DisplayOrder.Length; i++)
        {
            slots[i] = CreateSlot(DisplayOrder[i], startX + i * (slotSize.x + slotGap));
        }

        EnsureCameraParent();
        UpdateHud();
    }

    private AmmoSlot CreateSlot(AmmoType ammoType, float x)
    {
        GameObject slotObject = new GameObject($"{ammoType} Ammo Slot");
        slotObject.transform.SetParent(canvasRect, false);

        RectTransform slotRect = slotObject.AddComponent<RectTransform>();
        slotRect.sizeDelta = slotSize;
        slotRect.anchoredPosition = new Vector2(x, 0f);

        Image background = slotObject.AddComponent<Image>();
        background.color = GetBackgroundColor(ammoType, false);

        Image activeMarker = CreateImage("Active Marker", slotRect, new Vector2(slotSize.x - 10f, 5f), new Vector2(0f, -slotSize.y * 0.5f + 5f));
        activeMarker.color = new Color(1f, 0.86f, 0.18f, 1f);

        Text icon = CreateText("Icon", slotRect, GetIconText(ammoType), 28, new Vector2(slotSize.x, 40f), new Vector2(0f, 12f));
        icon.color = GetIconColor(ammoType);
        icon.fontStyle = FontStyle.Bold;

        Text count = CreateText("Count", slotRect, "0", 18, new Vector2(slotSize.x, 28f), new Vector2(0f, -24f));
        count.color = Color.white;
        count.fontStyle = FontStyle.Bold;

        return new AmmoSlot
        {
            AmmoType = ammoType,
            Background = background,
            ActiveMarker = activeMarker,
            Icon = icon,
            Count = count
        };
    }

    private Image CreateImage(string name, RectTransform parent, Vector2 size, Vector2 position)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);

        RectTransform rect = imageObject.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        return imageObject.AddComponent<Image>();
    }

    private Text CreateText(string name, RectTransform parent, string text, int fontSize, Vector2 size, Vector2 position)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Text label = textObject.AddComponent<Text>();
        label.text = text;
        label.font = hudFont;
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;

        return label;
    }

    private void EnsureCameraParent()
    {
        if (canvasRect == null) return;

        Camera camera = Camera.main;
        if (camera == null) return;

        if (canvasRect.parent != camera.transform)
        {
            canvasRect.SetParent(camera.transform, false);
        }

        canvasRect.localPosition = new Vector3(localOffset.x, localOffset.y, hudDistance);
        canvasRect.localRotation = Quaternion.identity;
        canvasRect.localScale = Vector3.one * 0.00145f;
    }

    private void UpdateHud()
    {
        if (inventory == null || slots == null) return;

        AmmoType activeAmmo = inventory.CurrentAmmo;
        for (int i = 0; i < slots.Length; i++)
        {
            AmmoSlot slot = slots[i];
            bool isActive = slot.AmmoType == activeAmmo;
            int ammoCount = inventory.GetAmmoCount(slot.AmmoType);

            if (slot.Background != null)
            {
                slot.Background.color = GetBackgroundColor(slot.AmmoType, isActive);
            }

            if (slot.ActiveMarker != null)
            {
                slot.ActiveMarker.enabled = isActive;
            }

            if (slot.Count != null)
            {
                slot.Count.text = slot.AmmoType == AmmoType.Normal ? "--" : ammoCount.ToString();
                slot.Count.color = slot.AmmoType == AmmoType.Normal || ammoCount > 0
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.35f);
            }

            if (slot.Icon != null)
            {
                Color iconColor = GetIconColor(slot.AmmoType);
                iconColor.a = slot.AmmoType == AmmoType.Normal || ammoCount > 0 ? 1f : 0.35f;
                slot.Icon.color = iconColor;
            }
        }
    }

    private static string GetIconText(AmmoType ammoType)
    {
        return ammoType switch
        {
            AmmoType.Fire => "F",
            AmmoType.Grenade => "G",
            AmmoType.Air => "A",
            _ => "N"
        };
    }

    private static Color GetIconColor(AmmoType ammoType)
    {
        return ammoType switch
        {
            AmmoType.Fire => new Color(1f, 0.36f, 0.08f, 1f),
            AmmoType.Grenade => new Color(0.58f, 1f, 0.22f, 1f),
            AmmoType.Air => new Color(0.24f, 0.78f, 1f, 1f),
            _ => new Color(0.88f, 0.9f, 0.95f, 1f)
        };
    }

    private static Color GetBackgroundColor(AmmoType ammoType, bool active)
    {
        Color baseColor = ammoType switch
        {
            AmmoType.Fire => new Color(0.28f, 0.08f, 0.03f, 0.78f),
            AmmoType.Grenade => new Color(0.07f, 0.18f, 0.05f, 0.78f),
            AmmoType.Air => new Color(0.04f, 0.13f, 0.2f, 0.78f),
            _ => new Color(0.08f, 0.09f, 0.12f, 0.78f)
        };

        if (!active) return baseColor;

        return Color.Lerp(baseColor, new Color(1f, 0.86f, 0.18f, 0.92f), 0.28f);
    }
}
