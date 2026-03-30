#region

using System;
using System.Collections.Generic;
using System.Threading;
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
		public Task LeaveRaceAsync(string raceId);
		public Task CancelRaceAsync(string raceId);
		public void StartStepCounter();
		public void StopStepCounter();
	}

	public class DatabaseManager : IDatabaseManager
	{
		// Step counter — in-memory state
		private long _baseStepCount;    // StepCount loaded from Firebase at session start
		private long _deviceAnchor = -1; // Device sensor value at session start (-1 = not yet anchored)
		private long _currentTotal;     // Running total reported to UI
		private long _lastSyncedTotal = -1; // Last value written to Firebase
		private CancellationTokenSource _stepCts;

		private const int SensorPollMs   = 2000;  // How often to read the device sensor
		private const int FirebaseSyncMs = 60000; // How often to write to Firebase

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

			Task task = DatabaseReference.Child("users").Child(user.UserID).SetRawJsonValueAsync(json);

			await Task.WhenAll(task);

			if (task.IsFaulted)
			{
				$"CreateUser operation failed with {task.Exception}".Log();
			}
			else if (task.IsCompleted)
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

			       Task updateTask = DatabaseReference.Child("users").Child(userID).SetRawJsonValueAsync(json);

			       await Task.WhenAll(updateTask);

			       if (updateTask.IsFaulted)
			       {
				       $"UpdateUser Write operation failed with {updateTask.Exception}".Log();
			       }
			       else if (updateTask.IsCompleted)
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

			ResetStepCountBase(updatedStepCount);

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

            await EnsureUserCanHostRace(Auth.CurrentUser.UserId);

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
                [$"userRaces/{Auth.CurrentUser.UserId}/{raceId}/joinedAt"] = timestamp,
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

            await EnsureUserCanJoinRace(Auth.CurrentUser.UserId, raceId);

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

            await EnsureUserCanJoinRace(requesterUserId, raceId);

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

        public async Task LeaveRaceAsync(string raceId)
        {
            if (Auth?.CurrentUser == null)
            {
                throw new InvalidOperationException("Cannot leave a race without an authenticated user.");
            }

            int? currentUserLevel = await GetUserLevelAsync(Auth.CurrentUser.UserId);

            if (IsDeactivatedLevel(currentUserLevel))
            {
                throw new InvalidOperationException("Deactivated users cannot leave races.");
            }

            DataSnapshot raceSnapshot = await GetRaceSnapshotAsync(raceId);

            if (raceSnapshot is not { Exists: true })
            {
                throw new InvalidOperationException("Race not found.");
            }

            string status = raceSnapshot.Child("status").Value?.ToString();

            if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "in_progress", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("You cannot leave a race that is in progress or completed.");
            }

            string hostId = raceSnapshot.Child("hostId").Value?.ToString();
            if (hostId == Auth.CurrentUser.UserId)
            {
                throw new InvalidOperationException("Hosts must cancel their race instead of leaving it.");
            }

            if (raceSnapshot.Child("participants").Child(Auth.CurrentUser.UserId) is not { Exists: true })
            {
                return;
            }

            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                [$"races/{raceId}/participants/{Auth.CurrentUser.UserId}"] = null,
                [$"userRaces/{Auth.CurrentUser.UserId}/{raceId}"] = null,
            };

            updates[$"joinRequests/{raceId}/{Auth.CurrentUser.UserId}"] = null;

            await DatabaseReference.UpdateChildrenAsync(updates);
        }

        public async Task CancelRaceAsync(string raceId)
        {
            if (Auth?.CurrentUser == null)
            {
                throw new InvalidOperationException("Cannot cancel a race without an authenticated user.");
            }

            int? currentUserLevel = await GetUserLevelAsync(Auth.CurrentUser.UserId);

            if (IsDeactivatedLevel(currentUserLevel))
            {
                throw new InvalidOperationException("Deactivated users cannot cancel races.");
            }

            DataSnapshot raceSnapshot = await GetRaceSnapshotAsync(raceId);

            if (raceSnapshot is not { Exists: true })
            {
                throw new InvalidOperationException("Race not found.");
            }

            string hostId = raceSnapshot.Child("hostId").Value?.ToString();

            if (!IsAdminLevel(currentUserLevel) && hostId != Auth.CurrentUser.UserId)
            {
                throw new InvalidOperationException("Only the host or an admin can cancel this race.");
            }

            string status = raceSnapshot.Child("status").Value?.ToString();

            if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Completed races cannot be cancelled.");
            }

            if (string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                [$"races/{raceId}/status"] = "cancelled",
                [$"races/{raceId}/cancelledAt"] = GetUnixTimestampMilliseconds(),
                [$"joinRequests/{raceId}"] = null,
            };

            await DatabaseReference.UpdateChildrenAsync(updates);
        }

  //       public async Task<List<string>> FindNearbyLobbies()
		// {
		// 	// var nearbyLobbies = new List<string>();
		// 	var raceList = await NearbyRaces(10);
		// 	// var raceList = await GetAllRaces();
  //
		// 	// foreach (DataSnapshot raceListChild in raceList.Children)
		// 	// {
		// 	// 	foreach (string user in userList)
		// 	// 	{
		// 	// 		if (raceListChild.Key == user)
		// 	// 		{
		// 	// 			nearbyLobbies.Add(raceListChild.Key);
		// 	// 		}
		// 	// 	}
		// 	// }
  //
		// 	return raceList;
		// }

		public async Task<List<DataSnapshot>> NearbyRaces(int range)
		{
#if !UNITY_EDITOR
			if (!Input.location.isEnabledByUser)
			{
				return null;
			}

			if (Input.location.status != LocationServiceStatus.Running)
			{
				Input.location.Start();
			}
#endif

			var raceData = await GetAllRaces();
#if !UNITY_EDITOR
			return UtilityMethods.FindRacesInRange(raceData, Input.location.lastData.latitude, Input.location.lastData.longitude, range);
#else
			return UtilityMethods.FindRacesInRange(raceData, 0, 0, range);
#endif
		}

		private async Task<DataSnapshot> GetAllRaces()
		{
			DataSnapshot snapshot = await DatabaseReference.Child("races").GetValueAsync();

			if (snapshot.Exists)
			{
				return snapshot;
			}

			return null;
		}

		private async Task<DataSnapshot> GetAllUsers()
		{
			DataSnapshot snapshot = await DatabaseReference.Child("users").GetValueAsync();

			if (snapshot.Exists)
			{
				return snapshot;
			}

			return null;
        }

        private async Task<DataSnapshot> GetRaceSnapshotAsync(string raceId)
        {
            DataSnapshot snapshot = await DatabaseReference.Child("races").Child(raceId).GetValueAsync();

            if (snapshot.Exists)
            {
                return snapshot;
            }

            return null;
        }

        private async Task<DataSnapshot> GetUserRacesSnapshotAsync(string userId)
        {
            DataSnapshot snapshot = await DatabaseReference.Child("userRaces").Child(userId).GetValueAsync();

            if (snapshot.Exists)
            {
                return snapshot;
            }

            return null;
        }

        private async Task EnsureUserCanHostRace(string userId)
        {
            var (isHostingActive, isParticipatingActive) = await GetActiveRaceParticipationAsync(userId);

            if (isHostingActive)
            {
                throw new InvalidOperationException("You are already hosting an active race.");
            }

            if (isParticipatingActive)
            {
                throw new InvalidOperationException("You cannot host a race while participating in another active race.");
            }
        }

        private async Task EnsureUserCanJoinRace(string userId, string raceIdToIgnore)
        {
            var (isHostingActive, isParticipatingActive) = await GetActiveRaceParticipationAsync(userId, raceIdToIgnore);

            if (isHostingActive)
            {
                throw new InvalidOperationException("You cannot join another race while hosting an active race.");
            }

            if (isParticipatingActive)
            {
                throw new InvalidOperationException("You are already participating in an active race.");
            }
        }

        private async Task<(bool isHostingActive, bool isParticipatingActive)> GetActiveRaceParticipationAsync(string userId, string raceIdToIgnore = null)
        {
            DataSnapshot snapshot = await GetUserRacesSnapshotAsync(userId);

            bool hostingActive = false;
            bool participatingActive = false;

            if (snapshot is { Exists: true })
            {
                foreach (DataSnapshot raceEntry in snapshot.Children)
                {
                    if (string.Equals(raceEntry.Key, raceIdToIgnore, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    DataSnapshot raceSnapshot = await GetRaceSnapshotAsync(raceEntry.Key);

                    if (raceSnapshot is not { Exists: true })
                    {
                        continue;
                    }

                    string status = raceSnapshot.Child("status").Value?.ToString();

                    if (!IsActiveRaceStatus(status))
                    {
                        continue;
                    }

                    string role = raceEntry.Child("role").Value?.ToString();

                    if (string.Equals(role, "host", StringComparison.OrdinalIgnoreCase))
                    {
                        hostingActive = true;
                    }
                    else
                    {
                        participatingActive = true;
                    }

                    if (hostingActive && participatingActive)
                    {
                        break;
                    }
                }
            }

            return (hostingActive, participatingActive);
        }

        private static bool IsActiveRaceStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return true;
            }

            return !string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<DataSnapshot> FetchUserDataById(string userId)
        {
            DataSnapshot snapshot = await DatabaseReference.Child("users").Child(userId).GetValueAsync();

            if (snapshot.Exists)
            {
                return snapshot;
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

		// -------------------------------------------------------------------------
		// Step counter
		// -------------------------------------------------------------------------

		/// <summary>
		/// Loads the saved step count from Firebase once, then starts a lightweight
		/// polling loop that reads the device sensor every 2 s and fires
		/// StepCountChanged immediately — no Firebase involved per poll.
		/// A single Firebase write happens every 60 s (and once on stop).
		/// Call StopStepCounter() on OnApplicationPause/OnDestroy.
		/// </summary>
		public async void StartStepCounter()
		{
			if (StepCounterRunning) return;
			if (Auth?.CurrentUser == null) return;
			if (!Permission.HasUserAuthorizedPermission("android.permission.ACTIVITY_RECOGNITION")) return;
			if (!EnsureStepCounterDevice()) return;

			// Load the authoritative base from Firebase once
			DataSnapshot data = await FetchUserData(Auth.CurrentUser);
			_baseStepCount   = ReadLong(data?.Child("StepCount").Value);
			_deviceAnchor    = ReadLong(data?.Child("StepCountSnapshot").Value);
			_currentTotal    = _baseStepCount;
			_lastSyncedTotal = _baseStepCount;

			// Immediately show the saved count while we wait for the first sensor tick
			StepCountChanged?.Invoke(_currentTotal);

			_stepCts = new CancellationTokenSource();
			StepCounterRunning = true;
			_ = StepCounterLoop(_stepCts.Token);
		}

		/// <summary>
		/// Stops the polling loop and performs a final Firebase sync.
		/// Should be called from OnApplicationPause(true) and OnDestroy.
		/// </summary>
		public void StopStepCounter()
		{
			_stepCts?.Cancel();
			_stepCts = null;
			// StepCounterRunning is set to false at the end of StepCounterLoop
		}

		private async Task StepCounterLoop(CancellationToken ct)
		{
			int timeSinceLastSyncMs = 0;

			try
			{
				while (!ct.IsCancellationRequested)
				{
					await UniTask.Delay(SensorPollMs, cancellationToken: ct);

					if (!EnsureStepCounterDevice()) continue;

					long deviceValue = StepCounter.current.stepCounter.ReadValue();
					if (deviceValue <= 0) continue;

					// Anchor the device counter on the first valid reading of this session.
					// If Firebase returned 0 (new user), we anchor here so we don't
					// miscount steps the user walked before installing the app.
					if (_deviceAnchor <= 0)
						_deviceAnchor = deviceValue;

					// If deviceValue < _deviceAnchor the device was rebooted and the
					// hardware counter reset to 0 — treat deviceValue as the full delta.
					long delta = deviceValue >= _deviceAnchor
						? deviceValue - _deviceAnchor
						: deviceValue;

					long newTotal = _baseStepCount + delta;
					if (newTotal != _currentTotal)
					{
						_currentTotal = newTotal;
						StepCountChanged?.Invoke(_currentTotal); // instant UI update, no Firebase
					}

					timeSinceLastSyncMs += SensorPollMs;
					if (timeSinceLastSyncMs >= FirebaseSyncMs)
					{
						timeSinceLastSyncMs = 0;
						await SyncStepsToFirebase(deviceValue);
					}
				}
			}
			catch (OperationCanceledException) { }

			// Final sync before fully stopping
			if (StepCounter.current is { enabled: true })
			{
				long finalDevice = StepCounter.current.stepCounter.ReadValue();
				if (finalDevice > 0)
					await SyncStepsToFirebase(finalDevice);
			}

			StepCounterRunning = false;
		}

		/// <summary>
		/// Writes only StepCount and StepCountSnapshot — no full object read/write cycle.
		/// Skips the write if nothing changed since last sync.
		/// </summary>
		private async Task SyncStepsToFirebase(long deviceValue)
		{
			if (Auth?.CurrentUser == null) return;
			if (_currentTotal == _lastSyncedTotal) return;

			try
			{
				await DatabaseReference
					.Child("users")
					.Child(Auth.CurrentUser.UserId)
					.UpdateChildrenAsync(new Dictionary<string, object>
					{
						{ "StepCount",         (int)_currentTotal },
						{ "StepCountSnapshot", (int)deviceValue   }
					});

				_lastSyncedTotal = _currentTotal;
			}
			catch (Exception ex)
			{
				$"SyncStepsToFirebase failed: {ex}".Log();
			}
		}

		/// <summary>
		/// Re-anchors the in-memory tracking after an external change to the stored
		/// step count (e.g. investing steps reduces it). Without this, the next poll
		/// cycle would compute an incorrect total from the stale _baseStepCount.
		/// </summary>
		private void ResetStepCountBase(long newTotal)
		{
			_currentTotal    = newTotal;
			_lastSyncedTotal = newTotal;
			_baseStepCount   = newTotal;

			long deviceValue = StepCounter.current?.stepCounter.ReadValue() ?? 0;
			if (deviceValue > 0)
				_deviceAnchor = deviceValue;
		}

		private bool EnsureStepCounterDevice()
		{
			if (StepCounter.current == null)
			{
				Debug.Log("StepCounter unavailable, attempting to re-register");
				InputSystem.AddDevice<StepCounter>();
			}

			if (StepCounter.current == null) return false;

			if (!StepCounter.current.enabled)
			{
				Debug.Log("StepCounter disabled, enabling");
				InputSystem.EnableDevice(StepCounter.current);
			}

			return StepCounter.current.enabled;
		}

		private static long ReadLong(object value) => value switch
		{
			long   l => l,
			int    i => i,
			double d => (long)d,
			null     => 0,
			_        => Convert.ToInt64(value)
		};
	}
}