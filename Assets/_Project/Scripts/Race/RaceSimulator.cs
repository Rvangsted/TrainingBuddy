using System;
using System.Collections.Generic;
using TrainingBuddy.FireBase;
using UnityEngine;

namespace TrainingBuddy
{
	/// <summary>
	/// Generates a deterministic race simulation from participant stats.
	/// Base race duration is 60 s; each player finishes within ±5 s of that,
	/// biased by their SpeedPoints (faster finish) and AccelerationPoints
	/// (faster early-race progress curve).
	/// </summary>
	public static class RaceSimulator
	{
		private const float MaxTimeDelta   = 5f;
		private const float MaxStatPoints  = 50f;  // normalisation cap
		private const float SpeedAdvantage = 3f;   // max seconds a top-speed runner gains

		public static RaceSimulation Generate(
			IList<(string userId, string displayName, string sex, int speedPoints, int accelPoints)> participants,
			float baseDuration = 60f)
		{
			long seed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			return GenerateWithSeed(participants, seed, baseDuration);
		}

		/// <summary>Deterministic overload — useful for replay / unit tests.</summary>
		public static RaceSimulation GenerateWithSeed(
			IList<(string userId, string displayName, string sex, int speedPoints, int accelPoints)> participants,
			long seed,
			float baseDuration = 60f)
		{
			var rng   = new System.Random((int)(seed & 0x7FFFFFFF));
			int count = Math.Min(participants.Count, 5);

			// Shuffle lane assignments [0..count-1]
			var lanes = new int[count];
			for (int i = 0; i < count; i++) lanes[i] = i;
			for (int i = count - 1; i > 0; i--)
			{
				int j = rng.Next(i + 1);
				(lanes[i], lanes[j]) = (lanes[j], lanes[i]);
			}

			var simParticipants = new List<RaceSimulationParticipant>(count);
			for (int i = 0; i < count; i++)
			{
				var (userId, displayName, sex, speedPoints, accelPoints) = participants[i];

				// Speed stat shifts the finish-time window earlier (faster = lower finish time)
				float speedNorm  = Mathf.Clamp01(speedPoints / MaxStatPoints);
				float speedAdj   = -speedNorm * SpeedAdvantage;
				float min        = -MaxTimeDelta + speedAdj;
				float max        =  MaxTimeDelta + speedAdj;
				float delta      = (float)(rng.NextDouble() * (max - min) + min);
				float finishTime = Mathf.Clamp(baseDuration + delta,
				                               baseDuration - MaxTimeDelta,
				                               baseDuration + MaxTimeDelta);

				// Acceleration stat controls the progress curve shape (1 = fast start)
				float accelBias = Mathf.Clamp01(accelPoints / MaxStatPoints);

				simParticipants.Add(new RaceSimulationParticipant
				{
					UserId           = userId,
					DisplayName      = displayName,
					Sex              = sex,
					Lane             = lanes[i],
					FinishTime       = finishTime,
					AccelerationBias = accelBias,
				});
			}

			return new RaceSimulation
			{
				Seed         = seed,
				BaseDuration = baseDuration,
				Participants = simParticipants,
			};
		}
	}
}