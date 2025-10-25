using TrainingBuddy.Managers;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class RaceMenuScreen : UILayout
	{
		protected RaceMenuScreen(LayoutData layoutData, UIManager uiManager, DatabaseManager databaseManager) : base(layoutData, uiManager, databaseManager)
		{
			ConfigureLayoutAsset(_layoutData.RaceMenuScreenVisualTree, "race-menu-wrapper");
			_layoutData.RaceMenuScreen = this;
		}
		
		public override void Initialize()
		{
			// throw new System.NotImplementedException();
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
			_uiManager.Header.Q<Button>("BackButton").RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.MainMenu));
			
			Layout.Q<Button>("HostButton").RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.HostScreen));
			Layout.Q<Button>("FindRace").RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.FindLobbyScreen));
			Layout.Q<Button>("FindRace").RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.RaceScreen));
		}
	}
}