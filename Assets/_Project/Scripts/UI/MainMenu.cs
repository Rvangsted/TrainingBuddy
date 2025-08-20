using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using TrainingBuddy.UI.Effects;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class MainMenu : UILayout
	{
		private Button _raceButton;
		private Button _profileButton;
		private Button _highScoreButton;
		
		private readonly DatabaseManager _databaseManager;
		
		protected MainMenu(LayoutData layoutData, UIManager uiManager, DatabaseManager databaseManager) : base(layoutData, uiManager)
		{
			Layout = _layoutData.MainMenuVisualTree.Instantiate();
			_layoutData.MainMenu = this;
			_databaseManager = databaseManager;
		}
		
		public override void Initialize()
		{
			// var containerWithShadow = new Shadow
			// {
			// 	name = "ContainerWithShadow",
			// 	shadowCornerRadius = 54,
			// 	shadowScale = 1,
			// 	shadowOffsetX = 0,
			// 	shadowOffsetY = 0,
			// };
			// containerWithShadow.AddToClassList("drop-shadow");
			//
			// var gradientContainer = new GradientElement
			// {
			// 	name = "GradientContainer",
			// };
			// gradientContainer.AddToClassList("gradient-container");
			
			// var button = new LocalizedButton
			// {
			// 	name = "RaceButton",
			// 	key = "interface_button_race",
			// };
			// var glow = new Glow();
			// glow.AddToClassList("button-glow");
			// button.AddToClassList("button-large");
			// button.Add(glow);
			
			var shadowSettings = new ShadowSettings
			{
				CornerRadius = 54,
				ShadowScale = .9f,
				ShadowOffsetX = 0,
				ShadowOffsetY = 10,
			};

			var raceButton = ShadowButton("RaceButton", "interface_button_race", shadowSettings);
			var profileButton = ShadowButton("ProfileButton", "interface_button_profile", shadowSettings);
			var highScoreButton = ShadowButton("HighScoreButton", "interface_button_highscore", shadowSettings);
			var privacyButton = TextButton("PrivacyButton", "interface_button_privacy", "<u>", "</u>");
			
			// gradientContainer.Content.Add(raceButton);
			// gradientContainer.Content.Add(profileButton);
			// gradientContainer.Content.Add(highScoreButton);
			//
			// containerWithShadow.Add(gradientContainer);
			Layout.Q<VisualElement>("MainMenu").Add(raceButton);
			Layout.Q<VisualElement>("MainMenu").Add(profileButton);
			Layout.Q<VisualElement>("MainMenu").Add(highScoreButton);
			Layout.Q<VisualElement>("MainMenu").Add(privacyButton);
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
			if (!_databaseManager.StepCounterRunning)
			{
				_databaseManager.StartStepCounter();
			}
			
			_raceButton = Layout.Q<Button>("RaceButton");
			_profileButton = Layout.Q<Button>("ProfileButton");
			_highScoreButton = Layout.Q<Button>("HighScoreButton");
			
			_raceButton.RegisterCallback<ClickEvent>(_ =>
			{
				_uiManager.ChangePage(_layoutData.RaceMenuScreen);
			});
			
			_profileButton.RegisterCallback<ClickEvent>(_ =>
			{
				_uiManager.ChangePage(_layoutData.ProfileScreen);
			});
			
			_highScoreButton.RegisterCallback<ClickEvent>(_ =>
			{
				_uiManager.ChangePage(_layoutData.HighScoreScreen);
			});
		}
	}
}