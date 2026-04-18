using Swof.Engine;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NumericsVector2 = System.Numerics.Vector2;

namespace Swof.Windows
{
    public partial class MainWindow : Window
    {
        private readonly MatchEngine _engine = MatchEngine.CreateDefault();
        private readonly DispatcherTimer _timer = new();
        private readonly HashSet<Key> _keysDown = new();
        private DateTime _previousFrame = DateTime.UtcNow;

        public MainWindow()
        {
            InitializeComponent();

            Field.State = _engine.State;
            _timer.Interval = TimeSpan.FromMilliseconds(16);
            _timer.Tick += Tick;
            _timer.Start();
        }

        private void Tick(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            var deltaSeconds = Math.Min((float)(now - _previousFrame).TotalSeconds, 0.05f);
            _previousFrame = now;

            _engine.Step(deltaSeconds, ReadInput());
            Field.InvalidateVisual();
            UpdateStatus();
        }

        private PlayerInput ReadInput()
        {
            var move = NumericsVector2.Zero;
            if (_keysDown.Contains(Key.Left))
            {
                move.X -= 1f;
            }

            if (_keysDown.Contains(Key.Right))
            {
                move.X += 1f;
            }

            if (_keysDown.Contains(Key.Up))
            {
                move.Y -= 1f;
            }

            if (_keysDown.Contains(Key.Down))
            {
                move.Y += 1f;
            }

            return new PlayerInput(move, _keysDown.Contains(Key.Space));
        }

        private void UpdateStatus()
        {
            var state = _engine.State;
            var active = state.ActivePlayer;
            var possession = state.Ball.PossessorId.HasValue
                ? $"P{state.Ball.PossessorId.Value}"
                : "loose";

            var eventText = state.IsBallOutOfPlay
                ? $" | {state.LastEvent.Description} Restart: {state.LastEvent.Restart}. Press R."
                : string.Empty;

            StatusText.Text =
                $"Home {state.HomeScore} - {state.AwayScore} Away | Tick {state.Tick} | Active P{active.Id} {active.Team} | Ball {possession} | {state.ClockSeconds:0.0}s{eventText}";
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            _keysDown.Add(e.Key);
            if (e.Key == Key.R)
            {
                _engine.ResumeFromRestart();
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            _keysDown.Remove(e.Key);
        }
    }

    public sealed class FieldView : FrameworkElement
    {
        private static readonly Brush GrassBrush = new SolidColorBrush(Color.FromRgb(33, 126, 55));
        private static readonly Brush LineBrush = new SolidColorBrush(Color.FromRgb(242, 242, 242));
        private static readonly Brush HomeBrush = new SolidColorBrush(Color.FromRgb(235, 58, 58));
        private static readonly Brush AwayBrush = new SolidColorBrush(Color.FromRgb(64, 119, 230));
        private static readonly Brush ActiveOutlineBrush = new SolidColorBrush(Color.FromRgb(255, 230, 80));
        private static readonly Brush SkinBrush = new SolidColorBrush(Color.FromRgb(239, 190, 135));
        private static readonly Brush ShortsBrush = new SolidColorBrush(Color.FromRgb(30, 30, 34));
        private static readonly Brush BallBrush = new SolidColorBrush(Color.FromRgb(245, 245, 245));
        private static readonly Brush BallShadowBrush = new SolidColorBrush(Color.FromArgb(95, 0, 0, 0));
        private static readonly Pen LinePen = new(LineBrush, 2);
        private static readonly Pen ActivePen = new(ActiveOutlineBrush, 3);

        public MatchState? State { get; set; }

        protected override void OnRender(DrawingContext context)
        {
            base.OnRender(context);

            if (State is null)
            {
                return;
            }

            var fieldRect = GetFieldRect(State.Field);
            context.DrawRectangle(GrassBrush, null, fieldRect);
            DrawFieldLines(context, State.Field, fieldRect);
            DrawPlayers(context, State.Field, fieldRect);
            DrawBall(context, State.Field, fieldRect);
        }

        private Rect GetFieldRect(FieldDimensions field)
        {
            var margin = 28d;
            var availableWidth = Math.Max(1d, ActualWidth - margin * 2d);
            var availableHeight = Math.Max(1d, ActualHeight - margin * 2d);
            var fieldAspect = field.Length / field.Width;
            var width = availableWidth;
            var height = width / fieldAspect;

            if (height > availableHeight)
            {
                height = availableHeight;
                width = height * fieldAspect;
            }

            return new Rect(
                (ActualWidth - width) / 2d,
                (ActualHeight - height) / 2d,
                width,
                height);
        }

        private void DrawFieldLines(DrawingContext context, FieldDimensions field, Rect rect)
        {
            context.DrawRectangle(null, LinePen, rect);

            var centerTop = ToScreen(new NumericsVector2(0f, field.Top), field, rect);
            var centerBottom = ToScreen(new NumericsVector2(0f, field.Bottom), field, rect);
            context.DrawLine(LinePen, centerTop, centerBottom);
            context.DrawEllipse(null, LinePen, ToScreen(NumericsVector2.Zero, field, rect), 58, 58);

            DrawGoal(context, field, rect, field.Left);
            DrawGoal(context, field, rect, field.Right);
            DrawPenaltyArea(context, field, rect, leftSide: true);
            DrawPenaltyArea(context, field, rect, leftSide: false);
        }

        private void DrawGoal(DrawingContext context, FieldDimensions field, Rect rect, float x)
        {
            var top = ToScreen(new NumericsVector2(x, field.GoalTop), field, rect);
            var bottom = ToScreen(new NumericsVector2(x, field.GoalBottom), field, rect);
            context.DrawLine(new Pen(LineBrush, 5), top, bottom);
        }

        private void DrawPenaltyArea(DrawingContext context, FieldDimensions field, Rect rect, bool leftSide)
        {
            var x1 = leftSide ? field.Left : field.Right - field.PenaltyAreaLength;
            var x2 = leftSide ? field.Left + field.PenaltyAreaLength : field.Right;
            var y1 = -field.PenaltyAreaWidth / 2f;
            var y2 = field.PenaltyAreaWidth / 2f;
            var topLeft = ToScreen(new NumericsVector2(x1, y1), field, rect);
            var bottomRight = ToScreen(new NumericsVector2(x2, y2), field, rect);
            context.DrawRectangle(null, LinePen, new Rect(topLeft, bottomRight));
        }

        private void DrawPlayers(DrawingContext context, FieldDimensions field, Rect rect)
        {
            foreach (var player in State!.Players)
            {
                var center = ToScreen(player.Position, field, rect);
                var radius = ScaleDistance(player.Radius, field, rect);
                var brush = player.Team == TeamSide.Home ? HomeBrush : AwayBrush;
                DrawPlayerBody(context, player, center, radius, brush);

                if (player.Id == State.ActivePlayerId)
                {
                    context.DrawEllipse(null, ActivePen, center, radius + 5, radius + 5);
                }

                if (player.HasPossession)
                {
                    context.DrawEllipse(null, new Pen(LineBrush, 2), center, radius + 5, radius + 5);
                }
            }
        }

        private static void DrawPlayerBody(DrawingContext context, PlayerState player, Point center, double radius, Brush shirtBrush)
        {
            var forward = player.Facing.ToUnitVector();
            var right = new NumericsVector2(forward.Y, -forward.X);

            var head = Offset(center, forward, radius * 0.8);
            var torsoFront = Offset(center, forward, radius * 0.35);
            var torsoBack = Offset(center, forward, -radius * 0.6);
            var leftShoulder = Offset(torsoFront, right, -radius * 0.65);
            var rightShoulder = Offset(torsoFront, right, radius * 0.65);
            var leftHip = Offset(torsoBack, right, -radius * 0.42);
            var rightHip = Offset(torsoBack, right, radius * 0.42);

            var body = new StreamGeometry();
            using (var geometry = body.Open())
            {
                geometry.BeginFigure(leftShoulder, isFilled: true, isClosed: true);
                geometry.LineTo(rightShoulder, isStroked: true, isSmoothJoin: true);
                geometry.LineTo(rightHip, isStroked: true, isSmoothJoin: true);
                geometry.LineTo(leftHip, isStroked: true, isSmoothJoin: true);
            }

            body.Freeze();
            context.DrawGeometry(shirtBrush, new Pen(Brushes.Black, 1), body);
            context.DrawEllipse(ShortsBrush, null, Offset(center, forward, -radius * 0.62), radius * 0.44, radius * 0.28);
            context.DrawEllipse(SkinBrush, new Pen(Brushes.Black, 1), head, radius * 0.42, radius * 0.42);
        }

        private void DrawBall(DrawingContext context, FieldDimensions field, Rect rect)
        {
            var shadowCenter = ToScreen(State!.Ball.Position, field, rect);
            var baseRadius = Math.Max(4d, ScaleDistance(State.Ball.Radius, field, rect));
            var heightPixels = State.Ball.Height / field.Length * rect.Width;
            var ballCenter = new Point(shadowCenter.X, shadowCenter.Y - heightPixels);
            var ballRadius = baseRadius * (1d + Math.Min(0.5d, State.Ball.Height * 0.045d));
            var shadowRadiusX = baseRadius * Math.Max(0.45d, 1d - State.Ball.Height * 0.035d);
            var shadowRadiusY = baseRadius * Math.Max(0.25d, 0.55d - State.Ball.Height * 0.025d);

            context.DrawEllipse(BallShadowBrush, null, shadowCenter, shadowRadiusX, shadowRadiusY);
            context.DrawEllipse(BallBrush, new Pen(Brushes.Black, 1), ballCenter, ballRadius, ballRadius);
        }

        private static Point ToScreen(NumericsVector2 world, FieldDimensions field, Rect rect)
        {
            var x01 = (world.X - field.Left) / field.Length;
            var y01 = (world.Y - field.Top) / field.Width;
            return new Point(rect.Left + x01 * rect.Width, rect.Top + y01 * rect.Height);
        }

        private static double ScaleDistance(float worldDistance, FieldDimensions field, Rect rect)
        {
            return worldDistance / field.Length * rect.Width;
        }

        private static Point Offset(Point point, NumericsVector2 direction, double distance)
        {
            return new Point(point.X + direction.X * distance, point.Y + direction.Y * distance);
        }
    }
}
