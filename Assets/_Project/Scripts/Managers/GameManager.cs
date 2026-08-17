using System;
using Newtonsoft.Json;
using TrainingBuddy.FireBase;
using TrainingBuddy.UI;
using VContainer.Unity;

namespace TrainingBuddy.Managers
{
	// IDisposable is called by VContainer when the container is disposed (app quit / scene unload).
	public class GameManager : IInitializable, IDisposable
	{
		private readonly IFirebaseController _firebaseController;
		private readonly IDatabaseManager _databaseManager;
		private readonly UIManager _uiManager;

		public GameManager(IFirebaseController firebaseController, IDatabaseManager databaseManager, UIManager uiManager)
		{
			_firebaseController = firebaseController;
			_databaseManager = databaseManager;
			_uiManager = uiManager;
		}

		public async void Initialize()
		{
			await _firebaseController.InitializeFirebase();
			_databaseManager.JsonSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };

			// Firebase Auth persists the signed-in user on its own — now that it's initialized and
			// Auth.CurrentUser reflects any restored session, route to the right first screen instead
			// of the login flow UIManager would otherwise show by default before this point.
			_uiManager.NavigateToInitialScreen();
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
			{
				_ = _databaseManager.StartStepCounter();
				_ = _databaseManager.ClaimPendingRefundsAsync();
			}
		}
	}
}