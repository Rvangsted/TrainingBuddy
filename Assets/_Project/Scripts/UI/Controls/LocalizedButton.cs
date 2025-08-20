using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI.Controls
{
	[UxmlElement]
	public partial class LocalizedButton : Button
	{
		public static BindingId keyProperty = nameof(key);
		[UxmlAttribute] public string key;
		[UxmlAttribute] public string startTag;
		[UxmlAttribute] public string endTag;

		public LocalizedButton()
		{
			schedule.Execute(() =>
			{
				if (!string.IsNullOrEmpty(key))
				{
					string loc = LocalizationSettings.StringDatabase.GetLocalizedString(key);

					if (!string.IsNullOrEmpty(loc))
					{
						text = $"{startTag}{loc}{endTag}";
						return;
					}

					text = key;
				}
			});
		}
	}
}