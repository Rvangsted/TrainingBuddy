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

		/// <summary>
		/// Shows the platform's consent UI (Health Connect's permission screen, HealthKit's
		/// authorization sheet). Callers trigger this explicitly rather than have it fire as a
		/// side effect of CheckAvailabilityAsync.
		/// </summary>
		Task<StepCounterAvailability> RequestPermissionAsync();
		Task<long> GetStepsSinceAsync(DateTimeOffset since);
	}
}