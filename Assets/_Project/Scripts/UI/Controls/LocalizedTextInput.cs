using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI.Controls
{
	[UxmlElement]
	public partial class LocalizedTextInput : TextField
	{
		public static BindingId keyProperty = nameof(key);
		[UxmlAttribute] public string key;
		
		private readonly VisualElement _input;

		public LocalizedTextInput()
		{
			_input = this.Q(className: inputUssClassName);
			
			RegisterCallback<InputEvent>(OnInputEvent);
			
			schedule.Execute(() =>
			{
				if (!string.IsNullOrEmpty(key))
				{
					string loc = LocalizationSettings.StringDatabase.GetLocalizedString(key);

					if (!string.IsNullOrEmpty(loc))
					{
						textEdition.placeholder = loc;
						return;
					}

					textEdition.placeholder = key;
				}
			});
		}
		
		private void OnInputEvent(InputEvent evt)
		{
			if (!string.IsNullOrEmpty(text))
			{
				_input.AddToClassList("has-value");
			} else {
				_input.RemoveFromClassList("has-value");
			}
		}
	}
} 