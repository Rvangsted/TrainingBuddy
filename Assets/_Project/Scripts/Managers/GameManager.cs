using System;
using Newtonsoft.Json;
using TrainingBuddy.FireBase;
using VContainer.Unity;

namespace TrainingBuddy.Managers
{
	// IDisposable is called by VContainer when the container is disposed (app quit / scene unload).
	public class GameManager : IInitializable, IDisposable
	{
		private readonly IFirebaseController _firebaseController;
		private readonly IDatabaseManager _databaseManager;

		public GameManager(IFirebaseController firebaseController, IDatabaseManager databaseManager)
		{
			_firebaseController = firebaseController;
			_databaseManager = databaseManager;
		}

		public async void Initialize()
		{
			await _firebaseController.InitializeFirebase();
			_databaseManager.JsonSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
		}

		// Called by VContainer on app quit / scene unload — triggers the final Firebase step sync.
		public void Dispose()
		{
			_databaseManager.StopStepCounter();
		}

		// Call this from a MonoBehaviour's OnApplicationPause so steps are synced when the app backgrounds.
		public void OnApplicationPause(bool paused)
		{
			if (paused)
				_databaseManager.StopStepCounter();
			else
				_databaseManager.StartStepCounter();
		}
	}
}