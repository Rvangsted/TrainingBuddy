using System;
using BedtimeCore;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	/// <summary>
	/// New screen — see PaidRunsUI_Scope.md. Replaces MainMenu's old one-tap "create race with
	/// auto-generated everything" flow: lets the host name their race and pick a paid entry tier
	/// before HostRaceAsync actually runs.
	/// </summary>
	public class CreateRaceScreen : UILayout
	{
		private TextField _raceNameField;
		private RadioButtonGroup _tierGroup;
		private Button _createButton;

		protected CreateRaceScreen(LayoutData layoutData, UIManager uiManager, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.CreateRaceScreenVisualTree, "create-race-wrapper");
			_layoutData.CreateRaceScreen = this;
		}

		public override void Initialize()
		{
			_raceNameField = Layout.Q<TextField>("RaceNameField");
			_tierGroup = Layout.Q<RadioButtonGroup>("TierGroup");
			_createButton = Layout.Q<Button>("CreateRaceButton");

			_tierGroup.choices = DatabaseManager.GetRaceEntryTierChoiceLabels();
			_tierGroup.value = 0;

			_createButton.RegisterCallback<ClickEvent>(OnCreateRace);
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
			_uiManager.Header.Q<Label>("SiteTitle").text = "Opret løb";
			_raceNameField.value = string.Empty;
			_tierGroup.value = 0;
		}

		private async void OnCreateRace(ClickEvent evt)
		{
			var tier = (RaceEntryTier)_tierGroup.value;
			string displayName = _databaseManager.Auth?.CurrentUser?.DisplayName;
			string raceName = string.IsNullOrWhiteSpace(_raceNameField.value)
				? $"{displayName}'s Race"
				: _raceNameField.value.Trim();

			try
			{
				await _databaseManager.CreateLobby(new RaceData
				{
					RaceName = raceName,
					HostName = displayName,
					Longitude = 0,
					Latitude = 0,
					Status = 0,
				}, tier);

				_uiManager.ChangePage(_layoutData.HostScreen);
			}
			catch (Exception ex)
			{
				$"Failed to create race: {ex.Message}".LogError();
				_databaseManager.ShowError("Kan ikke oprette løb", ex);
			}
		}
	}
}
