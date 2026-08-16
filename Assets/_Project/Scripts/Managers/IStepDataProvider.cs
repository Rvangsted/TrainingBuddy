using System;
using System.Threading.Tasks;

namespace TrainingBuddy.Managers
{
	/// <summary>
	/// Platform-agnostic source of step data, backed by an OS-level aggregator
	/// (Health Connect on Android, HealthKit on iOS) rather than a live sensor —
	/// see StepCounter_HealthPlatform_Migration_Scope.md.
	/// </summary>
	public interface IStepDataProvider
	{
		Task<StepCounterAvailability> CheckAvailabilityAsync();
		Task<long> GetStepsSinceAsync(DateTimeOffset since);
	}
}