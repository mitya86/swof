using Swof.Engine;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NumericsVector2 = System.Numerics.Vector2;

namespace Swof.Windows
{
    public partial class MainWindow : Window
    {
        private readonly MatchEngine _engine;
        private readonly DispatcherTimer _timer = new();
        private readonly HashSet<Key> _keysDown = new();
        private readonly ObservableCollection<TuningEntry> _tuningEntries = new();
        private DateTime _previousFrame = DateTime.UtcNow;

        public MainWindow()
        {
            InitializeComponent();

            var tuning = PhysicsTuningStore.LoadOrDefault();
            _engine = MatchEngine.CreateDefault(tuning);
            LoadEntriesFromTuning(tuning);

            TuningPathText.Text = $"Profile: {PhysicsTuningStore.DefaultPath}";
            TuningGrid.ItemsSource = _tuningEntries;
            ConfigureTuningView();
            TuningFeedbackText.Text = "Loaded persisted tuning profile.";

            Field.Snapshot = _engine.CurrentSnapshot;
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
            Field.Snapshot = _engine.GetRenderSnapshot();
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
            HandleGameKeyDown(e, consume: false);
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            HandleGameKeyUp(e, consume: false);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            HandleGameKeyDown(e, consume: true);
        }

        private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            HandleGameKeyUp(e, consume: true);
        }

        private void HandleGameKeyDown(KeyEventArgs e, bool consume)
        {
            if (!IsGameControlKey(e.Key))
            {
                return;
            }

            _keysDown.Add(e.Key);
            if (e.Key == Key.R)
            {
                _engine.ResumeFromRestart();
            }

            if (consume)
            {
                e.Handled = true;
            }
        }

        private void HandleGameKeyUp(KeyEventArgs e, bool consume)
        {
            if (!IsGameControlKey(e.Key))
            {
                return;
            }

            _keysDown.Remove(e.Key);
            if (consume)
            {
                e.Handled = true;
            }
        }

        private static bool IsGameControlKey(Key key)
        {
            return key == Key.Left
                || key == Key.Right
                || key == Key.Up
                || key == Key.Down
                || key == Key.Space
                || key == Key.R;
        }

        private void ApplyLive_Click(object sender, RoutedEventArgs e)
        {
            if (!TryBuildTuningFromEntries(out var tuning, out var error))
            {
                TuningFeedbackText.Text = error;
                return;
            }

            _engine.ApplyTuning(tuning);
            TuningFeedbackText.Text = "Applied tuning to live engine.";
        }

        private void SaveTuning_Click(object sender, RoutedEventArgs e)
        {
            if (!TryBuildTuningFromEntries(out var tuning, out var error))
            {
                TuningFeedbackText.Text = error;
                return;
            }

            PhysicsTuningStore.Save(tuning);
            _engine.ApplyTuning(tuning);
            TuningFeedbackText.Text = "Saved tuning profile and applied live.";
        }

        private void LoadTuning_Click(object sender, RoutedEventArgs e)
        {
            var tuning = PhysicsTuningStore.LoadOrDefault();
            _engine.ApplyTuning(tuning);
            LoadEntriesFromTuning(tuning);
            TuningFeedbackText.Text = "Loaded tuning profile from disk and applied live.";
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            LoadEntriesFromTuning(PhysicsTuning.Default);
            _engine.ApplyTuning(PhysicsTuning.Default);
            TuningFeedbackText.Text = "Reset to default tuning and applied live.";
        }

        private void LoadEntriesFromTuning(PhysicsTuning tuning)
        {
            _tuningEntries.Clear();
            foreach (var property in GetTuningProperties())
            {
                var value = property.GetValue(tuning);
                var defaultValue = property.GetValue(PhysicsTuning.Default);
                _tuningEntries.Add(new TuningEntry(
                    property.Name,
                    GetCategory(property.Name),
                    GetOrderIndex(property.Name),
                    property.PropertyType,
                    FormatValue(value, property.PropertyType),
                    defaultValue));
            }
        }

        private void ConfigureTuningView()
        {
            var view = CollectionViewSource.GetDefaultView(_tuningEntries);
            view.GroupDescriptions.Clear();
            view.SortDescriptions.Clear();
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TuningEntry.Category)));
            view.SortDescriptions.Add(new SortDescription(nameof(TuningEntry.OrderIndex), ListSortDirection.Ascending));
            view.SortDescriptions.Add(new SortDescription(nameof(TuningEntry.Name), ListSortDirection.Ascending));
        }

        private static string FormatValue(object? value, Type type)
        {
            if (value is null)
            {
                return string.Empty;
            }

            if (type == typeof(float))
            {
                return ((float)value).ToString("G9", CultureInfo.InvariantCulture);
            }

            if (type == typeof(int))
            {
                return ((int)value).ToString(CultureInfo.InvariantCulture);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private bool TryBuildTuningFromEntries(out PhysicsTuning tuning, out string error)
        {
            var objectNode = new JsonObject();
            foreach (var entry in _tuningEntries)
            {
                if (entry.ValueType == typeof(int))
                {
                    if (!int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                    {
                        tuning = PhysicsTuning.Default;
                        error = $"Invalid int value for {entry.Name}: '{entry.Value}'.";
                        return false;
                    }

                    objectNode[entry.Name] = intValue;
                    continue;
                }

                if (entry.ValueType == typeof(float))
                {
                    if (!float.TryParse(entry.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var floatValue))
                    {
                        tuning = PhysicsTuning.Default;
                        error = $"Invalid float value for {entry.Name}: '{entry.Value}'.";
                        return false;
                    }

                    objectNode[entry.Name] = floatValue;
                }
            }

            try
            {
                var json = objectNode.ToJsonString(new JsonSerializerOptions());
                tuning = JsonSerializer.Deserialize<PhysicsTuning>(json) ?? PhysicsTuning.Default;
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                tuning = PhysicsTuning.Default;
                error = $"Failed to build tuning: {ex.Message}";
                return false;
            }
        }

        private static IReadOnlyList<PropertyInfo> GetTuningProperties()
        {
            return typeof(PhysicsTuning)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.CanRead && (property.PropertyType == typeof(float) || property.PropertyType == typeof(int)))
                .OrderBy(property => GetOrderIndex(property.Name))
                .ThenBy(property => property.Name, StringComparer.Ordinal)
                .ToArray();
        }

        private static int GetOrderIndex(string name)
        {
            if (name == nameof(PhysicsTuning.TickRate))
            {
                return 0;
            }

            if (name.StartsWith("Player", StringComparison.Ordinal))
            {
                return 100;
            }

            if (name.StartsWith("Ball", StringComparison.Ordinal) || name.StartsWith("MinBall", StringComparison.Ordinal))
            {
                return 200;
            }

            if (name.StartsWith("Dribble", StringComparison.Ordinal)
                || name.StartsWith("Possession", StringComparison.Ordinal)
                || name.StartsWith("MovingFront", StringComparison.Ordinal)
                || name.StartsWith("Stationary", StringComparison.Ordinal)
                || name.StartsWith("PassiveContest", StringComparison.Ordinal))
            {
                return 300;
            }

            if (name.StartsWith("Slide", StringComparison.Ordinal) || name.StartsWith("Tackle", StringComparison.Ordinal))
            {
                return 400;
            }

            if (name.StartsWith("TapPass", StringComparison.Ordinal)
                || name.StartsWith("Pass", StringComparison.Ordinal)
                || name.StartsWith("Kick", StringComparison.Ordinal)
                || name.StartsWith("LongPass", StringComparison.Ordinal)
                || name.StartsWith("MaxKick", StringComparison.Ordinal)
                || name.StartsWith("MinKick", StringComparison.Ordinal)
                || name.StartsWith("RunnerVelocityCarryToKick", StringComparison.Ordinal))
            {
                return 500;
            }

            return 900;
        }

        private static string GetCategory(string name)
        {
            var order = GetOrderIndex(name);
            return order switch
            {
                0 => "Core Timing",
                100 => "Player Movement",
                200 => "Ball Flight",
                300 => "Possession and Dribble",
                400 => "Tackles and Sliding",
                500 => "Passing and Kicking",
                _ => "Other"
            };
        }
    }

    public sealed class TuningEntry : INotifyPropertyChanged
    {
        private string _value;
        private double _sliderValue;
        private bool _updating;

        public TuningEntry(string name, string category, int orderIndex, Type valueType, string value, object? defaultValue)
        {
            Name = name;
            Category = category;
            OrderIndex = orderIndex;
            ValueType = valueType;
            _value = value;
            (SliderMinimum, SliderMaximum, SliderTick, SliderLargeChange) = BuildSliderMetadata(valueType, defaultValue);
            _sliderValue = ParseSliderValue(valueType, value);
        }

        public string Name { get; }
        public string Category { get; }
        public int OrderIndex { get; }
        public Type ValueType { get; }
        public string TypeName => ValueType == typeof(int) ? "int" : "float";
        public double SliderMinimum { get; }
        public double SliderMaximum { get; }
        public double SliderTick { get; }
        public double SliderLargeChange { get; }

        public string Value
        {
            get => _value;
            set
            {
                if (_value == value)
                {
                    return;
                }

                _value = value;
                if (!_updating)
                {
                    _updating = true;
                    _sliderValue = Math.Clamp(ParseSliderValue(ValueType, _value), SliderMinimum, SliderMaximum);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SliderValue)));
                    _updating = false;
                }

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public double SliderValue
        {
            get => _sliderValue;
            set
            {
                var clamped = Math.Clamp(value, SliderMinimum, SliderMaximum);
                if (Math.Abs(_sliderValue - clamped) <= 0.0000001d)
                {
                    return;
                }

                _sliderValue = clamped;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SliderValue)));

                if (_updating)
                {
                    return;
                }

                _updating = true;
                _value = ValueType == typeof(int)
                    ? ((int)Math.Round(clamped, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture)
                    : ((float)clamped).ToString("G9", CultureInfo.InvariantCulture);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                _updating = false;
            }
        }

        private static (double min, double max, double tick, double largeChange) BuildSliderMetadata(Type type, object? defaultValue)
        {
            if (type == typeof(int))
            {
                var baseValue = defaultValue is int i ? i : 1;
                var min = Math.Max(0, baseValue / 4.0);
                var max = Math.Max(baseValue * 4.0, min + 10.0);
                return (min, max, 1.0, 5.0);
            }

            var baseFloat = defaultValue is float f ? Math.Abs(f) : 1f;
            var minFloat = 0.0;
            var maxFloat = Math.Max(baseFloat * 4.0, 0.2);
            var tick = Math.Max(maxFloat / 500.0, 0.0001);
            return (minFloat, maxFloat, tick, tick * 20.0);
        }

        private static double ParseSliderValue(Type type, string value)
        {
            if (type == typeof(int))
            {
                return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue)
                    ? intValue
                    : 0d;
            }

            return float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var floatValue)
                ? floatValue
                : 0d;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
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

        public MatchSnapshot? Snapshot { get; set; }

        protected override void OnRender(DrawingContext context)
        {
            base.OnRender(context);

            if (Snapshot is null)
            {
                return;
            }

            var fieldRect = GetFieldRect(Snapshot.Field);
            context.DrawRectangle(GrassBrush, null, fieldRect);
            DrawFieldLines(context, Snapshot.Field, fieldRect);
            DrawPlayers(context, Snapshot.Field, fieldRect);
            DrawBall(context, Snapshot.Field, fieldRect);
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
            foreach (var player in Snapshot!.Players)
            {
                var center = ToScreen(player.Position, field, rect);
                var radius = ScaleDistance(player.Radius, field, rect);
                var brush = player.Team == TeamSide.Home ? HomeBrush : AwayBrush;
                DrawPlayerBody(context, player, center, radius, brush);

                if (player.Id == Snapshot.ActivePlayerId)
                {
                    context.DrawEllipse(null, ActivePen, center, radius + 5, radius + 5);
                }

                if (player.HasPossession)
                {
                    context.DrawEllipse(null, new Pen(LineBrush, 2), center, radius + 5, radius + 5);
                }
            }
        }

        private static void DrawPlayerBody(DrawingContext context, PlayerSnapshot player, Point center, double radius, Brush shirtBrush)
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
            var shadowCenter = ToScreen(Snapshot!.Ball.Position, field, rect);
            var baseRadius = Math.Max(4d, ScaleDistance(Snapshot.Ball.Radius, field, rect));
            var heightPixels = Snapshot.Ball.Height / field.Length * rect.Width;
            var ballCenter = new Point(shadowCenter.X, shadowCenter.Y - heightPixels);
            var ballRadius = baseRadius * (1d + Math.Min(0.5d, Snapshot.Ball.Height * 0.045d));
            var shadowRadiusX = baseRadius * Math.Max(0.45d, 1d - Snapshot.Ball.Height * 0.035d);
            var shadowRadiusY = baseRadius * Math.Max(0.25d, 0.55d - Snapshot.Ball.Height * 0.025d);

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
