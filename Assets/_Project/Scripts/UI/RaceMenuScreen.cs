using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class RaceMenuScreen : UILayout
	{
		protected RaceMenuScreen(LayoutData layoutData, UIManager uiManager) : base(layoutData, uiManager, layoutData.RaceMenuScreenVisualTree)
		{
			_layoutData.RaceMenuScreen = this;
		}

		protected override void OnLayoutBuilt(VisualElement root)
		{
			root.AddToClassList("race-menu-wrapper");
		}

		public override void Initialize()
		{
			base.Initialize();
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