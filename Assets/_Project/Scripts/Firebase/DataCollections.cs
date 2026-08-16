using System.Collections.Generic;

namespace TrainingBuddy.FireBase
{
	public struct UserData
	{
		public string UserName;
		public string Sex;
		public string UserID;
		public string FriendCode;
		public string Email;
		public int? DateOfBirthDay;
		public int? DateOfBirthMonth;
		public int? DateOfBirthYear;
		public int? AccelerationPoints;
		public int? SpeedPoints;
		public int? StepCount;
		public int? StepCountSnapshot;
		public int? DailyStepBase;
		public string DailyStepDate;
		public long? LastSyncTimestamp;
		public int? UserLevel;
	}
	
	public struct FriendEntry
	{
		public long AddedAt;
	}

	public struct FriendRequest
	{
		public long RequestedAt;
		public string Status;
	}

	public struct RaceData
	{
		public string RaceName;
		public string HostName;
		public float Longitude;
		public float Latitude;
		public int Status;
	}

	public struct LeaderboardEntry
	{
		public string UserName;
		public string Sex;
		public int StepCount;
	}

	public struct RaceSimulationParticipant
	{
		public string UserId;
		public string DisplayName;
		public string Sex;
		public int    Lane;
		public float  FinishTime;
		public float  AccelerationBias;
	}

	public class RaceSimulation
	{
		public long   Seed;
		public float  BaseDuration;
		public List<RaceSimulationParticipant> Participants;
	}

	public struct RaceListEntry
	{
		public string RaceId;
		public string Title;
		public string HostName;
		public string HostSex;
		public string Status;
		public long CreatedAt;
		public int ParticipantCount;
		public int Capacity;
	}
}