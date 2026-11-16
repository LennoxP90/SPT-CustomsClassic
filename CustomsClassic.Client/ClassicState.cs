using CustomsClassic.Shared;
using UnityEngine.SceneManagement;

namespace CustomsClassic.Client
{
    internal static class ClassicState
    {
        /// <summary>Answered from the engine, not from mod state, so everything downstream agrees with what is loaded.</summary>
        internal static bool Loaded()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (CustomsScenes.IsClassicScene(SceneManager.GetSceneAt(i).name)) return true;
            return false;
        }
    }
}
