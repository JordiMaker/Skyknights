using UnityEngine;
using UnityEngine.AI;

namespace Redbaron
{
    /// <summary>
    /// Drives the Animator of an animated unit look (a rigged FBX: the Asset Store mechs) from the unit's movement:
    /// the controller's "Speed" float is the agent's speed over the type's walking speed (0 standing, 1 walking, more running).
    /// Built by UnitModels.ApplyAnimated.
    /// </summary>
    public class AnimatedLook : MonoBehaviour
    {
        public Animator animator;
        public float walkSpeed = 2f;
        public string speedParam = "Speed";
        NavMeshAgent agent;
        int speedId;
        bool hasParam;

        void Start()
        {
            agent = GetComponentInParent<NavMeshAgent>();
            if (animator == null) animator = GetComponent<Animator>();
            speedId = Animator.StringToHash(speedParam);
            if (animator != null && animator.runtimeAnimatorController != null)
                foreach (var p in animator.parameters) if (p.nameHash == speedId) hasParam = true;
        }

        void Update()
        {
            if (animator == null || !hasParam || agent == null) return;
            float v = agent.velocity.magnitude / Mathf.Max(0.1f, walkSpeed);
            animator.SetFloat(speedId, v, 0.15f, Time.deltaTime);
        }
    }
}
