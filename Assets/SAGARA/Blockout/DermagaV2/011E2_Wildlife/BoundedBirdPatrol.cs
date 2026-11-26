using UnityEngine;

namespace Sagara.DermagaV2.Wildlife
{
    [DisallowMultipleComponent]
    public sealed class BoundedBirdPatrol : MonoBehaviour
    {
        private const int SamplesPerCurve = 24;
        private const float HeadingLookAhead = 0.55f;
        private static readonly int GlideParameter = Animator.StringToHash("Glide");

        // The two courses share their first, second and last knots for a tangent-continuous join.
        [SerializeField] private Transform patrolOrigin;
        [SerializeField] private Vector3[] primaryCourse;
        [SerializeField] private Vector3[] alternateCourse;
        [SerializeField, Min(0.1f)] private float cruisingSpeed = 2.1f;
        [SerializeField, Min(0f)] private float speedVariation = 0.47f;
        [SerializeField, Min(1f)] private float flapGlidePeriod = 4.8f;
        [SerializeField, Range(0f, 1f)] private float glideStart = 0.46f;
        [SerializeField, Range(0f, 35f)] private float maximumBank = 22f;
        [SerializeField] private Animator animator;

        private FlightCourse currentCourse;
        private FlightCourse nextCourse;
        private float distanceTravelled;
        private float flightTime;
        private float speed;
        private float bank;

        private void Start()
        {
            if (!patrolOrigin || !animator || !animator.runtimeAnimatorController || !CoursesJoin())
            {
                Debug.LogWarning("Wildlife flight requires two closed courses with a shared entry and an animator.", this);
                enabled = false;
                return;
            }

            var primary = new FlightCourse(patrolOrigin, primaryCourse);
            var alternate = new FlightCourse(patrolOrigin, alternateCourse);
            if (primary.Length <= HeadingLookAhead || alternate.Length <= HeadingLookAhead)
            {
                Debug.LogWarning("Wildlife flight courses require distinct knots.", this);
                enabled = false;
                return;
            }

            currentCourse = primary;
            nextCourse = alternate;
            speed = cruisingSpeed;
            animator.applyRootMotion = false;
            currentCourse.Evaluate(0f, out Vector3 position, out Vector3 heading);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(heading));
        }

        private bool CoursesJoin()
        {
            return primaryCourse != null && alternateCourse != null
                && primaryCourse.Length >= 4 && alternateCourse.Length >= 4
                && primaryCourse[0] == alternateCourse[0]
                && primaryCourse[1] == alternateCourse[1]
                && primaryCourse[primaryCourse.Length - 1] == alternateCourse[alternateCourse.Length - 1];
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.10f);
            flightTime += dt;
            currentCourse.Evaluate(distanceTravelled, out _, out Vector3 heading);

            float rhythm = 0.68f * Mathf.Sin(flightTime * 0.64f) + 0.32f * Mathf.Sin(flightTime * 1.43f + 0.65f);
            float targetSpeed = Mathf.Max(0.2f, cruisingSpeed + rhythm * speedVariation - 1.05f * heading.y);
            speed = Mathf.MoveTowards(speed, targetSpeed, 0.95f * dt);
            distanceTravelled += speed * dt;
            if (distanceTravelled >= currentCourse.Length)
            {
                distanceTravelled -= currentCourse.Length;
                FlightCourse completedCourse = currentCourse;
                currentCourse = nextCourse;
                nextCourse = completedCourse;
            }

            currentCourse.Evaluate(distanceTravelled, out Vector3 position, out heading);
            float ahead = distanceTravelled + HeadingLookAhead;
            FlightCourse aheadCourse = currentCourse;
            if (ahead >= currentCourse.Length)
            {
                ahead -= currentCourse.Length;
                aheadCourse = nextCourse;
            }

            aheadCourse.Evaluate(ahead, out _, out Vector3 nextHeading);
            Vector3 flatHeading = Vector3.ProjectOnPlane(heading, Vector3.up);
            Vector3 flatAhead = Vector3.ProjectOnPlane(nextHeading, Vector3.up);
            float turn = Vector3.SignedAngle(flatHeading, flatAhead, Vector3.up);
            float targetBank = Mathf.Clamp(-turn * speed * 0.85f, -maximumBank, maximumBank);
            bank = Mathf.Lerp(bank, targetBank, 1f - Mathf.Exp(-5f * dt));
            Quaternion rotation = Quaternion.LookRotation(heading, Vector3.up) * Quaternion.AngleAxis(bank, Vector3.forward);
            transform.SetPositionAndRotation(position, Quaternion.Slerp(transform.rotation, rotation, 1f - Mathf.Exp(-10f * dt)));

            float cycle = Mathf.Repeat(flightTime / flapGlidePeriod + 0.07f * Mathf.Sin(flightTime * 0.71f), 1f);
            float spread = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(glideStart, glideStart + 0.15f, cycle));
            float returnToFlap = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.84f, 0.98f, cycle));
            float climb = Mathf.InverseLerp(0.10f, 0.34f, heading.y);
            float glide = spread * (1f - returnToFlap) * (1f - 0.72f * climb);
            animator.SetFloat(GlideParameter, glide, 0.14f, dt);
        }

        private void OnDrawGizmosSelected()
        {
            if (!patrolOrigin)
                return;

            DrawCourse(primaryCourse, new Color(0.22f, 0.89f, 1f));
            DrawCourse(alternateCourse, new Color(0.98f, 0.71f, 0.3f));
        }

        private void DrawCourse(Vector3[] knots, Color color)
        {
            if (knots == null || knots.Length < 4)
                return;

            Gizmos.color = color;
            var course = new FlightCourse(patrolOrigin, knots);
            course.Draw();
        }

        private sealed class FlightCourse
        {
            private readonly Vector3[] positions;
            private readonly Vector3[] directions;
            private readonly float[] cumulativeDistance;

            public float Length => cumulativeDistance[cumulativeDistance.Length - 1];

            public FlightCourse(Transform origin, Vector3[] knots)
            {
                int sampleCount = knots.Length * SamplesPerCurve;
                positions = new Vector3[sampleCount + 1];
                directions = new Vector3[sampleCount + 1];
                cumulativeDistance = new float[sampleCount + 1];

                for (int i = 0; i <= sampleCount; i++)
                {
                    int segment = (i / SamplesPerCurve) % knots.Length;
                    float t = (float)(i % SamplesPerCurve) / SamplesPerCurve;
                    Vector3 a = knots[(segment + knots.Length - 1) % knots.Length];
                    Vector3 b = knots[segment];
                    Vector3 c = knots[(segment + 1) % knots.Length];
                    Vector3 d = knots[(segment + 2) % knots.Length];
                    Vector3 linear = c - a;
                    Vector3 quadratic = 2f * a - 5f * b + 4f * c - d;
                    Vector3 cubic = 3f * (b - c) + d - a;
                    positions[i] = origin.TransformPoint(b + 0.5f * t * (linear + t * (quadratic + t * cubic)));
                    directions[i] = origin.TransformVector(linear + t * (2f * quadratic + 3f * t * cubic)).normalized;
                    if (i > 0)
                        cumulativeDistance[i] = cumulativeDistance[i - 1] + Vector3.Distance(positions[i - 1], positions[i]);
                }
            }

            public void Evaluate(float distance, out Vector3 position, out Vector3 direction)
            {
                int before = 0;
                int after = cumulativeDistance.Length - 1;
                while (after - before > 1)
                {
                    int middle = (before + after) / 2;
                    if (cumulativeDistance[middle] <= distance)
                        before = middle;
                    else
                        after = middle;
                }

                float blend = Mathf.InverseLerp(cumulativeDistance[before], cumulativeDistance[after], distance);
                position = Vector3.LerpUnclamped(positions[before], positions[after], blend);
                direction = Vector3.LerpUnclamped(directions[before], directions[after], blend).normalized;
            }

            public void Draw()
            {
                for (int i = 1; i < positions.Length; i++)
                    Gizmos.DrawLine(positions[i - 1], positions[i]);
            }
        }
    }
}

