using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

namespace TrainingBuddy.UI
{
	public class ForgotPasswordScreen : UILayout
	{
		private LocalizedTextInput _recoverEmailField;
		private LocalizedButton _recoverButton;

		private readonly FirebaseController _firebaseController;
		
		public ForgotPasswordScreen(LayoutData layoutData, UIManager uiManager, FirebaseController firebaseController, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.ForgotPasswordScreenVisualTree, "forgot-password-wrapper");
			_layoutData.ForgotPasswordScreen = this;
			_firebaseController = firebaseController;
		}
		
		public override void Initialize()
		{
			_recoverEmailField = Layout.Q<LocalizedTextInput>("Email");
			_recoverButton = Layout.Q<LocalizedButton>("ForgotPasswordButton");

			_recoverButton.RegisterCallback<ClickEvent>(OnRecover);
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
		}

		private async void OnRecover(ClickEvent evt)
		{
			await _firebaseController.SendPasswordResetEmailAsync(_recoverEmailField.value);
		}
	}
}
