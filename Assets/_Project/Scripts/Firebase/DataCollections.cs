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
		public int? UserLevel;
	}
	
	public struct RaceData
	{
		public string RaceName;
		public string HostName;
		public float Longitude;
		public float Latitude;
		public int Status;
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