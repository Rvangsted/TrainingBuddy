

using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class HighScoreScreen : UILayout
	{
		protected HighScoreScreen(LayoutData layoutData, UIManager uiManager) : base(layoutData, uiManager)
		{
			Layout = _layoutData.HighScoreVisualTree.Instantiate();
			Layout.AddToClassList("highscore-wrapper");
			_layoutData.HighScoreScreen = this;
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