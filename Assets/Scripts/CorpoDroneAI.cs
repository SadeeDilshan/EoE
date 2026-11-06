using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class CorpoDroneAI : MonoBehaviour
{
    [Header("Patrol & Movement Speeds")]
    public Transform[] waypoints;
    public float patrolSpeed = 3.8f;
    public float chaseSpeed = 6.2f;
    public float retreatSpeed = 2.0f;      // Slow retreat speed while rebooting
    public float retreatDistance = 35f;    // How far it backs away from the tree/player
    private int currentWaypointIndex = 0;

    [Header("Red Vision Cone Settings")]
    public float detectionRange = 13f;
    public float detectionAngle = 50f;
    public Color alertVisionColor = new Color(1f, 0.12f, 0.12f, 0.26f);
    public Color rebootedVisionColor = new Color(0.15f, 0.95f, 0.85f, 0.18f);

    [Header("Level 3 Tree Assault")]
    public bool attackTreeInLevel3 = false;
    public Transform targetTree;
    public Level3TreeDefense level3Manager;
    public Transform player;

    [Header("Short-Circuit / EMP State")]
    public bool isShortCircuited = false;
    public bool isPermanentlyRebooted = false;
    public float shortCircuitDuration = 12f;

    private NavMeshAgent agent;
    private MeshRenderer visionConeRenderer;
    private Light visionSpotlight;
    private float treeDamageCooldown = 0f;
    private Coroutine empCoroutine;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (player == null)
        {
            ARIEPlayerController p = FindFirstObjectByType<ARIEPlayerController>();
            if (p != null) player = p.transform;
        }

        BuildTransparentRedVisionCone();

        if (agent != null && agent.isOnNavMesh && waypoints != null && waypoints.Length > 0)
        {
            agent.speed = patrolSpeed;
            agent.SetDestination(waypoints[0].position);
        }
    }

    void Update()
    {
        // If the game is won or drone is currently retreating in EMP reboot mode, skip normal attack/patrol
        if (isPermanentlyRebooted || isShortCircuited) return;

        if (agent == null || !agent.isOnNavMesh) return;

        // Phase 3: Rush the Sapling when red!
        if (attackTreeInLevel3 && targetTree != null)
        {
            agent.isStopped = false;
            agent.speed = chaseSpeed;
            agent.SetDestination(targetTree.position);

            float distToTree = Vector3.Distance(transform.position, targetTree.position);
            if (distToTree <= 3.2f)
            {
                treeDamageCooldown -= Time.deltaTime;
                if (treeDamageCooldown <= 0f)
                {
                    treeDamageCooldown = 1.2f;
                    if (level3Manager != null)
                        level3Manager.DamageTree(8f);
                }
            }
            return;
        }

        // Standard Patrol + Check if Player enters the Red Vision Cone
        PatrolWaypoints();
        CheckPlayerInVisionCone();
    }

    void PatrolWaypoints()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        agent.speed = patrolSpeed;
        if (!agent.pathPending && agent.remainingDistance < 1.2f)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            if (waypoints[currentWaypointIndex] != null)
                agent.SetDestination(waypoints[currentWaypointIndex].position);
        }
    }

    void CheckPlayerInVisionCone()
    {
        if (player == null) return;

        ARIEPlayerController pc = player.GetComponent<ARIEPlayerController>();
        float effectiveRange = (pc != null && pc.IsCrouching) ? detectionRange * 0.65f : detectionRange;

        Vector3 toPlayer = player.position - transform.position;
        Vector3 flatToPlayer = new Vector3(toPlayer.x, 0f, toPlayer.z);
        float dist = flatToPlayer.magnitude;

        if (dist <= effectiveRange)
        {
            float angle = Vector3.Angle(transform.forward, flatToPlayer.normalized);
            if (angle <= detectionAngle * 0.5f)
            {
                if (!Physics.Linecast(transform.position + Vector3.up * 0.5f, player.position + Vector3.up * 0.5f, out RaycastHit hit) ||
                    hit.transform == player || hit.transform.IsChildOf(player))
                {
                    if (pc != null)
                        pc.LoseBatteryLife();
                }
            }
        }
    }

    // ==================== RIGHT-CLICK EMP RETREAT & REBOOT ====================

    public void ShortCircuitEMP()
    {
        if (isShortCircuited || isPermanentlyRebooted) return;
        if (empCoroutine != null) StopCoroutine(empCoroutine);
        empCoroutine = StartCoroutine(ShortCircuitRetreatRoutine());
    }

    IEnumerator ShortCircuitRetreatRoutine()
    {
        isShortCircuited = true;
        ARIEPlayerController.totalDronesRebooted++;

        // Switch Vision Cone & Spotlight to friendly Cyan while rebooting
        if (visionConeRenderer != null)
            visionConeRenderer.material.color = rebootedVisionColor;
        if (visionSpotlight != null)
            visionSpotlight.color = new Color(0.2f, 1f, 0.9f);

        // Calculate a retreat point far away from the Growing Tree (or Player if not in Level 3)
        Vector3 threatOrigin = (attackTreeInLevel3 && targetTree != null)
            ? targetTree.position
            : (player != null ? player.position : transform.position - transform.forward * 5f);

        Vector3 retreatDir = (transform.position - threatOrigin);
        retreatDir.y = 0f;
        if (retreatDir.sqrMagnitude < 0.01f)
            retreatDir = -transform.forward;
        retreatDir.Normalize();

        // Turn the drone 180 degrees around immediately so it faces away from the tree
        transform.rotation = Quaternion.LookRotation(retreatDir, Vector3.up);

        Vector3 desiredRetreatPos = transform.position + retreatDir * retreatDistance;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = retreatSpeed; // Move away slowly!

            if (NavMesh.SamplePosition(desiredRetreatPos, out NavMeshHit navHit, 15f, NavMesh.AllAreas))
                agent.SetDestination(navHit.position);
            else
                agent.SetDestination(desiredRetreatPos);
        }

        // Wait while the drone slowly retreats away from the tree
        yield return new WaitForSeconds(shortCircuitDuration);

        // If the game was won during the retreat, stay permanently rebooted
        if (isPermanentlyRebooted) yield break;

        // Turn Vision Cone back to Red Alert & resume attacking the tree (or patrolling)
        if (visionConeRenderer != null)
            visionConeRenderer.material.color = alertVisionColor;
        if (visionSpotlight != null)
            visionSpotlight.color = new Color(1f, 0.12f, 0.08f);

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            if (attackTreeInLevel3 && targetTree != null)
            {
                agent.speed = chaseSpeed;
                agent.SetDestination(targetTree.position);
            }
            else if (waypoints != null && waypoints.Length > 0 && waypoints[currentWaypointIndex] != null)
            {
                agent.speed = patrolSpeed;
                agent.SetDestination(waypoints[currentWaypointIndex].position);
            }
        }

        isShortCircuited = false;
    }

    // ==================== PERMANENT SHUTDOWN ON VICTORY ====================

    public void PermanentVictoryReboot()
    {
        isPermanentlyRebooted = true;
        isShortCircuited = true;

        if (empCoroutine != null) StopCoroutine(empCoroutine);

        // Turn cone cyan and move away slowly one final time
        if (visionConeRenderer != null)
            visionConeRenderer.material.color = rebootedVisionColor;
        if (visionSpotlight != null)
            visionSpotlight.color = new Color(0.2f, 1f, 0.9f);

        if (agent != null && agent.isOnNavMesh)
        {
            Vector3 awayFromTree = (targetTree != null)
                ? (transform.position - targetTree.position).normalized
                : -transform.forward;
            awayFromTree.y = 0f;

            agent.isStopped = false;
            agent.speed = retreatSpeed;
            agent.SetDestination(transform.position + awayFromTree * 45f);
        }
    }

    // ==================== VISION CONE GENERATOR ====================

    void BuildTransparentRedVisionCone()
    {
        Transform oldCone = transform.Find("Drone_RedVisionCone");
        if (oldCone != null) Destroy(oldCone.gameObject);

        GameObject coneObj = new GameObject("Drone_RedVisionCone");
        coneObj.transform.SetParent(transform, false);
        coneObj.transform.localPosition = new Vector3(0f, 0.15f, 0.4f);
        coneObj.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);

        MeshFilter mf = coneObj.AddComponent<MeshFilter>();
        mf.sharedMesh = GenerateVisionConeMesh(detectionRange, detectionAngle, 24);

        visionConeRenderer = coneObj.AddComponent<MeshRenderer>();
        visionConeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        visionConeRenderer.receiveShadows = false;

        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        Material coneMat = new Material(sh);
        coneMat.color = alertVisionColor;
        if (coneMat.HasProperty("_BaseColor")) coneMat.SetColor("_BaseColor", alertVisionColor);
        visionConeRenderer.material = coneMat;

        GameObject lightObj = new GameObject("Drone_RedSpotLight");
        lightObj.transform.SetParent(coneObj.transform, false);
        lightObj.transform.localPosition = Vector3.zero;
        lightObj.transform.localRotation = Quaternion.identity;

        visionSpotlight = lightObj.AddComponent<Light>();
        visionSpotlight.type = LightType.Spot;
        visionSpotlight.color = new Color(1f, 0.12f, 0.08f);
        visionSpotlight.range = detectionRange * 1.15f;
        visionSpotlight.spotAngle = detectionAngle;
        visionSpotlight.intensity = 8f;
        visionSpotlight.shadows = LightShadows.None;
    }

    Mesh GenerateVisionConeMesh(float length, float angleDegrees, int segments)
    {
        Mesh m = new Mesh();
        float halfAngleRad = (angleDegrees * 0.5f) * Mathf.Deg2Rad;
        float endRadius = Mathf.Tan(halfAngleRad) * length;

        Vector3[] verts = new Vector3[segments + 2];
        List<int> tris = new List<int>();

        verts[0] = Vector3.zero;
        for (int i = 0; i <= segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            verts[i + 1] = new Vector3(Mathf.Cos(a) * endRadius, Mathf.Sin(a) * endRadius, length);
        }

        for (int i = 1; i <= segments; i++)
        {
            tris.Add(0); tris.Add(i); tris.Add(i + 1);
            tris.Add(0); tris.Add(i + 1); tris.Add(i);
        }

        m.vertices = verts;
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        return m;
    }
}