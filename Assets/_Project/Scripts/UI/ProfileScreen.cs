using System;
using System.Collections.Generic;
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

		private ActivityGraph _activityGraph;
		
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
			
			// SpeedStatUpperMiddle
			var speedProgressBar = new LinearProgressBar
			{
				name = "SpeedProgressBar", 
				Value = 0.7f,
				TrackColor = new Color(0.88f, 0.88f, 0.88f, 1f),
				ProgressColor = new Color(0.76f, 1f, 0f, 1f),
				KnobColor = new Color(0.68f, 0.94f, 0f, 1f),
				InnerKnobColor = new Color(0.88f, 0.88f, 0.88f, 1f),
				
				LineThickness = 35f,
				KnobSize = 25f,
				InnerKnobSize = 6f,
			};
			speedProgressBar.AddToClassList($"linear-progress-bar");
			Layout.Q<VisualElement>("SpeedStatUpperMiddle").Add(speedProgressBar);
			
			var accelerationProgressBar = new LinearProgressBar
			{
				name = "AccelerationProgressBar", 
				Value = 0.7f,
				TrackColor = new Color(0.88f, 0.88f, 0.88f, 1f),
				ProgressColor = new Color(0.76f, 1f, 0f, 1f),
				KnobColor = new Color(0.68f, 0.94f, 0f, 1f),
				InnerKnobColor = new Color(0.88f, 0.88f, 0.88f, 1f),
				
				LineThickness = 35f,
				KnobSize = 25f,
				InnerKnobSize = 6f,
			};
			accelerationProgressBar.AddToClassList($"linear-progress-bar");
			Layout.Q<VisualElement>("AccelerationStatUpperMiddle").Add(accelerationProgressBar);
			
			Layout.Q<VisualElement>("LevelingMiddleContainer").Add(levelingProgressWrapper);

			InitializeActivitySection();
		}

		public override async void DrawLayout()
		{
			base.DrawLayout();
			_uiManager.Header.Q<Button>("BackButton").RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.MainMenu));
        }

        private void InitializeActivitySection()
        {
            var activityContainer = Layout.Q<VisualElement>("ActivityContainer");
            if (activityContainer == null)
            {
				return;
            }

            activityContainer.style.display = DisplayStyle.Flex;

            var header = new VisualElement { name = "ActivityHeader" };
            header.AddToClassList("activity-header");

            var titleLabel = new Label("Træningsdistance")
            {
				name = "ActivityTitle",
            };
            titleLabel.AddToClassList("activity-title");
            titleLabel.AddToClassList("font-title");

            var subtitleLabel = new Label("Sidste 5 dage")
            {
				name = "ActivitySubtitle",
            };
            subtitleLabel.AddToClassList("activity-subtitle");
            subtitleLabel.AddToClassList("font-regular");

            header.Add(titleLabel);
            header.Add(subtitleLabel);

            _activityGraph = new ActivityGraph
            {
				name = "WeeklyDistanceGraph",
            };
            _activityGraph.ValueFormatter = value => $"{value:0.#} KM";

            activityContainer.Add(header);
            activityContainer.Add(_activityGraph);

            var sampleData = new List<ActivityGraph.DataPoint>
            {
                new("28. april", 2.8f),
                new("29. april", 3.6f),
                new("30. april", 2.9f),
                new("I dag", 5.0f),
                new("1. maj", 2.4f),
            };

            _activityGraph.SetData(sampleData, 3);
        }

        public void UpdateActivityData(IEnumerable<ActivityGraph.DataPoint> dataPoints, int highlightIndex)
        {
            _activityGraph?.SetData(dataPoints, highlightIndex);
        }

		private void OnLogout(ClickEvent evt)
		{
			_firebaseController.FirebaseLogout();
			_uiManager.ChangePage(_layoutData.LoginScreen);
		}
	}
}