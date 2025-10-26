using System.Threading.Tasks;
using BedtimeCore;
using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using TrainingBuddy.Managers;
using UnityEngine;

namespace TrainingBuddy.FireBase
{
	public interface IFirebaseController
	{
		public Task InitializeFirebase();
		public Task<bool> CheckDependencies();
		public Task<bool> FirebaseLogin(string email, string password);
		public void FirebaseLogout();
		public Task<bool> FirebaseRegister(string username, string sex, string email, string password, string passwordConfirm);
	}
	
	public class FirebaseController : IFirebaseController
	{
		private readonly DatabaseManager _databaseManager;
		private readonly IDatabaseTasks _databaseTasks;
		public FirebaseController(DatabaseManager databaseManager, IDatabaseTasks databaseTasks)
		{
			_databaseManager = databaseManager;
			_databaseTasks = databaseTasks;
		}
		
		private DependencyStatus _dependencyStatus;
		
		public async Task InitializeFirebase()
		{
			if (await CheckDependencies())
			{
				Debug.Log("Setting up Firebase Auth");
				//Set the authentication Instance object
				_databaseManager.Auth = FirebaseAuth.DefaultInstance;
				_databaseManager.DatabaseReference = FirebaseDatabase.GetInstance("https://trainingbuddy-81bca-default-rtdb.europe-west1.firebasedatabase.app/").RootReference;
			}
			else
			{
				Debug.LogError("Something went wrong with Firebase Dependency Check");
			}
		}
		
		public async Task<bool> CheckDependencies()
		{
			//Check that all the necessary dependencies for Firebase are present on the system
			await FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
			{
				_dependencyStatus = task.Result;
			});
			
			return _dependencyStatus == DependencyStatus.Available;
		}
		
		public async Task<bool> FirebaseLogin(string email, string password)
		{
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
					AuthError.MissingPassword => "Missing Password", 
					AuthError.WrongPassword => "Wrong Password", 
					AuthError.InvalidEmail => "Invalid Email", 
					AuthError.UserNotFound => "Account does not exist", 
					_ => "Login Failed!",
				};
				
				$"Error message: {message}".LogError();
				return false;
			}

			$"Logged in".Log();
			return true;
		}
		
		public void FirebaseLogout()
		{
			_databaseManager.Auth.SignOut();
		}
		
		public async Task<bool> FirebaseRegister(string username, string sex, string email, string password, string passwordConfirm)
	    {
		    if (username == "")
		    {
			    $"Username is empty".Log();
			    return false;
		    }
		    
		    if (sex == "")
		    {
			    $"Sex is empty".Log();
			    return false;
		    }
	        
		    if (email == "")
	        {
	            $"Email is empty".Log();
	            return false;
	        }
	        
		    if (password == "")
	        {
		        $"Password is empty".Log();
		        return false;
	        }
		    
		    if (passwordConfirm == "")
		    {
			    $"Password confirm is empty".Log();
			    return false;
		    }
		    
		    if (password != passwordConfirm)
		    {
			    $"Passwords doesn't match".Log();
			    return false;
		    }

		    Task<AuthResult> RegisterTask = _databaseManager.Auth.CreateUserWithEmailAndPasswordAsync(email, password);

	        await RegisterTask;
	        if (RegisterTask.IsFaulted)
	        {
		        $"RegisterTask failed with {RegisterTask.Exception}".Log();
		        return false;
	        }
	        
	        $"RegisterTask completed".Log();

	        if (_databaseManager.Auth.CurrentUser == null)
	        {
		        return false;
	        }
	        
	        var profile = new UserProfile{DisplayName = username};
			        
	        Task ProfileTask = _databaseManager.Auth.CurrentUser.UpdateUserProfileAsync(profile);
		        
	        await ProfileTask;
	        if (ProfileTask.IsFaulted)
	        {
		        $"ProfileTask failed with {ProfileTask.Exception}".Log(); 
		        return false;
	        }
	        
	        var user = new UserData
	        {
		        UserName = username,
		        Sex = sex,
		        UserID = _databaseManager.Auth.CurrentUser.UserId,
		        Email = email,
		        Longitude = 0,
		        Latitude = 0,
		        Level = 1,
		        ExperiencePoints = 0,
		        SkillPoints = 0,
		        AccelerationPoints = 0,
		        SpeedPoints = 0,
		        StepCount = 0,
		        StepCountSnapshot = 0,
		        UserLevel = 1,
	        };
			        
	        _databaseManager.CreateUser(user);

	        return true;
	    }
	}
}