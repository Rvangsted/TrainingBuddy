using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class LobbyScreen : UILayout
	{
		protected LobbyScreen(LayoutData layoutData, UIManager uiManager) : base(layoutData, uiManager)
		{
			ConfigureLayoutAsset(_layoutData.LobbyScreenVisualTree, "lobby-wrapper");
			_layoutData.LobbyScreen = this;
		}
		
		public override void Initialize()
		{
			// throw new System.NotImplementedException();
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
			_uiManager.Header.Q<Button>("BackButton").RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.MainMenu));
		}
	}
}