using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public sealed class SceneTransitionFade : MonoBehaviour
{
    private static SceneTransitionFade instance;
    private VisualElement overlay;

    public static bool IsTransitioning => instance != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => instance = null;

    public static void Load(string scene, PanelSettings panel, float duration, string spawnId = null)
    {
        if (IsTransitioning)
        {
            return;
        }

        var host = new GameObject("SceneTransitionFade");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<SceneTransitionFade>();
        var document = host.AddComponent<UIDocument>();
        document.panelSettings = panel;
        document.sortingOrder = 10000;
        instance.overlay = document.rootVisualElement;
        instance.overlay.name = "SceneTransitionOverlay";
        instance.overlay.style.position = Position.Absolute;
        instance.overlay.style.top = 0;
        instance.overlay.style.right = 0;
        instance.overlay.style.bottom = 0;
        instance.overlay.style.left = 0;
        instance.overlay.style.backgroundColor = Color.black;
        instance.overlay.style.opacity = 0;
        instance.StartCoroutine(instance.Transition(scene, Mathf.Max(0f, duration), spawnId));
    }

    private IEnumerator Transition(string scene, float duration, string spawnId)
    {
        // sceneLoaded runs before Start, so the camera initializes at the arrival point.
        void PlacePlayer(Scene loaded, LoadSceneMode mode)
        {
            if (loaded.path == scene || loaded.name == scene)
            {
                SceneSpawnPoint.TryPlacePlayer(loaded, spawnId);
            }
        }

        try
        {
            yield return Fade(0f, 1f, duration);
            // Render a fully black frame before replacing the scene.
            yield return null;
            SceneManager.sceneLoaded += PlacePlayer;
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            SceneManager.sceneLoaded -= PlacePlayer;
            // Let the new camera and UI initialize behind the black overlay.
            yield return null;
            yield return Fade(1f, 0f, duration);
        }
        finally
        {
            SceneManager.sceneLoaded -= PlacePlayer;
            instance = null;
            Destroy(gameObject);
        }
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            overlay.style.opacity = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }

        overlay.style.opacity = to;
    }
}
