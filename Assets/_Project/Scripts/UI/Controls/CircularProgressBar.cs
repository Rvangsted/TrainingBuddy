using UnityEngine;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI.Controls
{
	[UxmlElement]
    public partial class CircularProgressBar : VisualElement
    {
        public static BindingId valueProperty = nameof(Value);
        float m_Value;

        [UxmlAttribute]
        public float Value
        {
            get => m_Value;
            set
            {
                m_Value = Mathf.Clamp01(value);
                MarkDirtyRepaint();
            }
        }

        [UxmlAttribute]
        public Color TrackColor { get; set; } = new Color(0.9f, 0.9f, 0.9f, 1f);

        [UxmlAttribute]
        public Color ProgressColor { get; set; } = new Color(0.82f, 1f, 0f, 1f);

        [UxmlAttribute]
        public Color KnobColor { get; set; } = new Color(0.78f, 0.98f, 0f, 1f);

        [UxmlAttribute]
        public Color InnerKnobColor { get; set; } = Color.white;

        [UxmlAttribute]
        public Color ArrowColor { get; set; } = Color.black;

        [UxmlAttribute]
        public float LineThickness { get; set; } = 20f;

        [UxmlAttribute]
        public float KnobSize { get; set; } = 15f;

        [UxmlAttribute]
        public float InnerKnobSize { get; set; } = 5f;

        public CircularProgressBar()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        void OnGenerateVisualContent(MeshGenerationContext context)
        {
            var rect = contentRect;
            float r = Mathf.Min(rect.width, rect.height) * 0.5f;
            Vector2 center = rect.center;

            var painter = context.painter2D;
            float thickness = resolvedStyle.borderTopWidth > 0 ? resolvedStyle.borderTopWidth : LineThickness;
            painter.lineWidth = thickness;
            painter.lineCap = LineCap.Round;

            // background circle
            painter.strokeColor = TrackColor;
            painter.BeginPath();
            painter.Arc(center, r, 270, 630);
            painter.Stroke();

            // progress arc
            float startAngle = 270f;
            float endAngle = startAngle + 360f * m_Value;
            painter.strokeColor = ProgressColor;
            painter.BeginPath();
            painter.Arc(center, r, startAngle, endAngle);
            painter.Stroke();

            // knob at end of progress
            Vector2 end = center + new Vector2(Mathf.Cos(endAngle * Mathf.Deg2Rad), Mathf.Sin(endAngle * Mathf.Deg2Rad)) * r;
            painter.fillColor = KnobColor;
            painter.BeginPath();
            float knobRadius = KnobSize > 0 ? KnobSize : thickness * 0.6f;
            painter.Arc(end, knobRadius, 0, 360);
            painter.Fill();

            // inner knob inside the main knob
            painter.fillColor = InnerKnobColor;
            painter.BeginPath();
            float innerRadius = InnerKnobSize > 0 ? InnerKnobSize : knobRadius * 0.5f;
            painter.Arc(end, innerRadius, 0, 360);
            painter.Fill();

            // arrow inside the circle pointing to the knob
            painter.fillColor = ArrowColor;
            painter.BeginPath();
            Vector2 dir = (end - center).normalized;
            float arrowLength = knobRadius;
            float arrowWidth = arrowLength * 0.8f;
            Vector2 tip = end - dir * (knobRadius + 5f);
            Vector2 baseCenter = tip - dir * arrowLength;
            Vector2 perp = new Vector2(-dir.y, dir.x) * (arrowWidth * 0.6f);
            Vector2 b = baseCenter - perp;
            Vector2 c = baseCenter + perp;
            painter.MoveTo(tip);
            painter.LineTo(b);
            painter.LineTo(c);
            painter.ClosePath();
            painter.Fill();
        }
    }
}