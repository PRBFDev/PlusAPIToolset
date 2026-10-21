using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlusAPIToolset.Core {
    public class PlusAssetsManager : MonoBehaviour {
        private void Awake() {
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
        }

        private void OnDestroy() {
            SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
        }

        private void SceneManager_sceneLoaded(Scene scene, LoadSceneMode mode) {
            SodaBuilder.ApplyGenerationToLoadedScenes();
        }
    }
}
