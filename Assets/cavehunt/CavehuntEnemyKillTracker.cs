using UnityEngine;

[RequireComponent(typeof(Damageable))]
public class CavehuntEnemyKillTracker : MonoBehaviour
{
    [SerializeField] private CavehuntEnemyRole role = CavehuntEnemyRole.Tutorial;
    [SerializeField] private CavehuntEncounterDirector director;

    private Damageable damageable;

    public CavehuntEnemyRole Role => role;

    public void Configure(CavehuntEnemyRole newRole, CavehuntEncounterDirector newDirector)
    {
        role = newRole;
        director = newDirector;
        EnsureSubscribed();
    }

    private void OnEnable()
    {
        EnsureSubscribed();
    }

    private void OnDisable()
    {
        if (damageable != null)
        {
            damageable.Died -= HandleDied;
        }
    }

    private void EnsureSubscribed()
    {
        if (damageable == null)
        {
            damageable = GetComponent<Damageable>();
        }

        if (director == null)
        {
            director = CavehuntEncounterDirector.Resolve(false);
        }

        if (damageable == null) return;

        damageable.Died -= HandleDied;
        damageable.Died += HandleDied;
    }

    private void HandleDied()
    {

        if (director == null)
        {
            director = CavehuntEncounterDirector.Resolve(false);
        }

        if (director != null)
        {
            director.ReportEnemyDefeated(role, this);
        }
    }
}
