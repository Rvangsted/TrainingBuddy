using TrainingBuddy.Managers;
using UnityEngine;
using VContainer;

namespace TrainingBuddy
{
	/// <summary>
	/// Forwards Unity's OnApplicationPause lifecycle event to GameManager.
	/// Attach this to any root GameObject in the scene — GlobalScope will
	/// auto-inject it via autoInjectGameObjects.
	/// </summary>
	public class AppPauseBridge : MonoBehaviour
	{
		[Inject] private GameManager _gameManager;

		private void OnApplicationPause(bool paused)
		{
			_gameManager?.OnApplicationPause(paused);
		}
	}
}