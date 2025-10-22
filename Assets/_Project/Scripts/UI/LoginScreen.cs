using BedtimeCore;
using TrainingBuddy.FireBase;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

namespace TrainingBuddy.UI
{
	public class LoginScreen : UILayout
	{
		private TextField _loginEmailField;
		private TextField _loginPasswordField;
		
		private Button _loginButton;
		private Button _registerButton;
		private Button _testButton;

		private readonly FirebaseController _firebaseController;
		
		public LoginScreen(LayoutData layoutData, UIManager uiManager, FirebaseController firebaseController) : base(layoutData, uiManager)
		{
			Layout = _layoutData.LoginScreenVisualTree.Instantiate();
			Layout.AddToClassList("login-wrapper");
			_layoutData.LoginScreen = this;
			_firebaseController = firebaseController;
		}
		
		public override void Initialize()
		{
			_loginEmailField = Layout.Q<TextField>("Email");
			_loginPasswordField = Layout.Q<TextField>("Password");
			
			_loginButton = Layout.Q<Button>("LoginButton");
			_registerButton = Layout.Q<Button>("RegisterButton");
			_testButton = Layout.Q<Button>("TestButton");

			_loginButton.RegisterCallback<ClickEvent>(OnLogin);
			_registerButton.RegisterCallback<ClickEvent>(OnRegister);
			_testButton.RegisterCallback<ClickEvent>(OnTest);
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
			if( Input.GetMouseButtonDown( 0 ) && Input.mousePosition.x > Screen.width * 0.8f && Input.mousePosition.y < Screen.height * 0.2f )
				RequestPermission();
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
			if (await _firebaseController.FirebaseLogin(_loginEmailField.value, _loginPasswordField.value))
			{
				_uiManager.ChangePage(_layoutData.MainMenu);
			}
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
			// $"show overlay".Log();
			// _uiManager.ShowOverlay("string title", "string message");
#if !UNITY_EDITOR
			if (!CheckPermission())
			{
				return;
			}
#endif
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