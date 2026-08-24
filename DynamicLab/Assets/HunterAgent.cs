using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.Netcode; // ADDED: So we can check who is the Server!

public class HunterAgent : Agent
{
    [Header("Target Settings")]
    public Transform playerTransform;
    public float moveSpeed = 7f; 

    [Header("Capture Settings")]
    public float captureDistance = 2.5f; 
    private float timeNearPlayer = 0f; 

    private Rigidbody rb;
    private float previousDistance; 
    
    private Animator anim; 

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        anim = GetComponentInChildren<Animator>();
        
        FindPlayer();
    }

    private void FindPlayer()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }
    }

    public override void OnEpisodeBegin()
    {
        // ==========================================
        // THE FIX #1: WIPE THE TIMER ON RESTART!
        // ==========================================
        timeNearPlayer = 0f; 

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        FindPlayer();

        bool positionFound = false;
        int safetyCounter = 0;

        while (!positionFound && safetyCounter < 100)
        {
            safetyCounter++;
            
            Vector3 randomPos = new Vector3(Random.Range(-20f, 20f), 1.5f, Random.Range(-20f, 20f));

            if (!Physics.CheckSphere(randomPos, 1f, LayerMask.GetMask("Obstacles")))
            {
                transform.position = randomPos;
                positionFound = true;
            }
        }

        if (playerTransform != null)
        {
            previousDistance = Vector3.Distance(transform.position, playerTransform.position);
        }
    }

    void Update()
    {
        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            
            if (dist <= captureDistance)
            {
                // ==========================================
                // THE FIX #2: ONLY THE PC REFEREE CAN COUNT!
                // ==========================================
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    timeNearPlayer += Time.deltaTime; 
                    
                    if (timeNearPlayer >= 3f) 
                    {
                        if (GameManager.Instance != null)
                        {
                            GameManager.Instance.TriggerGameOver(); 
                        }
                        timeNearPlayer = 0f; 
                    }
                }
            }
            else
            {
                // If the player escapes, EVERYONE resets the timer
                timeNearPlayer = 0f; 
            }
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (playerTransform != null)
        {
            Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
            sensor.AddObservation(directionToPlayer);
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            sensor.AddObservation(distanceToPlayer);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
        }

        sensor.AddObservation(rb.linearVelocity);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];

        Vector3 moveForce = new Vector3(moveX, 0, moveZ) * moveSpeed;
        rb.linearVelocity = new Vector3(moveForce.x, rb.linearVelocity.y, moveForce.z);

        if (moveForce.sqrMagnitude > 0.1f)
        {
            transform.forward = new Vector3(moveForce.x, 0, moveForce.z);
        }

        if (playerTransform != null)
        {
            float currentDistance = Vector3.Distance(transform.position, playerTransform.position);

            if (currentDistance < previousDistance)
            {
                AddReward(0.005f); 
            }
            else if (currentDistance > previousDistance)
            {
                AddReward(-0.005f);
            }

            previousDistance = currentDistance; 
        }

        AddReward(-0.0001f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Target Captured!");
            SetReward(2.0f); 
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacles"))
        {
            AddReward(-0.01f);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Horizontal");
        continuousActions[1] = Input.GetAxisRaw("Vertical");
    }
}