using Firebase.Database;
using TrainingBuddy.Managers;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace TrainingBuddy.UI
{
	public class UILayout : IInitializable
	{
		private VisualElement _layout;
		private VisualTreeAsset _layoutAsset;
		private string[] _layoutClassNames;

		public VisualElement Layout
		{
			get
			{
				EnsureLayout();
				return _layout;
			}
			protected set => _layout = value;
		}

		protected readonly UIManager _uiManager;
		protected readonly LayoutData _layoutData;
		protected readonly DatabaseManager _databaseManager;
		private DataSnapshot _dataSnapshot;
		protected bool _layoutDrawn;

		protected UILayout(LayoutData layoutData, UIManager uiManager, DatabaseManager databaseManager)
		{
			_layoutData = layoutData;
			_uiManager = uiManager;
			_databaseManager = databaseManager;
		}

		public virtual void Initialize() {}

		protected void ConfigureLayoutAsset(VisualTreeAsset asset, params string[] classNames)
		{
			_layoutAsset = asset;
			_layoutClassNames = classNames;
		}

		private void EnsureLayout()
		{
			if (_layout != null || _layoutAsset == null)
			{
				return;
			}

			_layout = _layoutAsset.Instantiate();

			if (_layoutClassNames == null)
			{
				return;
			}

			foreach (var className in _layoutClassNames)
			{
				if (string.IsNullOrEmpty(className))
				{
					continue;
				}

				_layout.AddToClassList(className);
			}
		}
		
		protected virtual void ReDrawLayout()
		{
			_layoutDrawn = false;
			DrawLayout();
		}

		public virtual async void DrawLayout()
		{
			var levelLabel = _uiManager.Header?.Q<Label>("HeaderLevelLabel");

			if (_databaseManager.Auth != null)
			{
				_dataSnapshot = await _databaseManager.FetchUserData(_databaseManager.Auth.CurrentUser);

				if (levelLabel != null)
				{
					var levelValue = _dataSnapshot?.Child("Level")?.Value?.ToString();

					if (string.IsNullOrEmpty(levelValue))
					{
						levelLabel.AddToClassList("hidden");
					}
					else
					{
						levelLabel.RemoveFromClassList("hidden");
						levelLabel.text = levelValue;
					}
				}
			}
			else if (levelLabel != null)
			{
				levelLabel.AddToClassList("hidden");
			}

			if (_layoutDrawn)
			{
				return;
			}

			_layoutDrawn = true;
		}
	}
}