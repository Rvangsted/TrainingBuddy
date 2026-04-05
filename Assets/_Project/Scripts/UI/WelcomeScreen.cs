using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

namespace TrainingBuddy.UI
{
	public class WelcomeScreen : UILayout
	{
		private Button _loginButton;
		private Button _registerButton;
		private Button _privacyButton;
		
		private Button _test1Button;
		private Button _test2Button;
		private Button _test3Button;
		private Button _test4Button;
		private Button _test5Button;
		private Button _test6Button;
		private Button _test7Button;
		
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
			
			_test1Button  = Layout.Q<Button>("Test1Button");
			_test2Button  = Layout.Q<Button>("Test2Button");
			_test3Button  = Layout.Q<Button>("Test3Button");
			_test4Button  = Layout.Q<Button>("Test4Button");
			_test5Button  = Layout.Q<Button>("Test5Button");
			_test6Button  = Layout.Q<Button>("Test6Button");
			_test7Button  = Layout.Q<Button>("Test7Button");

			_loginButton.RegisterCallback<ClickEvent>(OnLogin);
			_registerButton.RegisterCallback<ClickEvent>(OnRegister);
			_privacyButton.RegisterCallback<ClickEvent>(OnTest);
			
			_test1Button.RegisterCallback<ClickEvent>(UserTest1);
			_test2Button.RegisterCallback<ClickEvent>(UserTest2);
			_test3Button.RegisterCallback<ClickEvent>(UserTest3);
			_test4Button.RegisterCallback<ClickEvent>(UserTest4);
			_test5Button.RegisterCallback<ClickEvent>(UserTest5);
			_test6Button.RegisterCallback<ClickEvent>(UserTest6);
			_test7Button.RegisterCallback<ClickEvent>(UserTest7);
		}

		private VisualElement _permissionOverlay;
		private bool _permissionRequestInProgress;

		public override void DrawLayout()
		{
			base.DrawLayout();

			_permissionOverlay = Layout.Q<VisualElement>("PermissionOverlay");
			_permissionOverlay.Q<Button>("GrantPermissionButton")
			                  .RegisterCallback<ClickEvent>(_ => RequestPermission());

#if !UNITY_EDITOR
			if (!CheckPermission())
			{
				ShowPermissionOverlay();
				RequestPermission();
			}
#endif
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

		private async void RequestPermission()
		{
			if (_permissionRequestInProgress) return;
			_permissionRequestInProgress = true;

			AndroidRuntimePermissions.Permission[] result =
				await AndroidRuntimePermissions.RequestPermissionsAsync(
					"android.permission.ACCESS_FINE_LOCATION",
					"android.permission.ACTIVITY_RECOGNITION");

			_permissionRequestInProgress = false;

			if (result[0] == AndroidRuntimePermissions.Permission.Granted &&
			    result[1] == AndroidRuntimePermissions.Permission.Granted)
			{
				HidePermissionOverlay();
			}
			else
			{
				ShowPermissionOverlay();
			}
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

		private async void UserTest1(ClickEvent evt)
		{
			if (await _firebaseController.FirebaseLogin("testuser1@example.com", "TestPass123!"))
			{
				_uiManager.ChangePage(_layoutData.MainMenu);
			}
		}
		
		private async void UserTest2(ClickEvent evt)
		{
			if (await _firebaseController.FirebaseLogin("testuser2@example.com", "TestPass123!"))
			{
				_uiManager.ChangePage(_layoutData.MainMenu);
			}
		}
		
		private async void UserTest3(ClickEvent evt)
		{
			if (await _firebaseController.FirebaseLogin("testuser3@example.com", "TestPass123!"))
			{
				_uiManager.ChangePage(_layoutData.MainMenu);
			}
		}
		
		private async void UserTest4(ClickEvent evt)
		{
			if (await _firebaseController.FirebaseLogin("testuser4@example.com", "TestPass123!"))
			{
				_uiManager.ChangePage(_layoutData.MainMenu);
			}
		}
		
		private async void UserTest5(ClickEvent evt)
		{
			if (await _firebaseController.FirebaseLogin("testuser5@example.com", "TestPass123!"))
			{
				_uiManager.ChangePage(_layoutData.MainMenu);
			}
		}
		
		private async void UserTest6(ClickEvent evt)
		{
			if (await _firebaseController.FirebaseLogin("testuser6@example.com", "TestPass123!"))
			{
				_uiManager.ChangePage(_layoutData.MainMenu);
			}
		}
		
		private async void UserTest7(ClickEvent evt)
		{
			if (await _firebaseController.FirebaseLogin("testuser7@example.com", "TestPass123!"))
			{
				_uiManager.ChangePage(_layoutData.MainMenu);
			}
		}

	}
}
