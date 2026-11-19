using UnityEngine;
using UnityEngine.AI;

namespace AhMa
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class GrandmaPursuit : MonoBehaviour
    {
        public enum BehaviourState { Patrol, Chase, Search, Restored }

        [SerializeField] private Transform player;
        [SerializeField] private float sightDistance = 12f;
        [SerializeField] private float fieldOfView = 110f;
        [SerializeField] private float chaseSpeed = 3.2f;
        [SerializeField] private float searchSeconds = 5f;
        [SerializeField] private float catchDistance = 1.2f;
        [SerializeField] private LayerMask sightMask = ~0;

        public BehaviourState State { get; private set; }
        public int TimesCaught { get; private set; }
        public bool CanSeePlayer { get; private set; }

        private NavMeshAgent agent;
        private Animator animator;
        private Vector3 home;
        private Vector3 playerSpawn;
        private Quaternion playerRotation;
        private Vector3 lastSeen;
        private float searchUntil;
        private float nextPatrol;
        private float graceUntil;
        private float nextDecision;
        private NavMeshPath patrolPath;
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int MotionSpeed = Animator.StringToHash("MotionSpeed");
        private static readonly int Grounded = Animator.StringToHash("Grounded");

        private void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            patrolPath = new NavMeshPath();
            animator = GetComponentInChildren<Animator>();
            home = transform.position;
            if (player == null)
            {
                Debug.LogError("阿嬤尚未指定玩家。", this);
                enabled = false;
                return;
            }

            playerSpawn = player.position;
            playerRotation = player.rotation;
            if (!NavMesh.SamplePosition(home, out var hit, 3f, agent.areaMask))
            {
                Debug.LogError("阿嬤附近沒有可行走的導航網格。", this);
                enabled = false;
                return;
            }

            agent.Warp(hit.position);
            home = hit.position;
            if (animator != null)
            {
                animator.applyRootMotion = false;
                // Starter Assets clips contain callbacks for the player-only audio controller.
                animator.fireEvents = false;
                animator.SetBool(Grounded, true);
                animator.SetFloat(MotionSpeed, 1f);
            }
            graceUntil = Time.time + 2f;
        }

        private void Update()
        {
            if (State == BehaviourState.Restored)
                return;
            if (player == null || !agent.isOnNavMesh)
                return;

            if (animator != null)
                animator.SetFloat(Speed, agent.velocity.magnitude, 0.1f, Time.deltaTime);

            if (Time.time < nextDecision)
                return;
            nextDecision = Time.time + 0.15f;
            CanSeePlayer = Time.time >= graceUntil && HasLineOfSight();

            if (CanSeePlayer)
            {
                State = BehaviourState.Chase;
                lastSeen = player.position;
                searchUntil = Time.time + searchSeconds;
                agent.speed = chaseSpeed;
                agent.isStopped = false;
                agent.SetDestination(lastSeen);
                if (Vector3.Distance(transform.position, player.position) <= catchDistance)
                    CatchPlayer();
                return;
            }

            if (State == BehaviourState.Chase)
            {
                State = BehaviourState.Search;
                agent.SetDestination(lastSeen);
            }

            if (State == BehaviourState.Search && Time.time < searchUntil)
            {
                agent.speed = 2f;
                return;
            }

            State = BehaviourState.Patrol;
            agent.speed = 1.5f;
            if (Time.time >= nextPatrol && !agent.pathPending &&
                (!agent.hasPath || agent.remainingDistance < 0.6f))
                ChoosePatrolDestination();
        }

        public bool RestoreHuman()
        {
            if (State == BehaviourState.Restored)
                return false;
            State = BehaviourState.Restored;
            CanSeePlayer = false;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            if (animator != null)
                animator.SetFloat(Speed, 0f);
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (renderer.name.EndsWith("眼白"))
                    renderer.material.SetColor("_BaseColor", new Color(0.94f, 0.91f, 0.85f));
            }
            return true;
        }

        public bool HasLineOfSight()
        {
            if (player == null)
                return false;
            Vector3 origin = transform.position + Vector3.up * 1.55f;
            Vector3 target = player.position + Vector3.up * 1.2f;
            Vector3 direction = target - origin;
            if (direction.sqrMagnitude > sightDistance * sightDistance)
                return false;
            if (State != BehaviourState.Chase &&
                Vector3.Angle(transform.forward, direction) > fieldOfView * 0.5f)
                return false;

            // Ignore the two characters, but let solid scenery block perception.
            foreach (var hit in Physics.RaycastAll(origin, direction.normalized,
                         direction.magnitude, sightMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(player))
                    continue;
                return false;
            }
            return true;
        }

        private void ChoosePatrolDestination()
        {
            nextPatrol = Time.time + 2f;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * 7f;
                Vector3 candidate = home + new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(candidate, out var hit, 2f, agent.areaMask))
                    continue;
                if (Mathf.Abs(hit.position.y - home.y) > 1f)
                    continue;
                if (!agent.CalculatePath(hit.position, patrolPath) ||
                    patrolPath.status != NavMeshPathStatus.PathComplete)
                    continue;
                agent.SetPath(patrolPath);
                return;
            }
        }

        private void CatchPlayer()
        {
            TimesCaught++;
            var controller = player.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled)
                controller.enabled = false;
            player.SetPositionAndRotation(playerSpawn, playerRotation);
            if (wasEnabled)
                controller.enabled = true;

            agent.ResetPath();
            agent.Warp(home);
            agent.velocity = Vector3.zero;
            State = BehaviourState.Patrol;
            CanSeePlayer = false;
            graceUntil = Time.time + 3f;
            nextPatrol = graceUntil;
            Debug.Log("被阿嬤發現了，已回到起點。", this);
        }
    }
}
