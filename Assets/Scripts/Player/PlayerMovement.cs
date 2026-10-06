using UnityEngine;

namespace Darkforest.Player
{
    /// <summary>  이건 너가 명령한 프롬프트 내용을 영어로한거야
    /// Isaac-style top-down movement: 8 directions on WASD, quick acceleration
    /// and a short slide when the keys are released. Arrow keys are reserved for shooting.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Top speed in units per second.")]
        [SerializeField] private float moveSpeed = 5f;

        [Tooltip("How fast velocity approaches top speed while a key is held (units/s^2).")]
        [SerializeField] private float acceleration = 60f;

        [Tooltip("How fast velocity drops to zero after releasing keys (units/s^2). Lower = more slide.")]
        [SerializeField] private float deceleration = 30f;

        private Rigidbody2D rb;
        private Vector2 input;

        /// <summary>Normalized movement input this frame (zero when idle).</summary>
        public Vector2 MoveDirection => input;

        /// <summary>Last non-zero movement direction. Defaults to down, like Isaac.</summary>
        public Vector2 FacingDirection { get; private set; } = Vector2.down;

        /// <summary>Current Rigidbody2D velocity, e.g. for adding momentum to tears.</summary>
        public Vector2 Velocity => rb.velocity;

        public bool IsMoving => input.sqrMagnitude > 0f;

        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = Mathf.Max(0f, value);
        }

        private void Awake() //필요한거 찾기
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void Update() //input으로 인식
        {
            // Raw axes on fixed keys: no Input Manager smoothing, and arrow keys stay free for shooting.
            float x = 0f, y = 0f;
            if (Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.W)) y += 1f;
            if (Input.GetKey(KeyCode.S)) y -= 1f;

            input = new Vector2(x, y).normalized;
            if (input != Vector2.zero)
                FacingDirection = input;
        }

        private void FixedUpdate()//움직이게 하는코드
        {
            Vector2 target = input * moveSpeed;
            float rate = input == Vector2.zero ? deceleration : acceleration;
            rb.velocity = Vector2.MoveTowards(rb.velocity, target, rate * Time.fixedDeltaTime);
        }

        private void OnDisable() //멈추기
        {
            input = Vector2.zero;
            if (rb != null)
                rb.velocity = Vector2.zero;
        }

        private void OnValidate() //가속및 감속
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            acceleration = Mathf.Max(0.01f, acceleration);
            deceleration = Mathf.Max(0.01f, deceleration);
        }
    }
}
