using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI.Controls
{
	/// <summary>
	/// Lightweight, non-blocking, auto-dismissing notification — see NotificationToast_Scope.md.
	/// Unlike UniversalOverlay, this never blocks input and never needs an explicit dismiss tap;
	/// it's for passive "something happened in the background" moments (a referral reward
	/// landing), not anything the player needs to acknowledge. Message text only for v1 — no
	/// title, no buttons, no image.
	/// </summary>
	[DisallowMultipleComponent]
	public class ToastNotification : MonoBehaviour
	{
		[SerializeField] private UIDocument _uiDocument;
		[SerializeField] private VisualTreeAsset _toastAsset;

		private const int VisibleDurationMs = 3000;
		private const int TransitionMs = 300; // Keep >= the USS transition-duration on .toast-card

		private VisualElement _toastRoot;
		private Label _messageLabel;
		private readonly Queue<string> _pending = new();
		private bool _isShowing;

		public void Configure(UIDocument uiDocument, VisualTreeAsset toastAsset)
		{
			_uiDocument = uiDocument;
			_toastAsset = toastAsset;

			if (isActiveAndEnabled)
			{
				Initialize();
			}
		}

		private void Awake()
		{
			if (_uiDocument == null)
			{
				_uiDocument = GetComponent<UIDocument>();
			}

			Initialize();
		}

		private void OnEnable()
		{
			Initialize();
		}

		/// <summary>
		/// Shows a toast, or queues it if one is already showing — a second toast firing while
		/// one is visible is shown after the first dismisses, never overwritten and never shown
		/// simultaneously (deliberately different from UniversalOverlay's overwrite-on-second-call
		/// behavior, since toasts are expected to fire more casually/often).
		/// </summary>
		public void Show(string message)
		{
			if (string.IsNullOrEmpty(message)) return;

			Initialize();
			if (_toastRoot == null) return;

			if (_isShowing)
			{
				_pending.Enqueue(message);
				return;
			}

			DisplayMessage(message);
		}

		private void DisplayMessage(string message)
		{
			_isShowing = true;
			_messageLabel.text = message;

			_toastRoot.style.display = DisplayStyle.Flex;
			_toastRoot.BringToFront();

			// Start from the hidden state and flip to visible a frame later so the USS
			// transition actually plays instead of snapping straight to the visible state.
			_toastRoot.RemoveFromClassList("toast-root--visible");
			_toastRoot.schedule.Execute(() => _toastRoot.AddToClassList("toast-root--visible")).ExecuteLater(1);

			_toastRoot.schedule.Execute(HideCurrent).ExecuteLater(VisibleDurationMs);
		}

		private void HideCurrent()
		{
			_toastRoot.RemoveFromClassList("toast-root--visible");
			_toastRoot.schedule.Execute(AdvanceQueue).ExecuteLater(TransitionMs);
		}

		private void AdvanceQueue()
		{
			_toastRoot.style.display = DisplayStyle.None;
			_isShowing = false;

			if (_pending.Count > 0)
			{
				DisplayMessage(_pending.Dequeue());
			}
		}

		private void Initialize()
		{
			if (_toastRoot != null) return;
			if (_toastAsset == null || _uiDocument == null) return;

			_toastRoot = _toastAsset.Instantiate();
			_toastRoot.name = "ToastNotification";
			_toastRoot.style.display = DisplayStyle.None;
			_toastRoot.pickingMode = PickingMode.Ignore;
			_toastRoot.StretchToParentSize();

			_messageLabel = _toastRoot.Q<Label>("ToastMessage");

			var root = _uiDocument.rootVisualElement;
			root.Add(_toastRoot);
			_toastRoot.BringToFront();
		}
	}
}
