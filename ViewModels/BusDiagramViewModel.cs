using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using DevExpress.Mvvm;
using System.Collections.ObjectModel;
using System.Windows;

namespace TestMcAlgorithm.ViewModels
{
    public sealed class BusDiagram
    {
        private static readonly Brush DefaultPathBrush = CreateBrush("#AAB5C0");
        private static readonly Brush OutputPathBrush = CreateBrush("#BFC9D3");
        private static readonly Brush HighlightBrush = CreateBrush("#F59E0B");

        public Func<string, Task>? KBusClickRequestedAsync { get; set; }

        public BusDiagram()
        {
            var busFeeders = CreateFeeders(
            [
                // MC1
                ("K1", 30, 0),
                ("K2", 65, 1),
                ("K3", 100, 2),
                // MC2
                ("K4", 205, 0),
                // MC3
                ("K5", 310, 0),
                ("K6", 345, 1),
                ("K7", 380, 2),
                // MC4
                ("K8", 485, 0),
                // MC5
                ("K9", 590, 0),
                // MC6
                ("K10", 695, 0),
                // MC7
                ("K11", 800, 0),
                // MC8
                ("K12", 905, 0),
                ("K13", 940, 1),
                ("K14", 975, 2),
                // MC9
                ("K15", 1080, 0),
                ("K16", 1115, 1),
                ("K17", 1150, 2),
                // MC10
                ("K28", 1255, 1),
                ("K29", 1290, 2),
            ]);

            var nBusFeeders = CreateFeeders(
            [
                ("K18", 10, 0),
                ("K19", 115, 0),
                ("K20", 150, 1),
                ("K21", 255, 0),
                ("K22", 290, 1),
                ("K23", 325, 2),
                ("K24", 430, 1),
                ("K25", 465, 2)
            ]);

            Outputs =
            [
                new BusOutputItem("BUS OUT #1", 745, 34, 111, OutputPathBrush),
                new BusOutputItem("BUS OUT #2", 885, 34, 111, OutputPathBrush),
                new BusOutputItem("BUS OUT #3", 1095, 34, 111, OutputPathBrush),

                new BusOutputItem("NBUS OUT #1", 1354, 34, 111, OutputPathBrush),
                new BusOutputItem("NBUS OUT #2", 1496, 34, 111, OutputPathBrush),
                new BusOutputItem("NBUS OUT #3", 1670, 34, 111, OutputPathBrush)
            ];

            Sections =
            [
                new BusSectionItem(
                left: 0,
                top: 145,
                width: 1365,
                currentLabel: "100A",
                inputLabel: "BUS IN",
                outRails: CreateOutRails(1190),
                feeders: busFeeders,
                busTaps: CreateBusTaps(
                [
                    (775, 0),
                    (915, 1),
                    (1125, 2)
                ]),
                mcMarkers: CreateMcMarkers(
                    busFeeders,
                    [
                         // Over Current Relay
                        ("OCR1", ["K1", "K2", "K3"]),
                        ("OCR2", ["K4"]),
                        ("OCR3", ["K5", "K6", "K7"]),
                        ("OCR4", ["K8"]),
                        ("OCR5", ["K9"]),
                        ("OCR6", ["K10"]),
                        ("OCR7", ["K11"]),
                        ("OCR8", ["K12", "K13", "K14"]),
                        ("OCR9", ["K15", "K16", "K17"]),
                        ("OCR10", ["K28", "K29"]),
                    ]),
                inRails: CreateInRails(1265),
                outputStartIndex: 0,
                inputStemX: 714,
                inputStemY: 375,
                inputArriveY: 375,
                showMcMarker: true,
                _margin: new Thickness(120,395,0,0),
                defaultBrush: DefaultPathBrush),

            new BusSectionItem(
                left: 1400,
                top: 145,
                width: 520,
                currentLabel: "63A",
                inputLabel: "NBUS IN",
                outRails: CreateNOutRails(170),
                feeders: nBusFeeders,
                busTaps: CreateBusTaps(
                [
                    (-16, 0),
                    (126, 1),
                    (300, 2)
                ]),
                mcMarkers: [],
                inRails: CreateNInRails(457),
                outputStartIndex: 3,
                inputStemX: 275,
                inputStemY: 315,
                inputArriveY: 345,
                showMcMarker: false,
                _margin: new Thickness(30,345,0,0),
                defaultBrush: DefaultPathBrush)
            ];
        }

        public IReadOnlyList<BusOutputItem> Outputs { get; }

        public IReadOnlyList<BusSectionItem> Sections { get; }

        public Task HandleKBusClickAsync(string feederLabel) =>
            KBusClickRequestedAsync?.Invoke(feederLabel) ?? Task.CompletedTask;

        public void ClearPath()
        {
            foreach (var section in Sections)
            {
                foreach (var feeder in section.Feeders)
                {
                    feeder.IsActive = false;
                }

                RebuildSectionState(section);
            }
        }

        public void SynchronizeFeedback(IReadOnlyDictionary<string, bool> feedbackStates)
        {
            foreach (var section in Sections)
            {
                foreach (var feeder in section.Feeders)
                {
                    feeder.IsActive = feedbackStates.TryGetValue(feeder.Label, out var isOn) && isOn;
                }

                RebuildSectionState(section);
            }
        }

        // update marker currents based on the provided dictionary of current values
        public void UpdateMarkerCurrents(IReadOnlyDictionary<string, double?> currentValues)
        {
            foreach (var marker in Sections.SelectMany(section => section.McMarkers))
            {
                marker.CurrentValue = currentValues.TryGetValue(marker.DeviceName, out var value) ? value : null;
            }
        }

        private void RebuildSectionState(BusSectionItem section)
        {
            // feeder 목록 중에서 활성화된(feeder.IsActive == true) 항목만 추출
            var activeFeeders = section.Feeders
                .Where(feeder => feeder.IsActive)
                .ToList();
            // activeFeeders.Count > 0 이면 HighlightBrush, 그렇지 않으면 DefaultPathBrush 할당
            section.InputStemBrush = activeFeeders.Count > 0
                ? HighlightBrush
                : DefaultPathBrush;
            // segment 목록 초기화
            section.HighlightSegments.Clear();
            // feeder 목록을 순회하면서 feeder.IsActive 값에 따라 feeder.PathBrush 할당
            foreach (var feeder in section.Feeders)
            {
                feeder.PathBrush = feeder.IsActive
                    ? HighlightBrush
                    : DefaultPathBrush;
            }
            // busTap 목록을 순회하면서 tap.PathBrush 할당 및 Outputs[section.OutputStartIndex + tap.RailIndex].PathBrush 할당
            foreach (var tap in section.BusTaps)
            {
                var hasActiveFeederOnRail = activeFeeders.Any(feeder => feeder.RailIndex == tap.RailIndex);
                var output = Outputs[section.OutputStartIndex + tap.RailIndex];

                tap.PathBrush = hasActiveFeederOnRail
                    ? HighlightBrush
                    : OutputPathBrush;

                output.PathBrush = hasActiveFeederOnRail
                    ? HighlightBrush
                    : OutputPathBrush;
                output.IsOn = hasActiveFeederOnRail;
            }

            foreach (var activeFeeder in activeFeeders)
            {
                var tap = section.BusTaps.FirstOrDefault(item => item.RailIndex == activeFeeder.RailIndex);
                if (tap is null)
                {
                    continue;
                }

                section.HighlightSegments.Add(
                    new SectionPathSegment(
                        section.InputStemX,
                        315,
                        activeFeeder.CenterX,
                        315,
                        2,
                        HighlightBrush));

                section.HighlightSegments.Add(
                    new SectionPathSegment(
                        activeFeeder.CenterX,
                        RailCenterY(activeFeeder.RailIndex),
                        tap.CenterX + 37,
                        RailCenterY(activeFeeder.RailIndex),
                        3,
                        HighlightBrush));
            }
        }
        private static IReadOnlyList<BusRailItem> CreateOutRails(double width) =>
        [
            new BusRailItem(49, 18, width - 135),
            new BusRailItem(83, 36, width),
            new BusRailItem(118, 54, width)
        ]; private static IReadOnlyList<BusRailItem> CreateNOutRails(double width) =>
        [
            new BusRailItem(26, 18, width + 77),
            new BusRailItem(167, 36, width + 116),
            new BusRailItem(345, 54, width - 30)
        ];

        private static IReadOnlyList<BusRailItem> CreateInRails(double width) =>
        [
            new BusRailItem(49, 315, width - 3)
        ];
        private static IReadOnlyList<BusRailItem> CreateNInRails(double width) =>
        [
            new BusRailItem(29, 315, width)
        ];
        private static IReadOnlyList<BusTapItem> CreateBusTaps(IEnumerable<(double Left, int RailIndex)> tapPositions)
        {
            var items = new List<BusTapItem>();

            foreach (var (left, railIndex) in tapPositions)
            {
                items.Add(new BusTapItem(left, railIndex, OutputPathBrush));
            }

            return items;
        }

        private static IReadOnlyList<FeederItem> CreateFeeders(IEnumerable<(string Label, double Left, int RailIndex)> feeders)
        {
            var items = new List<FeederItem>();

            foreach (var (label, left, railIndex) in feeders)
            {
                items.Add(new FeederItem(label, left, railIndex, DefaultPathBrush));
            }

            return items;
        }

        private static IReadOnlyList<McMarkerItem> CreateMcMarkers(
            IReadOnlyList<FeederItem> feeders,
            IEnumerable<(string Label, IReadOnlyList<string> FeederLabels)> groups)
        {
            var items = new List<McMarkerItem>();

            foreach (var (label, feederLabels) in groups)
            {
                var targets = feeders
                    .Where(feeder => feederLabels.Contains(feeder.Label, StringComparer.OrdinalIgnoreCase))
                    .ToArray();

                if (targets.Length == 0)
                {
                    continue;
                }

                var centerX = targets.Average(feeder => feeder.CenterX);
                items.Add(new McMarkerItem(label, centerX - 53, 320));
            }

            return items;
        }

        private static double RailCenterY(int railIndex) =>
            railIndex switch
            {
                0 => 19,
                1 => 37,
                _ => 55
            };

        private static Brush CreateBrush(string hex)
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            brush.Freeze();
            return brush;
        }
    }

    public sealed class BusOutputItem : BindableBase
    {
        private bool _isOn;
        private Brush _pathBrush;

        public BusOutputItem(string title, double left, double top, double dropHeight, Brush pathBrush)
        {
            Title = title;
            Left = left;
            Top = top;
            DropHeight = dropHeight;
            _pathBrush = pathBrush;
        }

        public string Title { get; }

        public double Left { get; }

        public double Top { get; }

        public double DropHeight { get; }

        public double ContactTop => DropHeight - 2.75;

        public bool IsOn
        {
            get => _isOn;
            set => SetProperty(ref _isOn, value, nameof(IsOn));
        }

        public Brush PathBrush
        {
            get => _pathBrush;
            set => SetProperty(ref _pathBrush, value, nameof(PathBrush));
        }
    }

    public sealed class BusSectionItem : BindableBase
    {
        private Brush _inputStemBrush;

        public BusSectionItem(
            double left,
            double top,
            double width,
            string currentLabel,
            string inputLabel,
            IReadOnlyList<BusRailItem> outRails,
            IReadOnlyList<FeederItem> feeders,
            IReadOnlyList<BusTapItem> busTaps,
            IReadOnlyList<McMarkerItem> mcMarkers,
            IReadOnlyList<BusRailItem> inRails,
            int outputStartIndex,
            double inputStemX,
            double inputStemY,
            double inputArriveY,
            bool showMcMarker,
            Brush defaultBrush,
            Thickness _margin)
        {
            Left = left;
            Top = top;
            Width = width;
            CurrentLabel = currentLabel;
            InputLabel = inputLabel;
            OutRails = outRails;
            Feeders = feeders;
            BusTaps = busTaps;
            McMarkers = mcMarkers;
            InRails = inRails;
            OutputStartIndex = outputStartIndex;
            InputStemX = inputStemX;
            InputStemY = inputStemY;
            InputArriveY = inputArriveY;
            ShowMcMarker = showMcMarker;
            _inputStemBrush = defaultBrush;
            Margin = _margin;
        }

        public double Left { get; }

        public double Top { get; }

        public double Width { get; }

        public string CurrentLabel { get; }

        public string InputLabel { get; }

        public IReadOnlyList<BusRailItem> OutRails { get; }

        public IReadOnlyList<FeederItem> Feeders { get; }

        public IReadOnlyList<BusTapItem> BusTaps { get; }

        public IReadOnlyList<McMarkerItem> McMarkers { get; }

        public IReadOnlyList<BusRailItem> InRails { get; }

        public int OutputStartIndex { get; }

        public double InputStemX { get; }
        public double InputStemY { get; }
        public double InputArriveY { get; }

        public bool ShowMcMarker { get; }

        public ObservableCollection<SectionPathSegment> HighlightSegments { get; } = [];
        public Thickness Margin { get; }

        public Brush InputStemBrush
        {
            get => _inputStemBrush;
            set => SetProperty(ref _inputStemBrush, value, nameof(InputStemBrush));
        }
    }

    public sealed record BusRailItem(double Left, double Top, double Width);

    public sealed class McMarkerItem : BindableBase
    {
        private double? _currentValue;

        public McMarkerItem(string label, double left, double top)
        {
            Label = label;
            DeviceName = label;
            Left = left;
            Top = top;
        }

        public string Label { get; }

        public string DeviceName { get; }

        public double Left { get; }

        public double Top { get; }

        public double? CurrentValue
        {
            get => _currentValue;
            set
            {
                if (SetProperty(ref _currentValue, value, nameof(CurrentValue)))
                {
                    RaisePropertyChanged(nameof(CurrentText));
                    RaisePropertyChanged(nameof(CurrentBrush));
                }
            }
        }

        public string CurrentText => CurrentValue.HasValue ? $"{CurrentValue.Value:0.0}A" : "-";

        public Brush CurrentBrush => CurrentValue.HasValue
            ? CreateLocalBrush("Lime")
            : CreateLocalBrush("#94A3B8");

        private static Brush CreateLocalBrush(string hex)
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            brush.Freeze();
            return brush;
        }
    }

    public sealed class BusTapItem : BindableBase
    {
        private Brush _pathBrush;

        public BusTapItem(double left, int railIndex, Brush pathBrush)
        {
            Left = left;
            RailIndex = railIndex;
            _pathBrush = pathBrush;
        }

        public double Left { get; }

        public int RailIndex { get; }

        public double CenterX => Left + 7;

        public double ConnectionY => RailIndex switch
        {
            0 => 19,
            1 => 37,
            _ => 55
        };

        public double ContactTop => ConnectionY - 3;

        public Brush PathBrush
        {
            get => _pathBrush;
            set => SetProperty(ref _pathBrush, value, nameof(PathBrush));
        }
    }

    public sealed class FeederItem : BindableBase
    {
        private bool _isActive;
        private Brush _pathBrush;

        public FeederItem(string label, double left, int railIndex, Brush pathBrush)
        {
            Label = label;
            Left = left;
            RailIndex = railIndex;
            _pathBrush = pathBrush;
        }

        public string Label { get; }

        public double Left { get; }

        public int RailIndex { get; }

        public double CenterX => Left + 20;

        public double ConnectionY => RailIndex switch
        {
            0 => 4,
            1 => 22,
            _ => 40
        };

        public double ContactTop => ConnectionY - 3;

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value, nameof(IsActive));
        }

        public Brush PathBrush
        {
            get => _pathBrush;
            set => SetProperty(ref _pathBrush, value, nameof(PathBrush));
        }
    }

    public sealed record SectionPathSegment(
        double X1,
        double Y1,
        double X2,
        double Y2,
        double Thickness,
        Brush Stroke);
}
