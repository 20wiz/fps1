using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AI
{
    [RequireComponent(typeof(Enemy1Controller))]
    public class Enemy1Mobile : MonoBehaviour
    {
        public enum AIState
        {
            Patrol,
            Follow,
            Attack,
        }

        public Animator Animator;

        [Tooltip("Fraction of the enemy's attack range at which it will stop moving towards target while attacking")]
        [Range(0f, 1f)]
        public float AttackStopDistanceRatio = 0.5f;

        [Tooltip("The random hit damage effects")]
        public ParticleSystem[] RandomHitSparks;

        public ParticleSystem[] OnDetectVfx;
        public AudioClip OnDetectSfx;

        [Header("Sound")] public AudioClip MovementSound;
        public MinMaxFloat PitchDistortionMovementSpeed;

        public AIState AiState { get; private set; }
        Enemy1Controller m_Enemy1Controller;
        AudioSource m_AudioSource;

        const string k_AnimMoveSpeedParameter = "MoveSpeed";
        const string k_AnimAttackParameter = "Attack";
        const string k_AnimAlertedParameter = "Alerted";
        const string k_AnimOnDamagedParameter = "OnDamaged";

        void Start()
        {
            m_Enemy1Controller = GetComponent<Enemy1Controller>();
            DebugUtility.HandleErrorIfNullGetComponent<Enemy1Controller, Enemy1Mobile>(m_Enemy1Controller, this,
                gameObject);

            m_Enemy1Controller.onAttack += OnAttack;
            m_Enemy1Controller.onDetectedTarget += OnDetectedTarget;
            m_Enemy1Controller.onLostTarget += OnLostTarget;
            m_Enemy1Controller.SetPathDestinationToClosestNode();
            m_Enemy1Controller.onDamaged += OnDamaged;

            // Start patrolling
            AiState = AIState.Patrol;

            // adding a audio source to play the movement sound on it
            m_AudioSource = GetComponent<AudioSource>();
            DebugUtility.HandleErrorIfNullGetComponent<AudioSource, Enemy1Mobile>(m_AudioSource, this, gameObject);
            m_AudioSource.clip = MovementSound;
            m_AudioSource.Play();
        }

        void Update()
        {
            UpdateAiStateTransitions();
            UpdateCurrentAiState();

            float moveSpeed = m_Enemy1Controller.NavMeshAgent.velocity.magnitude;

            // Update animator speed parameter
            Animator.SetFloat(k_AnimMoveSpeedParameter, moveSpeed);

            // changing the pitch of the movement sound depending on the movement speed
            m_AudioSource.pitch = Mathf.Lerp(PitchDistortionMovementSpeed.Min, PitchDistortionMovementSpeed.Max,
                moveSpeed / m_Enemy1Controller.NavMeshAgent.speed);
        }

        void UpdateAiStateTransitions()
        {
            // Handle transitions 
            switch (AiState)
            {
                case AIState.Follow:
                    // Transition to attack when there is a line of sight to the target
                    if (m_Enemy1Controller.IsSeeingTarget && m_Enemy1Controller.IsTargetInAttackRange)
                    {
                        AiState = AIState.Attack;
                        m_Enemy1Controller.SetNavDestination(transform.position);
                    }

                    break;
                case AIState.Attack:
                    // Transition to follow when no longer a target in attack range
                    if (!m_Enemy1Controller.IsTargetInAttackRange)
                    {
                        AiState = AIState.Follow;
                    }

                    break;
            }
        }

        void UpdateCurrentAiState()
        {
            // Handle logic 
            switch (AiState)
            {
                case AIState.Patrol:
                    m_Enemy1Controller.UpdatePathDestination();
                    m_Enemy1Controller.SetNavDestination(m_Enemy1Controller.GetDestinationOnPath());
                    break;
                case AIState.Follow:
                    m_Enemy1Controller.SetNavDestination(m_Enemy1Controller.KnownDetectedTarget.transform.position);
                    m_Enemy1Controller.OrientTowards(m_Enemy1Controller.KnownDetectedTarget.transform.position);
                    m_Enemy1Controller.OrientWeaponsTowards(m_Enemy1Controller.KnownDetectedTarget.transform.position);
                    break;
                case AIState.Attack:
                    if (Vector3.Distance(m_Enemy1Controller.KnownDetectedTarget.transform.position,
                            m_Enemy1Controller.DetectionModule.DetectionSourcePoint.position)
                        >= (AttackStopDistanceRatio * m_Enemy1Controller.DetectionModule.AttackRange))
                    {
                        m_Enemy1Controller.SetNavDestination(m_Enemy1Controller.KnownDetectedTarget.transform.position);
                    }
                    else
                    {
                        m_Enemy1Controller.SetNavDestination(transform.position);
                    }

                    m_Enemy1Controller.OrientTowards(m_Enemy1Controller.KnownDetectedTarget.transform.position);
                    m_Enemy1Controller.TryAtack(m_Enemy1Controller.KnownDetectedTarget.transform.position);
                    break;
            }
        }

        void OnAttack()
        {
            Animator.SetTrigger(k_AnimAttackParameter);
        }

        void OnDetectedTarget()
        {
            if (AiState == AIState.Patrol)
            {
                AiState = AIState.Follow;
            }

            for (int i = 0; i < OnDetectVfx.Length; i++)
            {
                OnDetectVfx[i].Play();
            }

            if (OnDetectSfx)
            {
                AudioUtility.CreateSFX(OnDetectSfx, transform.position, AudioUtility.AudioGroups.EnemyDetection, 1f);
            }

            Animator.SetBool(k_AnimAlertedParameter, true);
        }

        void OnLostTarget()
        {
            if (AiState == AIState.Follow || AiState == AIState.Attack)
            {
                AiState = AIState.Patrol;
            }

            for (int i = 0; i < OnDetectVfx.Length; i++)
            {
                OnDetectVfx[i].Stop();
            }

            Animator.SetBool(k_AnimAlertedParameter, false);
        }

        void OnDamaged()
        {
            if (RandomHitSparks.Length > 0)
            {
                int n = Random.Range(0, RandomHitSparks.Length - 1);
                RandomHitSparks[n].Play();
            }

            Animator.SetTrigger(k_AnimOnDamagedParameter);
        }
    }
}