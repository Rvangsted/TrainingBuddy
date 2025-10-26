#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BedtimeCore;
using Cysharp.Threading.Tasks;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using Newtonsoft.Json;
using TrainingBuddy.FireBase;
using TrainingBuddy.UI;
using TrainingBuddy.Utility;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.InputSystem;

#endregion

namespace TrainingBuddy.Managers
{
	public interface IDatabaseManager
	{
		public FirebaseAuth Auth { get; set; }
		public DatabaseReference DatabaseReference { get; set; }
		public JsonSerializerSettings JsonSettings { get; set; }
	}

	public class DatabaseManager : IDatabaseManager
	{
		private int localStepCount;

		public event Action<long> StepCountChanged;

		public bool StepCounterRunning { get; private set; }
		public bool isLocationUpdaterRunning { get; private set; }

		public UIManager UIManager { private get; set; }
		public FirebaseAuth Auth { get; set; }
		public DatabaseReference DatabaseReference { get; set; }
		public JsonSerializerSettings JsonSettings { get; set; }

		public async void CreateUser(UserData user)
		{
			string json = JsonConvert.SerializeObject(user, JsonSettings);

			Task DBTask = DatabaseReference.Child("Users")
			                               .Child(user.UserName + "_" + user.UserID)
			                               .SetRawJsonValueAsync(json);
			await DBTask;

			if (DBTask.IsFaulted)
			{
				$"CreateUser operation failed with {DBTask.Exception}".Log();
			}
			else if (DBTask.IsCompleted)
			{
				//TODO: Handle the success???
			}
		}

		public async Task UpdateUser(FirebaseUser user, UserData userData)
		{
			string userName = user.DisplayName;
			string userID = user.UserId;
			UserData currentUserdata;

			await DatabaseReference.Child("Users").Child(userName + "_" + userID).GetValueAsync().ContinueWithOnMainThread(async task =>
			{
			   if (task.IsFaulted)
			   {
			       $"UpdateUsers Read operation failed with {task.Exception}".Log();
			   }
			   else if (task.IsCompleted)
			   {
			       DataSnapshot snapshot = task.Result;

			       currentUserdata = JsonConvert.DeserializeObject<UserData>(snapshot.GetRawJsonValue(), JsonSettings);

			       userData.UserName ??= currentUserdata.UserName;
			       userData.Sex ??= currentUserdata.Sex;
			       userData.UserID ??= currentUserdata.UserID;
			       userData.Email ??= currentUserdata.Email;
			       userData.Longitude ??= currentUserdata.Longitude;
			       userData.Latitude ??= currentUserdata.Latitude;
			       userData.Level ??= currentUserdata.Level;
			       userData.ExperiencePoints ??= currentUserdata.ExperiencePoints;
			       userData.SkillPoints ??= currentUserdata.SkillPoints;
			       userData.AccelerationPoints ??= currentUserdata.AccelerationPoints;
			       userData.SpeedPoints ??= currentUserdata.SpeedPoints;
			       userData.StepCount ??= currentUserdata.StepCount;
			       userData.StepCountSnapshot ??= currentUserdata.StepCountSnapshot;
			       userData.UserLevel ??= currentUserdata.UserLevel;

			       string json = JsonConvert.SerializeObject(userData, JsonSettings);

			       Task DBTask = DatabaseReference.Child("Users").Child(userName + "_" + userID).SetRawJsonValueAsync(json);
			       await DBTask;

			       if (DBTask.IsFaulted)
			       {
			           $"UpdateUser Write operation failed with {DBTask.Exception}".Log();
			       }
			       else if (DBTask.IsCompleted)
			       {
			           //TODO: Handle the success??? 
			       }
			   }
			});
		}

		public async Task<DataSnapshot> FetchUserData(FirebaseUser user)
		{
			string userName = user.DisplayName;
			string userID = user.UserId;
			DataSnapshot snapshot = null;

			await DatabaseReference.Child("Users")
			                       .Child(userName + "_" + userID)
			                       .GetValueAsync()
			                       .ContinueWithOnMainThread(task =>
			                       {
				                       if (task.IsFaulted)
				                       {
					                       $"FetchUserData Read operation failed with {task.Exception}".Log();
				                       }
				                       else if (task.IsCompleted)
				                       {
					                       snapshot = task.Result;
				                       }

				                       return Task.CompletedTask;
			                       });
			return snapshot;
		}

		public async Task InvestInTraining(LayoutData _layoutData)
		{
			DataSnapshot dataSnapshot = await FetchUserData(Auth.CurrentUser);

			var steps = (long)dataSnapshot.Child("StepCount")
			                              .Value;
			var experience = Convert.ToInt32(dataSnapshot.Child("ExperiencePoints")
			                                             .Value);
			var spdPoints = (long)dataSnapshot.Child("SpeedPoints")
			                                  .Value;
			var accPoints = (long)dataSnapshot.Child("AccelerationPoints")
			                                  .Value;

			// TODO: Settings
			float investCap = 10000;
			if (steps < investCap)
			{
				UIManager.ChangePage(_layoutData.ProfileScreen);
				return;
			}

			// TODO: Settings
			float expIncrease = 10000;
			int userLevel = Mathf.FloorToInt((1 + Mathf.Sqrt(1 + 8 * (experience + investCap) / expIncrease)) / 2);

			// TODO: Settings
			int skillPointsPerLevel = 5;
			var totalPoints = (userLevel * skillPointsPerLevel);
			totalPoints -= (int)spdPoints;
			totalPoints -= (int)accPoints;

			var updatedStepCount = (int)steps - (int)investCap;

			await UpdateUser(Auth.CurrentUser, new UserData { Level = userLevel, StepCount = updatedStepCount, ExperiencePoints = experience + (int)investCap, SkillPoints = (int)totalPoints });

			await UniTask.SwitchToMainThread();
			UIManager.UpdateStepCounter(updatedStepCount);
			StepCountChanged?.Invoke(updatedStepCount);
		}

		public async Task CreateLobby(RaceData race)
		{
			var lobbyId = Guid.NewGuid();

			string json = JsonConvert.SerializeObject(race, JsonSettings);

			Task DBTask = DatabaseReference.Child("Races").Child(race.RaceName + "_" + lobbyId).SetRawJsonValueAsync(json);
			await DBTask;

			if (DBTask.IsFaulted)
			{
				$"CreateLobby operation failed with {DBTask.Exception}".Log();
			}
			else if (DBTask.IsCompleted)
			{
				//TODO: Handle the success???
			}
		}

		public async Task<List<string>> FindNearbyLobbies()
		{
			var nearbyLobbies = new List<string>();
			var userList = await NearbyUsers(10);
			var raceList = await GetAllRaces();

			foreach (DataSnapshot raceListChild in raceList.Children)
			{
				foreach (string user in userList)
				{
					if (raceListChild.Key == user)
					{
						nearbyLobbies.Add(raceListChild.Key);
					}
				}
			}

			return nearbyLobbies;
		}

		public async Task<List<string>> NearbyUsers(int range)
		{
			if (!Input.location.isEnabledByUser)
			{
				return null;
			}

			if (Input.location.status != LocationServiceStatus.Running)
			{
				Input.location.Start();
			}

			var userData = await GetAllUsers();

			return UtilityMethods.FindUsersInRange(userData, Input.location.lastData.latitude, Input.location.lastData.longitude, range);
		}

		private async Task<DataSnapshot> GetAllRaces()
		{
			Task<DataSnapshot> DBTask = DatabaseReference.Child("Races")
			                                             .GetValueAsync();

			return await DBTask;
		}

		private async Task<DataSnapshot> GetAllUsers()
		{
			Task<DataSnapshot> DBTask = DatabaseReference.Child("Users")
			                                             .GetValueAsync();

			return await DBTask;
		}

		public void StartStepCounter()
		{
			if (StepCounterRunning)
			{
				return;
			}

			if (!Permission.HasUserAuthorizedPermission("android.permission.ACTIVITY_RECOGNITION"))
			{
				return;
			}

			if (StepCounter.current == null)
			{
				InputSystem.AddDevice<StepCounter>();
			}

			if (StepCounter.current == null)
			{
				return;
			}

			if (!StepCounter.current.enabled)
			{
				InputSystem.EnableDevice(StepCounter.current);
				if (StepCounter.current.enabled)
				{
					Debug.Log("StepCounter is enabled");
				}
			}

			if (!StepCounter.current.enabled)
			{
				return;
			}

			StepCounterRunning = true;
			Debug.Log("StepCounter Handler Call");
			_ = StepCounterHandler();
		}

		private async Task StepCounterHandler(float delay = 2f)
		{
			while (StepCounterRunning)
			{
				if (StepCounter.current == null)
				{
					Debug.Log("StepCounter unavailable, attempting to re-register");
					InputSystem.AddDevice<StepCounter>();
					await Task.Delay(1000);
					continue;
				}

				if (!StepCounter.current.enabled)
				{
					Debug.Log("StepCounter disabled, enabling");
					InputSystem.EnableDevice(StepCounter.current);

					if (!StepCounter.current.enabled)
					{
						await Task.Delay(1000);
						continue;
					}
				}

				Debug.Log("StepCounter is reading");
				localStepCount = StepCounter.current.stepCounter.ReadValue();

				if (localStepCount <= 0)
				{
					Debug.Log("StepCounter has no data yet");
					await Task.Delay(1000);
					continue;
				}

				await UpdateStepCount();

				await Task.Delay((int)delay * 1000);
			}
		}

		private async Task UpdateStepCount()
        {
            DataSnapshot data = await FetchUserData(Auth.CurrentUser);

            object stepSnapshotValue = data.Child("StepCountSnapshot").Value;
            object savedStepCountValue = data.Child("StepCount").Value;

            long savedStepCount = savedStepCountValue switch
            {
                long l => l,
                int i => i,
                double d => (long)d,
                null => 0,
                _ => Convert.ToInt64(savedStepCountValue)
            };

            long? stepSnapshot = stepSnapshotValue switch
            {
                long l => l,
                int i => i,
                double d => (long)d,
                null => (long?)null,
                _ => Convert.ToInt64(stepSnapshotValue)
            };

            if (stepSnapshot is null or 0)
            {
                await UpdateUser(Auth.CurrentUser, new UserData { StepCountSnapshot = localStepCount });
                return;
            }

            long? updatedStepCount = null;

            if (localStepCount > stepSnapshot)
            {
                updatedStepCount = savedStepCount + (localStepCount - stepSnapshot.Value);
            }
            else if (localStepCount < stepSnapshot)
            {
                updatedStepCount = savedStepCount + localStepCount;
            }

            if (updatedStepCount.HasValue)
            {
                await UpdateUser(Auth.CurrentUser, new UserData
                {
                    StepCount = (int)updatedStepCount.Value,
                    StepCountSnapshot = localStepCount
                });

                await UniTask.SwitchToMainThread();
                StepCountChanged?.Invoke(updatedStepCount.Value);
                Debug.Log(updatedStepCount.Value);
                return;
            }

            await UpdateUser(Auth.CurrentUser, new UserData { StepCountSnapshot = localStepCount });
        }


		// ---- OLD STUFF ----
		//
		// public void StartLocationUpdater()
		// {
		// 	if (Permission.HasUserAuthorizedPermission("android.permission.ACCESS_FINE_LOCATION"))
		// 	{
		// 		if (!Input.location.isEnabledByUser)
		// 		{
		// 			return;
		// 		}
		//
		// 		if (Input.location.status != LocationServiceStatus.Running)
		// 		{
		// 			Input.location.Start();
		// 		}
		// 		
		// 		LocationHandler();
		// 	}
		// }
		//
		// private async Task LocationHandler(float delay = 10f)
		// {
		// 	if (isLocationUpdaterRunning)
		// 	{
		// 		return;
		// 	}
		// 	
		// 	isLocationUpdaterRunning = true;
		// 	
		// 	while (true)
		// 	{
		// 		if (Input.location.status == LocationServiceStatus.Failed)
		// 		{
		// 			print("Unable to determine device location");
		// 		}
		//
		// 		await DatabaseManager.Instance.WriteCurrentUserData("Latitude", Input.location.lastData.latitude);
		// 		await DatabaseManager.Instance.WriteCurrentUserData("Longitude", Input.location.lastData.longitude);
		//
		// 		await Task.Delay((int)delay * 1000);
		// 	}
		// }
	}
}