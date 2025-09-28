using BedtimeCore;
using TrainingBuddy.FireBase;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class RegisterScreen : UILayout
	{
		private TextField _registerUsernameField;
		private TextField _registerEmailField;
		private TextField _registerPasswordField;
		private TextField _registerPasswordConfirmField;
		
		private Button _registerButton;
		
		private readonly FirebaseController _firebaseController;
		
		public RegisterScreen(LayoutData layoutData, UIManager uiManager, FirebaseController firebaseController) : base(layoutData, uiManager)
		{
			Layout = _layoutData.RegisterScreenVisualTree.Instantiate();
			Layout.AddToClassList("register-wrapper");
			_layoutData.RegisterScreen = this;
			_firebaseController = firebaseController;
		}
		
		public override void Initialize()
		{
			_registerUsernameField = Layout.Q<TextField>("Username");
			_registerEmailField = Layout.Q<TextField>("Email");
			_registerPasswordField = Layout.Q<TextField>("Password");
			_registerPasswordConfirmField = Layout.Q<TextField>("PasswordConfirm");
			
			_registerButton = Layout.Q<Button>("RegisterButton");
			
			_registerButton.RegisterCallback<ClickEvent>(OnRegister);
		}

		private async void OnRegister(ClickEvent evt)
		{
			if (await _firebaseController.FirebaseRegister(_registerUsernameField.value, _registerEmailField.value, _registerPasswordField.value, _registerPasswordConfirmField.value))
			{
				_uiManager.ChangePage(_layoutData.ProfileScreen);
			}
		}
	}
}