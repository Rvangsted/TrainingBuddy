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

        private readonly GraphCanvas _canvas;
        private readonly VisualElement _highlightDot;
        private readonly Label _highlightValueLabel;
        private readonly VisualElement _labelsContainer;
        private readonly List<DataPoint> _dataPoints = new();
        private readonly List<Vector2> _pointPositions = new();
        private readonly Color _fillTopColor = new(0.733f, 0.592f, 0.996f, 0.78f);
        private readonly Color _fillBottomColor = new(0.733f, 0.592f, 0.996f, 0.0f);
        private readonly Color _strokeColor = new(0.525f, 0.325f, 0.941f, 1f);
        private readonly Color _highlightStrokeColor = new(0f, 0f, 0f, 0.35f);

        private const float StrokeWidth = 5f;
        private const int SamplesPerSegment = 12;

        private readonly List<Vector2> _sampledPoints = new();

        private int _highlightIndex = -1;
        private float _maxValue = 1f;
        private float _smoothness = 0.6f;

        public ActivityGraph()
        {
            AddToClassList("activity-graph");
            style.flexDirection = FlexDirection.Column;
            pickingMode = PickingMode.Ignore;

            ValueFormatter = value => $"{value:0.#} KM";

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

        public Func<float, string> ValueFormatter { get; set; }

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
            _dataPoints.Clear();
            _labelsContainer.Clear();
            HighlightIndex = -1;
            _canvas.MarkDirtyRepaint();
        }

        private void UpdateOverlay()
        {
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
            _highlightDot.style.left = point.x - 6f;
            _highlightDot.style.top = point.y - 6f;

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

            var labelLeft = point.x + 14f;
            var labelTop = Mathf.Max(rectMin.y, point.y - 48f);

            var expectedWidth = Mathf.Abs(_highlightValueLabel.layout.width) > 0f
                ? _highlightValueLabel.layout.width
                : _highlightValueLabel.resolvedStyle.width;

            if (!float.IsNaN(expectedWidth) && expectedWidth > 0f && labelLeft + expectedWidth > rectMax.x)
            {
                labelLeft = point.x - expectedWidth - 14f;
            }

            _highlightValueLabel.style.left = labelLeft;
            _highlightValueLabel.style.top = labelTop;
        }

        private void CalculatePointPositions(Rect rect)
        {
            _pointPositions.Clear();

            if (_dataPoints.Count == 0 || rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var maxValue = Mathf.Max(_maxValue, Mathf.Epsilon);

            for (var i = 0; i < _dataPoints.Count; i++)
            {
                var normalizedX = _dataPoints.Count == 1 ? 0.5f : (float)i / (_dataPoints.Count - 1);
                var normalizedY = Mathf.Clamp01(_dataPoints[i].Value / maxValue);

                var x = rect.xMin + normalizedX * rect.width;
                var y = rect.yMax - normalizedY * rect.height;

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
            var sampleCount = _sampledPoints.Count;
            if (sampleCount < 2)
            {
                return;
            }

            var meshWriteData = context.Allocate(sampleCount * 2, (sampleCount - 1) * 6);

            for (var i = 0; i < sampleCount; i++)
            {
                var point = _sampledPoints[i];
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
            var painter = context.painter2D;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;
            painter.lineWidth = StrokeWidth;
            painter.strokeColor = _strokeColor;

            painter.BeginPath();
            painter.MoveTo(_sampledPoints[0]);

            for (var i = 1; i < _sampledPoints.Count; i++)
            {
                painter.LineTo(_sampledPoints[i]);
            }

            painter.Stroke();
        }

        private void DrawHighlightLine(MeshGenerationContext context, Rect rect)
        {
            if (_highlightIndex < 0 || _highlightIndex >= _pointPositions.Count)
            {
                return;
            }

            var point = _pointPositions[_highlightIndex];

            var painter = context.painter2D;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;
            painter.lineWidth = 2f;
            painter.strokeColor = _highlightStrokeColor;

            painter.BeginPath();
            painter.MoveTo(new Vector2(point.x, rect.yMax));
            painter.LineTo(point);
            painter.Stroke();
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