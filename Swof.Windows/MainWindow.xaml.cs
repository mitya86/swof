using Swof.Engine;
using Swof.Engine.AI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
        private static readonly JsonSerializerOptions TuningJsonOptions = CreateTuningJsonOptions();
        private readonly MatchEngine _engine;
        private readonly ComputerPlayerController _computerPlayers;
        private readonly PlayerInputBuffer _inputBuffer = new();
        private readonly DispatcherTimer _timer = new();
        private readonly HashSet<Key> _keysDown = new();
        private readonly ObservableCollection<TuningEntry> _tuningEntries = new();
        private DateTime _previousFrame = DateTime.UtcNow;

        public MainWindow()
        {
            InitializeComponent();

            var tuning = PhysicsTuningStore.LoadOrDefault();
            _engine = MatchEngine.CreateDefault(tuning);
            _computerPlayers = ComputerPlayerController.CreateDefault(_engine.CurrentSnapshot);
            LoadEntriesFromTuning(tuning);
            CurveModeCombo.ItemsSource = Enum.GetValues<BallCurveMode>();
            CurveModeCombo.SelectedItem = tuning.BallCurveMode;

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

            _inputBuffer.Capture(ReadInput());
            _engine.Step(deltaSeconds, _inputBuffer, _computerPlayers);
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
                ? $" | {state.LastEvent.Description} Restart: {state.LastEvent.Restart}."
                : string.Empty;

            StatusText.Text =
                $"Home {state.HomeScore} - {state.AwayScore} Away | Tick {state.Tick} | Active P{active.Id} {active.Team} | Ball {possession} | {state.ClockSeconds:0.0}s{eventText}";

            var skills = new List<(string Alias, int Value)>
            {
                ("P", active.Attributes.EffectivePassing),
                ("K", active.Attributes.EffectiveShooting),
                ("H", active.Attributes.EffectiveHeading),
                ("T", active.Attributes.EffectiveTackling),
                ("C", active.Attributes.EffectiveBallControl),
                ("S", active.Attributes.EffectiveSpeed),
                ("F", active.Attributes.EffectiveFinishing)
            };
            var top3 = string.Concat(skills
                .OrderByDescending(skill => skill.Value)
                .ThenBy(skill => skill.Alias, StringComparer.Ordinal)
                .Take(3)
                .Select(skill => skill.Alias));
            var skillText = string.Join(" ",
                skills.Select(skill => $"{skill.Alias}:{ToStarScale(skill.Value)}"));
            PlayerSkillText.Text = $"{active.Name} | {skillText} | Top:{top3}";
        }

        private static string ToStarScale(int value10)
        {
            var clamped = Math.Clamp(value10, 0, 10);
            var full = clamped / 2;
            var half = clamped % 2;
            var chars = new char[5];
            for (var i = 0; i < 5; i++)
            {
                chars[i] = i < full ? '★' : (i == full && half == 1 ? '⭑' : '·');
            }

            return new string(chars);
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
            CurveModeCombo.SelectedItem = tuning.BallCurveMode;
            TuningFeedbackText.Text = "Loaded tuning profile from disk and applied live.";
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            LoadEntriesFromTuning(PhysicsTuning.Default);
            _engine.ApplyTuning(PhysicsTuning.Default);
            CurveModeCombo.SelectedItem = PhysicsTuning.Default.BallCurveMode;
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
                var selectedMode = CurveModeCombo.SelectedItem is BallCurveMode mode
                    ? mode
                    : PhysicsTuning.Default.BallCurveMode;
                objectNode[nameof(PhysicsTuning.BallCurveMode)] = selectedMode.ToString();

                var json = objectNode.ToJsonString(TuningJsonOptions);
                tuning = JsonSerializer.Deserialize<PhysicsTuning>(json, TuningJsonOptions) ?? PhysicsTuning.Default;
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
                || name.StartsWith("ImmediateKick", StringComparison.Ordinal)
                || name.StartsWith("LongPass", StringComparison.Ordinal)
                || name.StartsWith("MaxKick", StringComparison.Ordinal)
                || name.StartsWith("MinKick", StringComparison.Ordinal)
                || name.StartsWith("RunnerVelocityCarryToKick", StringComparison.Ordinal))
            {
                return 500;
            }

            if (name.StartsWith("Aftertouch", StringComparison.Ordinal)
                || name.StartsWith("SwosCurve", StringComparison.Ordinal)
                || name.StartsWith("Spin", StringComparison.Ordinal)
                || name.StartsWith("GroundMagnus", StringComparison.Ordinal)
                || name.StartsWith("MaxGround", StringComparison.Ordinal)
                || name.StartsWith("Magnus", StringComparison.Ordinal))
            {
                return 600;
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
                600 => "Ball Curving",
                _ => "Other"
            };
        }

        private static JsonSerializerOptions CreateTuningJsonOptions()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
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
        private const float CameraVisibleCourtLengthRatio = 0.7f;
        private const float CameraBoundaryMargin = 3f;
        private const float CameraBallWeight = 0.72f;
        private const float CameraLookaheadTicks = 14f;
        private const float CameraMaxLookaheadRatio = 0.24f;
        private const float BenchOffsetFromTouchlineMeters = 1.2f;
        private const float BenchLengthMeters = 5f;
        private const float BenchDepthMeters = 1f;
        private static readonly Brush GrassBrush = new SolidColorBrush(Color.FromRgb(35, 123, 157));
        private static readonly Brush PitchStripeBrush = new SolidColorBrush(Color.FromRgb(32, 116, 149));
        private static readonly Brush SurroundBrush = new SolidColorBrush(Color.FromRgb(17, 25, 32));
        private static readonly Brush TrackBrush = new SolidColorBrush(Color.FromRgb(24, 77, 98));
        private static readonly Brush GoalNetBrush = new SolidColorBrush(Color.FromArgb(105, 235, 235, 235));
        private static readonly Brush BenchBrush = new SolidColorBrush(Color.FromRgb(42, 47, 58));
        private static readonly Brush BenchRoofBrush = new SolidColorBrush(Color.FromRgb(78, 88, 108));
        private static readonly Brush CrowdDarkBrush = new SolidColorBrush(Color.FromRgb(34, 34, 41));
        private static readonly Brush CrowdLightBrush = new SolidColorBrush(Color.FromRgb(58, 58, 68));
        private static readonly Brush LineBrush = new SolidColorBrush(Color.FromRgb(242, 242, 242));
        private static readonly Brush HomeBrush = new SolidColorBrush(Color.FromRgb(235, 58, 58));
        private static readonly Brush AwayBrush = new SolidColorBrush(Color.FromRgb(64, 119, 230));
        private static readonly Brush HomeGoalkeeperBrush = new SolidColorBrush(Color.FromRgb(72, 190, 105));
        private static readonly Brush AwayGoalkeeperBrush = new SolidColorBrush(Color.FromRgb(225, 176, 55));
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

            var viewRect = GetViewRect();
            var camera = GetCameraViewport(Snapshot, viewRect);
            DrawStadiumBackground(context, Snapshot.Field, camera, viewRect);
            DrawFieldLines(context, Snapshot.Field, camera, viewRect);
            DrawPlayers(context, Snapshot.Field, camera, viewRect);
            DrawBall(context, Snapshot.Field, camera, viewRect);
        }

        private Rect GetViewRect()
        {
            var margin = 28d;
            return new Rect(
                margin,
                margin,
                Math.Max(1d, ActualWidth - margin * 2d),
                Math.Max(1d, ActualHeight - margin * 2d));
        }

        private static CameraViewport GetCameraViewport(MatchSnapshot snapshot, Rect viewRect)
        {
            var field = snapshot.Field;
            var screenAspect = (float)(viewRect.Width / Math.Max(1d, viewRect.Height));
            var worldWidth = field.Length + CameraBoundaryMargin * 2f;
            var worldHeight = field.Width + CameraBoundaryMargin * 2f;
            var spanX = field.Length * CameraVisibleCourtLengthRatio;
            var spanY = spanX / MathF.Max(0.1f, screenAspect);

            if (spanY > worldHeight)
            {
                spanY = worldHeight;
                spanX = spanY * screenAspect;
            }

            if (spanX > worldWidth)
            {
                spanX = worldWidth;
                spanY = spanX / MathF.Max(0.1f, screenAspect);
            }

            var active = snapshot.Players.FirstOrDefault(player => player.Id == snapshot.ActivePlayerId) ?? snapshot.Players[0];
            var center = snapshot.Ball.Position * CameraBallWeight + active.Position * (1f - CameraBallWeight);
            var lookahead = snapshot.Ball.Velocity * CameraLookaheadTicks;
            var maxLookahead = new NumericsVector2(spanX * CameraMaxLookaheadRatio, spanY * CameraMaxLookaheadRatio);
            lookahead = new NumericsVector2(
                Math.Clamp(lookahead.X, -maxLookahead.X, maxLookahead.X),
                Math.Clamp(lookahead.Y, -maxLookahead.Y, maxLookahead.Y));
            center += lookahead;

            var halfX = spanX * 0.5f;
            var halfY = spanY * 0.5f;
            center = new NumericsVector2(
                Math.Clamp(center.X, field.Left - CameraBoundaryMargin + halfX, field.Right + CameraBoundaryMargin - halfX),
                Math.Clamp(center.Y, field.Top - CameraBoundaryMargin + halfY, field.Bottom + CameraBoundaryMargin - halfY));

            return new CameraViewport(
                center.X - halfX,
                center.X + halfX,
                center.Y - halfY,
                center.Y + halfY);
        }

        private void DrawFieldLines(DrawingContext context, FieldDimensions field, CameraViewport camera, Rect rect)
        {
            var pitchTopLeft = ToScreen(new NumericsVector2(field.Left, field.Top), camera, rect);
            var pitchBottomRight = ToScreen(new NumericsVector2(field.Right, field.Bottom), camera, rect);
            var pitchRect = new Rect(pitchTopLeft, pitchBottomRight);
            context.DrawRectangle(null, LinePen, pitchRect);

            var centerTop = ToScreen(new NumericsVector2(0f, field.Top), camera, rect);
            var centerBottom = ToScreen(new NumericsVector2(0f, field.Bottom), camera, rect);
            context.DrawLine(LinePen, centerTop, centerBottom);
            var centerRadius = ScaleDistance(field.CenterCircleRadius, camera, rect);
            context.DrawEllipse(null, LinePen, ToScreen(NumericsVector2.Zero, camera, rect), centerRadius, centerRadius);
            context.DrawEllipse(LineBrush, null, ToScreen(NumericsVector2.Zero, camera, rect), 2.5d, 2.5d);

            DrawGoal(context, field, camera, rect, field.Left);
            DrawGoal(context, field, camera, rect, field.Right);
            DrawPenaltyArea(context, field, camera, rect, leftSide: true);
            DrawPenaltyArea(context, field, camera, rect, leftSide: false);
            DrawFutsalMarks(context, field, camera, rect);
        }

        private void DrawStadiumBackground(DrawingContext context, FieldDimensions field, CameraViewport camera, Rect rect)
        {
            context.DrawRectangle(SurroundBrush, null, rect);

            var pitchTopLeft = ToScreen(new NumericsVector2(field.Left, field.Top), camera, rect);
            var pitchBottomRight = ToScreen(new NumericsVector2(field.Right, field.Bottom), camera, rect);
            var pitchRect = new Rect(pitchTopLeft, pitchBottomRight);
            var apron = new Rect(
                pitchRect.Left - 12d,
                pitchRect.Top - 12d,
                pitchRect.Width + 24d,
                pitchRect.Height + 24d);
            context.DrawRectangle(TrackBrush, null, apron);
            context.DrawRectangle(GrassBrush, null, pitchRect);

            var stripeCount = 8;
            for (var i = 0; i < stripeCount; i++)
            {
                if (i % 2 == 0)
                {
                    continue;
                }

                var stripeX = pitchRect.Left + pitchRect.Width * i / stripeCount;
                var stripeW = pitchRect.Width / stripeCount;
                context.DrawRectangle(PitchStripeBrush, null, new Rect(stripeX, pitchRect.Top, stripeW, pitchRect.Height));
            }

            DrawGoalWithNet(context, field, camera, rect, leftGoal: true);
            DrawGoalWithNet(context, field, camera, rect, leftGoal: false);
            DrawBenches(context, field, camera, rect);
        }

        private static void DrawTrack(DrawingContext context, Rect pitchRect)
        {
            const double lane = 14d;
            var outer = new Rect(
                pitchRect.Left - lane,
                pitchRect.Top - lane,
                pitchRect.Width + lane * 2d,
                pitchRect.Height + lane * 2d);
            var inner = new Rect(
                pitchRect.Left - lane * 0.3d,
                pitchRect.Top - lane * 0.3d,
                pitchRect.Width + lane * 0.6d,
                pitchRect.Height + lane * 0.6d);
            context.DrawRectangle(TrackBrush, null, outer);
            context.DrawRectangle(GrassBrush, null, inner);
            context.DrawRectangle(null, new Pen(LineBrush, 1), outer);
        }

        private void DrawGoalWithNet(DrawingContext context, FieldDimensions field, CameraViewport camera, Rect rect, bool leftGoal)
        {
            var goalX = leftGoal ? field.Left : field.Right;
            var postTop = ToScreen(new NumericsVector2(goalX, field.GoalTop), camera, rect);
            var postBottom = ToScreen(new NumericsVector2(goalX, field.GoalBottom), camera, rect);
            var depthX = leftGoal ? goalX - field.GoalDepth : goalX + field.GoalDepth;
            var backTop = ToScreen(new NumericsVector2(depthX, field.GoalTop), camera, rect);
            var backBottom = ToScreen(new NumericsVector2(depthX, field.GoalBottom), camera, rect);

            var net = new StreamGeometry();
            using (var geo = net.Open())
            {
                geo.BeginFigure(postTop, true, true);
                geo.LineTo(postBottom, true, true);
                geo.LineTo(backBottom, true, true);
                geo.LineTo(backTop, true, true);
            }
            net.Freeze();
            context.DrawGeometry(GoalNetBrush, new Pen(LineBrush, 1), net);
            context.DrawLine(new Pen(LineBrush, 3), postTop, postBottom);
            context.DrawLine(new Pen(LineBrush, 2), postTop, backTop);
            context.DrawLine(new Pen(LineBrush, 2), postBottom, backBottom);

            var meshRows = 4;
            for (var i = 1; i < meshRows; i++)
            {
                var t = i / (double)meshRows;
                var front = LerpPoint(postTop, postBottom, t);
                var back = LerpPoint(backTop, backBottom, t);
                context.DrawLine(new Pen(LineBrush, 0.7), front, back);
            }
        }

        private void DrawBenches(DrawingContext context, FieldDimensions field, CameraViewport camera, Rect rect)
        {
            var yTop = field.Top - BenchOffsetFromTouchlineMeters - BenchDepthMeters;
            DrawBench(context, camera, rect, -10f, -10f + BenchLengthMeters, yTop, yTop + BenchDepthMeters);
            DrawBench(context, camera, rect, 5f, 5f + BenchLengthMeters, yTop, yTop + BenchDepthMeters);
        }

        private void DrawBench(DrawingContext context, CameraViewport camera, Rect rect, float x1, float x2, float y1, float y2)
        {
            var topLeft = ToScreen(new NumericsVector2(x1, y1), camera, rect);
            var bottomRight = ToScreen(new NumericsVector2(x2, y2), camera, rect);
            var benchRect = new Rect(topLeft, bottomRight);
            context.DrawRectangle(BenchBrush, new Pen(LineBrush, 1), benchRect);
            var roof = new Rect(benchRect.Left, benchRect.Top - 3, benchRect.Width, 3);
            context.DrawRectangle(BenchRoofBrush, null, roof);
        }

        private static void DrawCrowdBands(DrawingContext context, Rect pitchRect, Rect viewRect)
        {
            var topBand = new Rect(viewRect.Left, viewRect.Top, viewRect.Width, Math.Max(0, pitchRect.Top - viewRect.Top - 18));
            var bottomBand = new Rect(viewRect.Left, Math.Min(viewRect.Bottom, pitchRect.Bottom + 18), viewRect.Width, Math.Max(0, viewRect.Bottom - pitchRect.Bottom - 18));
            var leftBand = new Rect(viewRect.Left, pitchRect.Top, Math.Max(0, pitchRect.Left - viewRect.Left - 18), pitchRect.Height);
            var rightBand = new Rect(Math.Min(viewRect.Right, pitchRect.Right + 18), pitchRect.Top, Math.Max(0, viewRect.Right - pitchRect.Right - 18), pitchRect.Height);

            FillCrowdBand(context, topBand);
            FillCrowdBand(context, bottomBand);
            FillCrowdBand(context, leftBand);
            FillCrowdBand(context, rightBand);
        }

        private static void FillCrowdBand(DrawingContext context, Rect band)
        {
            if (band.Width < 8 || band.Height < 8)
            {
                return;
            }

            context.DrawRectangle(CrowdDarkBrush, null, band);
            var spacing = 7d;
            for (double y = band.Top + 4; y < band.Bottom - 2; y += spacing)
            {
                for (double x = band.Left + 4; x < band.Right - 2; x += spacing)
                {
                    var parity = ((int)((x + y) / spacing)) % 3;
                    var brush = parity == 0 ? CrowdLightBrush : LineBrush;
                    var size = parity == 2 ? 1.2 : 1.8;
                    context.DrawEllipse(brush, null, new Point(x, y), size, size);
                }
            }
        }

        private void DrawGoal(DrawingContext context, FieldDimensions field, CameraViewport camera, Rect rect, float x)
        {
            var top = ToScreen(new NumericsVector2(x, field.GoalTop), camera, rect);
            var bottom = ToScreen(new NumericsVector2(x, field.GoalBottom), camera, rect);
            context.DrawLine(new Pen(LineBrush, 5), top, bottom);
        }

        private void DrawPenaltyArea(DrawingContext context, FieldDimensions field, CameraViewport camera, Rect rect, bool leftSide)
        {
            var goalX = leftSide ? field.Left : field.Right;
            var insideX = leftSide
                ? goalX + field.PenaltyAreaRadius
                : goalX - field.PenaltyAreaRadius;
            var topPost = new NumericsVector2(goalX, field.GoalTop);
            var bottomPost = new NumericsVector2(goalX, field.GoalBottom);
            if (leftSide)
            {
                DrawWorldArc(context, topPost, field.PenaltyAreaRadius, -MathF.PI / 2f, 0f, camera, rect);
                DrawWorldArc(context, bottomPost, field.PenaltyAreaRadius, 0f, MathF.PI / 2f, camera, rect);
            }
            else
            {
                DrawWorldArc(context, topPost, field.PenaltyAreaRadius, MathF.PI, MathF.PI * 1.5f, camera, rect);
                DrawWorldArc(context, bottomPost, field.PenaltyAreaRadius, MathF.PI / 2f, MathF.PI, camera, rect);
            }

            context.DrawLine(
                LinePen,
                ToScreen(new NumericsVector2(insideX, field.GoalTop), camera, rect),
                ToScreen(new NumericsVector2(insideX, field.GoalBottom), camera, rect));
        }

        private void DrawFutsalMarks(DrawingContext context, FieldDimensions field, CameraViewport camera, Rect rect)
        {
            foreach (var side in new[] { -1f, 1f })
            {
                var goalX = side < 0f ? field.Left : field.Right;
                var inward = side < 0f ? 1f : -1f;
                DrawMark(
                    context,
                    new NumericsVector2(goalX + inward * field.PenaltyMarkDistance, 0f),
                    camera,
                    rect);
                var secondMark = new NumericsVector2(
                    goalX + inward * field.SecondPenaltyMarkDistance,
                    0f);
                DrawMark(context, secondMark, camera, rect);
                DrawMark(context, secondMark + new NumericsVector2(0f, -5f), camera, rect);
                DrawMark(context, secondMark + new NumericsVector2(0f, 5f), camera, rect);
            }

            DrawWorldArc(context, new NumericsVector2(field.Left, field.Top), field.CornerArcRadius, 0f, MathF.PI / 2f, camera, rect);
            DrawWorldArc(context, new NumericsVector2(field.Right, field.Top), field.CornerArcRadius, MathF.PI / 2f, MathF.PI, camera, rect);
            DrawWorldArc(context, new NumericsVector2(field.Right, field.Bottom), field.CornerArcRadius, MathF.PI, MathF.PI * 1.5f, camera, rect);
            DrawWorldArc(context, new NumericsVector2(field.Left, field.Bottom), field.CornerArcRadius, MathF.PI * 1.5f, MathF.PI * 2f, camera, rect);

            foreach (var x in new[] { -10f, -5f, 5f, 10f })
            {
                context.DrawLine(
                    LinePen,
                    ToScreen(new NumericsVector2(x, field.Top - 0.4f), camera, rect),
                    ToScreen(new NumericsVector2(x, field.Top + 0.4f), camera, rect));
            }
        }

        private static void DrawMark(
            DrawingContext context,
            NumericsVector2 position,
            CameraViewport camera,
            Rect rect)
        {
            context.DrawEllipse(LineBrush, null, ToScreen(position, camera, rect), 2d, 2d);
        }

        private static void DrawWorldArc(
            DrawingContext context,
            NumericsVector2 center,
            float radius,
            float startAngle,
            float endAngle,
            CameraViewport camera,
            Rect rect)
        {
            const int segments = 24;
            var geometry = new StreamGeometry();
            using (var path = geometry.Open())
            {
                var first = center + new NumericsVector2(MathF.Cos(startAngle), MathF.Sin(startAngle)) * radius;
                path.BeginFigure(ToScreen(first, camera, rect), false, false);
                for (var i = 1; i <= segments; i++)
                {
                    var amount = i / (float)segments;
                    var angle = startAngle + (endAngle - startAngle) * amount;
                    var point = center + new NumericsVector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                    path.LineTo(ToScreen(point, camera, rect), true, false);
                }
            }

            geometry.Freeze();
            context.DrawGeometry(null, LinePen, geometry);
        }

        private void DrawPlayers(DrawingContext context, FieldDimensions field, CameraViewport camera, Rect rect)
        {
            foreach (var player in Snapshot!.Players)
            {
                var center = ToScreen(player.Position, camera, rect);
                var radius = ScaleDistance(player.Radius, camera, rect);
                var brush = player.Role switch
                {
                    PlayerRole.Goalkeeper when player.Team == TeamSide.Home => HomeGoalkeeperBrush,
                    PlayerRole.Goalkeeper => AwayGoalkeeperBrush,
                    _ when player.Team == TeamSide.Home => HomeBrush,
                    _ => AwayBrush
                };
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

        private void DrawBall(DrawingContext context, FieldDimensions field, CameraViewport camera, Rect rect)
        {
            var shadowCenter = ToScreen(Snapshot!.Ball.Position, camera, rect);
            var baseRadius = Math.Max(4d, ScaleDistance(Snapshot.Ball.Radius, camera, rect));
            var heightPixels = ScaleDistance(Snapshot.Ball.Height, camera, rect);
            var ballCenter = new Point(shadowCenter.X, shadowCenter.Y - heightPixels);
            var ballRadius = baseRadius * (1d + Math.Min(0.5d, Snapshot.Ball.Height * 0.045d));
            var shadowRadiusX = baseRadius * Math.Max(0.45d, 1d - Snapshot.Ball.Height * 0.035d);
            var shadowRadiusY = baseRadius * Math.Max(0.25d, 0.55d - Snapshot.Ball.Height * 0.025d);

            context.DrawEllipse(BallShadowBrush, null, shadowCenter, shadowRadiusX, shadowRadiusY);
            context.DrawEllipse(BallBrush, new Pen(Brushes.Black, 1), ballCenter, ballRadius, ballRadius);
        }

        private static Point ToScreen(NumericsVector2 world, CameraViewport camera, Rect rect)
        {
            var x01 = (world.X - camera.Left) / MathF.Max(0.0001f, camera.Width);
            var y01 = (world.Y - camera.Top) / MathF.Max(0.0001f, camera.Height);
            return new Point(rect.Left + x01 * rect.Width, rect.Top + y01 * rect.Height);
        }

        private static double ScaleDistance(float worldDistance, CameraViewport camera, Rect rect)
        {
            return worldDistance / camera.Width * rect.Width;
        }

        private static Point Offset(Point point, NumericsVector2 direction, double distance)
        {
            return new Point(point.X + direction.X * distance, point.Y + direction.Y * distance);
        }

        private static Point LerpPoint(Point a, Point b, double t)
        {
            return new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        }

        private readonly record struct CameraViewport(float Left, float Right, float Top, float Bottom)
        {
            public float Width => Right - Left;
            public float Height => Bottom - Top;
        }
    }
}
