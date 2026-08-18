using BedtimeCore;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class RegisterScreen : UILayout
	{
		private LocalizedTextInput _registerUsernameField;
		private RadioButtonGroup _registerGenderGroup;
		private LocalizedTextInput _registerEmailField;
		private LocalizedTextInput _registerPasswordField;
		private LocalizedTextInput _registerPasswordConfirmField;
		private TextField _registerReferralCodeField;

		private IntegerField _registerDobDayField;
		private IntegerField _registerDobMonthField;
		private IntegerField _registerDobYearField;

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
			_registerUsernameField = Layout.Q<LocalizedTextInput>("Username");
			_registerGenderGroup = Layout.Q<RadioButtonGroup>("GenderGroup");
			_registerEmailField = Layout.Q<LocalizedTextInput>("Email");
			_registerPasswordField = Layout.Q<LocalizedTextInput>("Password");
			_registerPasswordConfirmField = Layout.Q<LocalizedTextInput>("PasswordConfirm");
			_registerReferralCodeField = Layout.Q<TextField>("ReferralCode");

			_registerDobDayField = Layout.Q<IntegerField>("DobDay");
			_registerDobMonthField = Layout.Q<IntegerField>("DobMonth");
			_registerDobYearField = Layout.Q<IntegerField>("DobYear");

			_registerButton = Layout.Q<LocalizedButton>("RegisterButton");
			_loginSiteButton = Layout.Q<LocalizedButton>("LoginAccount");

			_registerDobDayField.RegisterValueChangedCallback(e =>
			{
				if (e.newValue == 0) return;
				int clamped = System.Math.Clamp(e.newValue, 1, 31);
				if (clamped != e.newValue) _registerDobDayField.SetValueWithoutNotify(clamped);
			});
			_registerDobMonthField.RegisterValueChangedCallback(e =>
			{
				if (e.newValue == 0) return;
				int clamped = System.Math.Clamp(e.newValue, 1, 12);
				if (clamped != e.newValue) _registerDobMonthField.SetValueWithoutNotify(clamped);
			});
			_registerDobYearField.RegisterValueChangedCallback(e =>
			{
				if (e.newValue == 0) return;
				if (e.newValue > 9999) _registerDobYearField.SetValueWithoutNotify(9999);
			});

			_registerButton.RegisterCallback<ClickEvent>(OnRegister);
			_loginSiteButton.RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.LoginScreen));
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
		}

		private async void OnRegister(ClickEvent evt)
		{
			var sex = _registerGenderGroup.value switch
			{
				0 => "Male",
				1 => "Female",
				_ => ""
			};

			int dobDay = _registerDobDayField.value;
			int dobMonth = _registerDobMonthField.value;
			int dobYear = _registerDobYearField.value;

			if (await _firebaseController.FirebaseRegister(
				_registerUsernameField.value,
				sex,
				_registerEmailField.value,
				_registerPasswordField.value,
				_registerPasswordConfirmField.value,
				dobDay,
				dobMonth,
				dobYear,
				_registerReferralCodeField.value))
			{
				_uiManager.ChangePage(_layoutData.ProfileScreen);
			}
		}
	}
}