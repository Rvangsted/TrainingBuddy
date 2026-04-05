using System.Collections.Generic;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI.Controls
{
	[UxmlElement]
	public partial class LocalizedRadioButtonGroup : RadioButtonGroup
	{
		public static BindingId choiceKeysProperty = nameof(choiceKeys);
		[UxmlAttribute] public string choiceKeys;

		public LocalizedRadioButtonGroup()
		{
			schedule.Execute(() =>
			{
				if (string.IsNullOrEmpty(choiceKeys)) return;

				var keys = choiceKeys.Split(',');
				var localized = new List<string>(keys.Length);

				foreach (var k in keys)
				{
					var trimmed = k.Trim();
					var loc = LocalizationSettings.StringDatabase.GetLocalizedString(trimmed);
					localized.Add(!string.IsNullOrEmpty(loc) ? loc : trimmed);
				}

				choices = localized;
			});
		}
	}
}