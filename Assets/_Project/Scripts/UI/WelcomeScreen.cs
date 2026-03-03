using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Settings;

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

		internal void PermissionCallbacks_PermissionGranted(string permissionName)
		{
			Debug.Log($"{permissionName} PermissionCallbacks_PermissionGranted");
		}

		internal void PermissionCallbacks_PermissionDenied(string permissionName)
		{
			Debug.Log($"{permissionName} PermissionCallbacks_PermissionDenied");
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
			var callbacks = new PermissionCallbacks();
			callbacks.PermissionDenied += PermissionCallbacks_PermissionDenied;
			callbacks.PermissionGranted += PermissionCallbacks_PermissionGranted;
			
			if (!Permission.HasUserAuthorizedPermission("android.permission.ACTIVITY_RECOGNITION") || !Permission.HasUserAuthorizedPermission("android.permission.ACCESS_FINE_LOCATION"))
			{
				RequestPermission();
			}
		}
		
		void Update()
		{
			Vector2? pointerPosition = null;
			bool pressed = false;

			var mouse = Mouse.current;
			if (mouse != null)
			{
				pointerPosition = mouse.position.ReadValue();
				pressed = mouse.leftButton.wasPressedThisFrame;
			}

			if (!pressed)
			{
				var touch = Touchscreen.current?.primaryTouch;
				if (touch != null)
				{
					pointerPosition = touch.position.ReadValue();
					pressed = touch.press.wasPressedThisFrame;
				}
			}

			if (pressed && pointerPosition.HasValue)
			{
				var position = pointerPosition.Value;
				var safeArea = Screen.safeArea;
				var triggerMinX = safeArea.xMin + safeArea.width * 0.8f;
				var triggerMaxY = safeArea.yMin + safeArea.height * 0.2f;

				if (position.x > triggerMinX && position.y < triggerMaxY)
				{
					RequestPermission();
				}
			}
		}

		async void RequestPermission()
		{
			AndroidRuntimePermissions.Permission[] result = await AndroidRuntimePermissions.RequestPermissionsAsync("android.permission.ACCESS_FINE_LOCATION", "android.permission.ACTIVITY_RECOGNITION");
			if (result[0] == AndroidRuntimePermissions.Permission.Granted && result[1] == AndroidRuntimePermissions.Permission.Granted)
			{
				Debug.Log("We have all the permissions!");
			}
			else
			{
				Debug.Log("Some permission(s) are not granted...");
			}
		}

		private async void OnLogin(ClickEvent evt)
		{
#if !UNITY_EDITOR
			CheckPermission();
			if (!CheckPermission())
			{
				return;
			}
#endif
			_uiManager.ChangePage(_layoutData.LoginScreen);
		}
		
		private void OnRegister(ClickEvent evt)
		{
#if !UNITY_EDITOR
			CheckPermission();
			if (!CheckPermission())
			{
				return;
			}
#endif
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

		private bool CheckPermission()
		{
			if (!Permission.HasUserAuthorizedPermission("android.permission.ACTIVITY_RECOGNITION") || !Permission.HasUserAuthorizedPermission("android.permission.ACCESS_FINE_LOCATION"))
			{
				_uiManager.ChangePage(_layoutData.LoginScreen);
				return false;
			}

			return true;
		}
	}
}
