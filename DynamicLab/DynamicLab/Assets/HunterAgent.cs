using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class HunterAgent : Agent
{
    [Header("Target Settings")]
    public Transform playerTransform;
    public float moveSpeed = 7f; 

    [Header("Capture Settings")]
    public float captureDistance = 2.5f; // המרחק שנחשב "תפיסה"
    private float timeNearPlayer = 0f; // טיימר שניות

    private Rigidbody rb;
    private float previousDistance; // שומר את המרחק בזיכרון
    
    // --- תוספת לאנימציה ---
    private Animator anim; 

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        // --- תוספת לאנימציה ---
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
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        FindPlayer();

        bool positionFound = false;
        int safetyCounter = 0;

        // מערכת השתגרות בטוחה על הרצפה שמונעת התנגשות במכשולים (עד 100 ניסיונות)
        while (!positionFound && safetyCounter < 100)
        {
            safetyCounter++;
            
            // השתגרות בגובה 1.5 כדי לעמוד בדיוק על הקרקע (ללא צניחה חופשית)
            Vector3 randomPos = new Vector3(Random.Range(-20f, 20f), 1.5f, Random.Range(-20f, 20f));

            // בדיקה שאין באזור מכשול (שכבת Obstacles)
            if (!Physics.CheckSphere(randomPos, 1f, LayerMask.GetMask("Obstacles")))
            {
                transform.position = randomPos;
                positionFound = true;
            }
        }

        // שומרים את המרחק הראשוני מיד כשהאפיזודה מתחילה
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
                timeNearPlayer += Time.deltaTime; // מתחיל לספור
                
                // אם עברו 3 שניות רצופות!
                if (timeNearPlayer >= 3f) 
                {
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.TriggerGameOver(); // הפעלת מסך הפסד
                    }
                    timeNearPlayer = 0f; // איפוס למקרה של ריסטרט
                }
            }
            else
            {
                timeNearPlayer = 0f; // השחקן הצליח להתרחק, הטיימר מתאפס
            }
        }
        
        // החלק ששלט ב-isWalking נמחק מכאן כדי שהאנימציה תרוץ ברצף
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

        // בונוס ויזואלי: גורם לבוט להסתובב עם הגוף לכיוון שאליו הוא הולך!
        if (moveForce.sqrMagnitude > 0.1f)
        {
            transform.forward = new Vector3(moveForce.x, 0, moveForce.z);
        }

        // --- מערכת התגמולים (חם/קר) ---
        if (playerTransform != null)
        {
            float currentDistance = Vector3.Distance(transform.position, playerTransform.position);

            if (currentDistance < previousDistance)
            {
                // התקרבת? קבל פרס!
                AddReward(0.005f); 
            }
            else if (currentDistance > previousDistance)
            {
                // התרחקת? קנס קטן.
                AddReward(-0.005f);
            }

            // מעדכנים את הזיכרון לצעד הבא
            previousDistance = currentDistance; 
        }

        // עונש זמן מוקטן מאוד
        AddReward(-0.0001f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Target Captured!");
            SetReward(2.0f); 
            // EndEpisode(); // <--- הפכנו להערה! הבוט לא ייעלם יותר ויתקע עליך 3 שניות!
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