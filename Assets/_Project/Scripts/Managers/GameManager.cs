using Newtonsoft.Json;
using TrainingBuddy.FireBase;
using VContainer.Unity;

namespace TrainingBuddy.Managers
{
	public class GameManager : IInitializable
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
	}
}