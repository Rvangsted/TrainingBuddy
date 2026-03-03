using System.Collections.Generic;
using BedtimeCore;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using UnityEngine;
using UnityEngine.Localization.SmartFormat.Core.Parsing;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class RegisterScreen : UILayout
	{
		private LocalizedTextInput _registerUsernameField;
		private DropdownField _registerSexField;
		private LocalizedTextInput _registerEmailField;
		private LocalizedTextInput _registerPasswordField;
		private LocalizedTextInput _registerPasswordConfirmField;
		
		private LocalizedButton _registerButton;
		private LocalizedButton _loginSiteButton;
		
		private readonly FirebaseController _firebaseController;
		
		public RegisterScreen(LayoutData layoutData, UIManager uiManager, FirebaseController firebaseController, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.RegisterScreenVisualTree, "register-wrapper");
			_layoutData.RegisterScreen = this;
			_firebaseController = firebaseController;
		}
		
		public override void Initialize()
		{
			_registerButton = Layout.Q<LocalizedButton>("RegisterButton");
			_loginSiteButton = Layout.Q<LocalizedButton>("LoginAccount");
			
			_registerButton.RegisterCallback<ClickEvent>(OnRegister);
			_loginSiteButton.RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.LoginScreen));
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
		}

		private async void OnRegister(ClickEvent evt)
		{
			if (await _firebaseController.FirebaseRegister(_registerUsernameField.value, _registerSexField.value, _registerEmailField.value, _registerPasswordField.value, _registerPasswordConfirmField.value))
			{
				_uiManager.ChangePage(_layoutData.ProfileScreen);
			}
		}
	}
}
