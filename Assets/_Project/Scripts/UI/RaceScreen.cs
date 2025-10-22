using UnityEngine.UIElements;

namespace TrainingBuddy.UI
{
	public class RaceScreen : UILayout
	{
		protected RaceScreen(LayoutData layoutData, UIManager uiManager) : base(layoutData, uiManager, layoutData.RaceScreenVisualTree)
		{
			_layoutData.RaceScreen = this;
		}

		protected override void OnLayoutBuilt(VisualElement root)
		{
			root.AddToClassList("race-wrapper");
		}

		public override void Initialize()
		{
			base.Initialize();
		}

		public override void DrawLayout()
		{
			base.DrawLayout();
			_uiManager.Header.Q<Button>("BackButton").RegisterCallback<ClickEvent>(_ => _uiManager.ChangePage(_layoutData.RaceMenuScreen));
		}
	}
}