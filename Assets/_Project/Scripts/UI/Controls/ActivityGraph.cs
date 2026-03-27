using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI.Controls
{
    [UxmlElement]
    public partial class ActivityGraph : VisualElement
    {

        public struct DataPoint
        {
            public DataPoint(string label, float value)
            {
                Label = label;
                Value = value;
            }

            public string Label { get; }
            public float Value { get; }
        }

        private GraphCanvas _canvas;
        private VisualElement _highlightDot;
        private Label _highlightValueLabel;
        private VisualElement _labelsContainer;
        private readonly List<DataPoint> _dataPoints = new();
        private readonly List<Vector2> _pointPositions = new();
        private readonly List<Vector2> _renderPoints = new();
        private Color _fillTopColor = new(0.78f, 0.63f, 1f, 0.38f);
        private Color _fillBottomColor = new(0.78f, 0.63f, 1f, 0.02f);
        private Color _strokeColor = new(0.525f, 0.325f, 0.941f, 0.18f);
        private Color _highlightStrokeColor = new(0.16f, 0.16f, 0.16f, 0.18f);
        private Color _highlightDotColor = new(0.16f, 0.16f, 0.16f, 1f);

        private const int SamplesPerSegment = 12;
        private const float DefaultLabelOffset = 28f;
        private const float DefaultDashLength = 14f;
        private const float DefaultDashGap = 14f;

        private readonly List<Vector2> _sampledPoints = new();
        private float _strokeWidth = 5f;
        private float _highlightLineWidth = 2f;
        private float _highlightDotSize = 12f;
        private float _valueLabelOffset = DefaultLabelOffset;
        private float _highlightDashLength = DefaultDashLength;
        private float _highlightDashGap = DefaultDashGap;
        private float _topPadding = 48f;
        private float _horizontalPlotPadding = 18f;

        private bool _initialized;
        
        private int _highlightIndex = -1;
        private float _maxValue = 1f;
        private float _smoothness = 0.6f;

        private static readonly CustomStyleProperty<Color> FillTopColorProperty = new("--activity-graph-fill-top-color");
        private static readonly CustomStyleProperty<Color> FillBottomColorProperty = new("--activity-graph-fill-bottom-color");
        private static readonly CustomStyleProperty<Color> StrokeColorProperty = new("--activity-graph-stroke-color");
        private static readonly CustomStyleProperty<float> StrokeWidthProperty = new("--activity-graph-stroke-width");
        private static readonly CustomStyleProperty<Color> HighlightLineColorProperty = new("--activity-graph-highlight-line-color");
        private static readonly CustomStyleProperty<float> HighlightLineWidthProperty = new("--activity-graph-highlight-line-width");
        private static readonly CustomStyleProperty<float> HighlightDashLengthProperty = new("--activity-graph-highlight-dash-length");
        private static readonly CustomStyleProperty<float> HighlightDashGapProperty = new("--activity-graph-highlight-dash-gap");
        private static readonly CustomStyleProperty<Color> HighlightDotColorProperty = new("--activity-graph-highlight-dot-color");
        private static readonly CustomStyleProperty<float> HighlightDotSizeProperty = new("--activity-graph-highlight-dot-size");
        private static readonly CustomStyleProperty<float> ValueLabelOffsetProperty = new("--activity-graph-value-label-offset");
        private static readonly CustomStyleProperty<float> TopPaddingProperty = new("--activity-graph-top-padding");
        private static readonly CustomStyleProperty<float> HorizontalPlotPaddingProperty = new("--activity-graph-horizontal-plot-padding");

        public ActivityGraph()
        {
            AddToClassList("activity-graph");
            style.flexDirection = FlexDirection.Column;
            pickingMode = PickingMode.Ignore;
            RegisterCallback<AttachToPanelEvent>(_ => EnsureInitialized());
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        public Func<float, string> ValueFormatter { get; set; } = value => $"{value:0.#} KM";

        [UxmlAttribute]
        public float Smoothness
        {
            get => _smoothness;
            set
            {
                var clamped = Mathf.Clamp01(value);
                if (!Mathf.Approximately(_smoothness, clamped))
                {
                    _smoothness = clamped;
                    _canvas?.MarkDirtyRepaint();
                }
            }
        }

        public int HighlightIndex
        {
            get => _highlightIndex;
            set
            {
                var clamped = Mathf.Clamp(value, -1, _dataPoints.Count - 1);
                if (_highlightIndex == clamped)
                {
                    return;
                }

                _highlightIndex = clamped;
                _canvas.MarkDirtyRepaint();
                UpdateOverlay();
            }
        }

        public void SetData(IEnumerable<DataPoint> dataPoints, int highlightIndex = -1)
        {
	        EnsureInitialized();
	        
            _dataPoints.Clear();
            if (dataPoints != null)
            {
                _dataPoints.AddRange(dataPoints);
            }

            _labelsContainer.Clear();

            foreach (var dataPoint in _dataPoints)
            {
                var label = new Label(dataPoint.Label);
                label.AddToClassList("activity-graph__label");
                _labelsContainer.Add(label);
            }

            _maxValue = Mathf.Max(1f, _dataPoints.Count == 0 ? 1f : _dataPoints.Max(point => point.Value));

            _canvas.MarkDirtyRepaint();
            HighlightIndex = highlightIndex;

            this.schedule.Execute(UpdateOverlay).StartingIn(16);
        }

        public void Clear()
        {
	        EnsureInitialized();
	        
            _dataPoints.Clear();
            _labelsContainer.Clear();
            HighlightIndex = -1;
            _canvas.MarkDirtyRepaint();
        }

        private void UpdateOverlay()
        {
	        if (!_initialized)
	        {
		        return;
	        }
	        
            if (_canvas == null)
            {
                return;
            }

            var rect = _canvas.contentRect;
            CalculatePointPositions(rect);

            if (_highlightIndex < 0 || _highlightIndex >= _dataPoints.Count || _pointPositions.Count <= _highlightIndex)
            {
                _highlightDot.style.display = DisplayStyle.None;
                _highlightValueLabel.style.display = DisplayStyle.None;
                return;
            }

            var point = _pointPositions[_highlightIndex];

            _highlightDot.style.display = DisplayStyle.Flex;
            var dotRadius = _highlightDotSize * 0.5f;
            _highlightDot.style.left = point.x - dotRadius;
            _highlightDot.style.top = point.y - dotRadius;

            if (ValueFormatter != null)
            {
                _highlightValueLabel.text = ValueFormatter.Invoke(_dataPoints[_highlightIndex].Value);
            }
            else
            {
                _highlightValueLabel.text = _dataPoints[_highlightIndex].Value.ToString("0.#");
            }

            _highlightValueLabel.style.display = DisplayStyle.Flex;

            var rectMin = rect.position;
            var rectMax = rectMin + rect.size;

            var expectedWidth = Mathf.Abs(_highlightValueLabel.layout.width) > 0f
                ? _highlightValueLabel.layout.width
                : _highlightValueLabel.resolvedStyle.width;
            var expectedHeight = Mathf.Abs(_highlightValueLabel.layout.height) > 0f
                ? _highlightValueLabel.layout.height
                : _highlightValueLabel.resolvedStyle.height;
            var labelWidth = !float.IsNaN(expectedWidth) && expectedWidth > 0f ? expectedWidth : 0f;
            var labelHeight = !float.IsNaN(expectedHeight) && expectedHeight > 0f ? expectedHeight : 0f;

            var labelLeft = point.x - labelWidth * 0.5f;
            var labelTop = point.y - _valueLabelOffset - labelHeight;

            labelLeft = Mathf.Clamp(labelLeft, rectMin.x, Mathf.Max(rectMin.x, rectMax.x - labelWidth));
            labelTop = Mathf.Max(rectMin.y, labelTop);

            _highlightValueLabel.style.left = labelLeft;
            _highlightValueLabel.style.top = labelTop;
        }

        private void EnsureInitialized()
        {
	        if (_initialized)
	        {
		        return;
	        }

	        _initialized = true;

	        if (ValueFormatter == null)
	        {
		        ValueFormatter = value => $"{value:0.#} KM";
	        }

	        _canvas = new GraphCanvas(this)
	        {
		        name = "Canvas",
	        };
	        _canvas.AddToClassList("activity-graph__canvas");
	        _canvas.style.flexGrow = 1f;
	        _canvas.pickingMode = PickingMode.Ignore;
	        hierarchy.Add(_canvas);

	        _highlightDot = new VisualElement { name = "HighlightDot" };
	        _highlightDot.AddToClassList("activity-graph__highlight-dot");
	        _highlightDot.style.display = DisplayStyle.None;
	        _highlightDot.style.position = Position.Absolute;
            _highlightDot.style.width = _highlightDotSize;
            _highlightDot.style.height = _highlightDotSize;
            _highlightDot.style.backgroundColor = _highlightDotColor;

	        _highlightValueLabel = new Label { name = "HighlightValueLabel" };
	        _highlightValueLabel.AddToClassList("activity-graph__value-label");
	        _highlightValueLabel.style.display = DisplayStyle.None;
	        _highlightValueLabel.style.position = Position.Absolute;

	        _canvas.Add(_highlightDot);
	        _canvas.Add(_highlightValueLabel);

	        _labelsContainer = new VisualElement { name = "LabelsContainer" };
	        _labelsContainer.AddToClassList("activity-graph__labels");
	        hierarchy.Add(_labelsContainer);

	        _canvas.RegisterCallback<GeometryChangedEvent>(_ => UpdateOverlay());
        }
        
        private void CalculatePointPositions(Rect rect)
        {
            _pointPositions.Clear();

            if (_dataPoints.Count == 0 || rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var maxValue = Mathf.Max(_maxValue, Mathf.Epsilon);
            var drawableHeight = Mathf.Max(0f, rect.height - _topPadding);
            var plotWidth = Mathf.Max(0f, rect.width - (_horizontalPlotPadding * 2f));

            for (var i = 0; i < _dataPoints.Count; i++)
            {
                var normalizedX = _dataPoints.Count == 1 ? 0.5f : (float)i / (_dataPoints.Count - 1);
                var normalizedY = Mathf.Clamp01(_dataPoints[i].Value / maxValue);

                var x = rect.xMin + _horizontalPlotPadding + normalizedX * plotWidth;
                var y = rect.yMax - normalizedY * drawableHeight;

                _pointPositions.Add(new Vector2(x, y));
            }
        }

        private void DrawGraph(MeshGenerationContext context, Rect rect)
        {
            if (_dataPoints.Count < 2 || rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            CalculatePointPositions(rect);

            if (_pointPositions.Count < 2)
            {
                return;
            }

            SampleCurvePoints();

            if (_sampledPoints.Count < 2)
            {
                return;
            }

            DrawFill(context, rect);
            DrawStroke(context);
            DrawHighlightLine(context, rect);
        }

        private void DrawFill(MeshGenerationContext context, Rect rect)
        {
            PrepareRenderPoints(rect);

            var sampleCount = _renderPoints.Count;
            if (sampleCount < 2)
            {
                return;
            }

            var meshWriteData = context.Allocate(sampleCount * 2, (sampleCount - 1) * 6);

            for (var i = 0; i < sampleCount; i++)
            {
                var point = _renderPoints[i];
                var t = sampleCount > 1 ? i / (float)(sampleCount - 1) : 0f;

                var topVertex = new Vertex
                {
                    position = new Vector3(point.x, point.y, Vertex.nearZ),
                    tint = _fillTopColor,
                    uv = new Vector2(t, 0f),
                };

                var bottomVertex = new Vertex
                {
                    position = new Vector3(point.x, rect.yMax, Vertex.nearZ),
                    tint = _fillBottomColor,
                    uv = new Vector2(t, 1f),
                };

                meshWriteData.SetNextVertex(topVertex);
                meshWriteData.SetNextVertex(bottomVertex);
            }

            for (ushort i = 0; i < sampleCount - 1; i++)
            {
                var top0 = (ushort)(i * 2);
                var bottom0 = (ushort)(top0 + 1);
                var top1 = (ushort)((i + 1) * 2);
                var bottom1 = (ushort)(top1 + 1);

                meshWriteData.SetNextIndex(top0);
                meshWriteData.SetNextIndex(top1);
                meshWriteData.SetNextIndex(bottom1);

                meshWriteData.SetNextIndex(top0);
                meshWriteData.SetNextIndex(bottom1);
                meshWriteData.SetNextIndex(bottom0);
            }
        }

        private void DrawStroke(MeshGenerationContext context)
        {
            var sampleCount = _renderPoints.Count;
            if (sampleCount < 2)
            {
                return;
            }

            if (_strokeWidth <= 0f || _strokeColor.a <= 0f)
            {
                return;
            }

            var painter = context.painter2D;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;
            painter.lineWidth = _strokeWidth;
            painter.strokeColor = _strokeColor;

            painter.BeginPath();
            painter.MoveTo(_renderPoints[0]);

            for (var i = 1; i < sampleCount; i++)
            {
                painter.LineTo(_renderPoints[i]);
            }

            painter.Stroke();
        }

        private void PrepareRenderPoints(Rect rect)
        {
            _renderPoints.Clear();

            if (_sampledPoints.Count == 0)
            {
                return;
            }

            var firstPoint = _sampledPoints[0];
            if (firstPoint.x > rect.xMin)
            {
                _renderPoints.Add(new Vector2(rect.xMin, firstPoint.y));
            }

            _renderPoints.AddRange(_sampledPoints);

            var lastPoint = _sampledPoints[^1];
            if (lastPoint.x < rect.xMax)
            {
                _renderPoints.Add(new Vector2(rect.xMax, lastPoint.y));
            }
        }

        private void DrawHighlightLine(MeshGenerationContext context, Rect rect)
        {
            if (_highlightIndex < 0 || _highlightIndex >= _pointPositions.Count)
            {
                return;
            }

            var point = _pointPositions[_highlightIndex];

            if (_highlightLineWidth <= 0f || _highlightStrokeColor.a <= 0f)
            {
                return;
            }

            var painter = context.painter2D;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;
            painter.lineWidth = _highlightLineWidth;
            painter.strokeColor = _highlightStrokeColor;

            DrawDashedLine(
                painter,
                new Vector2(point.x, rect.yMax),
                point,
                Mathf.Max(1f, _highlightDashLength),
                Mathf.Max(1f, _highlightDashGap));
        }

        private static void DrawDashedLine(Painter2D painter, Vector2 start, Vector2 end, float dashLength, float gapLength)
        {
            var direction = end - start;
            var length = direction.magnitude;
            if (length <= Mathf.Epsilon)
            {
                return;
            }

            direction /= length;

            var distance = 0f;
            while (distance < length)
            {
                var dashStart = start + direction * distance;
                var dashEnd = start + direction * Mathf.Min(length, distance + dashLength);
                painter.BeginPath();
                painter.MoveTo(dashStart);
                painter.LineTo(dashEnd);
                painter.Stroke();
                distance += dashLength + gapLength;
            }
        }

        private (Vector2 cp1, Vector2 cp2) GetControlPoints(int segmentIndex)
        {
            var p0 = segmentIndex > 0 ? _pointPositions[segmentIndex - 1] : _pointPositions[segmentIndex];
            var p1 = _pointPositions[segmentIndex];
            var p2 = _pointPositions[segmentIndex + 1];
            var p3 = segmentIndex + 2 < _pointPositions.Count ? _pointPositions[segmentIndex + 2] : _pointPositions[segmentIndex + 1];

            var tension = Mathf.Clamp01(_smoothness);

            var cp1 = p1 + (p2 - p0) * (tension / 6f);
            var cp2 = p2 - (p3 - p1) * (tension / 6f);

            return (cp1, cp2);
        }

        private void SampleCurvePoints()
        {
            _sampledPoints.Clear();

            if (_pointPositions.Count == 0)
            {
                return;
            }

            _sampledPoints.Add(_pointPositions[0]);

            for (var i = 0; i < _pointPositions.Count - 1; i++)
            {
                var start = _pointPositions[i];
                var end = _pointPositions[i + 1];
                var (cp1, cp2) = GetControlPoints(i);

                for (var step = 1; step <= SamplesPerSegment; step++)
                {
                    var t = step / (float)SamplesPerSegment;
                    var sample = EvaluateCubic(start, cp1, cp2, end, t);
                    _sampledPoints.Add(sample);
                }
            }
        }

        private static Vector2 EvaluateCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            var u = 1f - t;
            var tt = t * t;
            var uu = u * u;
            var uuu = uu * u;
            var ttt = tt * t;

            var point = uuu * p0;
            point += 3f * uu * t * p1;
            point += 3f * u * tt * p2;
            point += ttt * p3;

            return point;
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            if (evt.customStyle.TryGetValue(FillTopColorProperty, out var fillTopColor))
            {
                _fillTopColor = fillTopColor;
            }

            if (evt.customStyle.TryGetValue(FillBottomColorProperty, out var fillBottomColor))
            {
                _fillBottomColor = fillBottomColor;
            }

            if (evt.customStyle.TryGetValue(StrokeColorProperty, out var strokeColor))
            {
                _strokeColor = strokeColor;
            }

            if (evt.customStyle.TryGetValue(StrokeWidthProperty, out var strokeWidth))
            {
                _strokeWidth = Mathf.Max(0f, strokeWidth);
            }

            if (evt.customStyle.TryGetValue(HighlightLineColorProperty, out var highlightLineColor))
            {
                _highlightStrokeColor = highlightLineColor;
            }

            if (evt.customStyle.TryGetValue(HighlightLineWidthProperty, out var highlightLineWidth))
            {
                _highlightLineWidth = Mathf.Max(0f, highlightLineWidth);
            }

            if (evt.customStyle.TryGetValue(HighlightDashLengthProperty, out var highlightDashLength))
            {
                _highlightDashLength = Mathf.Max(1f, highlightDashLength);
            }

            if (evt.customStyle.TryGetValue(HighlightDashGapProperty, out var highlightDashGap))
            {
                _highlightDashGap = Mathf.Max(1f, highlightDashGap);
            }

            if (evt.customStyle.TryGetValue(HighlightDotColorProperty, out var highlightDotColor))
            {
                _highlightDotColor = highlightDotColor;
            }

            if (evt.customStyle.TryGetValue(HighlightDotSizeProperty, out var highlightDotSize))
            {
                _highlightDotSize = Mathf.Max(0f, highlightDotSize);
            }

            if (evt.customStyle.TryGetValue(ValueLabelOffsetProperty, out var valueLabelOffset))
            {
                _valueLabelOffset = Mathf.Max(0f, valueLabelOffset);
            }

            if (evt.customStyle.TryGetValue(TopPaddingProperty, out var topPadding))
            {
                _topPadding = Mathf.Max(0f, topPadding);
            }

            if (evt.customStyle.TryGetValue(HorizontalPlotPaddingProperty, out var horizontalPlotPadding))
            {
                _horizontalPlotPadding = Mathf.Max(0f, horizontalPlotPadding);
            }

            if (_highlightDot != null)
            {
                _highlightDot.style.width = _highlightDotSize;
                _highlightDot.style.height = _highlightDotSize;
                _highlightDot.style.backgroundColor = _highlightDotColor;
            }

            _canvas?.MarkDirtyRepaint();
            UpdateOverlay();
        }

        private class GraphCanvas : VisualElement
        {
            private readonly ActivityGraph _owner;

            public GraphCanvas(ActivityGraph owner)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                generateVisualContent += OnGenerateVisualContent;
            }

            private void OnGenerateVisualContent(MeshGenerationContext ctx)
            {
                _owner.DrawGraph(ctx, contentRect);
            }
        }
    }
}
