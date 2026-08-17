using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BedtimeCore;
#if UNITY_ANDROID
using UnityEngine;
#endif

namespace TrainingBuddy.Managers
{
	/// <summary>
	/// Android IStepDataProvider backed by Health Connect, via the native module in
	/// Assets/Plugins/Android/HealthConnectBridge.androidlib. See
	/// StepCounter_HealthPlatform_Migration_Scope.md.
	/// </summary>
	public class HealthConnectStepProvider : IStepDataProvider
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		private const string BridgeClassName = "dk.trainingbuddy.game.healthconnect.HealthConnectBridge";

		private static AndroidJavaObject CurrentActivity
		{
			get
			{
				using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
				return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
			}
		}

		// Mirrors dk.trainingbuddy.game.healthconnect.AvailabilityReceiver.
		private class AvailabilityCallback : AndroidJavaProxy
		{
			private readonly TaskCompletionSource<StepCounterAvailability> _tcs;

			public AvailabilityCallback(TaskCompletionSource<StepCounterAvailability> tcs)
				: base("dk.trainingbuddy.game.healthconnect.AvailabilityReceiver")
			{
				_tcs = tcs;
			}

			// Must be named exactly onResult (lowercase) to match AvailabilityReceiver.onResult in
			// HealthConnectBridge.kt — AndroidJavaProxy dispatches by exact case-sensitive name match.
			[UnityEngine.Scripting.Preserve]
			public void onResult(string status)
			{
				$"HealthConnect availability result: {status}".Log();
				_tcs.TrySetResult(status switch
				{
					"available" => StepCounterAvailability.Available,
					"permissionDenied" => StepCounterAvailability.PermissionDenied,
					"notInstalled" => StepCounterAvailability.ProviderNotInstalled,
					_ => StepCounterAvailability.SensorUnsupported
				});
			}
		}

		// Mirrors dk.trainingbuddy.game.healthconnect.StepsReceiver.
		private class StepsCallback : AndroidJavaProxy
		{
			private readonly TaskCompletionSource<long> _tcs;

			public StepsCallback(TaskCompletionSource<long> tcs)
				: base("dk.trainingbuddy.game.healthconnect.StepsReceiver")
			{
				_tcs = tcs;
			}

			// Must be named exactly onResult (lowercase) to match StepsReceiver.onResult in
			// HealthConnectBridge.kt — AndroidJavaProxy dispatches by exact case-sensitive name match.
			[UnityEngine.Scripting.Preserve]
			public void onResult(long steps, bool success)
			{
				_tcs.TrySetResult(success ? steps : 0);
			}
		}

		// Mirrors dk.trainingbuddy.game.healthconnect.DailyStepsReceiver.
		private class DailyStepsCallback : AndroidJavaProxy
		{
			private readonly TaskCompletionSource<IReadOnlyList<(string dateKey, long steps)>> _tcs;

			public DailyStepsCallback(TaskCompletionSource<IReadOnlyList<(string dateKey, long steps)>> tcs)
				: base("dk.trainingbuddy.game.healthconnect.DailyStepsReceiver")
			{
				_tcs = tcs;
			}

			// Must be named exactly onResult (lowercase) to match DailyStepsReceiver.onResult in
			// HealthConnectBridge.kt — AndroidJavaProxy dispatches by exact case-sensitive name match.
			[UnityEngine.Scripting.Preserve]
			public void onResult(string[] dateKeys, long[] steps, bool success)
			{
				var result = new List<(string, long)>();
				if (success)
				{
					for (int i = 0; i < dateKeys.Length; i++)
						result.Add((dateKeys[i], steps[i]));
				}
				_tcs.TrySetResult(result);
			}
		}
#endif

		public Task<StepCounterAvailability> CheckAvailabilityAsync()
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			var tcs = new TaskCompletionSource<StepCounterAvailability>();
			using var bridge = new AndroidJavaClass(BridgeClassName);
			bridge.CallStatic("checkAvailability", CurrentActivity, new AvailabilityCallback(tcs));
			return tcs.Task;
#else
			return Task.FromResult(StepCounterAvailability.Available);
#endif
		}

		public Task<StepCounterAvailability> RequestPermissionAsync()
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			var tcs = new TaskCompletionSource<StepCounterAvailability>();
			using var bridge = new AndroidJavaClass(BridgeClassName);
			bridge.CallStatic("requestPermission", CurrentActivity, new AvailabilityCallback(tcs));
			return tcs.Task;
#else
			return Task.FromResult(StepCounterAvailability.Available);
#endif
		}

		public Task<long> GetStepsSinceAsync(DateTimeOffset since)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			var tcs = new TaskCompletionSource<long>();
			using var bridge = new AndroidJavaClass(BridgeClassName);
			bridge.CallStatic("getStepsSince", CurrentActivity, since.ToUnixTimeMilliseconds(), new StepsCallback(tcs));
			return tcs.Task;
#else
			return Task.FromResult(0L);
#endif
		}

		public bool OpenPlatformSettings()
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			using var bridge = new AndroidJavaClass(BridgeClassName);
			return bridge.CallStatic<bool>("openHealthConnectSettings", CurrentActivity);
#else
			return false;
#endif
		}

		public Task<IReadOnlyList<(string dateKey, long steps)>> GetDailyStepsAsync(int days)
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			var tcs = new TaskCompletionSource<IReadOnlyList<(string dateKey, long steps)>>();
			using var bridge = new AndroidJavaClass(BridgeClassName);
			bridge.CallStatic("getDailyStepsSince", CurrentActivity, days, new DailyStepsCallback(tcs));
			return tcs.Task;
#else
			return Task.FromResult<IReadOnlyList<(string, long)>>(Array.Empty<(string, long)>());
#endif
		}
	}
}