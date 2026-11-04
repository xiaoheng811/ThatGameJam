using UnityEngine;
using UnityEngine.Rendering;

namespace ThatGameJam.LevelOne
{
    /// <summary>Visual-only squash, stretch and tilt. The grid root never deforms.</summary>
    [DisallowMultipleComponent]
    public sealed class JellyObstacle : MonoBehaviour
    {
        [Tooltip("Optional: a visual child with a MeshFilter. Leave empty for a mesh on this root.")]
        public Transform visual;
        [Header("Movement")]
        [Min(.1f)] public float referenceSpeed = 1f;
        [Range(0, .2f)] public float movementSquash = .055f;
        [Range(0, 15)] public float movementTilt = 3f;
        [Range(.1f, 6)] public float movementFrequency = 2f;
        [Header("Impact")]
        [Range(0, 8)] public float impactSquashImpulse = 2.8f;
        [Range(0, 300)] public float impactTiltImpulse = 95f;
        [Range(.01f, .5f)] public float impactCooldown = .12f;
        [Header("Spring")]
        [Range(.5f, 6)] public float springFrequency = 2.5f;
        [Range(.1f, 1.5f)] public float dampingRatio = .28f;
        [Range(.05f, .4f)] public float maxSquash = .22f;
        [Range(1, 25)] public float maxTilt = 12f;

        Level1Game owner;
        MeshRenderer sourceRenderer;
        GameObject generatedVisual;
        bool sourceWasEnabled, initialized, wasPlaying;
        Vector3 restPosition, restScale, bottomPoint, pivot, previousPosition;
        Quaternion restRotation;
        Vector3 previousVelocity;
        Vector2 tilt, tiltVelocity;
        float squash, squashVelocity, phase, cooldown;

        void Awake() { Prepare(); }
        public void Bind(Level1Game game)
        {
            owner = game;
            if (!initialized) Prepare();
            ResetWobble();
        }

        bool Prepare()
        {
            if (initialized) return true;
            if (!visual)
            {
                var filter = GetComponent<MeshFilter>();
                sourceRenderer = GetComponent<MeshRenderer>();
                if (!filter || !filter.sharedMesh || !sourceRenderer)
                    return ConfigurationError("No root MeshFilter/MeshRenderer. Assign a visual mesh child to Visual.");

                // Runtime-only duplicate of the visible mesh, never its collider or material assets.
                generatedVisual = new GameObject("JellyVisual_Runtime");
                generatedVisual.transform.SetParent(transform, false);
                generatedVisual.layer = gameObject.layer;
                visual = generatedVisual.transform;
                generatedVisual.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = generatedVisual.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = sourceRenderer.sharedMaterials;
                renderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                renderer.receiveShadows = sourceRenderer.receiveShadows;
                renderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
                renderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
                renderer.probeAnchor = sourceRenderer.probeAnchor;
                renderer.renderingLayerMask = sourceRenderer.renderingLayerMask;
                renderer.motionVectorGenerationMode = sourceRenderer.motionVectorGenerationMode;
                renderer.sortingLayerID = sourceRenderer.sortingLayerID;
                renderer.sortingOrder = sourceRenderer.sortingOrder;
                var properties = new MaterialPropertyBlock();
                sourceRenderer.GetPropertyBlock(properties);
                renderer.SetPropertyBlock(properties);
                sourceWasEnabled = sourceRenderer.enabled;
                renderer.enabled = sourceWasEnabled;
                sourceRenderer.enabled = false;
            }
            if (visual == transform || !visual.IsChildOf(transform))
                return ConfigurationError("Visual must be a child, never the obstacle root itself.");
            if (visual.GetComponentsInChildren<Collider>(true).Length != 0 ||
                visual.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                return ConfigurationError("Keep colliders and Rigidbody outside the Visual subtree.");
            var mesh = visual.GetComponent<MeshFilter>();
            if (!mesh || !mesh.sharedMesh)
                return ConfigurationError("Visual needs a MeshFilter with a valid mesh.");

            restPosition = visual.localPosition;
            restRotation = visual.localRotation;
            restScale = visual.localScale;
            Bounds bounds = mesh.sharedMesh.bounds;
            bottomPoint = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            pivot = restPosition + restRotation * Vector3.Scale(bottomPoint, restScale);
            previousPosition = transform.position;
            initialized = true;
            return true;
        }
        bool ConfigurationError(string message)
        {
            Debug.LogError("[JellyObstacle] " + name + ": " + message, this);
            enabled = false;
            return false;
        }

        public void ResetWobble()
        {
            if (!initialized) return;
            squash = squashVelocity = phase = cooldown = 0;
            tilt = tiltVelocity = Vector2.zero;
            previousPosition = transform.position;
            previousVelocity = Vector3.zero;
            RestoreVisual();
        }

        /// <param name="towardsPlayer">World-space vector from jelly to the player.</param>
        public void Impact(Vector3 towardsPlayer)
        {
            if (!initialized || !isActiveAndEnabled || cooldown > 0) return;
            Vector3 d = transform.InverseTransformDirection(towardsPlayer);
            d.y = 0;
            if (d.sqrMagnitude < .0001f) d = Vector3.forward;
            d.Normalize();
            // Tilt away from the contact and squash vertically, then spring back.
            tiltVelocity += new Vector2(-d.z, d.x) * impactTiltImpulse;
            squashVelocity -= impactSquashImpulse;
            cooldown = impactCooldown;
        }

        void LateUpdate()
        {
            if (!initialized || !owner) return;
            if (!owner.Playing)
            {
                if (wasPlaying) ResetWobble();
                wasPlaying = false;
                previousPosition = transform.position;
                return;
            }
            wasPlaying = true;
            if (owner.Paused)
            {
                previousPosition = transform.position;
                return;
            }
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            Vector3 delta = transform.position - previousPosition;
            previousPosition = transform.position;
            // A teleport/restart must not look like an enormous collision.
            if (delta.magnitude > Mathf.Max(1f, referenceSpeed * 3f))
            { ResetWobble(); return; }
            Vector3 velocity = delta / dt;
            velocity.y = 0;
            Vector3 localVelocity = transform.InverseTransformDirection(velocity);
            Vector3 localChange = transform.InverseTransformDirection(velocity - previousVelocity);
            previousVelocity = velocity;
            float speed = Mathf.Clamp01(velocity.magnitude / Mathf.Max(.1f, referenceSpeed));
            // Starts, stops and reversals add a small impulse even at a waiting endpoint.
            localChange = Vector3.ClampMagnitude(localChange / Mathf.Max(.1f, referenceSpeed), 2);
            tiltVelocity += new Vector2(-localChange.z, localChange.x) * movementTilt * 1.5f;
            phase += dt * movementFrequency * Mathf.PI * 2;
            if (phase > Mathf.PI * 2) phase %= Mathf.PI * 2;
            cooldown = Mathf.Max(0, cooldown - dt);
            float targetSquash = Mathf.Sin(phase) * movementSquash * speed;
            Vector3 direction = localVelocity.sqrMagnitude > .0001f ? localVelocity.normalized : Vector3.zero;
            Vector2 targetTilt = new Vector2(-direction.z, direction.x) *
                (movementTilt * speed * (.65f + .35f * Mathf.Sin(phase)));
            float omega = Mathf.Max(.5f, springFrequency) * Mathf.PI * 2;
            // Small substeps keep the damped spring stable on slower frames.
            float remaining = Mathf.Min(dt, .1f);
            while (remaining > 0)
            {
                float h = Mathf.Min(remaining, 1f / 120);
                remaining -= h;
                squashVelocity += (omega * omega * (targetSquash - squash) -
                    2 * dampingRatio * omega * squashVelocity) * h;
                squash += squashVelocity * h;
                tiltVelocity += (omega * omega * (targetTilt - tilt) -
                    2 * dampingRatio * omega * tiltVelocity) * h;
                tilt += tiltVelocity * h;
                float limit = Mathf.Clamp(maxSquash, .05f, .4f);
                if (Mathf.Abs(squash) > limit) { squash = Mathf.Clamp(squash, -limit, limit); squashVelocity = 0; }
                float tiltLimit = Mathf.Clamp(maxTilt, 1, 25);
                if (tilt.magnitude > tiltLimit) { tilt = Vector2.ClampMagnitude(tilt, tiltLimit); tiltVelocity *= .5f; }
            }
            float height = 1 + squash;
            float width = 1f / Mathf.Sqrt(height); // approximately constant volume
            Vector3 scale = Vector3.Scale(restScale, new Vector3(width, height, width));
            Quaternion rotation = Quaternion.Euler(tilt.x, 0, tilt.y) * restRotation;
            visual.localScale = scale;
            visual.localRotation = rotation;
            visual.localPosition = pivot - rotation * Vector3.Scale(bottomPoint, scale);
        }

        void RestoreVisual()
        {
            if (!visual || !initialized) return;
            visual.localPosition = restPosition;
            visual.localRotation = restRotation;
            visual.localScale = restScale;
        }
        void OnDisable()
        {
            ResetWobble();
            if (generatedVisual) generatedVisual.SetActive(false);
            if (sourceRenderer) sourceRenderer.enabled = sourceWasEnabled;
        }
        void OnEnable()
        {
            if (!initialized) return;
            if (generatedVisual) generatedVisual.SetActive(true);
            if (sourceRenderer) sourceRenderer.enabled = false;
            ResetWobble();
        }
        void OnDestroy()
        {
            if (sourceRenderer) sourceRenderer.enabled = sourceWasEnabled;
            if (generatedVisual) Destroy(generatedVisual);
        }
    }
}
