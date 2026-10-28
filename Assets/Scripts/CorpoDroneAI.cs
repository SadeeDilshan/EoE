using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class CorpoDroneAI : MonoBehaviour
{
    [Header("Level 2: Patrol Waypoints")]
    public Transform[] waypoints;
    private int currentWaypointIndex = 0;

    [Header("Level 3: Rush & Damage Tree Mode")]
    public bool attackTreeInLevel3 = true; // Defaulted to TRUE for testing!
    public Transform targetTree;
    public Level3TreeDefense level3Manager;
    public float treeAttackDistance = 4.5f; // Increased so big trees don't block range!
    public float damagePerSecond = 10f;
    private Vector3 startSpawnPosition;

    [Header("Stealth Detection (Level 2)")]
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

        // Auto-find Level3Manager & GrowingTree if you are in Level 3!
        if (level3Manager == null)
        {
            level3Manager = FindFirstObjectByType<Level3TreeDefense>();
        }

        if (level3Manager != null)
        {
            attackTreeInLevel3 = true; // Automatically enables attack mode in Level 3!
            if (targetTree == null && level3Manager.growingTree != null)
            {
                targetTree = level3Manager.growingTree;
            }
        }
        else
        {
            attackTreeInLevel3 = false; // In Level 2, stay in Patrol mode
        }

        if (player == null)
        {
            ARIEPlayerController foundPlayer = FindFirstObjectByType<ARIEPlayerController>();
            if (foundPlayer != null) player = foundPlayer.transform;
        }

        if (player != null)
            playerController = player.GetComponent<ARIEPlayerController>();

        if (!attackTreeInLevel3 && waypoints.Length > 0)
            agent.SetDestination(waypoints[0].position);
    }

    void Update()
    {
        if (attackTreeInLevel3)
        {
            RushAndDamageTree();
        }
        else
        {
            Patrol();
            DetectPlayer();
        }
    }

    void RushAndDamageTree()
    {
        if (targetTree == null)
        {
            Debug.LogWarning(gameObject.name + ": Target Tree is missing! Assign GrowingTree to Level3Manager!");
            return;
        }

        // Only move if the drone is properly placed on a baked blue NavMesh floor
        if (agent.isOnNavMesh)
        {
            agent.stoppingDistance = 1.5f;
            agent.SetDestination(targetTree.position);
        }
        else
        {
            // Fallback movement if you forgot to Bake NavMesh in Level 3!
            Vector3 movePos = new Vector3(targetTree.position.x, transform.position.y, targetTree.position.z);
            transform.position = Vector3.MoveTowards(transform.position, movePos, 3.5f * Time.deltaTime);
        }

        // Measure flat horizontal (X, Z) distance so a tall tree's Y height doesn't mess up the distance!
        Vector2 droneFlatPos = new Vector2(transform.position.x, transform.position.z);
        Vector2 treeFlatPos = new Vector2(targetTree.position.x, targetTree.position.z);
        float flatDistToTree = Vector2.Distance(droneFlatPos, treeFlatPos);

        if (flatDistToTree <= treeAttackDistance && level3Manager != null)
        {
            level3Manager.DamageTree(damagePerSecond);
        }
    }

    public void RebootAndSendBack()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.Warp(startSpawnPosition);
            if (targetTree != null)
                agent.SetDestination(targetTree.position);
        }
        else
        {
            transform.position = startSpawnPosition;
        }
        Debug.Log("Drone non-violently rebooted and sent back to perimeter!");
    }

    void Patrol()
    {
        if (waypoints.Length == 0 || !agent.isOnNavMesh) return;

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