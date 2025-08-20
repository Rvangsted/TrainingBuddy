namespace TrainingBuddy.FireBase
{
	public struct UserData
	{
		public string UserName;
		public string UserID;
		public string Email;
		public float? Longitude;
		public float? Latitude;
		public int? Level;
		public int? ExperiencePoints;
		public int? SkillPoints;
		public int? AccelerationPoints;
		public int? SpeedPoints;
		public int? StepCount;
		public int? StepCountSnapshot;
	}
	
	public struct RaceData
	{
		public string RaceName;
		public string HostName;
		public string HostID;
		public float Longitude;
		public float Latitude;
		public int Status;
		public string Timestamp;
	}
}