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
		private TextField _registerUsernameField;
		private DropdownField _registerSexField;
		private TextField _registerEmailField;
		private TextField _registerPasswordField;
		private TextField _registerPasswordConfirmField;
		
		private LocalizedButton _registerButton;
		
		private readonly FirebaseController _firebaseController;
		
		public RegisterScreen(LayoutData layoutData, UIManager uiManager, FirebaseController firebaseController, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.RegisterScreenVisualTree, "register-wrapper");
			_layoutData.RegisterScreen = this;
			_firebaseController = firebaseController;
		}
		
		public override void Initialize()
		{
			// _registerUsernameField = Layout.Q<TextField>("Username");
			// _registerSexField = Layout.Q<DropdownField>("Sex");
			// _registerEmailField = Layout.Q<TextField>("Email");
			// _registerPasswordField = Layout.Q<TextField>("Password");
			// _registerPasswordConfirmField = Layout.Q<TextField>("PasswordConfirm");
			//
			// _registerButton = Layout.Q<Button>("RegisterButton");
			//
			// _registerButton.RegisterCallback<ClickEvent>(OnRegister);
			
// 			var dropdown = new DropdownField(new List<string> { "Option 1", "Option 2", "Option 3" }, 0);
//
// // Add another option.
// 			dropdown.choices.Add("Option 4");
//
// // To return int value instead of string.
// 			dropdown.index = 1; // Option 2
// 			dropdown.value = "Option 3"; // Set to an existing value
// // Assert that the index is set correctly.
// 			Assert.IsTrue(dropdown.index == 2);
//
// // Register to the value changed callback.
// 			dropdown.RegisterValueChangedCallback(evt => Debug.Log(evt.newValue));
//
// // Style the dropdown.
// 			dropdown.style.width = 200;
// 			dropdown.style.height = 50;

				// <TrainingBuddy.UI.Controls.LocalizedTextInput name="Username" key="interface_text_username" hide-placeholder-on-focus="true" class="field-username" />
				// <ui:DropdownField label="Køn" name="Sex" choices="Mand,Kvinde" class="field-sex" />
				// <TrainingBuddy.UI.Controls.LocalizedTextInput name="Email" key="interface_text_email" hide-placeholder-on-focus="true" class="field-email" />
				// <TrainingBuddy.UI.Controls.LocalizedTextInput name="Password" key="interface_text_password" hide-placeholder-on-focus="true" password="true" class="field-password" />
				// <TrainingBuddy.UI.Controls.LocalizedTextInput name="PasswordConfirm" password="true" hide-placeholder-on-focus="true" key="interface_text_password_confirm" class="field-password" />
				// <TrainingBuddy.UI.Controls.LocalizedButton name="RegisterButton" key="interface_button_register" class="button-large" />
		}
		
		public override void DrawLayout()
		{
			var container = Layout.Q<VisualElement>("Container");

			_registerUsernameField = new LocalizedTextInput
			{ 
				name = "Username",
				key = "interface_text_username",
				hidePlaceholderOnFocus = true,
			};
			_registerUsernameField.AddToClassList("field-username");
			
			_registerSexField = new DropdownField(new List<string> { "Mand", "Kvinde" }, 0);
			_registerSexField.AddToClassList("field-sex");
			
			_registerEmailField = new LocalizedTextInput
			{ 
				name = "Email",
				key = "interface_text_email",
				hidePlaceholderOnFocus = true,
			};
			_registerEmailField.AddToClassList("field-email");
			
			_registerPasswordField = new LocalizedTextInput
			{ 
				name = "Password",
				key = "interface_text_password",
				hidePlaceholderOnFocus = true,
				isPasswordField = true,
			};
			_registerPasswordField.AddToClassList("field-password");
			
			_registerPasswordConfirmField = new LocalizedTextInput
			{ 
				name = "PasswordConfirm",
				key = "interface_text_password_confirm",
				hidePlaceholderOnFocus = true,
				isPasswordField = true,
			};
			_registerPasswordConfirmField.AddToClassList("field-password");
			
			_registerButton = new LocalizedButton()
			{ 
				name = "RegisterButton",
				key = "interface_button_register",
			};
			_registerButton.AddToClassList("button-large");
			 
			container.Add(_registerUsernameField);
			container.Add(_registerSexField);
			container.Add(_registerEmailField);
			container.Add(_registerPasswordField);
			container.Add(_registerPasswordConfirmField);
			container.Add(_registerButton);
			
			_registerButton.RegisterCallback<ClickEvent>(OnRegister);
			_uiManager.Header.Q<Button>("BackButton").RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.LoginScreen));
			_uiManager.Header.Q<Label>("SiteTitle").text = "Min Profil";
			
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