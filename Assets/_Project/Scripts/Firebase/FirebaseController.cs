using System;
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
		public Task<bool> FirebaseRegister(string username, string sex, string email, string password, string passwordConfirm, int dobDay, int dobMonth, int dobYear);
		public Task<bool> SendPasswordResetEmailAsync(string email);
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
				_databaseManager.Auth = FirebaseAuth.DefaultInstance;
				var db = FirebaseDatabase.GetInstance("https://trainingbuddy-81bca-default-rtdb.europe-west1.firebasedatabase.app/");
				db.SetPersistenceEnabled(false);
				_databaseManager.DatabaseReference = db.RootReference;
			}
			else
			{
				Debug.LogError("Something went wrong with Firebase Dependency Check");
			}
		}

		public async Task<bool> CheckDependencies()
		{
			await FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
			{
				_dependencyStatus = task.Result;
			});

			return _dependencyStatus == DependencyStatus.Available;
		}

		public async Task<bool> FirebaseLogin(string email, string password)
		{
			Task<AuthResult> LoginTask = _databaseManager.Auth.SignInWithEmailAndPasswordAsync(email, password);
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

			if (!await _databaseManager.EnsureEmailVerifiedAsync())
				return false;

#if !UNITY_EDITOR
			if (await _databaseManager.StartStepCounter() != StepCounterAvailability.Available)
				return false;
#endif
			// Fire-and-forget: claims any paid-run refunds owed from a kick/cancel that happened
			// while this account wasn't the acting client — see PaidRuns_Scope.md / ClaimPendingRefundsAsync.
			_ = _databaseManager.ClaimPendingRefundsAsync();
			// Fire-and-forget: claims any PlacementPoints owed from a race another participant's
			// client completed — see PlacementPoints_Scope.md / ClaimPendingPlacementPointsAsync.
			_ = _databaseManager.ClaimPendingPlacementPointsAsync();
			return true;
		}

		public void FirebaseLogout()
		{
			_databaseManager.Auth.SignOut();
		}

		public async Task<bool> SendPasswordResetEmailAsync(string email)
		{
			if (string.IsNullOrEmpty(email))
			{
				_databaseManager.ShowMessage("Glemt adgangskode", "Indtast venligst din emailadresse.");
				return false;
			}

			Task resetTask = _databaseManager.Auth.SendPasswordResetEmailAsync(email);
			await new WaitUntil(() => resetTask.IsCompleted);

			if (resetTask.IsFaulted)
			{
				var firebaseEx = resetTask.Exception?.GetBaseException() as FirebaseException;
				string message = "Der opstod en fejl. Prøv venligst igen.";

				if (firebaseEx != null)
				{
					message = (AuthError)firebaseEx.ErrorCode switch
					{
						AuthError.InvalidEmail => "Emailadressen ser ugyldig ud.",
						AuthError.UserNotFound => "Der findes ingen konto med denne email.",
						AuthError.MissingEmail => "Indtast venligst din emailadresse.",
						_ => message,
					};
				}

				$"SendPasswordResetEmailAsync failed: {resetTask.Exception}".LogError();
				_databaseManager.ShowMessage("Glemt adgangskode", message);
				return false;
			}

			$"Password reset email sent to {email}".Log();
			_databaseManager.ShowMessage("Tjek din email", $"Vi har sendt et link til nulstilling af adgangskode til {email}.");
			return true;
		}

		public async Task<bool> FirebaseRegister(string username, string sex, string email, string password, string passwordConfirm, int dobDay, int dobMonth, int dobYear)
	    {
		    if (string.IsNullOrEmpty(username))
		    {
			    $"Username is empty".Log();
			    return false;
		    }

		    if (string.IsNullOrEmpty(sex))
		    {
			    $"Sex is empty".Log();
			    return false;
		    }

		    if (string.IsNullOrEmpty(email))
	        {
	            $"Email is empty".Log();
	            return false;
	        }

		    if (string.IsNullOrEmpty(password))
	        {
		        $"Password is empty".Log();
		        return false;
	        }

		    if (string.IsNullOrEmpty(passwordConfirm))
		    {
			    $"Password confirm is empty".Log();
			    return false;
		    }

		    if (password != passwordConfirm)
		    {
			    $"Passwords doesn't match".Log();
			    return false;
		    }

		    if (dobDay <= 0 || dobMonth <= 0 || dobYear <= 0)
		    {
			    $"Date of birth is incomplete".Log();
			    return false;
		    }

		    Task<AuthResult> RegisterTask = _databaseManager.Auth.CreateUserWithEmailAndPasswordAsync(email, password);
	        await new WaitUntil(() => RegisterTask.IsCompleted);

	        if (RegisterTask.IsFaulted)
	        {
		        var firebaseEx = RegisterTask.Exception?.GetBaseException() as FirebaseException;
		        bool emailTaken = firebaseEx != null && (AuthError)firebaseEx.ErrorCode == AuthError.EmailAlreadyInUse;

		        $"RegisterTask failed with {RegisterTask.Exception}".Log();
		        _databaseManager.ShowMessage(
			        "Registrering fejlede",
			        emailTaken
				        ? "En konto med denne email findes allerede. Log ind i stedet."
				        : "Registrering fejlede. Tjek venligst at oplysningerne er korrekte.");
		        return false;
	        }

	        $"RegisterTask completed".Log();

	        if (_databaseManager.Auth.CurrentUser == null)
	        {
		        return false;
	        }

	        var profile = new UserProfile { DisplayName = username };
	        Task ProfileTask = _databaseManager.Auth.CurrentUser.UpdateUserProfileAsync(profile);
	        await new WaitUntil(() => ProfileTask.IsCompleted);

	        if (ProfileTask.IsFaulted)
	        {
		        $"ProfileTask failed with {ProfileTask.Exception}".Log();
		        return false;
	        }

	        var userId = _databaseManager.Auth.CurrentUser.UserId;
	        var user = new UserData
	        {
		        UserName = username,
		        Sex = sex,
		        UserID = userId,
		        FriendCode = GenerateFriendCode(userId),
		        Email = email,
		        DateOfBirthDay = dobDay,
		        DateOfBirthMonth = dobMonth,
		        DateOfBirthYear = dobYear,
		        AccelerationPoints = 0,
		        SpeedPoints = 0,
		        StepCount = 0,
		        StepCountSnapshot = 0,
		        StepCurrency = 0,
		        UserLevel = 1,
	        };

	        if (!await _databaseManager.CreateUser(user))
	        {
		        var currentUser = _databaseManager.Auth.CurrentUser;
		        if (currentUser != null)
		        {
			        try
			        {
				        Task deleteTask = currentUser.DeleteAsync();
				        await new WaitUntil(() => deleteTask.IsCompleted);
				        if (deleteTask.IsFaulted)
					        $"Failed to delete auth user: {deleteTask.Exception?.GetBaseException().Message}".LogError();
				        else
					        $"Auth user deleted after failed CreateUser".Log();
			        }
			        catch (Exception e)
			        {
				        $"Failed to delete auth user after CreateUser failure: {e.Message}".LogError();
			        }
		        }
		        return false;
	        }

	        try
	        {
		        await _databaseManager.Auth.CurrentUser.SendEmailVerificationAsync();
	        }
	        catch (Exception ex)
	        {
		        $"Failed to send verification email: {ex}".LogError();
	        }

	        if (!await _databaseManager.EnsureEmailVerifiedAsync())
		        return false;

#if !UNITY_EDITOR
	        if (await _databaseManager.StartStepCounter() != StepCounterAvailability.Available)
		        return false;
#endif
	        return true;
	    }

	    // Generates a 7-character friend code from a Firebase UID using FNV-1a hashing.
	    // Uses an unambiguous alphabet (no 0, 1, I, O) to avoid visual confusion.
	    // Example output: "K7MXQP3"
	    private static string GenerateFriendCode(string userId)
	    {
		    const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // 32 chars
		    ulong hash = 14695981039346656037UL; // FNV-1a offset basis
		    foreach (char c in userId)
		    {
			    hash ^= c;
			    hash *= 1099511628211UL; // FNV-1a prime
		    }

		    var code = new char[7];
		    for (int i = 0; i < 7; i++)
		    {
			    code[i] = alphabet[(int)(hash & 31)];
			    hash >>= 5;
		    }
		    return new string(code);
	    }
	}
}