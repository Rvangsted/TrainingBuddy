using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI.Controls
{
	[UxmlElement]
	public partial class LocalizedLabel : Label
	{
		public static BindingId keyProperty = nameof(key);
		[UxmlAttribute] public string key;

		public LocalizedLabel()
		{
			schedule.Execute(() =>
			{
				if (!string.IsNullOrEmpty(key))
				{
					string loc = LocalizationSettings.StringDatabase.GetLocalizedString(key);

					if (!string.IsNullOrEmpty(loc))
					{
						text = $"{loc}";
						return;
					}

					text = key;
				}
			});
		}
	}
}