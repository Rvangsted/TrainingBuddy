using TrainingBuddy.UI.Controls;
using TrainingBuddy.UI.Effects;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace TrainingBuddy.UI
{
	public class UILayout : IInitializable
	{
		public VisualElement Layout { get; set; }
		
		protected UIManager _uiManager;
		protected LayoutData _layoutData;
		private bool _layoutDrawn;

		protected UILayout(LayoutData layoutData, UIManager uiManager)
		{
			_layoutData = layoutData;
			_uiManager = uiManager;
		}

		public virtual void Initialize() {}
		
		protected virtual void ReDrawLayout()
		{
			_layoutDrawn = false;
			DrawLayout();
		}

		public virtual void DrawLayout()
		{
			if (_layoutDrawn)
			{
				return;
			}
			_layoutDrawn = true;
		}
		
		protected Shadow ShadowButton(string buttonName, string key, ShadowSettings settings)
		{
			var buttonShadow = new Shadow()
			{
				name = $"{buttonName}Shadow",
				shadowCornerRadius = settings.CornerRadius,
				shadowScale = settings.ShadowScale,
				shadowOffsetX = settings.ShadowOffsetX,
				shadowOffsetY = settings.ShadowOffsetY,
			};
			buttonShadow.AddToClassList("shadow-button");
			
			var button = new LocalizedButton
			{
				name = buttonName,
				key = key,
			};
			button.AddToClassList("button-large");
			
			buttonShadow.Add(button);

			return buttonShadow;
		}
		
		protected LocalizedButton TextButton(string buttonName, string key, string startTag = "", string endTag = "")
		{
			var button = new LocalizedButton
			{
				name = buttonName,
				key = key,
				startTag = startTag,
				endTag = endTag,
			};
			button.AddToClassList("text-button");

			return button;
		}
	}

	public class ShadowSettings
	{
		public int CornerRadius { get; set; }
		public float ShadowScale { get; set; }
		public int ShadowOffsetX { get; set; }
		public int ShadowOffsetY { get; set; }
	}
}