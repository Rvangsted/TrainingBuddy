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
		public Task<string> GetActiveRaceIdAsync();
		public Task<UserData?> GetUserByFriendCodeAsync(string friendCode);
		public Task JoinRaceDirectlyAsync(string raceId);
		public Task KickParticipantAsync(string raceId, string participantUserId);
		public Task SubmitJoinRequestAsync(string raceId);
		public Task RetractJoinRequestAsync(string raceId);
		public Task<bool> HandleJoinRequestAsync(string raceId, string requesterUserId, bool approve);
		public Task LeaveRaceAsync(string raceId);
		public Task CancelRaceAsync(string raceId);
		public Task PatchUserFields(Dictionary<string, object> fields);
		public void StartStepCounter();
		public void StopStepCounter();
		public long DailyStepBase { get; }
		public Task<List<(string dateKey, long steps)>> FetchDailyStepsAsync(int days = 5);
	}

	public class DatabaseManager : IDatabaseManager
	{
		// Step counter — in-memory state
		private long _baseStepCount;       // StepCount loaded from Firebase at session start
		private long _deviceAnchor = -1;   // Device sensor value at session start (-1 = not yet anchored)
		private long _currentTotal;        // Running total reported to UI
		private long _lastSyncedTotal = -1;// Last value written to Firebase
		private long _dailyStepBase;       // Value of _currentTotal at start of today
		private string _dailyStepDate;     // The date string (yyyy-MM-dd) for _dailyStepBase
		private CancellationTokenSource _stepCts;

		public const int StepsPerPoint   = 2000;  // Steps required to earn 1 skill point (used for progress bar)
		private const int SensorPollMs   = 2000;  // How often to read the device sensor (ms)
		private const int FirebaseSyncMs = 60000; // How often to write to Firebase (ms)

		public event Action<long> StepCountChanged;

		public bool StepCounterRunning { get; private set; }
		public long DailyStepBase => _dailyStepBase;
		public bool isLocationUpdaterRunning { get; private set; }

		public UIManager UIManager { private get; set; }
		public FirebaseAuth Auth { get; set; }
		public DatabaseReference DatabaseReference { get; set; }
		public JsonSerializerSettings JsonSettings { get; set; }

		public async Task<bool> CreateUser(UserData user)
		{
			string json = JsonConvert.SerializeObject(user, JsonSettings);
			var userDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json, JsonSettings);

			$"CreateUser → path: users/{user.UserID}".Log();
			$"CreateUser → auth.uid: {Auth?.CurrentUser?.UserId ?? "NULL"}".Log();
			$"CreateUser → json: {json}".Log();

			var updates = new Dictionary<string, object>
			{
				[$"users/{user.UserID}"] = userDict,
				[$"friendCodes/{user.FriendCode}"] = user.UserID,
				[$"usernames/{user.UserName}"] = user.UserID,
			};

			Task task = DatabaseReference.UpdateChildrenAsync(updates);
			await task;

			if (task.IsFaulted)
			{
				$"CreateUser operation failed with {task.Exception}".Log();
				return false;
			}

			return true;
		}

		public async Task PatchUserFields(Dictionary<string, object> fields)
		{
			if (Auth?.CurrentUser == null) return;
			await DatabaseReference.Child("users").Child(Auth.CurrentUser.UserId).UpdateChildrenAsync(fields);
		}

		public async Task<UserData?> GetUserByFriendCodeAsync(string friendCode)
		{
			try
			{
				DataSnapshot codeSnapshot = await DatabaseReference.Child("friendCodes").Child(friendCode).GetValueAsync();
				if (!codeSnapshot.Exists)
					return null;

				string userId = codeSnapshot.Value.ToString();
				DataSnapshot userSnapshot = await DatabaseReference.Child("users").Child(userId).GetValueAsync();
				if (!userSnapshot.Exists)
					return null;

				return JsonConvert.DeserializeObject<UserData>(userSnapshot.GetRawJsonValue(), JsonSettings);
			}
			catch (Exception exception)
			{
				$"GetUserByFriendCodeAsync failed with {exception}".Log();
				return null;
			}
		}

		public async Task UpdateUser(FirebaseUser user, UserData userData)
		{
			string userName = user.DisplayName;
			string userID = user.UserId;
			UserData currentUserdata;

			await DatabaseReference.Child("users").Child(userID).GetValueAsync().ContinueWithOnMainThread(async task =>
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
			       userData.FriendCode ??= currentUserdata.FriendCode;
			       userData.Email ??= currentUserdata.Email;
			       userData.DateOfBirthDay ??= currentUserdata.DateOfBirthDay;
			       userData.DateOfBirthMonth ??= currentUserdata.DateOfBirthMonth;
			       userData.DateOfBirthYear ??= currentUserdata.DateOfBirthYear;
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
			string userID = user.UserId;

			try
			{
				return await DatabaseReference.Child("users").Child(userID).GetValueAsync();
			}
			catch (Exception exception)
			{
				$"FetchUserData Read operation failed with {exception}".Log();
				return null;
			}
		}

		public async Task CreateLobby(RaceData race)
		{
			await HostRaceAsync(race, 5);
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

            DataSnapshot hostSnapshot = await FetchUserDataById(Auth.CurrentUser.UserId);
            string hostSex = hostSnapshot?.Child("Sex").Value?.ToString() ?? string.Empty;

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
                [$"races/{raceId}/participants/{Auth.CurrentUser.UserId}/sex"] = hostSex,
                [$"userRaces/{Auth.CurrentUser.UserId}/{raceId}/role"] = "host",
                [$"userRaces/{Auth.CurrentUser.UserId}/{raceId}/joinedAt"] = timestamp,
            };

            await DatabaseReference.UpdateChildrenAsync(updates);

            return raceId;
        }

        public async Task JoinRaceDirectlyAsync(string raceId)
        {
            if (Auth?.CurrentUser == null)
            {
                throw new InvalidOperationException("Cannot join a race without an authenticated user.");
            }

            int? currentUserLevel = await GetUserLevelAsync(Auth.CurrentUser.UserId);

            if (IsDeactivatedLevel(currentUserLevel))
            {
                throw new InvalidOperationException("Deactivated users cannot join races.");
            }

            DataSnapshot raceSnapshot = await GetRaceSnapshotAsync(raceId);

            if (raceSnapshot is not { Exists: true })
            {
                throw new InvalidOperationException("Race not found.");
            }

            string status = raceSnapshot.Child("status").Value?.ToString();
            if (!string.IsNullOrEmpty(status) && status != "open")
            {
                throw new InvalidOperationException("Race is not open for joining.");
            }

            int capacity = ConvertToNullableInt(raceSnapshot.Child("capacity").Value) ?? 0;
            long participantsCount = raceSnapshot.Child("participants").ChildrenCount;

            if (capacity > 0 && participantsCount >= capacity)
            {
                throw new InvalidOperationException("Race is already at capacity.");
            }

            await EnsureUserCanJoinRace(Auth.CurrentUser.UserId, raceId);

            string userId = Auth.CurrentUser.UserId;
            long timestamp = GetUnixTimestampMilliseconds();

            DataSnapshot userSnapshot = await FetchUserDataById(userId);
            string displayName = userSnapshot?.Child("UserName").Value?.ToString()
                ?? Auth.CurrentUser.DisplayName
                ?? string.Empty;
            string sex = userSnapshot?.Child("Sex").Value?.ToString() ?? string.Empty;

            var updates = new Dictionary<string, object>
            {
                [$"races/{raceId}/participants/{userId}/joinedAt"] = timestamp,
                [$"races/{raceId}/participants/{userId}/displayName"] = displayName,
                [$"races/{raceId}/participants/{userId}/isHost"] = false,
                [$"races/{raceId}/participants/{userId}/sex"] = sex,
                [$"userRaces/{userId}/{raceId}/role"] = "participant",
                [$"userRaces/{userId}/{raceId}/joinedAt"] = timestamp,
            };

            await DatabaseReference.UpdateChildrenAsync(updates);
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

                DataSnapshot userSnapshot = await FetchUserDataById(requesterUserId);
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = userSnapshot?.Child("UserName").Value?.ToString() ?? string.Empty;
                }
                string sex = userSnapshot?.Child("Sex").Value?.ToString() ?? string.Empty;

                updates[$"races/{raceId}/participants/{requesterUserId}/joinedAt"] = timestamp;
                updates[$"races/{raceId}/participants/{requesterUserId}/displayName"] = displayName ?? string.Empty;
                updates[$"races/{raceId}/participants/{requesterUserId}/isHost"] = false;
                updates[$"races/{raceId}/participants/{requesterUserId}/sex"] = sex;
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

        public async Task KickParticipantAsync(string raceId, string participantUserId)
        {
            if (Auth?.CurrentUser == null)
            {
                throw new InvalidOperationException("Cannot kick a participant without an authenticated user.");
            }

            DataSnapshot raceSnapshot = await GetRaceSnapshotAsync(raceId);

            if (raceSnapshot is not { Exists: true })
            {
                throw new InvalidOperationException("Race not found.");
            }

            string hostId = raceSnapshot.Child("hostId").Value?.ToString();
            int? currentUserLevel = await GetUserLevelAsync(Auth.CurrentUser.UserId);

            if (!IsAdminLevel(currentUserLevel) && hostId != Auth.CurrentUser.UserId)
            {
                throw new InvalidOperationException("Only the host or an admin can kick participants.");
            }

            if (participantUserId == hostId)
            {
                throw new InvalidOperationException("The host cannot kick themselves.");
            }

            var updates = new Dictionary<string, object>
            {
                [$"races/{raceId}/participants/{participantUserId}"] = null,
                [$"userRaces/{participantUserId}/{raceId}"] = null,
                [$"joinRequests/{raceId}/{participantUserId}"] = null,
            };

            await DatabaseReference.UpdateChildrenAsync(updates);
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

            var updates = new Dictionary<string, object>
            {
                [$"races/{raceId}/status"] = "cancelled",
                [$"races/{raceId}/cancelledAt"] = GetUnixTimestampMilliseconds(),
                [$"races/{raceId}/participants"] = null,
                [$"joinRequests/{raceId}"] = null,
            };

            foreach (DataSnapshot participant in raceSnapshot.Child("participants").Children)
            {
                updates[$"userRaces/{participant.Key}/{raceId}"] = null;
            }

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
			try
			{
				DataSnapshot snapshot = await DatabaseReference.Child("races").GetValueAsync();
				return snapshot.Exists ? snapshot : null;
			}
			catch (Exception ex)
			{
				$"GetAllRaces failed: {ex.Message}".LogError();
				return null;
			}
		}

		public async Task<List<RaceListEntry>> FetchRaceListAsync()
		{
			var result = new List<RaceListEntry>();
			DataSnapshot snapshot = await GetAllRaces();

			if (snapshot == null)
			{
				return result;
			}

			foreach (DataSnapshot raceSnapshot in snapshot.Children)
			{
				string status = raceSnapshot.Child("status").Value?.ToString() ?? string.Empty;
				string title = raceSnapshot.Child("title").Value?.ToString() ?? string.Empty;
				long createdAt = ReadLong(raceSnapshot.Child("createdAt").Value);
				int capacity = ConvertToNullableInt(raceSnapshot.Child("capacity").Value) ?? 0;
				int participantCount = (int)raceSnapshot.Child("participants").ChildrenCount;

				string hostName = string.Empty;
				string hostSex = string.Empty;
				foreach (DataSnapshot participant in raceSnapshot.Child("participants").Children)
				{
					if (participant.Child("isHost").Value is true)
					{
						hostName = participant.Child("displayName").Value?.ToString() ?? string.Empty;
						hostSex = participant.Child("sex").Value?.ToString() ?? string.Empty;
						break;
					}
				}

				result.Add(new RaceListEntry
				{
					RaceId = raceSnapshot.Key,
					Title = title,
					HostName = hostName,
					HostSex = hostSex,
					Status = status,
					CreatedAt = createdAt,
					ParticipantCount = participantCount,
					Capacity = capacity,
				});
			}

			return result;
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

        public async Task<string> GetActiveRaceIdAsync()
        {
            if (Auth?.CurrentUser == null)
                return null;

            return await FindActiveRaceIdAsync(Auth.CurrentUser.UserId);
        }

        public async Task<List<(string displayName, bool isHost, long joinedAt, string sex, string userId)>> FetchCurrentRaceParticipantsAsync()
        {
            var empty = new List<(string, bool, long, string, string)>();

            if (Auth?.CurrentUser == null)
                return empty;

            string activeRaceId = await FindActiveRaceIdAsync(Auth.CurrentUser.UserId);

            if (activeRaceId == null)
                return empty;

            DataSnapshot activeRace = await GetRaceSnapshotAsync(activeRaceId);
            var participants = new List<(string displayName, bool isHost, long joinedAt, string sex, string userId)>();

            foreach (DataSnapshot participant in activeRace.Child("participants").Children)
            {
                string displayName = participant.Child("displayName").Value?.ToString() ?? string.Empty;
                bool isHost = participant.Child("isHost").Value is true;
                long joinedAt = ReadLong(participant.Child("joinedAt").Value);
                string sex = participant.Child("sex").Value?.ToString() ?? string.Empty;
                string userId = participant.Key;
                participants.Add((displayName, isHost, joinedAt, sex, userId));
            }

            return participants;
        }

        private async Task<string> FindActiveRaceIdAsync(string userId)
        {
            DataSnapshot userRacesSnapshot = await GetUserRacesSnapshotAsync(userId);

            if (userRacesSnapshot is not { Exists: true })
                return null;

            foreach (DataSnapshot raceEntry in userRacesSnapshot.Children)
            {
                DataSnapshot raceSnapshot = await GetRaceSnapshotAsync(raceEntry.Key);
                if (raceSnapshot is not { Exists: true })
                    continue;

                string status = raceSnapshot.Child("status").Value?.ToString();
                if (!IsActiveRaceStatus(status))
                    continue;

                return raceEntry.Key;
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

        public async Task<bool> IsUserInActiveRaceAsync(string userId)
        {
            var (isHostingActive, isParticipatingActive) = await GetActiveRaceParticipationAsync(userId);
            return isHostingActive || isParticipatingActive;
        }

        public async Task<bool> IsUserHostingActiveRaceAsync(string userId)
        {
            var (isHostingActive, _) = await GetActiveRaceParticipationAsync(userId);
            return isHostingActive;
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

			// Load from Firebase, then compare with local PlayerPrefs backup.
			// If Firebase is unreachable (offline) it may return 0 — in that case
			// the locally saved value is more accurate.
			DataSnapshot data = await FetchUserData(Auth.CurrentUser);
			long firebaseSteps    = ReadLong(data?.Child("StepCount").Value);
			long firebaseSnapshot = ReadLong(data?.Child("StepCountSnapshot").Value);

			long localSteps    = PlayerPrefs.GetInt(StepCountKey, 0);
			long localSnapshot = PlayerPrefs.GetInt(StepSnapshotKey, 0);

			bool useLocal  = localSteps > firebaseSteps;
			_baseStepCount = useLocal ? localSteps    : firebaseSteps;
			_deviceAnchor  = useLocal ? localSnapshot : firebaseSnapshot;
			_currentTotal     = _baseStepCount;
			_lastSyncedTotal  = _baseStepCount;

			// Load daily step base and handle day rollover
			long firebaseDailyBase   = ReadLong(data?.Child("DailyStepBase").Value);
			string firebaseDailyDate = data?.Child("DailyStepDate").Value?.ToString() ?? "";
			long localDailyBase      = PlayerPrefs.GetInt(DailyStepBaseKey, 0);
			string localDailyDate    = PlayerPrefs.GetString(DailyStepDateKey, "");

			_dailyStepBase = useLocal ? localDailyBase : firebaseDailyBase;
			_dailyStepDate = (useLocal ? localDailyDate : firebaseDailyDate) ?? "";

			string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
			if (string.IsNullOrEmpty(_dailyStepDate))
			{
				// First session ever — seed today's base from the loaded total
				_dailyStepBase = _baseStepCount;
				_dailyStepDate = today;
				SaveDailyBaseLocally();
			}
			else if (_dailyStepDate != today)
			{
				// Day rolled over since last session — archive the previous day's steps
				long prevDaySteps = Math.Max(0, _baseStepCount - _dailyStepBase);
				string prevDate = _dailyStepDate;
				_dailyStepBase = _baseStepCount;
				_dailyStepDate = today;
				SaveDailyBaseLocally();
				_ = ArchiveDailyStepsAsync(prevDate, prevDaySteps);
			}

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

					// Anchor on the first valid reading of this session (new user, or first
					// time the counter starts). This prevents counting steps taken before
					// account creation.
					if (_deviceAnchor <= 0)
					{
						_deviceAnchor = deviceValue;
					}
					else if (deviceValue < _deviceAnchor)
					{
						// Device step counter was reset (reboot, OS reset, etc.).
						// Preserve accumulated total and re-anchor from the new device value.
						_baseStepCount = _currentTotal;
						_deviceAnchor  = deviceValue;
						SaveStepsLocally(_currentTotal, deviceValue);
					}

					long newTotal = _baseStepCount + (deviceValue - _deviceAnchor);
					if (newTotal != _currentTotal)
					{
						_currentTotal = newTotal;
						SaveStepsLocally(newTotal, deviceValue);
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

			// Always persist locally first so data survives even if Firebase is unreachable.
			SaveStepsLocally(_currentTotal, deviceValue);

			string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
			var updates = new Dictionary<string, object>
			{
				{ "StepCount",         (int)_currentTotal },
				{ "StepCountSnapshot", (int)deviceValue   },
			};

			// Day rolled over mid-session — archive the previous day then reset base
			if (_dailyStepDate != today)
			{
				long prevDaySteps = Math.Max(0, _lastSyncedTotal - _dailyStepBase);
				updates[$"dailySteps/{_dailyStepDate}"] = (int)prevDaySteps;
				_dailyStepBase = _currentTotal;
				_dailyStepDate = today;
				updates["DailyStepBase"] = (int)_dailyStepBase;
				updates["DailyStepDate"] = _dailyStepDate;
				SaveDailyBaseLocally();
			}

			// Always write today's live step count so the graph stays current
			updates[$"dailySteps/{today}"] = (int)(_currentTotal - _dailyStepBase);

			try
			{
				await DatabaseReference
					.Child("users")
					.Child(Auth.CurrentUser.UserId)
					.UpdateChildrenAsync(updates);

				_lastSyncedTotal = _currentTotal;
			}
			catch (Exception ex)
			{
				$"SyncStepsToFirebase failed: {ex}".Log();
			}
		}

		private async Task ArchiveDailyStepsAsync(string date, long steps)
		{
			if (Auth?.CurrentUser == null) return;
			try
			{
				await DatabaseReference
					.Child("users")
					.Child(Auth.CurrentUser.UserId)
					.UpdateChildrenAsync(new Dictionary<string, object>
					{
						{ "DailyStepBase",        (int)_dailyStepBase },
						{ "DailyStepDate",         _dailyStepDate     },
						{ $"dailySteps/{date}",    (int)steps          }
					});
			}
			catch (Exception ex)
			{
				$"ArchiveDailyStepsAsync failed: {ex}".Log();
			}
		}

		public async Task<List<(string dateKey, long steps)>> FetchDailyStepsAsync(int days = 5)
		{
			if (Auth?.CurrentUser == null) return new List<(string, long)>();
			try
			{
				DataSnapshot snapshot = await DatabaseReference
					.Child("users")
					.Child(Auth.CurrentUser.UserId)
					.Child("dailySteps")
					.OrderByKey()
					.LimitToLast(days)
					.GetValueAsync();

				var result = new List<(string, long)>();
				foreach (DataSnapshot child in snapshot.Children)
					result.Add((child.Key, ReadLong(child.Value)));
				return result; // Firebase returns children in ascending key order
			}
			catch (Exception ex)
			{
				$"FetchDailyStepsAsync failed: {ex}".Log();
				return new List<(string, long)>();
			}
		}

		private void SaveStepsLocally(long steps, long deviceSnapshot)
		{
			PlayerPrefs.SetInt(StepCountKey, (int)steps);
			PlayerPrefs.SetInt(StepSnapshotKey, (int)deviceSnapshot);
			PlayerPrefs.Save();
		}

		// Scoped per user so multiple accounts on the same device don't share data.
		private string StepCountKey    => $"StepCount_{Auth?.CurrentUser?.UserId ?? "anon"}";
		private string StepSnapshotKey => $"StepCountSnapshot_{Auth?.CurrentUser?.UserId ?? "anon"}";
		private string DailyStepBaseKey => $"DailyStepBase_{Auth?.CurrentUser?.UserId ?? "anon"}";
		private string DailyStepDateKey => $"DailyStepDate_{Auth?.CurrentUser?.UserId ?? "anon"}";

		private void SaveDailyBaseLocally()
		{
			PlayerPrefs.SetInt(DailyStepBaseKey, (int)_dailyStepBase);
			PlayerPrefs.SetString(DailyStepDateKey, _dailyStepDate);
			PlayerPrefs.Save();
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