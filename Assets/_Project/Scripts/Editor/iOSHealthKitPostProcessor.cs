using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace TrainingBuddy.Editor
{
	/// <summary>
	/// Wires up the Xcode-side requirements HealthKit needs beyond just linking the native plugin:
	/// the HealthKit capability/entitlement (app target), HealthKit.framework (UnityFramework
	/// target — that's where Assets/Plugins/iOS/HealthKitBridge.mm actually compiles into), and the
	/// NSHealthShareUsageDescription Info.plist string Apple's authorization sheet displays. See
	/// Assets/_Project/Docs/StepCounter_HealthPlatform_Migration_Scope.md.
	/// </summary>
	public static class iOSHealthKitPostProcessor
	{
		// Shown on HealthKit's authorization sheet — Apple reviews this string specifically, so
		// confirm the wording before any store submission.
		private const string HealthShareUsageDescription =
			"Appen bruger Apple Health til at tælle dine skridt, så de tælles korrekt, selv når appen ikke har kørt i baggrunden.";

		[PostProcessBuild(1)]
		public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuiltProject)
		{
			if (buildTarget != BuildTarget.iOS) return;

			string plistPath = pathToBuiltProject + "/Info.plist";
			var plist = new PlistDocument();
			plist.ReadFromFile(plistPath);
			plist.root.SetString("NSHealthShareUsageDescription", HealthShareUsageDescription);
			plist.WriteToFile(plistPath);

			string pbxPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
			var pbxProject = new PBXProject();
			pbxProject.ReadFromFile(pbxPath);

			string mainTargetGuid = pbxProject.GetUnityMainTargetGuid();
			string frameworkTargetGuid = pbxProject.GetUnityFrameworkTargetGuid();

			// The native bridge compiles into UnityFramework, not the app wrapper, so that's the
			// target that needs the framework linked.
			pbxProject.AddFrameworkToProject(frameworkTargetGuid, "HealthKit.framework", false);
			pbxProject.WriteToFile(pbxPath);

			// The capability/entitlement is an app-level concept, so it goes on the main target.
			var capabilities = new ProjectCapabilityManager(pbxPath, "HealthKit.entitlements", targetGuid: mainTargetGuid);
			capabilities.AddHealthKit();
			capabilities.WriteToFile();
		}
	}
}
