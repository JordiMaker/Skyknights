using UnityEngine;

namespace Redbaron
{
    /// <summary>
    /// Turns a Redbaron soldier into a Quick RTS unit: box/click selection, outline, info panel,
    /// control groups and right-click move orders.
    ///
    /// Put it on the player's soldier prefab together with Quick RTS's Selectable and Mover.
    /// Quick RTS expects things a Redbaron soldier does not have, so this component:
    /// - gives the soldier its own parent object (Selectable reads and destroys transform.parent);
    /// - fills in Selectable's hidden fields before Selectable.Awake runs (execution order -1000);
    /// - keeps the info panel's health in step with the soldier's Health;
    /// - takes a fallen soldier out of the selection and the control lists.
    /// Move orders need nothing extra: Mover sets the NavMeshAgent destination and SoldierAI
    /// reads it as the player's order.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(SoldierAI))]
    public class QuickRtsUnitBridge : MonoBehaviour
    {
        public string unitName = "Soldado Entente";
        [TextArea] public string description = "Infantería de la Entente. Clic derecho en el suelo para moverla: al llegar se cubre y dispara.";
        public Sprite icon;
        public Color minimapColor = new Color(0.36f, 0.48f, 0.62f);

        global::Selectable selectable;
        SoldierAI ai;
        Transform wrapper;
        bool removed;

        void Awake()
        {
            if (transform.parent == null)
            {
                wrapper = new GameObject("Unidad_" + name).transform;
                wrapper.SetPositionAndRotation(transform.position, Quaternion.identity);
                transform.SetParent(wrapper, true);
            }

            ai = GetComponent<SoldierAI>();
            selectable = GetComponent<global::Selectable>();
            if (selectable == null) return;

            var selector = SelectorRTS.Instance;
            selectable._group = selector != null ? selector._group : 0;
            selectable._team = selector != null ? selector._team : 0;
            selectable._name = unitName;
            selectable._description = description;
            selectable._icon = icon;
            selectable._colorInMinimap = minimapColor;
            selectable.CanMove = true;
            selectable.CanAttack = false;
            selectable.CanCollect = false;
            selectable.CanSpawnTroops = false;
            selectable.IsBuildable = false;
            // With CanReceiveDamage on, Selectable.Awake reads the materials of a Renderer on this
            // same object. The soldier's mesh is on a child, so it stays off until Start.
            selectable.CanReceiveDamage = GetComponent<Renderer>() != null;
            selectable._maxHealth = 2;
            selectable._moveSpeed = ai != null ? ai.runSpeed : 3.6f;
            selectable._distanceVision = ai != null ? ai.rifleRange : 50f;
            selectable._timeBlendWhenReceiveDamage = 0.1f;
        }

        void Start()
        {
            if (wrapper != null) wrapper.name = "Unidad_" + name;
            if (selectable == null || ai == null || ai.Health == null) return;
            selectable.CanReceiveDamage = true; // shows health in the Quick RTS info panel
            selectable._maxHealth = ai.Health.maxHp;
            selectable._currentHealth = Mathf.Max(0.01f, ai.Health.hp);
            ai.Health.Died += (h, src) => RemoveFromQuickRts();
        }

        void Update()
        {
            // Selectable destroys the unit when _currentHealth reaches 0, so it never goes below 0.01.
            // A fallen soldier is removed by SoldierAI after its fall, not by Quick RTS.
            if (selectable != null && ai != null && ai.Health != null && !removed)
                selectable._currentHealth = Mathf.Max(0.01f, ai.Health.hp);
        }

        void RemoveFromQuickRts()
        {
            if (removed || selectable == null) return;
            removed = true;
            selectable._isSelected = false;
            var selector = SelectorRTS.Instance;
            if (selector != null)
            {
                selector.ObjectsSelected.Remove(gameObject);
                selector.SelectablesAndCommandable.Remove(gameObject);
            }
            if (TeamManager.Instance != null && selectable._group >= 0)
            {
                TeamManager.Instance.RemoveFromGroup(selectable, selectable._group);
                TeamManager.Instance.RemoveFromTeam(selectable, selectable._team);
            }
            var outline = GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
        }

        void OnDestroy()
        {
            if (wrapper != null && gameObject.scene.isLoaded && wrapper.childCount <= 1)
                Destroy(wrapper.gameObject);
        }
    }
}
