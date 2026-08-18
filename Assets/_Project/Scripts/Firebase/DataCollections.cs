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
		public int? StepCurrency;
		public int? DailyStepBase;
		public string DailyStepDate;
		public long? LastSyncTimestamp;
		public int? UserLevel;
		public int? PlacementPoints;
	}

	/// <summary>
	/// Ledger entry under walletTransactions/{uid}/{txId} — see StepsAsCurrency_Scope.md.
	/// Written directly by DatabaseManager as a plain dictionary, not via this struct
	/// (same convention as FriendRequest/FriendEntry); kept here to document the shape.
	/// </summary>
	public struct WalletTransaction
	{
		public string Type;         // "earn" | "spend" | "refund"
		public int Amount;
		public string RelatedRaceId; // set for spend/refund entries once #3 (paid runs) exists; absent for earns
		public string Status;       // "settled" | "refunded"
		public long CreatedAt;
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
		public int PlacementPoints;
	}

	/// <summary>
	/// Placement Points — see PlacementPoints_Scope.md. Single shared rank→points lookup used by
	/// both RaceScreen's client-side post-race display and DatabaseManager's authoritative award,
	/// so the two can never drift out of sync. Rebalance by editing this table only.
	/// </summary>
	public static class PlacementPointsTable
	{
		private static readonly Dictionary<int, int> PointsByRank = new()
		{
			{ 1, 50 },
			{ 2, 30 },
			{ 3, 20 },
			{ 4, 10 },
			{ 5, 5 },
		};

		// Ranks past the table above — future-proofing only, race capacity is a fixed 5 today.
		private const int DefaultPoints = 5;

		public static int GetPoints(int rank) => PointsByRank.TryGetValue(rank, out int points) ? points : DefaultPoints;
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