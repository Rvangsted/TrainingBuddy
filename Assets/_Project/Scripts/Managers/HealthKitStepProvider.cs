using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BedtimeCore;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using AOT;
using Newtonsoft.Json;
#endif

namespace TrainingBuddy.Managers
{
	/// <summary>
	/// iOS IStepDataProvider backed by HealthKit, via the native module in
	/// Assets/Plugins/iOS/HealthKitBridge.mm. See StepCounter_HealthPlatform_Migration_Scope.md.
	/// </summary>
	public class HealthKitStepProvider : IStepDataProvider
	{
#if UNITY_IOS && !UNITY_EDITOR
		private delegate void IntCallback(int requestId, int value);
		private delegate void StepsCallback(int requestId, long steps, int success);
		private delegate void JsonCallback(int requestId, string json, int success);

		[DllImport("__Internal")] private static extern int _HealthKit_IsAvailable();
		[DllImport("__Internal")] private static extern void _HealthKit_RequestAuthorization(int requestId, IntCallback callback);
		[DllImport("__Internal")] private static extern void _HealthKit_QueryStepsSince(int requestId, long sinceUnixMillis, StepsCallback callback);
		[DllImport("__Internal")] private static extern void _HealthKit_QueryDailySteps(int requestId, long startUnixMillis, long endUnixMillis, JsonCallback callback);
		[DllImport("__Internal")] private static extern int _HealthKit_OpenSettings();

		private static int _nextRequestId;
		private static readonly Dictionary<int, TaskCompletionSource<StepCounterAvailability>> AuthRequests = new();
		private static readonly Dictionary<int, TaskCompletionSource<long>> StepsRequests = new();
		private static readonly Dictionary<int, TaskCompletionSource<IReadOnlyList<(string dateKey, long steps)>>> DailyStepsRequests = new();

		private class DailyStepBucketJson
		{
			public string date;
			public long steps;
		}

		// Must be static — native code holds this as a raw function pointer, not a managed
		// delegate/closure, so IL2CPP/AOT can only marshal a static method group here (the same
		// constraint Unity's own iOSStepCounter.cs works around with its OnDataReceived pattern).
		// The requestId round-trips through native code as the correlation key back to the right
		// pending TaskCompletionSource, since there's no per-call object to close over.
		[MonoPInvokeCallback(typeof(IntCallback))]
		private static void OnAuthResult(int requestId, int success)
		{
			$"HealthKit authorization request completed: success={success}".Log();
			if (AuthRequests.TryGetValue(requestId, out var tcs))
			{
				AuthRequests.Remove(requestId);
				// HealthKit's completion flag only reflects whether the authorization sheet was
				// presented and dismissed without a system error — never whether read access was
				// actually granted (Apple doesn't expose that for read-only types). A query
				// returning real data is the only trustworthy access signal; see GetStepsSinceAsync.
				tcs.TrySetResult(success != 0 ? StepCounterAvailability.Available : StepCounterAvailability.PermissionDenied);
			}
		}

		[MonoPInvokeCallback(typeof(StepsCallback))]
		private static void OnStepsResult(int requestId, long steps, int success)
		{
			if (StepsRequests.TryGetValue(requestId, out var tcs))
			{
				StepsRequests.Remove(requestId);
				tcs.TrySetResult(success != 0 ? steps : 0);
			}
		}

		[MonoPInvokeCallback(typeof(JsonCallback))]
		private static void OnDailyStepsResult(int requestId, string json, int success)
		{
			if (!DailyStepsRequests.TryGetValue(requestId, out var tcs)) return;
			DailyStepsRequests.Remove(requestId);

			var result = new List<(string, long)>();
			if (success != 0 && !string.IsNullOrEmpty(json))
			{
				try
				{
					var buckets = JsonConvert.DeserializeObject<List<DailyStepBucketJson>>(json);
					if (buckets != null)
					{
						foreach (var bucket in buckets)
							result.Add((bucket.date, bucket.steps));
					}
				}
				catch (Exception ex)
				{
					$"HealthKit daily steps: failed to parse native JSON response: {ex}".LogError();
				}
			}
			tcs.TrySetResult(result);
		}
#endif

		public Task<StepCounterAvailability> CheckAvailabilityAsync()
		{
#if UNITY_IOS && !UNITY_EDITOR
			// HealthKit deliberately doesn't expose reliable read-permission status (see the
			// migration scope doc's HealthKit gotcha) — "available" here just means the device
			// supports HealthKit at all. Whether steps actually come back is GetStepsSinceAsync's
			// problem, not this one's.
			bool available = _HealthKit_IsAvailable() != 0;
			return Task.FromResult(available ? StepCounterAvailability.Available : StepCounterAvailability.SensorUnsupported);
#else
			return Task.FromResult(StepCounterAvailability.Available);
#endif
		}

		public Task<StepCounterAvailability> RequestPermissionAsync()
		{
#if UNITY_IOS && !UNITY_EDITOR
			var tcs = new TaskCompletionSource<StepCounterAvailability>();
			int requestId = Interlocked.Increment(ref _nextRequestId);
			AuthRequests[requestId] = tcs;
			_HealthKit_RequestAuthorization(requestId, OnAuthResult);
			return tcs.Task;
#else
			return Task.FromResult(StepCounterAvailability.Available);
#endif
		}

		public Task<long> GetStepsSinceAsync(DateTimeOffset since)
		{
#if UNITY_IOS && !UNITY_EDITOR
			var tcs = new TaskCompletionSource<long>();
			int requestId = Interlocked.Increment(ref _nextRequestId);
			StepsRequests[requestId] = tcs;
			_HealthKit_QueryStepsSince(requestId, since.ToUnixTimeMilliseconds(), OnStepsResult);
			return tcs.Task;
#else
			return Task.FromResult(0L);
#endif
		}

		public bool OpenPlatformSettings()
		{
#if UNITY_IOS && !UNITY_EDITOR
			return _HealthKit_OpenSettings() != 0;
#else
			return false;
#endif
		}

		public Task<IReadOnlyList<(string dateKey, long steps)>> GetDailyStepsAsync(int days)
		{
#if UNITY_IOS && !UNITY_EDITOR
			var tcs = new TaskCompletionSource<IReadOnlyList<(string dateKey, long steps)>>();
			int requestId = Interlocked.Increment(ref _nextRequestId);
			DailyStepsRequests[requestId] = tcs;

			// Range ends at local midnight *today*, not "now" — matches HealthConnectBridge.kt's
			// getDailyStepsSince exactly, so this never hands back a partial today bucket at all
			// (DatabaseManager.FetchDailyStepsAsync's today-exclusion filter is then just a
			// defensive no-op here, same as it is on Android).
			DateTimeOffset localNow = DateTimeOffset.Now;
			DateTimeOffset endOfRange = new DateTimeOffset(localNow.Date, localNow.Offset);
			DateTimeOffset startOfRange = endOfRange.AddDays(-days);
			_HealthKit_QueryDailySteps(requestId, startOfRange.ToUnixTimeMilliseconds(), endOfRange.ToUnixTimeMilliseconds(), OnDailyStepsResult);
			return tcs.Task;
#else
			return Task.FromResult<IReadOnlyList<(string, long)>>(Array.Empty<(string, long)>());
#endif
		}
	}
}
