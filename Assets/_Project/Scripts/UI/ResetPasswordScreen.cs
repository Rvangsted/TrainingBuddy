using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

namespace TrainingBuddy.UI
{
	public class ResetPasswordScreen : UILayout
	{
		private LocalizedTextInput _newPasswordField;
		private LocalizedTextInput _repeatPasswordField;
		private LocalizedButton _resetButton;

		private readonly FirebaseController _firebaseController;
		
		public ResetPasswordScreen(LayoutData layoutData, UIManager uiManager, FirebaseController firebaseController, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.ResetPasswordScreenVisualTree, "reset-password-wrapper");
			_layoutData.ResetPasswordScreen = this;
			_firebaseController = firebaseController;
		}
		
		public override void Initialize()
		{
			_newPasswordField = Layout.Q<LocalizedTextInput>("NewPassword");
			_repeatPasswordField = Layout.Q<LocalizedTextInput>("RepeatPassword");
			_resetButton = Layout.Q<LocalizedButton>("ResetPasswordButton");

			_resetButton.RegisterCallback<ClickEvent>(OnReset);
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
		}

		private async void OnReset(ClickEvent evt)
		{
#if !UNITY_EDITOR
			CheckPermission();
			if (!CheckPermission())
			{
				return;
			}
#endif
			// if (await _firebaseController.FirebaseLogin(_loginEmailField.value, _loginPasswordField.value))
			// {
			// 	_uiManager.ChangePage(_layoutData.MainMenu);
			// }
		}
	}
}
