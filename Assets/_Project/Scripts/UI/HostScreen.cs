using System;
using TrainingBuddy.FireBase;
using TrainingBuddy.Managers;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class HostScreen : UILayout
	{
		private Button _createButton;
		
		protected HostScreen(LayoutData layoutData, UIManager uiManager, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.HostScreenVisualTree, "host-wrapper");
			_layoutData.HostScreen = this;
		}
		
		public override void Initialize()
		{
			// throw new System.NotImplementedException();
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
			// _uiManager.Header.Q<Button>("BackButton").RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.RaceMenuScreen));
			
			_createButton = Layout.Q<Button>("CreateButton");
			_createButton.RegisterCallback<ClickEvent>(CreateLobby);
		}
		
		private async void CreateLobby(ClickEvent evt)
		{
			await _databaseManager.CreateLobby(new RaceData
			{
				RaceName = Layout.Q<TextField>("RaceName").value,
				HostName = _databaseManager.Auth.CurrentUser.DisplayName,
				Longitude = 0,
				Latitude = 0,
				Status = 0,
			});
			// ReDrawLayout();
		}
	}
}