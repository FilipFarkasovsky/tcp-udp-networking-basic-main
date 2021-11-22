
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using RiptideNetworking;

public class Enemy : MonoBehaviour
{
    public static Dictionary<ushort, Enemy> List { get; private set; } = new Dictionary<ushort, Enemy>();
    public int health = 100;
    public ushort id;
    private static ushort lastId = 0;

    private NavMeshAgent agent;
    private Transform player;
    [SerializeField] private LayerMask whatIsGround, whatIsPlayer;

    //Patroling
    private Vector3 walkPoint;
    private bool walkPointSet;
    public float walkPointRange;

    //Attacking
    public float timeBetweenAttacks;
    private bool alreadyAttacked;
    public GameObject projectile;

    //States
    public float sightRange, attackRange;
    public bool playerInSightRange, playerInAttackRange;


    public static void Spawn(Vector3 position)
    {
        Enemy enemy = Instantiate(EnemySpawning.Singleton.EnemyPrefab, position, Quaternion.identity).GetComponent<Enemy>();
        enemy.name = $"Enemy {lastId}";
        enemy.id = lastId;
        List.Add(lastId, enemy);
        lastId++;

        enemy.SendSpawn();
    }

    private void SendSpawn()
    {
        Message message = Message.Create(MessageSendMode.reliable, (ushort)ServerToClientId.spawnEnemy);
        message.Add(id);
        message.Add(transform.position);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    public void SendSpawn(ushort toClient)
    {
        Message message = Message.Create(MessageSendMode.reliable, (ushort)ServerToClientId.spawnEnemy);
        message.Add(id);
        message.Add(transform.position);
        NetworkManager.Singleton.Server.Send(message, toClient);
    }

    private void OnDestroy()
    {
        List.Remove(id);
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        StartCoroutine(StartFindingClosestPlayer(3 * sightRange));
    }

    private void FixedUpdate()
    {

        //Check for sight and attack range
        playerInSightRange = Physics.CheckSphere(transform.position, sightRange, whatIsPlayer);
        playerInAttackRange = Physics.CheckSphere(transform.position, attackRange, whatIsPlayer);

        if (!playerInSightRange && !playerInAttackRange) Patroling();
        if (playerInSightRange && !playerInAttackRange) ChasePlayer();
        if (playerInAttackRange && playerInSightRange) AttackPlayer();

        SendMessages.SetTransform(this);

    }

    private IEnumerator StartFindingClosestPlayer(float max)
    {
        while (true)
        {
            GameObject[] gos = GameObject.FindGameObjectsWithTag("Player");
            GameObject closest = null;
            float distance = Mathf.Infinity;
            Vector3 position = transform.position;

            // calculate squared distances
            max = max * max;
            foreach (GameObject go in gos)
            {
                Vector3 diff = go.transform.position - position;
                float curDistance = diff.sqrMagnitude;
                if (curDistance < distance && curDistance <= max)
                {
                    closest = go;
                    distance = curDistance;
                }
            }


            if (closest != null)
            {
                player = closest.transform;
            }
            else
            {
                player = transform;
            }

            yield return new WaitForSeconds(2f);
        }
    }

    private void Patroling()
    {
        if (!walkPointSet) SearchWalkPoint();

        if (walkPointSet)
            agent.SetDestination(walkPoint);

        Vector3 distanceToWalkPoint = transform.position - walkPoint;

        //Walkpoint reached
        if (distanceToWalkPoint.magnitude < 1f)
            walkPointSet = false;
    }

    private void SearchWalkPoint()
    {
        //Calculate random point in range
        float randomZ = Random.Range(-walkPointRange, walkPointRange);
        float randomX = Random.Range(-walkPointRange, walkPointRange);

        walkPoint = new Vector3(transform.position.x + randomX, transform.position.y, transform.position.z + randomZ);

        if (Physics.Raycast(walkPoint, -transform.up, 2f, whatIsGround))
            walkPointSet = true;
    }

    private void ChasePlayer()
    {
        agent.SetDestination(player.position);
    }

    private void AttackPlayer()
    {
        //Make sure enemy doesn't move
        agent.SetDestination(transform.position);

        transform.LookAt(player);

        if (!alreadyAttacked)
        {
            ///Attack code here
            //Rigidbody rb = Instantiate(projectile, transform.position, Quaternion.identity).GetComponent<Rigidbody>();
            //rb.AddForce(transform.forward * 32f, ForceMode.Impulse);
            //rb.AddForce(transform.up * 8f, ForceMode.Impulse);
            ///End of attack code

            alreadyAttacked = true;
            Invoke(nameof(ResetAttack), timeBetweenAttacks);
        }
    }

    private void ResetAttack()
    {
        alreadyAttacked = false;
    }

    public void TakeDamage(int amount)
    {
        health -= amount;
        if (health <= 0) Invoke(nameof(gameObject), 0.5f);
    }

    public void SetHealth(int amount)
    {
        health = amount;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        if(player != null)Gizmos.DrawWireSphere(player.position, 1f);
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
    }
}
