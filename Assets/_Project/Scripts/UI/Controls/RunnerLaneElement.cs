using UnityEngine;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI.Controls
{
	/// <summary>
	/// A lane that draws a curved path and moves an animated runner along it.
	/// The path shape is defined by two AnimationCurves (X and Y), both evaluated
	/// over t = 0..1. Coordinates are normalised to the element's resolved size.
	/// </summary>
	public class RunnerLaneElement : VisualElement
	{
		private const float RunnerWidth  = 60f;
		private const float RunnerHeight = 80f;

		private AnimationCurve _pathX;
		private AnimationCurve _pathY;
		private AnimationCurve _sizeCurve;
		private Vector2 _startSize;
		private Vector2 _endSize;

		private readonly VisualElement _runner = new();
		private readonly Label _nameLabel = new();
		private IVisualElementScheduledItem _animSchedule;
		private Texture2D[] _frames;
		private int _fps;
		private int _frameIndex;
		private float _progress;

		public RunnerLaneElement()
		{
			_runner.style.position = Position.Absolute;
			_runner.style.width    = RunnerWidth;
			_runner.style.height   = RunnerHeight;
			_runner.style.backgroundSize    = new BackgroundSize(BackgroundSizeType.Contain);
			_runner.style.unityBackgroundImageTintColor = Color.white;
			_runner.style.overflow = Overflow.Visible;

			_nameLabel.AddToClassList("runner-name");
			_nameLabel.AddToClassList("font-title");
			_nameLabel.style.position          = Position.Absolute;
			_nameLabel.style.bottom            = new Length(90, LengthUnit.Percent);
			_nameLabel.style.left              = 0;
			_nameLabel.style.right             = 0;
			_nameLabel.style.unityTextAlign    = TextAnchor.MiddleLeft;
			_nameLabel.style.whiteSpace        = WhiteSpace.NoWrap;
			_nameLabel.style.overflow          = Overflow.Visible;
			_runner.Add(_nameLabel);

			Add(_runner);

			generateVisualContent += DrawPath;
			RegisterCallback<GeometryChangedEvent>(_ =>
			{
				MarkDirtyRepaint();
				PlaceRunner();
			});
		}

		/// <summary>Call once after construction to supply curves, frames, fps, and a USS class for the runner (e.g. "runner-male").</summary>
		public void Configure(AnimationCurve pathX, AnimationCurve pathY, Texture2D[] frames, int fps, string runnerClass = null,
			Vector2? startSize = null, Vector2? endSize = null, AnimationCurve sizeCurve = null)
		{
			_pathX     = pathX ?? AnimationCurve.Linear(0f, 0.95f, 1f, 0.05f);
			_pathY     = pathY ?? AnimationCurve.Linear(0f, 0.5f,  1f, 0.5f);
			_startSize = startSize ?? new Vector2(RunnerWidth, RunnerHeight);
			_endSize   = endSize   ?? new Vector2(RunnerWidth, RunnerHeight);
			_sizeCurve = sizeCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
			_frames    = frames;
			_fps       = fps;

			if (runnerClass != null)
				_runner.AddToClassList(runnerClass);

			_animSchedule?.Pause();
			_animSchedule = null;

			if (_frames is { Length: > 0 })
			{
				var intervalMs = Mathf.RoundToInt(1000f / Mathf.Max(1, fps));
				AdvanceFrame();
				_animSchedule = _runner.schedule.Execute(AdvanceFrame).Every(intervalMs);
			}

			MarkDirtyRepaint();
			PlaceRunner();
		}

		public void SetName(string playerName) => _nameLabel.text = playerName;

		/// <summary>Move the runner to a normalised position along the path (0 = start, 1 = finish).</summary>
		public void SetProgress(float t)
		{
			_progress = Mathf.Clamp01(t);
			PlaceRunner();
		}

		public void StopAnimation()
		{
			_animSchedule?.Pause();
			_animSchedule = null;
		}

		public void EnsureAnimating()
		{
			if (_animSchedule != null || _frames is not { Length: > 0 }) return;
			var intervalMs = Mathf.RoundToInt(1000f / Mathf.Max(1, _fps));
			_animSchedule = _runner.schedule.Execute(AdvanceFrame).Every(intervalMs);
		}

		// ── private ────────────────────────────────────────────────────────────

		private void AdvanceFrame()
		{
			if (_frames == null || _frames.Length == 0) return;
			var tex = _frames[_frameIndex % _frames.Length];
			if (tex != null)
				_runner.style.backgroundImage = new StyleBackground(tex);
			_frameIndex = (_frameIndex + 1) % _frames.Length;
			MarkDirtyRepaint();
		}

		private void PlaceRunner()
		{
			if (_pathX == null || _pathY == null) return;

			var w = resolvedStyle.width;
			var h = resolvedStyle.height;
			if (w <= 0 || h <= 0) return;

			var t      = _sizeCurve?.Evaluate(_progress) ?? _progress;
			var size   = Vector2.Lerp(_startSize, _endSize, t);

			_runner.style.width  = size.x;
			_runner.style.height = size.y;

			var x = _pathX.Evaluate(_progress) * w - size.x * 0.5f;
			var y = _pathY.Evaluate(_progress) * h - size.y * 0.5f;

			_runner.style.left = x;
			_runner.style.top  = y;
		}

		private void DrawPath(MeshGenerationContext ctx)
		{
			if (_pathX == null || _pathY == null) return;

			var w = resolvedStyle.width;
			var h = resolvedStyle.height;
			if (w <= 0 || h <= 0) return;

			const int samples = 60;
			var painter = ctx.painter2D;
			painter.strokeColor = new Color(1f, 1f, 1f, 0f);
			painter.lineWidth   = 3f;
			painter.lineCap     = LineCap.Round;
			painter.lineJoin    = LineJoin.Round;

			painter.BeginPath();
			for (var i = 0; i <= samples; i++)
			{
				var t = i / (float)samples;
				var pt = new Vector2(_pathX.Evaluate(t) * w, _pathY.Evaluate(t) * h);
				if (i == 0) painter.MoveTo(pt);
				else        painter.LineTo(pt);
			}
			painter.Stroke();
		}
	}
}