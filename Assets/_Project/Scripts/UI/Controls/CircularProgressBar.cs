using UnityEngine;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI.Controls
{
	[UxmlElement]
    public partial class CircularProgressBar : VisualElement
    {
	    public static readonly BindingId valueProperty = nameof(Value);

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

        [UxmlAttribute] public Color TrackColor { get; set; } = new Color(0.88f, 0.88f, 0.88f, 1f);
        [UxmlAttribute] public Color ProgressColor { get; set; } = new Color(0.76f, 1f, 0f, 1f);
        [UxmlAttribute] public Color KnobColor { get; set; } = new Color(0.68f, 0.94f, 0f, 1f);
        [UxmlAttribute] public Color InnerKnobColor { get; set; } = Color.clear;
        [UxmlAttribute] public Color ArrowColor { get; set; } = Color.black;

        [UxmlAttribute] public float LineThickness { get; set; } = 22f;
        [UxmlAttribute] public float StartAngle { get; set; } = 270f;
        [UxmlAttribute] public float KnobSize { get; set; } = 18f;
        [UxmlAttribute] public float InnerKnobSize { get; set; } = 6f;
        [UxmlAttribute] public bool ShowKnob { get; set; } = true;
        [UxmlAttribute] public bool ShowArrow { get; set; } = true;
        [UxmlAttribute] public float ArrowLength { get; set; } = 18f;
        [UxmlAttribute] public float ArrowWidth { get; set; } = 12f;
        [UxmlAttribute] public float ArrowOffset { get; set; } = 6f;

        public CircularProgressBar()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        void OnGenerateVisualContent(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
	            return;
            }

            var painter = context.painter2D;
            
            float thickness = resolvedStyle.borderTopWidth > 0f ? resolvedStyle.borderTopWidth : Mathf.Max(0f, LineThickness);
            float radius = Mathf.Max(0f, Mathf.Min(rect.width, rect.height) * 0.5f - thickness * 0.5f);

            if (radius <= 0f)
            {
	            return;
            }

            Vector2 center = rect.center;

            painter.lineWidth = thickness;
            painter.lineCap = LineCap.Round;

            float startAngle = StartAngle;
            float endAngle = startAngle + 360f * m_Value;
            // Draw background track
            if (TrackColor.a > 0f)
            {
                painter.strokeColor = TrackColor;
                painter.BeginPath();
                painter.Arc(center, radius, startAngle, startAngle + 360f);
                painter.Stroke();
            }

            // Draw progress arc
            if (m_Value > 0f && ProgressColor.a > 0f)
            {
                painter.strokeColor = ProgressColor;
                painter.BeginPath();
                painter.Arc(center, radius, startAngle, endAngle);
                painter.Stroke();
            }

            if (!ShowKnob && !ShowArrow)
            {
                return;
            }

            Vector2 direction = new Vector2(Mathf.Cos(endAngle * Mathf.Deg2Rad), Mathf.Sin(endAngle * Mathf.Deg2Rad));
            Vector2 knobCenter = center + direction * radius;

            // Draw knob at the end of the progress arc
            if (ShowKnob && ProgressColor.a > 0f)
            {
                float knobRadius = KnobSize > 0f ? KnobSize : thickness * 0.6f;
                painter.fillColor = KnobColor;
                painter.BeginPath();
                painter.Arc(knobCenter, knobRadius, 0f, 360f);
                painter.Fill();

                if (InnerKnobColor.a > 0f)
                {
                    float innerRadius = InnerKnobSize > 0f ? InnerKnobSize : knobRadius * 0.5f;
                    painter.fillColor = InnerKnobColor;
                    painter.BeginPath();
                    painter.Arc(knobCenter, innerRadius, 0f, 360f);
                    painter.Fill();
                }
            }

            // Draw arrow pointing towards the knob from the inside of the circle
            if (ShowArrow && ArrowColor.a > 0f)
            {
                float knobRadius = KnobSize > 0f ? KnobSize : thickness * 0.6f;
                float arrowLength = ArrowLength > 0f ? ArrowLength : knobRadius;
                float arrowHalfWidth = Mathf.Max(0f, ArrowWidth > 0f ? ArrowWidth * 0.5f : knobRadius * 0.4f);
                Vector2 tip = knobCenter - direction * (knobRadius + Mathf.Max(0f, ArrowOffset));
                Vector2 baseCenter = tip - direction * arrowLength;
                Vector2 perpendicular = new Vector2(-direction.y, direction.x) * arrowHalfWidth;

                painter.fillColor = ArrowColor;
                painter.BeginPath();
                painter.MoveTo(tip);
                painter.LineTo(baseCenter - perpendicular);
                painter.LineTo(baseCenter + perpendicular);
                painter.ClosePath();
                painter.Fill();
            }
        }
    }
}