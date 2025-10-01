

using System;
using BedtimeCore;
using Firebase.Database;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using TrainingBuddy.UI.Controls;
using TrainingBuddy.UI.Effects;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class ProfileScreen : UILayout
	{
		private Button _accPlusButton;
		private Button _accMinusButton;
		private Button _spdPlusButton;
		private Button _spdMinusButton;
		private Button _trainingButton;
		private Button _logoutButton;
		
		private readonly FirebaseController _firebaseController;
		private readonly DatabaseManager _databaseManager;

		private DataSnapshot _dataSnapshot;
		
		protected ProfileScreen(LayoutData layoutData, UIManager uiManager, FirebaseController firebaseController, DatabaseManager databaseManager) : base(layoutData, uiManager)
		{
			Layout = _layoutData.ProfileScreenVisualTree.Instantiate();
			Layout.AddToClassList("profile-wrapper");
			_layoutData.ProfileScreen = this;
			_firebaseController = firebaseController;
			_databaseManager = databaseManager;
		}
		
		public override void Initialize()
		{
			var levelingProgressWrapper = new VisualElement();
			levelingProgressWrapper.AddToClassList("leveling-progress-wrapper");
			
			var circularProgressBar = new CircularProgressBar
			{
				name = "LevelingProgressBar", 
				Value = 0.7f,
				TrackColor = new Color(0.88f, 0.88f, 0.88f, 1f),
				ProgressColor = new Color(0.76f, 1f, 0f, 1f),
				KnobColor = new Color(0.68f, 0.94f, 0f, 1f),
				InnerKnobColor = new Color(0.88f, 0.88f, 0.88f, 1f),
				ArrowColor = new Color(0, 0f, 0f, 1f),
				
				LineThickness = 30f,
				StartAngle = 270f,
				KnobSize = 20f,
				InnerKnobSize = 6f,
				ShowKnob = true,
				ShowArrow = true,
				ArrowLength = 18f,
				ArrowWidth = 18f,
				ArrowOffset = 6f,
			};
			circularProgressBar.AddToClassList($"leveling-progress-bar");

			var levelingProgressContent = new VisualElement();
			levelingProgressContent.AddToClassList("leveling-progress-content");
			
			var levelingProgressValueLabel = new Label
			{
				name = "LevelingProgressValueLabel",
				text = "64 KM",
			};
			levelingProgressValueLabel.AddToClassList("leveling-progress-value-label");
			levelingProgressValueLabel.AddToClassList("font-title");
			
			var levelingProgressSubtitleLabel = new Label
			{
				name = "LevelingProgressSubtitleLabel",
				text = "Til næste niveau",
			};
			levelingProgressSubtitleLabel.AddToClassList("leveling-progress-subtitle-label");
			levelingProgressSubtitleLabel.AddToClassList("font-regular");
			
			levelingProgressContent.Add(levelingProgressValueLabel);
			levelingProgressContent.Add(levelingProgressSubtitleLabel);
			
			levelingProgressWrapper.Add(circularProgressBar);
			levelingProgressWrapper.Add(levelingProgressContent);
			Layout.Q<VisualElement>("LevelingMiddleContainer").Add(levelingProgressWrapper);
		}

		public override async void DrawLayout()
		{
			base.DrawLayout();
			_uiManager.Header.Q<Button>("BackButton").RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.MainMenu));
		}

		private void OnLogout(ClickEvent evt)
		{
			_firebaseController.FirebaseLogout();
			_uiManager.ChangePage(_layoutData.LoginScreen);
		}
	}
}