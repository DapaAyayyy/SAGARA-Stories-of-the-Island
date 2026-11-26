using System;
using System.Collections;
using Sagara.DermagaV2;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sagara.SceneTransitions
{
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
    public sealed class CoastalSceneCrossing : MonoBehaviour
    {
        private const float FadeSeconds = 0.42f;
        private const float MaximumFadeFrameTime = 0.05f;
        private static readonly int IsWalking = Animator.StringToHash("isWalking");
        private static string pendingScene;

        [SerializeField] private string destinationScene;
        [SerializeField] private Transform arrival;
        [SerializeField] private CharacterController player;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private Behaviour[] interactions;
        [SerializeField] private DermagaIntroProgress dermagaQuest;

        private Image fade;
        private bool[] interactionStates;
        private bool arriving;
        private bool movementState;
        private bool questLockState;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTransfer()
        {
            pendingScene = null;
        }

        public static bool IsArrivingScene(string scenePath) => !string.IsNullOrEmpty(scenePath) && pendingScene == scenePath;

        private void Awake()
        {
            if (pendingScene != gameObject.scene.path)
            {
                return;
            }

            arriving = true;
            SetFade(1f);
            LockControls();
            player.enabled = false;
            player.transform.SetPositionAndRotation(arrival.position, arrival.rotation);
            player.enabled = true;
            Physics.SyncTransforms();
        }

        private IEnumerator Start()
        {
            if (arriving)
            {
                // Allow scene Start and the follow camera's first LateUpdate under the opaque cover.
                yield return new WaitForEndOfFrame();
                var arrivalDrone = FindFirstObjectByType<DermagaOpeningDroneCinematic>();
                if (arrivalDrone && arrivalDrone.ShouldPlayOpeningDrone)
                {
                    // Cleanly hand off control locks under the black cover so the fade reveals Shot 1 directly.
                    UnlockControls();
                    arrivalDrone.PlayIntroDrone();
                    yield return Fade(0f);
                    yield break;
                }
                yield return Fade(0f);
                UnlockControls();
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (other != player || pendingScene != null || !movement.isActiveAndEnabled
                || Vector3.Dot(player.transform.position - transform.position, transform.forward) < 0f
                || Vector3.Dot(player.velocity, transform.forward) <= 0.1f)
            {
                return;
            }

            int index = SceneUtility.GetBuildIndexByScenePath(destinationScene);
            if (index < 0)
            {
                enabled = false;
                Debug.LogError($"Scene crossing destination is absent from the build scene list: {destinationScene}", this);
                return;
            }

            pendingScene = destinationScene;
            LockControls();
            StartCoroutine(Depart(index));
        }

        private IEnumerator Depart(int index)
        {
            yield return Fade(1f);

            AsyncOperation load = null;
            try
            {
                load = SceneManager.LoadSceneAsync(index, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            if (load == null)
            {
                yield return Fade(0f);
                UnlockControls();
                yield break;
            }

            // The destination consumes the pending path and fades its own fully opaque cover.
            yield return load;
        }

        private void LockControls()
        {
            movementState = movement.enabled;
            if (dermagaQuest)
            {
                questLockState = dermagaQuest.OverlayControlsHeld;
                dermagaQuest.OverlayControlsHeld = true;
            }

            interactionStates = new bool[interactions.Length];
            for (int i = 0; i < interactions.Length; i++)
            {
                interactionStates[i] = interactions[i].enabled;
                interactions[i].enabled = false;
            }

            movement.enabled = false;
            var animator = player.GetComponentInChildren<Animator>();
            if (animator)
            {
                animator.SetBool(IsWalking, false);
            }
        }

        private void UnlockControls()
        {
            for (int i = 0; i < interactions.Length; i++)
            {
                interactions[i].enabled = interactionStates[i];
            }

            if (dermagaQuest)
            {
                dermagaQuest.OverlayControlsHeld = questLockState;
            }
            movement.enabled = movementState;
            pendingScene = null;
        }

        private IEnumerator Fade(float target)
        {
            SetFade(fade ? fade.color.a : 0f);
            float from = fade.color.a;
            float elapsed = 0f;

            while (elapsed < FadeSeconds)
            {
                yield return null;
                // A loading hitch can leave the first frame with an oversized delta.
                elapsed += Mathf.Min(Time.unscaledDeltaTime, MaximumFadeFrameTime);
                float progress = Mathf.Clamp01(elapsed / FadeSeconds);
                SetFade(Mathf.SmoothStep(from, target, progress));
            }

            SetFade(target);
        }

        private void SetFade(float alpha)
        {
            if (!fade)
            {
                var cover = new GameObject("Coastal crossing fade", typeof(RectTransform), typeof(Canvas),
                    typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(cover, gameObject.scene);
                var canvas = cover.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = short.MaxValue;

                var panel = new GameObject("Black cover", typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(cover.transform, false);
                fade = panel.GetComponent<Image>();
                var rect = fade.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            fade.color = new Color(0f, 0f, 0f, alpha);
            fade.transform.parent.gameObject.SetActive(alpha > 0f);
        }
    }
}
