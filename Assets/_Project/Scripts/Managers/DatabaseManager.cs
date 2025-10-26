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

		public Task<string> HostRaceAsync(RaceData race, int capacity, string description = null);
		public Task SubmitJoinRequestAsync(string raceId);
		public Task RetractJoinRequestAsync(string raceId);
		public Task<bool> HandleJoinRequestAsync(string raceId, string requesterUserId, bool approve);
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

			Task legacyTask = DatabaseReference.Child("Users").Child(user.UserName + "_" + user.UserID).SetRawJsonValueAsync(json);

			Task userByIdTask = DatabaseReference.Child("users").Child(user.UserID).SetRawJsonValueAsync(json);

			await Task.WhenAll(legacyTask, userByIdTask);

			if (legacyTask.IsFaulted || userByIdTask.IsFaulted)
			{
				Exception exception = legacyTask.Exception ?? userByIdTask.Exception;
				$"CreateUser operation failed with {exception}".Log();
			}
			else if (legacyTask.IsCompleted && userByIdTask.IsCompleted)
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

			       Task legacyTask = DatabaseReference.Child("Users").Child(userName + "_" + userID).SetRawJsonValueAsync(json);
			       Task userByIdTask = DatabaseReference.Child("users").Child(userID).SetRawJsonValueAsync(json);

			       await Task.WhenAll(legacyTask, userByIdTask);

			       if (legacyTask.IsFaulted || userByIdTask.IsFaulted)
			       {
				       Exception exception = legacyTask.Exception ?? userByIdTask.Exception;
				       $"UpdateUser Write operation failed with {exception}".Log();
			       }
			       else if (legacyTask.IsCompleted && userByIdTask.IsCompleted)
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

            try
            {
                DataSnapshot newStructureSnapshot = await DatabaseReference.Child("users").Child(userID).GetValueAsync();

                if (newStructureSnapshot.Exists)
                {
                    return newStructureSnapshot;
                }
            }
            catch (Exception exception)
            {
                $"FetchUserData Read operation failed with {exception}".Log();
            }

            await DatabaseReference.Child("Users").Child(userName + "_" + userID).GetValueAsync().ContinueWithOnMainThread(task =>
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
			await HostRaceAsync(race, 3);
		}

		public async Task<string> HostRaceAsync(RaceData race, int capacity, string description = null)
        {
            if (Auth?.CurrentUser == null)
            {
                throw new InvalidOperationException("Cannot host a race without an authenticated user.");
            }

            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Race capacity must be greater than zero.");
            }

            int? currentUserLevel = await GetUserLevelAsync(Auth.CurrentUser.UserId);

            if (IsDeactivatedLevel(currentUserLevel))
            {
                throw new InvalidOperationException("Deactivated users cannot host races.");
            }

            string raceId = Guid.NewGuid().ToString("N");
            long timestamp = GetUnixTimestampMilliseconds();
            string hostDisplayName = race.HostName ?? Auth.CurrentUser.DisplayName ?? string.Empty;
            string raceStatus = MapRaceStatus(race.Status);

            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                [$"races/{raceId}/hostId"] = Auth.CurrentUser.UserId,
                [$"races/{raceId}/title"] = race.RaceName ?? string.Empty,
                [$"races/{raceId}/description"] = description ?? string.Empty,
                [$"races/{raceId}/capacity"] = capacity,
                [$"races/{raceId}/status"] = raceStatus,
                [$"races/{raceId}/createdAt"] = timestamp,
                [$"races/{raceId}/longitude"] = race.Longitude,
                [$"races/{raceId}/latitude"] = race.Latitude,
                [$"races/{raceId}/participants/{Auth.CurrentUser.UserId}/joinedAt"] = timestamp,
                [$"races/{raceId}/participants/{Auth.CurrentUser.UserId}/displayName"] = hostDisplayName,
                [$"races/{raceId}/participants/{Auth.CurrentUser.UserId}/isHost"] = true,
                [$"userRaces/{Auth.CurrentUser.UserId}/{raceId}/role"] = "host",
                [$"userRaces/{Auth.CurrentUser.UserId}/{raceId}/joinedAt"] = timestamp
            };

            await DatabaseReference.UpdateChildrenAsync(updates);

            return raceId;
        }

        public async Task SubmitJoinRequestAsync(string raceId)
        {
            if (Auth?.CurrentUser == null)
            {
                throw new InvalidOperationException("Cannot join a race without an authenticated user.");
            }

            int? currentUserLevel = await GetUserLevelAsync(Auth.CurrentUser.UserId);

            if (IsDeactivatedLevel(currentUserLevel))
            {
                throw new InvalidOperationException("Deactivated users cannot submit join requests.");
            }

            DataSnapshot raceSnapshot = await GetRaceSnapshotAsync(raceId);

            if (raceSnapshot is not { Exists: true })
            {
                throw new InvalidOperationException("Race not found.");
            }

            string status = raceSnapshot.Child("status").Value?.ToString();
            if (!string.IsNullOrEmpty(status) && status != "open")
            {
                throw new InvalidOperationException("Race is not open for join requests.");
            }

            int capacity = ConvertToNullableInt(raceSnapshot.Child("capacity").Value) ?? 0;
            long participantsCount = raceSnapshot.Child("participants").ChildrenCount;

            if (capacity > 0 && participantsCount >= capacity)
            {
                throw new InvalidOperationException("Race is already at capacity.");
            }

            long timestamp = GetUnixTimestampMilliseconds();

            var requestData = new Dictionary<string, object>
            {
                { "requestedAt", timestamp },
                { "status", "pending" },
                { "displayName", Auth.CurrentUser.DisplayName ?? string.Empty },
            };

            await DatabaseReference.Child("joinRequests").Child(raceId).Child(Auth.CurrentUser.UserId).SetValueAsync(requestData);
        }

        public async Task RetractJoinRequestAsync(string raceId)
        {
            if (Auth?.CurrentUser == null)
            {
                throw new InvalidOperationException("Cannot retract a request without an authenticated user.");
            }

            int? currentUserLevel = await GetUserLevelAsync(Auth.CurrentUser.UserId);

            if (IsDeactivatedLevel(currentUserLevel))
            {
                throw new InvalidOperationException("Deactivated users cannot retract join requests.");
            }

            await DatabaseReference.Child("joinRequests").Child(raceId).Child(Auth.CurrentUser.UserId).RemoveValueAsync();
        }

        public async Task<bool> HandleJoinRequestAsync(string raceId, string requesterUserId, bool approve)
        {
            if (Auth?.CurrentUser == null)
            {
                throw new InvalidOperationException("Cannot handle requests without an authenticated user.");
            }

            int? currentUserLevel = await GetUserLevelAsync(Auth.CurrentUser.UserId);

            if (IsDeactivatedLevel(currentUserLevel))
            {
                throw new InvalidOperationException("Deactivated users cannot manage races.");
            }

            DataSnapshot raceSnapshot = await GetRaceSnapshotAsync(raceId);

            if (raceSnapshot is not { Exists: true })
            {
                return false;
            }

            string hostId = raceSnapshot.Child("hostId").Value?.ToString();

            if (!IsAdminLevel(currentUserLevel) && hostId != Auth.CurrentUser.UserId)
            {
                throw new InvalidOperationException("Only the host or an admin can handle join requests.");
            }

            DataSnapshot requestSnapshot = await DatabaseReference.Child("joinRequests").Child(raceId).Child(requesterUserId).GetValueAsync();

            if (requestSnapshot is not { Exists: true })
            {
                    return false;
            }

            string currentStatus = requestSnapshot.Child("status").Value?.ToString();
            if (!string.Equals(currentStatus, "pending", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            long timestamp = GetUnixTimestampMilliseconds();
            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                [$"joinRequests/{raceId}/{requesterUserId}/status"] = approve ? "approved" : "rejected",
                [$"joinRequests/{raceId}/{requesterUserId}/processedAt"] = timestamp,
                [$"joinRequests/{raceId}/{requesterUserId}/processedBy"] = Auth.CurrentUser.UserId
            };

            if (approve)
            {
                int capacity = ConvertToNullableInt(raceSnapshot.Child("capacity").Value) ?? 0;
                long participantsCount = raceSnapshot.Child("participants").ChildrenCount;

                if (capacity > 0 && participantsCount >= capacity)
                {
                    updates[$"joinRequests/{raceId}/{requesterUserId}/status"] = "rejected";
                    await DatabaseReference.UpdateChildrenAsync(updates);
                    return false;
                }

                string displayName = requestSnapshot.Child("displayName").Value?.ToString();

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    DataSnapshot userSnapshot = await FetchUserDataById(requesterUserId);
                    displayName = userSnapshot?.Child("UserName").Value?.ToString() ?? string.Empty;
                }

                updates[$"races/{raceId}/participants/{requesterUserId}/joinedAt"] = timestamp;
                updates[$"races/{raceId}/participants/{requesterUserId}/displayName"] = displayName ?? string.Empty;
                updates[$"races/{raceId}/participants/{requesterUserId}/isHost"] = false;
                updates[$"userRaces/{requesterUserId}/{raceId}/role"] = "participant";
                updates[$"userRaces/{requesterUserId}/{raceId}/joinedAt"] = timestamp;
            }
            else
            {
                updates[$"userRaces/{requesterUserId}/{raceId}"] = null;
            }

            await DatabaseReference.UpdateChildrenAsync(updates);
            return true;
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
			DataSnapshot snapshot = await DatabaseReference.Child("races").GetValueAsync();

			if (snapshot.Exists)
			{
				return snapshot;
			}

			return await DatabaseReference.Child("Races").GetValueAsync();
		}

		private async Task<DataSnapshot> GetAllUsers()
		{
			DataSnapshot snapshot = await DatabaseReference.Child("users").GetValueAsync();

			if (snapshot.Exists)
			{
				return snapshot;
			}
	
			return await DatabaseReference.Child("Users").GetValueAsync();
        }

        private async Task<DataSnapshot> GetRaceSnapshotAsync(string raceId)
        {
            DataSnapshot snapshot = await DatabaseReference.Child("races").Child(raceId).GetValueAsync();

            if (snapshot.Exists)
            {
                return snapshot;
            }

            return await DatabaseReference.Child("Races").Child(raceId).GetValueAsync();
        }

        private async Task<DataSnapshot> FetchUserDataById(string userId)
        {
            DataSnapshot snapshot = await DatabaseReference.Child("users").Child(userId).GetValueAsync();

            if (snapshot.Exists)
            {
                return snapshot;
            }

            DataSnapshot legacySnapshot = await DatabaseReference.Child("Users").GetValueAsync();

            foreach (DataSnapshot child in legacySnapshot.Children)
            {
                if (child.Child("UserID").Value?.ToString() == userId)
                {
                    return child;
                }
            }

            return null;
        }

        private async Task<int?> GetUserLevelAsync(string userId)
        {
            DataSnapshot snapshot = await FetchUserDataById(userId);

            if (snapshot is not { Exists: true })
            {
                return null;
            }

            return ConvertToNullableInt(snapshot.Child("UserLevel").Value);
        }

        private static int? ConvertToNullableInt(object value)
        {
            return value switch
            {
                null => null,
                long l => (int)l,
                int i => i,
                double d => (int)d,
                string s when int.TryParse(s, out int parsed) => parsed,
                _ => null
            };
        }

        private static bool IsAdminLevel(int? level)
        {
            return level.HasValue && level.Value >= 2;
        }

        private static bool IsDeactivatedLevel(int? level)
        {
            return level.HasValue && level.Value == 0;
        }

        private static string MapRaceStatus(int status)
        {
            return status switch
            {
                1 => "in_progress",
                2 => "completed",
                3 => "cancelled",
                _ => "open",
            };
        }

        private static long GetUnixTimestampMilliseconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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
	}
}