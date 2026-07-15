using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace PocketMC.App.Controls
{
    public class MetricGraphControl : Control
    {
        private List<double> _renderedPoints = new();
        private List<double> _startPoints = new();
        private List<double> _targetPoints = new();

        private DispatcherTimer? _animationTimer;
        private DateTime _animationStartTime;
        private const double AnimationDurationMs = 250.0; // 250ms smooth transition

        public static readonly StyledProperty<IEnumerable<double>?> PointsProperty =
            AvaloniaProperty.Register<MetricGraphControl, IEnumerable<double>?>(nameof(Points));

        public static readonly StyledProperty<IBrush> LineBrushProperty =
            AvaloniaProperty.Register<MetricGraphControl, IBrush>(nameof(LineBrush), Brushes.DodgerBlue);

        public static readonly StyledProperty<IBrush> FillBrushProperty =
            AvaloniaProperty.Register<MetricGraphControl, IBrush>(nameof(FillBrush), 
                new SolidColorBrush(Color.FromArgb(40, 30, 144, 255)));

        static MetricGraphControl()
        {
            AffectsRender<MetricGraphControl>(PointsProperty, LineBrushProperty, FillBrushProperty);
        }

        public IEnumerable<double>? Points
        {
            get => GetValue(PointsProperty);
            set => SetValue(PointsProperty, value);
        }

        public IBrush LineBrush
        {
            get => GetValue(LineBrushProperty);
            set => SetValue(LineBrushProperty, value);
        }

        public IBrush FillBrush
        {
            get => GetValue(FillBrushProperty);
            set => SetValue(FillBrushProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == PointsProperty)
            {
                var newPoints = change.GetNewValue<IEnumerable<double>?>()?.ToList();
                if (newPoints != null)
                {
                    StartTransition(newPoints);
                }
            }
        }

        private void StartTransition(List<double> targetPoints)
        {
            _targetPoints = targetPoints;

            if (_renderedPoints.Count == 0 || _renderedPoints.Count != targetPoints.Count)
            {
                _renderedPoints = new List<double>(targetPoints);
                InvalidateVisual();
                return;
            }

            _startPoints = new List<double>(_renderedPoints);
            _animationStartTime = DateTime.UtcNow;

            if (_animationTimer == null)
            {
                _animationTimer = new DispatcherTimer(
                    TimeSpan.FromMilliseconds(16),
                    DispatcherPriority.Render,
                    OnAnimationTick
                );
            }

            if (!_animationTimer.IsEnabled)
            {
                _animationTimer.Start();
            }
        }

        private void OnAnimationTick(object? sender, EventArgs e)
        {
            var elapsed = (DateTime.UtcNow - _animationStartTime).TotalMilliseconds;
            var progress = Math.Clamp(elapsed / AnimationDurationMs, 0.0, 1.0);

            double easeProgress = EaseOutCubic(progress);

            for (int i = 0; i < _renderedPoints.Count; i++)
            {
                double start = _startPoints[i];
                double target = _targetPoints[i];
                _renderedPoints[i] = start + (target - start) * easeProgress;
            }

            InvalidateVisual();

            if (progress >= 1.0)
            {
                _animationTimer?.Stop();
            }
        }

        private double EaseOutCubic(double x) => 1.0 - Math.Pow(1.0 - x, 3);

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (_renderedPoints == null || _renderedPoints.Count < 2) return;

            double width = Bounds.Width;
            double height = Bounds.Height;
            if (width <= 0 || height <= 0) return;

            // Percentage limits (0% to 100%)
            double min = 0;
            double max = 100;
            double stepX = width / (_renderedPoints.Count - 1);

            var lineGeometry = new StreamGeometry();
            using (var lineCtx = lineGeometry.Open())
            {
                var areaGeometry = new StreamGeometry();
                using (var areaCtx = areaGeometry.Open())
                {
                    double startY = height - (_renderedPoints[0] - min) / (max - min) * height;
                    startY = Math.Clamp(startY, 0, height);

                    var startPoint = new Point(0, startY);
                    lineCtx.BeginFigure(startPoint, false);
                    
                    areaCtx.BeginFigure(new Point(0, height), true);
                    areaCtx.LineTo(startPoint);

                    for (int i = 1; i < _renderedPoints.Count; i++)
                    {
                        double x = i * stepX;
                        double y = height - (_renderedPoints[i] - min) / (max - min) * height;
                        y = Math.Clamp(y, 0, height);

                        var nextPoint = new Point(x, y);
                        lineCtx.LineTo(nextPoint);
                        areaCtx.LineTo(nextPoint);
                    }

                    lineCtx.EndFigure(false);

                    areaCtx.LineTo(new Point(width, height));
                    areaCtx.EndFigure(true);

                    // Draw area fill
                    context.DrawGeometry(FillBrush, null, areaGeometry);
                }

                // Draw outline path
                var pen = new Pen(LineBrush, 2);
                context.DrawGeometry(null, pen, lineGeometry);
            }
        }
    }
}
