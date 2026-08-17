using System.Threading.Tasks;
using BedtimeCore;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using UnityEngine;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

namespace TrainingBuddy.UI
{
	public class WelcomeScreen : UILayout
	{
		private Button _loginButton;
		private Button _registerButton;
		private Button _privacyButton;
		
		private readonly FirebaseController _firebaseController;
		
		public WelcomeScreen(LayoutData layoutData, UIManager uiManager, FirebaseController firebaseController, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.WelcomeScreenVisualTree, "welcome-wrapper");
			_layoutData.WelcomeScreen = this;
			
			_firebaseController = firebaseController;
		}
		
		public override void Initialize()
		{
			_loginButton = Layout.Q<Button>("LoginButton");
			_registerButton = Layout.Q<Button>("RegisterButton");
			_privacyButton = Layout.Q<Button>("PrivacyButton");

			_loginButton.RegisterCallback<ClickEvent>(OnLogin);
			_registerButton.RegisterCallback<ClickEvent>(OnRegister);
			_privacyButton.RegisterCallback<ClickEvent>(OnTest);
		}

		private VisualElement _permissionOverlay;
		private Label _permissionMessage;
		private Button _openSettingsButton;
		private bool _permissionRequestInProgress;

		// Health Connect's own consent screen shows data types (e.g. Steps) as toggles that default
		// off — completing that screen without switching Steps on looks identical, from this app's
		// side, to denying the request. After one failed explicit attempt, surface a way to fix it
		// directly in Health Connect/App settings rather than silently re-showing the same overlay.
		private bool _permissionRequestFailedOnce;

		public override void DrawLayout()
		{
			base.DrawLayout();

			_permissionOverlay = Layout.Q<VisualElement>("PermissionOverlay");
			_permissionMessage = _permissionOverlay.Q<Label>("PermissionMessage");
			_permissionOverlay.Q<Button>("GrantPermissionButton")
			                  .RegisterCallback<ClickEvent>(_ => RequestPermission());

			_openSettingsButton = _permissionOverlay.Q<Button>("OpenSettingsButton");
			_openSettingsButton?.RegisterCallback<ClickEvent>(_ => OpenAppSettings());
			SetOpenSettingsButtonVisible(false);

#if !UNITY_EDITOR
			_ = EnsurePermissionsAsync();
#endif
		}

		private void SetOpenSettingsButtonVisible(bool visible)
		{
			if (_openSettingsButton != null)
				_openSettingsButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private void ShowPermissionOverlay()
		{
			if (_permissionOverlay != null)
				_permissionOverlay.style.display = DisplayStyle.Flex;
		}

		private void HidePermissionOverlay()
		{
			if (_permissionOverlay != null)
				_permissionOverlay.style.display = DisplayStyle.None;
		}

		// Health Connect's consent status can't be read synchronously the way the classic OS
		// runtime permissions can, so the initial check (unlike CheckPermission()) has to be async.
		private async Task EnsurePermissionsAsync()
		{
			bool osGranted = CheckPermission();
			bool stepProviderReady = await IsStepProviderReadyAsync();

			if (osGranted && stepProviderReady)
			{
				HidePermissionOverlay();
			}
			else
			{
				ShowPermissionOverlay();
				await RequestPermissionAsync();
			}
		}

		private async Task<bool> IsStepProviderReadyAsync()
		{
			if (!_databaseManager.HasStepDataProvider) return true;
			return await _databaseManager.CheckStepProviderAvailabilityAsync() == StepCounterAvailability.Available;
		}

		private async void RequestPermission() => await RequestPermissionAsync();

		private async Task RequestPermissionAsync()
		{
			if (_permissionRequestInProgress) return;
			_permissionRequestInProgress = true;

			AndroidRuntimePermissions.Permission[] result =
				await AndroidRuntimePermissions.RequestPermissionsAsync(
					"android.permission.ACCESS_FINE_LOCATION",
					"android.permission.ACTIVITY_RECOGNITION");

			bool osGranted =
				result[0] == AndroidRuntimePermissions.Permission.Granted &&
				result[1] == AndroidRuntimePermissions.Permission.Granted;

			StepCounterAvailability stepAvailability = StepCounterAvailability.Available;
			if (_databaseManager.HasStepDataProvider)
			{
				stepAvailability = await _databaseManager.CheckStepProviderAvailabilityAsync();
				if (stepAvailability == StepCounterAvailability.PermissionDenied)
					stepAvailability = await _databaseManager.RequestStepProviderPermissionAsync();
			}
			bool stepProviderReady = stepAvailability == StepCounterAvailability.Available;

			$"Permission request result: osGranted={osGranted} stepProviderAvailability={stepAvailability}".Log();

			_permissionRequestInProgress = false;

			if (osGranted && stepProviderReady)
			{
				HidePermissionOverlay();
			}
			else
			{
				// Only offer the settings fallback once an explicit request has actually round-tripped
				// through the OS/Health Connect UI and still come back short — on the very first pass
				// (e.g. before the user has been asked at all) the normal Grant button should be tried first.
				if (_permissionRequestFailedOnce)
				{
					UpdatePermissionMessage(stepAvailability);
					SetOpenSettingsButtonVisible(true);
				}
				_permissionRequestFailedOnce = true;
				ShowPermissionOverlay();
			}
		}

		private void UpdatePermissionMessage(StepCounterAvailability stepAvailability)
		{
			if (_permissionMessage == null) return;

			_permissionMessage.text = stepAvailability switch
			{
				StepCounterAvailability.ProviderNotInstalled =>
					"Appen skal bruge Health Connect for at tælle skridt. Installer Health Connect fra Play Store og prøv igen.",
				StepCounterAvailability.PermissionDenied =>
					"Husk at slå \"Skridt\" til i Health Connect, når du bekræfter adgangen. Åbn indstillinger for at rette det manuelt.",
				_ => "Appen skal bruge adgang til din aktivitet og placering for at tælle skridt og finde løb i nærheden."
			};
		}

		// Tries Health Connect's own permission screen for this app first — the OS "App info" page
		// opened below has no Health Connect section at all, so it can't actually fix a Health
		// Connect permission problem. Falls back to App info when there's no step provider (e.g.
		// iOS) or Health Connect itself can't be opened (e.g. not installed).
		private void OpenAppSettings()
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			if (_databaseManager.HasStepDataProvider && _databaseManager.OpenStepProviderSettings())
				return;

			try
			{
				using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
				using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
				using var uri = new AndroidJavaClass("android.net.Uri").CallStatic<AndroidJavaObject>(
					"fromParts", "package", Application.identifier, null);
				using var intent = new AndroidJavaObject("android.content.Intent",
					"android.settings.APPLICATION_DETAILS_SETTINGS", uri);
				activity.Call("startActivity", intent);
			}
			catch (System.Exception ex)
			{
				$"OpenAppSettings failed: {ex}".LogError();
			}
#endif
		}

		private void OnLogin(ClickEvent evt)
		{
			_uiManager.ChangePage(_layoutData.LoginScreen);
		}

		private void OnRegister(ClickEvent evt)
		{
			_uiManager.ChangePage(_layoutData.RegisterScreen);
		}
		
		private async void OnTest(ClickEvent evt)
		{
			// var title = LocalizationSettings.StringDatabase.GetLocalizedString("text_are_you_sure");
			// var message = LocalizationSettings.StringDatabase.GetLocalizedString("text_are_you_sure_subtitle");
			// var cancelText = LocalizationSettings.StringDatabase.GetLocalizedString("button_cancel");
			// var confirmText = LocalizationSettings.StringDatabase.GetLocalizedString("button_delete_user");
			//
			// _uiManager.ShowOverlay(
			// 	title,
			// 	message,
			// 	cancelText,
			// 	() => Debug.Log("Cancel"),
			// 	confirmText,
			// 	() => Debug.Log("Delete User"),
			// 	UniversalOverlay.PopupImage.Friends
			// );

// #if !UNITY_EDITOR
// 			if (!CheckPermission())
// 			{
// 				return;
// 			}
// #endif
			if (await _firebaseController.FirebaseLogin("admin@trainingbuddy.dk", "smjo3y2kZRfk7jN^@wGN4z8K^"))
			{
				_uiManager.ChangePage(_layoutData.MainMenu);
			}
		}
	}
}
