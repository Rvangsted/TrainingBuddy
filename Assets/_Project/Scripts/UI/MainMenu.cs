using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using TrainingBuddy.UI.Effects;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class MainMenu : UILayout
	{
		private Button _startRaceButton;
		private Button _participateRaceButton;
		private Button _profileButton;
		
		private readonly DatabaseManager _databaseManager;
		
		protected MainMenu(LayoutData layoutData, UIManager uiManager, DatabaseManager databaseManager) : base(layoutData, uiManager, layoutData.MainMenuVisualTree)
		{
			_layoutData.MainMenu = this;
			_databaseManager = databaseManager;
		}

		protected override void OnLayoutBuilt(VisualElement root)
		{
			root.AddToClassList("main-menu-wrapper");
		}

		public override void Initialize()
		{
			base.Initialize();
			var shadowSettings = new ShadowSettings
			{
				CornerRadius = 54,
				ShadowScale = .9f,
				ShadowOffsetX = 0,
				ShadowOffsetY = 10,
			};

			var startRaceButton = ShadowButton("StartRaceButton", "interface_button_start_race", shadowSettings);
			var participateRaceButton = ShadowButton("ParticipateRaceButton", "interface_button_participate_race", shadowSettings);
			var profileButton = ShadowButton("ProfileButton", "interface_button_profile", shadowSettings);
			var privacyButton = TextButton("PrivacyButton", "interface_button_privacy", "<u>", "</u>");
			
			Layout.Q<VisualElement>("MainMenu").Add(startRaceButton);
			Layout.Q<VisualElement>("MainMenu").Add(participateRaceButton);
			Layout.Q<VisualElement>("MainMenu").Add(profileButton);
			Layout.Q<VisualElement>("MainMenu").Add(privacyButton);
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
			if (!_databaseManager.StepCounterRunning)
			{
				_databaseManager.StartStepCounter();
			}
			
			_startRaceButton = Layout.Q<Button>("StartRaceButton");
			_participateRaceButton = Layout.Q<Button>("ParticipateRaceButton");
			_profileButton = Layout.Q<Button>("ProfileButton");
			
			_startRaceButton.RegisterCallback<ClickEvent>(_ =>
			{
				_uiManager.ChangePage(_layoutData.RaceMenuScreen);
			});
			
			_participateRaceButton.RegisterCallback<ClickEvent>(_ =>
			{
				// _uiManager.ChangePage(_layoutData.ProfileScreen);
			});
			
			_profileButton.RegisterCallback<ClickEvent>(_ =>
			{
				_uiManager.ChangePage(_layoutData.ProfileScreen);
			});
		}
	}
}