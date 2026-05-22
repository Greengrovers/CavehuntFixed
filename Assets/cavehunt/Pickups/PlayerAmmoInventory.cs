using UnityEngine;
using UnityEngine.InputSystem;

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
