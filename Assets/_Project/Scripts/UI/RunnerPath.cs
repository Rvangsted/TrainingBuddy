using System;
using UnityEngine;

namespace TrainingBuddy.UI
{
	[Serializable]
	public class RunnerPath
	{
		[Tooltip("Normalised X position within the lane (0 = left edge, 1 = right edge) evaluated at progress 0..1")]
		public AnimationCurve X = AnimationCurve.Linear(0f, 0.95f, 1f, 0.05f);

		[Tooltip("Normalised Y position within the lane (0 = top, 1 = bottom) evaluated at progress 0..1")]
		public AnimationCurve Y = AnimationCurve.Linear(0f, 0.5f, 1f, 0.5f);

		[Tooltip("Pixel size of the runner at progress = 0")]
		public Vector2 StartSize = new Vector2(60f, 80f);

		[Tooltip("Pixel size of the runner at progress = 1")]
		public Vector2 EndSize = new Vector2(60f, 80f);

		[Tooltip("Controls how size transitions from StartSize to EndSize over progress 0..1")]
		public AnimationCurve SizeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
	}
}