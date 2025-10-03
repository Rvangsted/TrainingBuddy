using UnityEngine;
using UnityEngine.UIElements;

namespace TrainingBuddy.UI.Controls
{
    [UxmlElement]
    public partial class LinearProgressBar : VisualElement
    {
        public static readonly BindingId valueProperty = nameof(Value);

        float m_Value;

        [UxmlAttribute]
        public float Value
        {
            get => m_Value;
            set
            {
                var clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(m_Value, clamped))
                {
                    return;
                }

                m_Value = clamped;
                MarkDirtyRepaint();
            }
        }

        [UxmlAttribute] public Color TrackColor { get; set; } = new Color(0.88f, 0.88f, 0.88f, 1f);
        [UxmlAttribute] public Color ProgressColor { get; set; } = new Color(0.76f, 1f, 0f, 1f);
        [UxmlAttribute] public Color KnobColor { get; set; } = new Color(0.68f, 0.94f, 0f, 1f);
        [UxmlAttribute] public Color InnerKnobColor { get; set; } = Color.clear;

        [UxmlAttribute] public float LineThickness { get; set; } = 22f;
        [UxmlAttribute] public bool ShowTrack { get; set; } = true;
        [UxmlAttribute] public bool ShowProgress { get; set; } = true;
        [UxmlAttribute] public bool ShowKnob { get; set; } = true;
        [UxmlAttribute] public bool UseRoundedCaps { get; set; } = true;
        [UxmlAttribute] public float KnobSize { get; set; } = 18f;
        [UxmlAttribute] public float InnerKnobSize { get; set; } = 6f;
        [UxmlAttribute] public float Padding { get; set; } = 0f;

        public LinearProgressBar()
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
            float halfThickness = thickness * 0.5f;

            float horizontalPadding = Mathf.Max(0f, Padding) + halfThickness;
            float startX = rect.xMin + horizontalPadding;
            float endX = rect.xMax - horizontalPadding;
            float centerY = rect.center.y;

            if (endX <= startX)
            {
                return;
            }

            painter.lineWidth = thickness;
            painter.lineCap = UseRoundedCaps ? LineCap.Round : LineCap.Butt;

            float trackLength = endX - startX;
            float progressLength = trackLength * Mathf.Clamp01(m_Value);
            float progressEndX = startX + progressLength;

            if (ShowTrack && TrackColor.a > 0f)
            {
                painter.strokeColor = TrackColor;
                painter.BeginPath();
                painter.MoveTo(new Vector2(startX, centerY));
                painter.LineTo(new Vector2(endX, centerY));
                painter.Stroke();
            }

            if (ShowProgress && ProgressColor.a > 0f && progressLength > 0f)
            {
                painter.strokeColor = ProgressColor;
                painter.BeginPath();
                painter.MoveTo(new Vector2(startX, centerY));
                painter.LineTo(new Vector2(progressEndX, centerY));
                painter.Stroke();
            }

            if (!ShowKnob)
            {
                return;
            }

            float knobRadius = KnobSize > 0f ? KnobSize : thickness * 0.6f;
            float knobCenterX = progressLength > 0f ? progressEndX : startX;
            var knobCenter = new Vector2(knobCenterX, centerY);

            painter.fillColor = KnobColor;
            painter.BeginPath();
            painter.Arc(knobCenter, knobRadius, 0f, 360f);
            painter.Fill();

            if (InnerKnobColor.a <= 0f)
            {
                return;
            }

            float innerRadius = InnerKnobSize > 0f ? InnerKnobSize : knobRadius * 0.5f;
            painter.fillColor = InnerKnobColor;
            painter.BeginPath();
            painter.Arc(knobCenter, innerRadius, 0f, 360f);
            painter.Fill();
        }
    }
}