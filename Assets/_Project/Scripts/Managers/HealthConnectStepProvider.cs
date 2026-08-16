using System;
using System.Threading.Tasks;
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

			[UnityEngine.Scripting.Preserve]
			public void OnResult(string status)
			{
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

			[UnityEngine.Scripting.Preserve]
			public void OnResult(long steps, bool success)
			{
				_tcs.TrySetResult(success ? steps : 0);
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

		/// <summary>
		/// Shows the Health Connect permission screen. Not part of IStepDataProvider — Health
		/// Connect's consent flow is its own system UI, distinct from the availability check, and
		/// callers need to trigger it explicitly (e.g. from onboarding) rather than have it pop up
		/// as a side effect of checking availability.
		/// </summary>
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
	}
}