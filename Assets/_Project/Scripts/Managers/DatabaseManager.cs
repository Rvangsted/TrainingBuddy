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
using TrainingBuddy;
using TrainingBuddy.FireBase;
using TrainingBuddy.UI;
using TrainingBuddy.Utility;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.InputSystem;

namespace TrainingBuddy.Managers
{
	public enum StepCounterAvailability
	{
		Available,
		PermissionDenied,
		SensorUnsupported,
		ProviderNotInstalled // Health Connect missing on Android 13 and below; not reachable via the current sensor-based provider.
	}

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
		public Task<int> FetchRaceCapacityAsync(string raceId);
		public Task<string> GetRaceStatusAsync(string raceId);
		public Task SubmitJoinRequestAsync(string raceId);
		public Task RetractJoinRequestAsync(string raceId);
		public Task<bool> HandleJoinRequestAsync(string raceId, string requesterUserId, bool approve);
		public Task LeaveRaceAsync(string raceId);
		public Task<RaceSimulation> StartRaceAsync(string raceId);
		public Task<RaceSimulation> FetchRaceSimulationAsync(string raceId);
		public void ListenForRaceStart(string raceId, Action onStarted);
		public void StopRaceStartListener();
		public Task MarkRaceWatchedAsync(string raceId);
		public Task CancelRaceAsync(string raceId);
		public Task PatchUserFields(Dictionary<string, object> fields);
		public Task<StepCounterAvailability> StartStepCounter();
		public void StopStepCounter();
		public bool HasStepDataProvider { get; }
		public Task<StepCounterAvailability> CheckStepProviderAvailabilityAsync();
		public Task<StepCounterAvailability> RequestStepProviderPermissionAsync();
		public long DailyStepBase { get; }
		public Task<List<(string dateKey, long steps)>> FetchDailyStepsAsync(int days = 5);
		public Task<bool> DeleteAccountAsync(string password);

		public Task SendFriendRequestAsync(string targetUserId);
		public Task RevokeFriendRequestAsync(string targetUserId);
		public Task<List<(UserData user, string fromUserId)>> FetchIncomingRequestsAsync();
		public Task HandleFriendRequestAsync(string requesterUserId, bool accept);
		public Task<List<UserData>> FetchFriendsAsync();
		public Task RemoveFriendAsync(string friendUserId);
		public Task<List<LeaderboardEntry>> FetchLeaderboardAsync();
	}

	public class DatabaseManager : IDatabaseManager
	{
		#region Fields & Constants

		// Step counter — in-memory state
		private long _baseStepCount;       // StepCount loaded from Firebase at session start
		private long _deviceAnchor = -1;   // Device sensor value at session start (-1 = not yet anchored)
		private long _currentTotal;        // Running total reported to UI
		private long _lastSyncedTotal = -1;// Last value written to Firebase
		private long _currentCurrency;     // StepCurrency balance — minted 1:1 with synced step deltas, see StepsAsCurrency_Scope.md
		private long _dailyStepBase;       // Value of _currentTotal at start of today
		private string _dailyStepDate;     // The date string (yyyy-MM-dd) for _dailyStepBase
		private long _lastSyncTimestampMillis = -1; // Provider-backed path only: unix ms anchor paired with _baseStepCount. -1 = not yet anchored.
		private CancellationTokenSource _stepCts;
		private string _cachedUserName;
		private string _cachedSex;

		private const long DeviceSyncMaxBacklogMillis = 30L * 24 * 60 * 60 * 1000; // 30-day cap on a single sync's reach
		private const long MaxPlausibleStepsPerSync   = 20000;                     // sanity clamp, per the migration doc

		// First per-device identifier in this codebase. Firebase RTDB keys forbid '.', '#', '$', '[', ']', '/',
		// so the platform-provided id is sanitized defensively even though real device ids don't contain them.
		private static readonly string DeviceId = SystemInfo.deviceUniqueIdentifier
			.Replace('.', '_').Replace('#', '_').Replace('$', '_').Replace('[', '_').Replace(']', '_').Replace('/', '_');

		// Android uses the Health Connect provider, iOS uses the HealthKit provider; only the
		// Editor still falls back to the raw-sensor path — see StepCounter_HealthPlatform_Migration_Scope.md.
		private readonly IStepDataProvider _stepDataProvider = CreateStepDataProvider();

		private static IStepDataProvider CreateStepDataProvider()
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			return new HealthConnectStepProvider();
#elif UNITY_IOS && !UNITY_EDITOR
			return new HealthKitStepProvider();
#else
			return null;
#endif
		}

		public const int StepsPerPoint   = 2000;  // Steps required to earn 1 skill point (used for progress bar)
		private const int SensorPollMs   = 2000;  // How often to read the device sensor (ms)
		private const int FirebaseSyncMs = 60000; // How often to write to Firebase (ms)

		// Some OEM sensor hubs (e.g. Samsung's dedicated sensor coprocessor) register the
		// native step-counter device shortly after app start rather than immediately, so we
		// poll briefly for it instead of declaring the sensor missing on the first check.
		private const int SensorDetectRetries     = 5;
		private const int SensorDetectRetryDelayMs = 500;

		public const string AiPlayerName    = "Buddy"; // Display name for AI fill-in players — change here
		private const int   MinRaceParticipants = 3;   // Minimum real players required to start

		public event Action<long> StepCountChanged;

		public bool StepCounterRunning { get; private set; }
		public bool HasStepDataProvider => _stepDataProvider != null;
		public long DailyStepBase => _dailyStepBase;
		public bool isLocationUpdaterRunning { get; private set; }

		public UIManager UIManager { private get; set; }
		public FirebaseAuth Auth { get; set; }
		public DatabaseReference DatabaseReference { get; set; }
		public JsonSerializerSettings JsonSettings { get; set; }

		#endregion

		#region User Data

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
				[$"leaderboard/{user.UserID}/UserName"]  = user.UserName,
				[$"leaderboard/{user.UserID}/Sex"]       = user.Sex ?? "",
				[$"leaderboard/{user.UserID}/StepCount"] = 0,
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
			       userData.StepCurrency ??= currentUserdata.StepCurrency;
			       userData.LastSyncTimestamp ??= currentUserdata.LastSyncTimestamp;
			       userData.UserLevel ??= currentUserdata.UserLevel;

			       string json = JsonConvert.SerializeObject(userData, JsonSettings);

			       Task updateTask = DatabaseReference.Child("users").Child(userID).SetRawJsonValueAsync(json);

			       await Task.WhenAll(updateTask);

			       if (updateTask.IsFaulted)
			       {
				       $"UpdateUser Write operation failed with {updateTask.Exception}".Log();
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

		#endregion

		#region Race Management

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
            await SyncRaceOpenClosedStatusAsync(raceId);
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
            if (approve) await SyncRaceOpenClosedStatusAsync(raceId);
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
            await SyncRaceOpenClosedStatusAsync(raceId);
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
            await SyncRaceOpenClosedStatusAsync(raceId);
        }

        public async Task<RaceSimulation> StartRaceAsync(string raceId)
        {
            if (Auth?.CurrentUser == null)
                throw new InvalidOperationException("Cannot start a race without an authenticated user.");

            DataSnapshot raceSnapshot = await GetRaceSnapshotAsync(raceId);

            if (raceSnapshot is not { Exists: true })
                throw new InvalidOperationException("Race not found.");

            string hostId = raceSnapshot.Child("hostId").Value?.ToString();
            int? currentUserLevel = await GetUserLevelAsync(Auth.CurrentUser.UserId);

            if (!IsAdminLevel(currentUserLevel) && hostId != Auth.CurrentUser.UserId)
                throw new InvalidOperationException("Only the host or an admin can start this race.");

            long realParticipantCount = raceSnapshot.Child("participants").ChildrenCount;
            if (realParticipantCount < MinRaceParticipants)
                throw new InvalidOperationException($"Løbet kræver mindst {MinRaceParticipants} spillere for at starte.");

            // Fetch each participant's stats to drive the simulation
            var participantInputs = new List<(string userId, string displayName, string sex, int speedPoints, int accelPoints)>();
            foreach (DataSnapshot participant in raceSnapshot.Child("participants").Children)
            {
                string userId      = participant.Key;
                string displayName = participant.Child("displayName").Value?.ToString() ?? string.Empty;
                string sex         = participant.Child("sex").Value?.ToString() ?? string.Empty;

                DataSnapshot userSnap  = await FetchUserDataById(userId);
                int speedPoints = ConvertToNullableInt(userSnap?.Child("SpeedPoints").Value) ?? 0;
                int accelPoints = ConvertToNullableInt(userSnap?.Child("AccelerationPoints").Value) ?? 0;

                participantInputs.Add((userId, displayName, sex, speedPoints, accelPoints));
            }

            // Fill empty slots with AI players using the lowest real-player stats
            int capacity = ConvertToNullableInt(raceSnapshot.Child("capacity").Value) ?? 5;
            int aiCount  = capacity - participantInputs.Count;
            if (aiCount > 0)
            {
                int minSpeed = participantInputs[0].speedPoints;
                int minAccel = participantInputs[0].accelPoints;
                foreach (var p in participantInputs)
                {
                    if (p.speedPoints < minSpeed) minSpeed = p.speedPoints;
                    if (p.accelPoints < minAccel) minAccel = p.accelPoints;
                }

                var rng       = new System.Random();
                long timestamp = GetUnixTimestampMilliseconds();
                var aiUpdates = new Dictionary<string, object>();

                for (int i = 0; i < aiCount; i++)
                {
                    string aiId  = $"ai_{Guid.NewGuid():N}";
                    string aiSex = rng.Next(2) == 0 ? "Male" : "Female";

                    aiUpdates[$"races/{raceId}/participants/{aiId}/displayName"] = AiPlayerName;
                    aiUpdates[$"races/{raceId}/participants/{aiId}/sex"]         = aiSex;
                    aiUpdates[$"races/{raceId}/participants/{aiId}/isHost"]      = false;
                    aiUpdates[$"races/{raceId}/participants/{aiId}/isAI"]        = true;
                    aiUpdates[$"races/{raceId}/participants/{aiId}/joinedAt"]    = timestamp;
                    // Pre-mark as watched so AI never blocks race completion
                    aiUpdates[$"races/{raceId}/participants/{aiId}/watchedAt"]   = timestamp;

                    participantInputs.Add((aiId, AiPlayerName, aiSex, minSpeed, minAccel));
                }

                await DatabaseReference.UpdateChildrenAsync(aiUpdates);
            }

            float baseDuration = UIManager?.RaceBaseDuration ?? 60f;
            RaceSimulation simulation = RaceSimulator.Generate(participantInputs, baseDuration);
            await StoreRaceSimulationAsync(raceId, simulation);

            var updates = new Dictionary<string, object>
            {
                [$"races/{raceId}/status"]    = "in_progress",
                [$"races/{raceId}/startedAt"] = GetUnixTimestampMilliseconds(),
            };

            await DatabaseReference.UpdateChildrenAsync(updates);
            return simulation;
        }

        public async Task<int> FetchRaceCapacityAsync(string raceId)
        {
            DataSnapshot snapshot = await GetRaceSnapshotAsync(raceId);
            return ConvertToNullableInt(snapshot?.Child("capacity").Value) ?? 0;
        }

        public async Task<string> GetRaceStatusAsync(string raceId)
        {
            DataSnapshot snapshot = await GetRaceSnapshotAsync(raceId);
            return snapshot?.Child("status").Value?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Keeps race status in sync with capacity after participants join or leave.
        /// Transitions between "open" ↔ "closed" only; never touches in_progress/completed/cancelled.
        /// </summary>
        private async Task SyncRaceOpenClosedStatusAsync(string raceId)
        {
            DataSnapshot snapshot = await GetRaceSnapshotAsync(raceId);
            if (snapshot is not { Exists: true }) return;

            string status = snapshot.Child("status").Value?.ToString() ?? string.Empty;
            if (!string.Equals(status, "open",   StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(status, "closed", StringComparison.OrdinalIgnoreCase))
                return;

            int  capacity         = ConvertToNullableInt(snapshot.Child("capacity").Value) ?? 0;
            long participantCount = snapshot.Child("participants").ChildrenCount;
            bool atCapacity       = capacity > 0 && participantCount >= capacity;
            string newStatus      = atCapacity ? "closed" : "open";

            if (!string.Equals(status, newStatus, StringComparison.OrdinalIgnoreCase))
                await DatabaseReference.Child("races").Child(raceId).Child("status").SetValueAsync(newStatus);
        }

        private async Task StoreRaceSimulationAsync(string raceId, RaceSimulation simulation)
        {
            var updates = new Dictionary<string, object>
            {
                [$"races/{raceId}/simulation/seed"]         = simulation.Seed,
                [$"races/{raceId}/simulation/baseDuration"] = (double)simulation.BaseDuration,
            };

            foreach (var p in simulation.Participants)
            {
                string prefix = $"races/{raceId}/simulation/participants/{p.UserId}";
                updates[$"{prefix}/displayName"]      = p.DisplayName;
                updates[$"{prefix}/sex"]              = p.Sex;
                updates[$"{prefix}/lane"]             = p.Lane;
                updates[$"{prefix}/finishTime"]       = (double)p.FinishTime;
                updates[$"{prefix}/accelerationBias"] = (double)p.AccelerationBias;
            }

            await DatabaseReference.UpdateChildrenAsync(updates);
        }

        public async Task<RaceSimulation> FetchRaceSimulationAsync(string raceId)
        {
            DataSnapshot snapshot = await DatabaseReference
                .Child("races").Child(raceId).Child("simulation").GetValueAsync();

            if (snapshot is not { Exists: true })
                return null;

            var simulation = new RaceSimulation
            {
                Seed         = ReadLong(snapshot.Child("seed").Value),
                BaseDuration = ReadFloat(snapshot.Child("baseDuration").Value),
                Participants = new List<RaceSimulationParticipant>(),
            };

            foreach (DataSnapshot p in snapshot.Child("participants").Children)
            {
                simulation.Participants.Add(new RaceSimulationParticipant
                {
                    UserId           = p.Key,
                    DisplayName      = p.Child("displayName").Value?.ToString() ?? string.Empty,
                    Sex              = p.Child("sex").Value?.ToString() ?? string.Empty,
                    Lane             = ConvertToNullableInt(p.Child("lane").Value) ?? 0,
                    FinishTime       = ReadFloat(p.Child("finishTime").Value),
                    AccelerationBias = ReadFloat(p.Child("accelerationBias").Value),
                });
            }

            return simulation;
        }

        private EventHandler<ValueChangedEventArgs> _raceStartListener;
        private DatabaseReference _raceStartRef;

        public void ListenForRaceStart(string raceId, Action onStarted)
        {
            StopRaceStartListener();
            _raceStartRef = DatabaseReference.Child("races").Child(raceId).Child("status");
            _raceStartListener = (_, args) =>
            {
                if (args.DatabaseError != null) return;
                string status = args.Snapshot.Value?.ToString();
                if (string.Equals(status, "in_progress", StringComparison.OrdinalIgnoreCase))
                    onStarted?.Invoke();
            };
            _raceStartRef.ValueChanged += _raceStartListener;
        }

        public void StopRaceStartListener()
        {
            if (_raceStartRef == null || _raceStartListener == null) return;
            _raceStartRef.ValueChanged -= _raceStartListener;
            _raceStartListener = null;
            _raceStartRef      = null;
        }

        /// <summary>
        /// Records that the current user has finished watching the race, frees them to join
        /// a new race, and sets the race to "completed" once every participant has watched.
        /// The participants list and all race data are preserved for historical lookup.
        /// </summary>
        public async Task MarkRaceWatchedAsync(string raceId)
        {
            if (Auth?.CurrentUser == null) return;

            string userId    = Auth.CurrentUser.UserId;
            long   timestamp = GetUnixTimestampMilliseconds();

            // Mark this player as having watched, and free them to join new races
            var updates = new Dictionary<string, object>
            {
                [$"races/{raceId}/participants/{userId}/watchedAt"] = timestamp,
                [$"userRaces/{userId}/{raceId}"]                    = null,
            };
            await DatabaseReference.UpdateChildrenAsync(updates);

            // If every participant has now watched, mark the race as completed
            DataSnapshot raceSnapshot = await GetRaceSnapshotAsync(raceId);
            if (raceSnapshot is not { Exists: true }) return;

            bool allWatched = true;
            foreach (DataSnapshot participant in raceSnapshot.Child("participants").Children)
            {
                if (participant.Child("watchedAt").Value == null)
                {
                    allWatched = false;
                    break;
                }
            }

            if (allWatched)
                await DatabaseReference.Child("races").Child(raceId).Child("status").SetValueAsync("completed");
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

		#endregion

		#region Race Queries

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

		#endregion

		#region Race Helpers (Private)

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

		#endregion

		#region Step Counter

		/// <summary>
		/// Loads the saved step count from Firebase once, then starts a background loop.
		/// On Android (HasStepDataProvider), that loop queries Health Connect and syncs to
		/// Firebase every 60 s. Elsewhere, it polls the raw device sensor every 2 s for instant
		/// UI updates and syncs to Firebase every 60 s.
		/// Call StopStepCounter() on OnApplicationPause/OnDestroy.
		/// </summary>
		public async Task<StepCounterAvailability> StartStepCounter()
		{
			if (StepCounterRunning) return StepCounterAvailability.Available;
			if (Auth?.CurrentUser == null) return StepCounterAvailability.Available;

			// Fail fast, before touching Firebase, if this device can't actually count steps —
			// the app is unusable without it.
			StepCounterAvailability availability = _stepDataProvider != null
				? await _stepDataProvider.CheckAvailabilityAsync()
				: await CheckStepCounterAvailabilityAsync();
			if (availability != StepCounterAvailability.Available)
			{
				HandleStepCounterUnavailable(availability);
				return availability;
			}

			// Load from Firebase, then compare with local PlayerPrefs backup.
			// If Firebase is unreachable (offline) it may return 0 — in that case
			// the locally saved value is more accurate.
			DataSnapshot data = await FetchUserData(Auth.CurrentUser);
			_cachedUserName = data?.Child("UserName").Value?.ToString() ?? "";
			_cachedSex      = data?.Child("Sex").Value?.ToString() ?? "";

			long firebaseSteps = ReadLong(data?.Child("StepCount").Value);
			long localSteps    = PlayerPrefs.GetInt(StepCountKey, 0);

			bool useLocal  = localSteps > firebaseSteps;
			_baseStepCount = useLocal ? localSteps : firebaseSteps;
			_currentTotal     = _baseStepCount;
			_lastSyncedTotal  = _baseStepCount;

			// StepCurrency accrues in lockstep with StepCount (see WriteStepsToFirebaseAsync), so
			// whichever source (local vs. Firebase) was ahead for steps is ahead for currency too.
			long firebaseCurrency = ReadLong(data?.Child("StepCurrency").Value);
			long localCurrency    = PlayerPrefs.GetInt(StepCurrencyKey, 0);
			_currentCurrency = useLocal ? localCurrency : firebaseCurrency;

			if (_stepDataProvider != null)
			{
				long firebaseSyncTimestamp = ReadLong(data?.Child("deviceSync").Child(DeviceId).Child("lastSyncTimestamp").Value);
				long localSyncTimestamp    = long.TryParse(PlayerPrefs.GetString(LastSyncTimestampKey, "0"), out long parsedSync) ? parsedSync : 0;
				_lastSyncTimestampMillis    = useLocal ? localSyncTimestamp : firebaseSyncTimestamp;
			}
			else
			{
				long firebaseSnapshot = ReadLong(data?.Child("StepCountSnapshot").Value);
				long localSnapshot    = PlayerPrefs.GetInt(StepSnapshotKey, 0);
				_deviceAnchor          = useLocal ? localSnapshot : firebaseSnapshot;
			}

			LoadDailyStepBaseAndHandleRollover(data, useLocal);

			// Immediately show the saved count while we wait for the first sensor tick
			StepCountChanged?.Invoke(_currentTotal);

			// Write leaderboard entry immediately so user appears before first 60s sync
			_ = WriteLeaderboardEntryAsync(Auth.CurrentUser.UserId);

#if UNITY_EDITOR
			return StepCounterAvailability.Available; // No step sensor in the editor — leaderboard entry already written above
#endif

			_stepCts = new CancellationTokenSource();
			StepCounterRunning = true;
			_ = _stepDataProvider != null ? ProviderSyncLoop(_stepDataProvider, _stepCts.Token) : StepCounterLoop(_stepCts.Token);
			return StepCounterAvailability.Available;
		}

		/// <summary>
		/// Loads DailyStepBase/DailyStepDate (Firebase vs. local PlayerPrefs backup, per
		/// `useLocal`) and rolls the day over if it's changed since the last session — archiving
		/// yesterday's total and reseeding today's baseline from the current running total.
		/// </summary>
		private void LoadDailyStepBaseAndHandleRollover(DataSnapshot data, bool useLocal)
		{
			long firebaseDailyBase   = ReadLong(data?.Child("DailyStepBase").Value);
			string firebaseDailyDate = data?.Child("DailyStepDate").Value?.ToString() ?? "";
			long localDailyBase      = PlayerPrefs.GetInt(DailyStepBaseKey, 0);
			string localDailyDate    = PlayerPrefs.GetString(DailyStepDateKey, "");

			_dailyStepBase = useLocal ? localDailyBase : firebaseDailyBase;
			_dailyStepDate = (useLocal ? localDailyDate : firebaseDailyDate) ?? "";

			// Local day, not UTC — matches GetDailyStepsAsync's local-calendar-day buckets. Using
			// UTC here caused a premature rollover at UTC midnight (1-2am local time for Denmark),
			// silently archiving part of the current local day as "yesterday" and resetting
			// DailyStepBase mid-day.
			string today = DateTime.Now.ToString("yyyy-MM-dd");
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
		}

		/// <summary>
		/// Writes UserName/Sex/StepCount to this user's leaderboard entry, if a display name has
		/// been loaded. Shared by StartStepCounter (fire-and-forget, for an instant first
		/// appearance) and WriteStepsToFirebaseAsync (awaited, as part of the periodic sync).
		/// </summary>
		private Task WriteLeaderboardEntryAsync(string uid)
		{
			if (string.IsNullOrEmpty(_cachedUserName)) return Task.CompletedTask;

			return DatabaseReference
				.Child("leaderboard")
				.Child(uid)
				.UpdateChildrenAsync(new Dictionary<string, object>
				{
					{ "UserName",  _cachedUserName },
					{ "Sex",       _cachedSex },
					{ "StepCount", (int)_currentTotal }
				});
		}

		public Task<StepCounterAvailability> CheckStepProviderAvailabilityAsync()
		{
			return _stepDataProvider?.CheckAvailabilityAsync() ?? Task.FromResult(StepCounterAvailability.Available);
		}

		public Task<StepCounterAvailability> RequestStepProviderPermissionAsync()
		{
			return _stepDataProvider?.RequestPermissionAsync() ?? Task.FromResult(StepCounterAvailability.Available);
		}

		public bool OpenStepProviderSettings()
		{
			return _stepDataProvider?.OpenPlatformSettings() ?? false;
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
		/// Writes only StepCount plus whatever provider-specific anchor field is passed in — no
		/// full object read/write cycle. Skips the write if nothing changed since last sync.
		/// </summary>
		private async Task<bool> WriteStepsToFirebaseAsync(Dictionary<string, object> extraFields)
		{
			if (Auth?.CurrentUser == null) return false;
			if (_currentTotal == _lastSyncedTotal) return false;

			// StepCount only ever increases between syncs (see StepCounterLoop/SyncFromProviderAsync),
			// so this delta is always positive — it's what gets minted into StepCurrency this sync.
			long stepsDelta  = _currentTotal - _lastSyncedTotal;
			long newCurrency = _currentCurrency + stepsDelta;

			// Local day, not UTC — see the matching comment in StartStepCounter().
			string today = DateTime.Now.ToString("yyyy-MM-dd");
			var updates = new Dictionary<string, object>(extraFields)
			{
				{ "StepCount", (int)_currentTotal },
				{ "StepCurrency", (int)newCurrency },
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
				string uid = Auth.CurrentUser.UserId;
				await DatabaseReference
					.Child("users")
					.Child(uid)
					.UpdateChildrenAsync(updates);

				await WriteLeaderboardEntryAsync(uid);
				await WriteWalletEarnTransactionAsync(uid, stepsDelta);

				_lastSyncedTotal = _currentTotal;
				_currentCurrency = newCurrency;
				PlayerPrefs.SetInt(StepCurrencyKey, (int)_currentCurrency);
				PlayerPrefs.Save();
				return true;
			}
			catch (Exception ex)
			{
				$"WriteStepsToFirebaseAsync failed: {ex}".Log();
				return false;
			}
		}

		/// <summary>
		/// Records a step-accrual credit in the wallet ledger (see StepsAsCurrency_Scope.md).
		/// Best-effort: unlike the StepCurrency balance write above, a failure here doesn't get
		/// retried — it just means this one earn isn't itemized in the ledger, which is an
		/// acceptable gap for accrual (unlike spends/refunds, nothing needs to reverse an earn).
		/// </summary>
		private async Task WriteWalletEarnTransactionAsync(string uid, long amount)
		{
			try
			{
				DatabaseReference txRef = DatabaseReference.Child("walletTransactions").Child(uid).Push();
				await txRef.SetValueAsync(new Dictionary<string, object>
				{
					{ "type", "earn" },
					{ "amount", (int)amount },
					{ "status", "settled" },
					{ "createdAt", GetUnixTimestampMilliseconds() },
				});
			}
			catch (Exception ex)
			{
				$"WriteWalletEarnTransactionAsync failed: {ex}".Log();
			}
		}

		private Task SyncStepsToFirebase(long deviceValue)
		{
			// Always persist locally first so data survives even if Firebase is unreachable.
			SaveStepsLocally(_currentTotal, deviceValue);
			return WriteStepsToFirebaseAsync(new Dictionary<string, object> { { "StepCountSnapshot", (int)deviceValue } });
		}

		private async Task ProviderSyncLoop(IStepDataProvider provider, CancellationToken ct)
		{
			try
			{
				while (!ct.IsCancellationRequested)
				{
					await SyncFromProviderAsync(provider);
					await UniTask.Delay(FirebaseSyncMs, cancellationToken: ct);
				}
			}
			catch (OperationCanceledException) { }

			await SyncFromProviderAsync(provider); // Final sync before fully stopping
			StepCounterRunning = false;
		}

		private async Task SyncFromProviderAsync(IStepDataProvider provider)
		{
			// Anchor on the first sync of this session (new account, new device, or first time the
			// provider is available) without backfilling any history — same principle as the
			// legacy device-anchor: prevents counting steps taken before this sync point existed.
			if (_lastSyncTimestampMillis <= 0)
			{
				_lastSyncTimestampMillis = GetUnixTimestampMilliseconds();
			}
			else
			{
				// Cap how far back a single sync can reach so a stale per-device anchor (e.g. an account
				// left logged into someone else's phone for weeks) can't harvest that device's entire
				// pre-existing Health Connect/HealthKit backlog — only up to 30 days of it.
				long earliestAllowed = GetUnixTimestampMilliseconds() - DeviceSyncMaxBacklogMillis;
				long since = Math.Max(_lastSyncTimestampMillis, earliestAllowed);

				long delta = await provider.GetStepsSinceAsync(DateTimeOffset.FromUnixTimeMilliseconds(since));
				if (delta > MaxPlausibleStepsPerSync)
				{
					$"SyncFromProviderAsync: clamping implausible delta {delta} to {MaxPlausibleStepsPerSync}".Log();
					delta = MaxPlausibleStepsPerSync;
				}
				if (delta > 0)
				{
					_currentTotal += delta;
					StepCountChanged?.Invoke(_currentTotal);
				}
				_lastSyncTimestampMillis = GetUnixTimestampMilliseconds();
			}

			SaveProviderSyncLocally(_currentTotal, _lastSyncTimestampMillis);
			await WriteStepsToFirebaseAsync(new Dictionary<string, object>
			{
				{ $"deviceSync/{DeviceId}/lastSyncTimestamp", _lastSyncTimestampMillis }
			});
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

		#endregion

		#region Friend System

		public async Task SendFriendRequestAsync(string targetUserId)
		{
			if (Auth?.CurrentUser == null)
				throw new InvalidOperationException("Must be authenticated to send friend requests.");

			string myUid = Auth.CurrentUser.UserId;
			if (myUid == targetUserId)
				throw new InvalidOperationException("Cannot send a friend request to yourself.");

			long timestamp = GetUnixTimestampMilliseconds();
			var requestData = new Dictionary<string, object>
			{
				{ "requestedAt", timestamp },
				{ "status", "pending" },
			};

			await DatabaseReference.Child("friendRequests").Child(targetUserId).Child(myUid).SetValueAsync(requestData);
		}

		public async Task RevokeFriendRequestAsync(string targetUserId)
		{
			if (Auth?.CurrentUser == null)
				throw new InvalidOperationException("Must be authenticated to revoke friend requests.");

			await DatabaseReference.Child("friendRequests").Child(targetUserId).Child(Auth.CurrentUser.UserId).RemoveValueAsync();
		}

		public async Task<List<(UserData user, string fromUserId)>> FetchIncomingRequestsAsync()
		{
			var result = new List<(UserData, string)>();
			if (Auth?.CurrentUser == null) return result;

			try
			{
				DataSnapshot snapshot = await DatabaseReference.Child("friendRequests").Child(Auth.CurrentUser.UserId).GetValueAsync();
				if (!snapshot.Exists) return result;

				foreach (DataSnapshot child in snapshot.Children)
				{
					string status = child.Child("status").Value?.ToString();
					if (!string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
						continue;

					DataSnapshot userSnapshot = await FetchUserDataById(child.Key);
					if (userSnapshot is not { Exists: true }) continue;

					UserData? userData = JsonConvert.DeserializeObject<UserData>(userSnapshot.GetRawJsonValue(), JsonSettings);
					if (userData.HasValue)
						result.Add((userData.Value, child.Key));
				}
			}
			catch (Exception ex)
			{
				$"FetchIncomingRequestsAsync failed: {ex}".Log();
			}

			return result;
		}

		public async Task HandleFriendRequestAsync(string requesterUserId, bool accept)
		{
			if (Auth?.CurrentUser == null)
				throw new InvalidOperationException("Must be authenticated to handle friend requests.");

			string myUid = Auth.CurrentUser.UserId;
			long timestamp = GetUnixTimestampMilliseconds();

			var updates = new Dictionary<string, object>
			{
				[$"friendRequests/{myUid}/{requesterUserId}"] = null,
			};

			if (accept)
			{
				var friendEntry = new Dictionary<string, object> { { "addedAt", timestamp } };
				updates[$"friends/{myUid}/{requesterUserId}"] = friendEntry;
				updates[$"friends/{requesterUserId}/{myUid}"] = friendEntry;
			}

			await DatabaseReference.UpdateChildrenAsync(updates);
		}

		public async Task<List<UserData>> FetchFriendsAsync()
		{
			var result = new List<UserData>();
			if (Auth?.CurrentUser == null) return result;

			try
			{
				DataSnapshot snapshot = await DatabaseReference.Child("friends").Child(Auth.CurrentUser.UserId).GetValueAsync();
				if (!snapshot.Exists) return result;

				foreach (DataSnapshot child in snapshot.Children)
				{
					DataSnapshot userSnapshot = await FetchUserDataById(child.Key);
					if (userSnapshot is not { Exists: true }) continue;

					UserData? userData = JsonConvert.DeserializeObject<UserData>(userSnapshot.GetRawJsonValue(), JsonSettings);
					if (userData.HasValue)
						result.Add(userData.Value);
				}
			}
			catch (Exception ex)
			{
				$"FetchFriendsAsync failed: {ex}".Log();
			}

			return result;
		}

		public async Task RemoveFriendAsync(string friendUserId)
		{
			if (Auth?.CurrentUser == null)
				throw new InvalidOperationException("Must be authenticated to remove friends.");

			string myUid = Auth.CurrentUser.UserId;
			var updates = new Dictionary<string, object>
			{
				[$"friends/{myUid}/{friendUserId}"] = null,
				[$"friends/{friendUserId}/{myUid}"] = null,
			};

			await DatabaseReference.UpdateChildrenAsync(updates);
		}

		#endregion

		#region Leaderboard & Daily Steps

		public async Task<List<LeaderboardEntry>> FetchLeaderboardAsync()
		{
			var result = new List<LeaderboardEntry>();
			try
			{
				DataSnapshot snapshot = await DatabaseReference.Child("leaderboard").GetValueAsync();
				if (!snapshot.Exists) return result;

				foreach (DataSnapshot child in snapshot.Children)
				{
					LeaderboardEntry? entry = JsonConvert.DeserializeObject<LeaderboardEntry>(child.GetRawJsonValue(), JsonSettings);
					if (entry.HasValue)
						result.Add(entry.Value);
				}
			}
			catch (Exception ex)
			{
				$"FetchLeaderboardAsync failed: {ex}".Log();
			}
			return result;
		}

		/// <summary>
		/// Never includes today — "today" is always computed live by the caller from the running
		/// total, the same way it already was before this method had a provider-backed path.
		/// </summary>
		public async Task<List<(string dateKey, long steps)>> FetchDailyStepsAsync(int days = 5)
		{
			if (Auth?.CurrentUser == null) return new List<(string, long)>();

			if (_stepDataProvider != null)
			{
				IReadOnlyList<(string dateKey, long steps)> providerDays = await _stepDataProvider.GetDailyStepsAsync(days);
				string todayLocal = DateTime.Now.ToString("yyyy-MM-dd");
				var result = new List<(string, long)>();
				foreach (var day in providerDays)
				{
					if (day.dateKey != todayLocal)
						result.Add(day);
				}
				return result;
			}

			// Legacy/Editor fallback — hand-maintained Firebase buckets, now local-day keyed to
			// match WriteStepsToFirebaseAsync/StartStepCounter's day-rollover (see their comments).
			try
			{
				DataSnapshot snapshot = await DatabaseReference
					.Child("users")
					.Child(Auth.CurrentUser.UserId)
					.Child("dailySteps")
					.OrderByKey()
					.LimitToLast(days)
					.GetValueAsync();

				string todayLocal = DateTime.Now.ToString("yyyy-MM-dd");
				var result = new List<(string, long)>();
				foreach (DataSnapshot child in snapshot.Children)
				{
					if (child.Key != todayLocal)
						result.Add((child.Key, ReadLong(child.Value)));
				}
				return result; // Firebase returns children in ascending key order
			}
			catch (Exception ex)
			{
				$"FetchDailyStepsAsync failed: {ex}".Log();
				return new List<(string, long)>();
			}
		}

		#endregion

		#region Private Helpers

		private void SaveStepsLocally(long steps, long deviceSnapshot)
		{
			PlayerPrefs.SetInt(StepCountKey, (int)steps);
			PlayerPrefs.SetInt(StepSnapshotKey, (int)deviceSnapshot);
			PlayerPrefs.Save();
		}

		// Long.MaxValue-range unix ms timestamp doesn't fit PlayerPrefs' int storage, hence string.
		private void SaveProviderSyncLocally(long steps, long syncTimestampMillis)
		{
			PlayerPrefs.SetInt(StepCountKey, (int)steps);
			PlayerPrefs.SetString(LastSyncTimestampKey, syncTimestampMillis.ToString());
			PlayerPrefs.Save();
		}

		// Scoped per user so multiple accounts on the same device don't share data.
		private string StepCountKey    => $"StepCount_{Auth?.CurrentUser?.UserId ?? "anon"}";
		private string StepSnapshotKey => $"StepCountSnapshot_{Auth?.CurrentUser?.UserId ?? "anon"}";
		private string StepCurrencyKey => $"StepCurrency_{Auth?.CurrentUser?.UserId ?? "anon"}";
		private string LastSyncTimestampKey => $"LastSyncTimestamp_{Auth?.CurrentUser?.UserId ?? "anon"}";
		private string DailyStepBaseKey => $"DailyStepBase_{Auth?.CurrentUser?.UserId ?? "anon"}";
		private string DailyStepDateKey => $"DailyStepDate_{Auth?.CurrentUser?.UserId ?? "anon"}";

		private void SaveDailyBaseLocally()
		{
			PlayerPrefs.SetInt(DailyStepBaseKey, (int)_dailyStepBase);
			PlayerPrefs.SetString(DailyStepDateKey, _dailyStepDate);
			PlayerPrefs.Save();
		}

		// StepCounter.current is only ever non-null when the platform has natively enumerated a
		// real hardware sensor (e.g. UnityEngine.InputSystem.Android.AndroidStepCounter, backed by
		// Android's TYPE_STEP_COUNTER). Do NOT call InputSystem.AddDevice<StepCounter>() here to
		// "fix" a null device — that fabricates a disconnected managed-only device with no native
		// sensor behind it, which reports enabled but never receives real step events. That fake
		// device previously masked devices that genuinely have no step sensor (or hadn't finished
		// registering it yet), causing steps to silently never count on those phones.
		private bool EnsureStepCounterDevice()
		{
			if (StepCounter.current == null) return false;

			if (!StepCounter.current.enabled)
				InputSystem.EnableDevice(StepCounter.current);

			return StepCounter.current.enabled;
		}

		/// <summary>
		/// Checks whether this device can actually count steps: permission granted AND a real
		/// native step-counter sensor present. Retries briefly since some OEM sensor hubs register
		/// the sensor a little after app start rather than immediately.
		/// </summary>
		private async Task<StepCounterAvailability> CheckStepCounterAvailabilityAsync()
		{
#if UNITY_EDITOR
			return StepCounterAvailability.Available;
#else
			if (!Permission.HasUserAuthorizedPermission("android.permission.ACTIVITY_RECOGNITION"))
				return StepCounterAvailability.PermissionDenied;

			for (int i = 0; i < SensorDetectRetries; i++)
			{
				if (EnsureStepCounterDevice()) return StepCounterAvailability.Available;
				await UniTask.Delay(SensorDetectRetryDelayMs);
			}

			return StepCounterAvailability.SensorUnsupported;
#endif
		}

		/// <summary>
		/// The app is unusable without step tracking, so a missing sensor or denied permission
		/// signs the user back out and blocks entry with an explanation instead of letting them
		/// into a screen that will just never show any progress.
		/// </summary>
		private void HandleStepCounterUnavailable(StepCounterAvailability availability)
		{
			Auth?.SignOut();

			string message = availability == StepCounterAvailability.PermissionDenied
				? "TrainingBuddy needs the Activity Recognition permission to count your steps. Please grant it and log in again."
				: "TrainingBuddy couldn't find a step counter sensor on this device. The app requires one to work and can't be used without it.";

			UIManager?.ShowOverlay("Step Counter Required", message, "OK", () => UIManager?.ReturnToWelcomeScreen());
		}

		#endregion

		#region Email Verification

		/// <summary>
		/// Checks whether the signed-in user has verified their email, reloading first since
		/// IsEmailVerified is only a locally-cached flag — clicking the emailed link updates
		/// Firebase server-side, and only shows up here after an explicit reload. Shows a
		/// blocking overlay with a manual resend option if verification is still pending.
		/// </summary>
		public async Task<bool> EnsureEmailVerifiedAsync()
		{
			if (Auth?.CurrentUser == null) return false;

			await Auth.CurrentUser.ReloadAsync();
			if (Auth.CurrentUser.IsEmailVerified) return true;

			UIManager?.ShowOverlay(
				"Verificer Din Email",
				$"Vi har sendt et verificeringslink til {Auth.CurrentUser.Email}. Venligst verificer din email og log ind igen.",
				"Gensend Email",
				() => _ = ResendVerificationEmailAsync(),
				"OK",
				() => { });

			return false;
		}

		public async Task ResendVerificationEmailAsync()
		{
			if (Auth?.CurrentUser == null) return;
			try
			{
				await Auth.CurrentUser.SendEmailVerificationAsync();
			}
			catch (Exception ex)
			{
				$"Failed to resend verification email: {ex}".Log();
			}
		}

		/// <summary>
		/// Shows a simple one-button informational overlay, if the UI layer is available.
		/// </summary>
		public void ShowMessage(string title, string message, string buttonText = "OK")
		{
			UIManager?.ShowOverlay(title, message, buttonText, () => { });
		}

		#endregion

		#region Account Deletion

		/// <summary>
		/// Permanently deletes the signed-in user: their profile (including nested daily-step
		/// history), leaderboard entry, both sides of every friendship, incoming friend
		/// requests, their reserved username and friend-code slots, and their race
		/// memberships — cancelling any race they're actively hosting so it doesn't dangle
		/// with a deleted host, and dropping plain participation elsewhere. Historical
		/// (completed/cancelled) races they hosted are left alone as records.
		///
		/// Requires the current password: Firebase Auth refuses to delete an account unless
		/// the session was "recently" authenticated, and re-entering a password is also a
		/// reasonable confirmation step for a destructive action regardless.
		/// </summary>
		public async Task<bool> DeleteAccountAsync(string password)
		{
			if (Auth?.CurrentUser == null) return false;

			FirebaseUser user = Auth.CurrentUser;
			string uid = user.UserId;

			try
			{
				Credential credential = EmailAuthProvider.GetCredential(user.Email, password);
				await user.ReauthenticateAsync(credential);
			}
			catch (Exception ex)
			{
				$"DeleteAccountAsync: reauthentication failed: {ex}".LogError();
				ShowMessage("Delete Account", "Incorrect password. Your account was not deleted.");
				return false;
			}

			DataSnapshot userSnapshot = await FetchUserData(user);
			string friendCode = userSnapshot?.Child("FriendCode").Value?.ToString();
			string userName   = userSnapshot?.Child("UserName").Value?.ToString();

			var updates = new Dictionary<string, object>();

			DataSnapshot userRaces = await GetUserRacesSnapshotAsync(uid);
			if (userRaces is { Exists: true })
			{
				foreach (DataSnapshot raceEntry in userRaces.Children)
				{
					string raceId = raceEntry.Key;
					string role = raceEntry.Child("role").Value?.ToString();
					DataSnapshot race = await GetRaceSnapshotAsync(raceId);

					if (race is { Exists: true })
					{
						string status = race.Child("status").Value?.ToString();
						if (string.Equals(role, "host", StringComparison.OrdinalIgnoreCase))
						{
							if (IsActiveRaceStatus(status))
							{
								updates[$"races/{raceId}/status"] = "cancelled";
								updates[$"joinRequests/{raceId}"] = null;
							}
						}
						else
						{
							updates[$"races/{raceId}/participants/{uid}"] = null;
						}
					}

					updates[$"joinRequests/{raceId}/{uid}"] = null;
					updates[$"userRaces/{uid}/{raceId}"] = null;
				}
			}

			DataSnapshot friends = await DatabaseReference.Child("friends").Child(uid).GetValueAsync();
			if (friends.Exists)
			{
				foreach (DataSnapshot friend in friends.Children)
					updates[$"friends/{friend.Key}/{uid}"] = null;
			}
			updates[$"friends/{uid}"] = null;

			// Incoming requests only — outgoing requests sent to others aren't indexed anywhere
			// to find them (no reverse lookup by sender). Left as harmless orphans; existing
			// reads already skip any request whose sender no longer resolves to a user.
			updates[$"friendRequests/{uid}"] = null;

			updates[$"leaderboard/{uid}"] = null;
			updates[$"walletTransactions/{uid}"] = null;
			if (!string.IsNullOrEmpty(friendCode)) updates[$"friendCodes/{friendCode}"] = null;
			if (!string.IsNullOrEmpty(userName))   updates[$"usernames/{userName}"] = null;
			updates[$"users/{uid}"] = null; // includes nested dailySteps

			try
			{
				await DatabaseReference.UpdateChildrenAsync(updates);
			}
			catch (Exception ex)
			{
				$"DeleteAccountAsync: database cleanup failed: {ex}".LogError();
				ShowMessage("Delete Account", "Something went wrong deleting your data. Please try again.");
				return false;
			}

			PlayerPrefs.DeleteKey(StepCountKey);
			PlayerPrefs.DeleteKey(StepSnapshotKey);
			PlayerPrefs.DeleteKey(StepCurrencyKey);
			PlayerPrefs.DeleteKey(LastSyncTimestampKey);
			PlayerPrefs.DeleteKey(DailyStepBaseKey);
			PlayerPrefs.DeleteKey(DailyStepDateKey);
			PlayerPrefs.Save();

			// Must be last: DB rules and everything above are authenticated as this user, and
			// deleting the auth record ends that session.
			try
			{
				await user.DeleteAsync();
			}
			catch (Exception ex)
			{
				$"DeleteAccountAsync: failed to delete auth user: {ex}".LogError();
				ShowMessage("Delete Account", "Your data was removed, but the sign-in record could not be deleted. Please contact support.");
				return false;
			}

			return true;
		}

		#endregion

		#region Utility

		private static long ReadLong(object value) => value switch
		{
			long   l => l,
			int    i => i,
			double d => (long)d,
			null     => 0,
			_        => Convert.ToInt64(value)
		};

		private static float ReadFloat(object value) => value switch
		{
			float  f => f,
			double d => (float)d,
			long   l => (float)l,
			int    i => (float)i,
			null     => 0f,
			_        => (float)Convert.ToDouble(value)
		};

		#endregion
	}
}