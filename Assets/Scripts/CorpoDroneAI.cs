using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class CorpoDroneAI : MonoBehaviour
{
    [Header("Patrol Waypoints (Before Sapling is Planted)")]
    public Transform[] waypoints;
    private int currentWaypointIndex = 0;

    [Header("Tree Rush Mode (After Sapling is Planted)")]
    public bool attackTreeInLevel3 = false;
    public Transform targetTree;
    public Level3TreeDefense level3Manager;
    public float treeAttackDistance = 4.5f;
    public float damagePerSecond = 10f;
    private Vector3 startSpawnPosition;

    [Header("Stealth Detection Settings")]
    public Transform player;
    public float sightRange = 10f;
    public float crouchedSightRange = 5f;
    public float viewAngle = 60f;

    private NavMeshAgent agent;
    private ARIEPlayerController playerController;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        startSpawnPosition = transform.position;

        // Auto-find Level3Manager & Player if left unassigned in Inspector
        if (level3Manager == null)
        {
            level3Manager = FindFirstObjectByType<Level3TreeDefense>();
        }

        if (level3Manager != null && targetTree == null && level3Manager.growingTree != null)
        {
            targetTree = level3Manager.growingTree;
        }

        if (player == null)
        {
            ARIEPlayerController foundPlayer = FindFirstObjectByType<ARIEPlayerController>();
            if (foundPlayer != null) player = foundPlayer.transform;
        }

        if (player != null)
            playerController = player.GetComponent<ARIEPlayerController>();

        // Start patrolling waypoints while waiting for the player to plant the sapling
        if (waypoints != null && waypoints.Length > 0 && agent.isOnNavMesh)
        {
            agent.SetDestination(waypoints[0].position);
        }
    }

    void Update()
    {
        // Check if the sapling has been planted (defenseStarted == true)
        bool shouldRushTree = attackTreeInLevel3 && (level3Manager == null || level3Manager.defenseStarted);

        if (shouldRushTree)
        {
            RushAndDamageTree();
        }
        else
        {
            // While defenseStarted is FALSE, patrol the canyon and watch for the player!
            Patrol();
            DetectPlayer();
        }
    }

    void RushAndDamageTree()
    {
        if (targetTree == null && level3Manager != null)
        {
            targetTree = level3Manager.growingTree;
        }

        if (targetTree == null) return;

        if (agent.isOnNavMesh)
        {
            agent.stoppingDistance = 1.5f;
            agent.SetDestination(targetTree.position);
        }
        else
        {
            Vector3 movePos = new Vector3(targetTree.position.x, transform.position.y, targetTree.position.z);
            transform.position = Vector3.MoveTowards(transform.position, movePos, 3.5f * Time.deltaTime);
        }

        // Measure flat horizontal (X, Z) distance to the tree trunk
        Vector2 droneFlatPos = new Vector2(transform.position.x, transform.position.z);
        Vector2 treeFlatPos = new Vector2(targetTree.position.x, targetTree.position.z);
        float flatDistToTree = Vector2.Distance(droneFlatPos, treeFlatPos);

        if (flatDistToTree <= treeAttackDistance && level3Manager != null)
        {
            level3Manager.DamageTree(damagePerSecond);
        }
    }

    // Called when player presses E on the drone to non-violently EMP reboot it
    public void RebootAndSendBack()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.Warp(startSpawnPosition);

            bool shouldRushTree = attackTreeInLevel3 && (level3Manager == null || level3Manager.defenseStarted);
            if (shouldRushTree && targetTree != null)
                agent.SetDestination(targetTree.position);
            else if (waypoints != null && waypoints.Length > 0)
                agent.SetDestination(waypoints[currentWaypointIndex].position);
        }
        else
        {
            transform.position = startSpawnPosition;
        }
        Debug.Log("Drone non-violently rebooted and sent back to spawn perimeter!");
    }

    void Patrol()
    {
        if (waypoints == null || waypoints.Length == 0 || !agent.isOnNavMesh) return;

        agent.stoppingDistance = 0f;
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            agent.SetDestination(waypoints[currentWaypointIndex].position);
        }
    }

    void DetectPlayer()
    {
        if (player == null) return;

        float currentDetectRange = (playerController != null && playerController.isCrouching)
            ? crouchedSightRange : sightRange;

        Vector3 dirToPlayer = player.position - transform.position;
        float distanceToPlayer = dirToPlayer.magnitude;

        if (distanceToPlayer <= currentDetectRange)
        {
            float angle = Vector3.Angle(transform.forward, dirToPlayer);
            if (angle <= viewAngle)
            {
                if (Physics.Raycast(transform.position, dirToPlayer.normalized, out RaycastHit hit, currentDetectRange))
                {
                    if (hit.transform == player && playerController != null)
                    {
                        playerController.LoseBatteryLife();
                    }
                }
            }
        }
    }
}