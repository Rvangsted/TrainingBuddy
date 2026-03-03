using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class LoginScreen : UILayout
	{
		private LocalizedTextInput _loginEmailField;
		private LocalizedTextInput _loginPasswordField;
		private LocalizedButton _forgotPasswordButton;
		private LocalizedButton _loginButton;
		private LocalizedButton _registerSiteButton;

		private readonly FirebaseController _firebaseController;
		
		public LoginScreen(LayoutData layoutData, UIManager uiManager, FirebaseController firebaseController, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.LoginScreenVisualTree, "login-wrapper");
			_layoutData.LoginScreen = this;
			_firebaseController = firebaseController;
		}
		
		public override void Initialize()
		{
			_loginEmailField = Layout.Q<LocalizedTextInput>("Email");
			_loginPasswordField = Layout.Q<LocalizedTextInput>("Password");
			_forgotPasswordButton = Layout.Q<LocalizedButton>("PasswordRecovery");
			_loginButton = Layout.Q<LocalizedButton>("LoginButton");
			_registerSiteButton = Layout.Q<LocalizedButton>("RegisterAccount");

			_loginButton.RegisterCallback<ClickEvent>(OnLogin);
			_forgotPasswordButton.RegisterCallback<ClickEvent>(OnForgotPassword);
			_registerSiteButton.RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.RegisterScreen));
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
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
		
		private void OnForgotPassword(ClickEvent evt)
		{
#if !UNITY_EDITOR
			CheckPermission();
			if (!CheckPermission())
			{
				return;
			}
#endif

			_uiManager.ChangePage(_layoutData.ForgotPasswordScreen);
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
	}
}
