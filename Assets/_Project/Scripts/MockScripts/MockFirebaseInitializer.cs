using System.Threading.Tasks;
using BedtimeCore;
using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using TrainingBuddy.UI;
using UnityEngine;

namespace TrainingBuddy.Testing
{
	public class MockFirebaseController : IFirebaseController
	{
		private DependencyStatus DependencyStatus;
		
		private IDatabaseManager _databaseManager;
		
		public Task InitializeFirebase()
		{
			throw new System.NotImplementedException();
		}

		public async Task Initialize()
		{
			if (await CheckDependencies())
			{
				_databaseManager = new DatabaseManager();

				_databaseManager.Auth = FirebaseAuth.DefaultInstance;
				_databaseManager.DatabaseReference = FirebaseDatabase.GetInstance("https://trainingbuddy-81bca-default-rtdb.europe-west1.firebasedatabase.app/").RootReference;
			}
		}
		
		public async Task<bool> CheckDependencies()
		{
			//Check that all the necessary dependencies for Firebase are present on the system
			await FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
			{
				DependencyStatus = task.Result;
			});

			return DependencyStatus == DependencyStatus.Available;
		}

		public async Task<bool> FirebaseLogin(string email, string password)
		{
			await Initialize();
			//Call the Firebase auth signin function passing the email and password
			Task<AuthResult> LoginTask = _databaseManager.Auth.SignInWithEmailAndPasswordAsync(email, password);
			//Wait until the task completes
			await new WaitUntil(predicate: () => LoginTask.IsCompleted);
	    
			if (LoginTask.Exception != null)
			{
				//If there are errors handle them
				var firebaseEx = LoginTask.Exception.GetBaseException() as FirebaseException;
				var errorCode = (AuthError)firebaseEx.ErrorCode;

				string message = errorCode switch
				{
					AuthError.MissingEmail => "Missing Email", 
					AuthError.UnverifiedEmail => "Unverified Email",
					AuthError.MissingPassword => "Missing Password", 
					AuthError.WrongPassword => "Wrong Password", 
					AuthError.InvalidEmail => "Invalid Email", 
					AuthError.UserNotFound => "Account does not exist", 
					_ => "Login Failed!",
				};
				
				$"Failed to register task with {LoginTask.Exception}. Error message: {message}".LogError();
				return false;
			}

			return true;
		}

		public void FirebaseLogout()
		{
			_databaseManager.Auth.SignOut();
		}

		public Task<bool> FirebaseRegister(string username, string sex, string email, string password, string passwordConfirm, int dobDay, int dobMonth, int dobYear, string referralCode = null)
		{
			throw new System.NotImplementedException();
		}

		public Task<bool> SendPasswordResetEmailAsync(string email)
		{
			throw new System.NotImplementedException();
		}
	}
}