using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NAudio.Wave;

namespace StageCue;

public partial class MainWindow : Window
{
    public ObservableCollection<Track> Tracks { get; } = new();
    public Track? CurrentTrack { get; private set; }
    private readonly MediaPlayer _player = new();
    private readonly string _dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StageCue");
    private readonly string _libraryFile;
    private HwndSource? _source;
    private bool _playing;
    private bool _mediaReady;
    private bool _pendingPlay;
    private bool _updatingTimeline;
    private bool _isSeeking;
    private bool _seekRequested;
    private bool _hasTimelineSelection;
    private TimeSpan _selectedPosition = TimeSpan.Zero;
    private TimeSpan _pendingPosition = TimeSpan.Zero;
    private double[] _waveformPeaks = Array.Empty<double>();
    private readonly HashSet<int> _registeredHotkeyIds = new();
    private readonly DispatcherTimer _timelineTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_NOREPEAT = 0x4000;
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Tracks.CollectionChanged += (_, _) => PlaylistCount.Text = Tracks.Count.ToString();
        _player.Volume = 0.8;
        Directory.CreateDirectory(_dataDir);
        _libraryFile = Path.Combine(_dataDir, "library.json");
        LoadLibrary();
        PageFrame.Navigate(new LibraryPage(this));
        Loaded += (_, _) => RegisterKeys();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        SourceInitialized += (_, _) => { _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle); _source?.AddHook(WndProc); };
        Closed += (_, _) => { UnregisterKeys(); SaveLibrary(); _timelineTimer.Stop(); _player.Close(); };
        _player.MediaOpened += (_, _) =>
        {
            _mediaReady = true;
            _player.Position = ClampPosition(_pendingPosition);
            UpdateTimelineUi();
            if (_pendingPlay) { _pendingPlay = false; _player.Play(); _playing = true; }
            UpdatePlayerUi();
        };
        _player.MediaEnded += (_, _) => { _playing = false; Dispatcher.Invoke(() => { UpdatePlayerUi(); UpdateTimelineUi(); }); };
        _player.MediaFailed += (_, e) => { _mediaReady = false; _pendingPlay = false; MessageBox.Show("Не удалось открыть аудио: " + e.ErrorException?.Message, "StageCue"); };
        _timelineTimer.Tick += (_, _) => { if (!_isSeeking) UpdateTimelineUi(); };
        _timelineTimer.Start();
    }

    public void ShowPage(Page page) => PageFrame.Navigate(page);

    public void SelectTrack(Track track, bool autoplay)
    {
        if (CurrentTrack == track)
        {
            if (autoplay) Play();
            UpdatePlayerUi();
            return;
        }

        CurrentTrack = track;
        LoadWaveform(track.Path);
        UpdateCurrentCover(track.Path);
        _playing = false;
        _mediaReady = false;
        _pendingPlay = autoplay;
        _selectedPosition = TimeSpan.Zero;
        _pendingPosition = TimeSpan.Zero;
        _seekRequested = false;
        _hasTimelineSelection = false;
        try
        {
            _player.Stop();
            _player.Open(new Uri(track.Path));
            UpdatePlayerUi();
            UpdateTimelineUi();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Ошибка аудио"); }
    }

    public void Play()
    {
        if (CurrentTrack is null) return;
        if (!_mediaReady)
        {
            if (_player.Source is null || _player.Source.LocalPath != CurrentTrack.Path)
            {
                _pendingPosition = _seekRequested ? _selectedPosition : TimeSpan.Zero;
                _pendingPlay = true;
                _player.Open(new Uri(CurrentTrack.Path));
            }
            else _pendingPlay = true;
            return;
        }

        if (_seekRequested)
        {
            _player.Position = ClampPosition(_selectedPosition);
            _seekRequested = false;
        }
        _player.Play();
        _playing = true;
        UpdatePlayerUi();
    }

    public void Pause()
    {
        _player.Pause();
        _playing = false;
        UpdatePlayerUi();
        UpdateTimelineUi();
    }

    public void TogglePlay()
    {
        if (_playing) Pause();
        else Play();
    }

    public void Restart()
    {
        if (CurrentTrack is null) return;
        _selectedPosition = TimeSpan.Zero;
        _seekRequested = true;
        if (_mediaReady)
        {
            _player.Position = TimeSpan.Zero;
            _seekRequested = false;
            _player.Play();
            _playing = true;
            UpdatePlayerUi();
        }
        else
        {
            _pendingPosition = TimeSpan.Zero;
            _pendingPlay = true;
            if (_player.Source is null) _player.Open(new Uri(CurrentTrack.Path));
        }
        UpdateTimelineUi();
    }

    // Ctrl+Space stores the selected timeline position as this track's persistent cue point.
    public void PlayFromCue()
    {
        if (CurrentTrack is null) return;
        if (_hasTimelineSelection)
            CurrentTrack.CuePositionSeconds = Math.Max(0, _selectedPosition.TotalSeconds);
        else if (_mediaReady)
            CurrentTrack.CuePositionSeconds = Math.Max(0, _player.Position.TotalSeconds);
        SaveLibrary();
        var cue = TimeSpan.FromSeconds(Math.Max(0, CurrentTrack.CuePositionSeconds));
        _selectedPosition = cue;
        _seekRequested = true;
        if (_mediaReady)
        {
            _player.Position = ClampPosition(cue);
            _seekRequested = false;
            _player.Play();
            _playing = true;
            UpdatePlayerUi();
            UpdateTimelineUi();
        }
        else
        {
            _pendingPosition = cue;
            _pendingPlay = true;
            if (_player.Source is null) _player.Open(new Uri(CurrentTrack.Path));
        }
        UpdateCueUi();
    }

    private TimeSpan ClampPosition(TimeSpan value)
    {
        if (_player.NaturalDuration.HasTimeSpan && value > _player.NaturalDuration.TimeSpan)
            return _player.NaturalDuration.TimeSpan;
        return value < TimeSpan.Zero ? TimeSpan.Zero : value;
    }

    private static string FormatTime(TimeSpan t) => $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";

    private void UpdateTimelineUi()
    {
        if (!IsLoaded) return;
        var duration = _player.NaturalDuration.HasTimeSpan ? _player.NaturalDuration.TimeSpan : TimeSpan.Zero;
        var position = _mediaReady ? _player.Position : _selectedPosition;
        if (!_isSeeking)
        {
            _updatingTimeline = true;
            TimelineSlider.Value = duration.TotalSeconds > 0
                ? Math.Clamp(position.TotalSeconds / duration.TotalSeconds * 1000, 0, 1000)
                : 0;
            _updatingTimeline = false;
        }
        TimelineTime.Text = $"{FormatTime(position)} / {FormatTime(duration)}";
        DrawWaveform(position, duration);
        UpdateCueUi();
    }


    private void LoadWaveform(string path)
    {
        try
        {
            using var reader = new AudioFileReader(path);
            const int bins = 520;
            var peaks = new double[bins];
            var totalSamples = Math.Max(1L, (long)(reader.TotalTime.TotalSeconds * reader.WaveFormat.SampleRate * reader.WaveFormat.Channels));
            var buffer = new float[8192];
            long sampleIndex = 0;
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    int bin = (int)Math.Min(bins - 1, sampleIndex * bins / totalSamples);
                    double value = Math.Abs(buffer[i]);
                    if (value > peaks[bin]) peaks[bin] = value;
                    sampleIndex++;
                }
            }
            double max = peaks.Max();
            _waveformPeaks = max > 0 ? peaks.Select(v => v / max).ToArray() : peaks;
        }
        catch { _waveformPeaks = Array.Empty<double>(); }
        if (WaveformCanvas != null) DrawWaveform(TimeSpan.Zero, TimeSpan.Zero);
    }

    private void DrawWaveform(TimeSpan position, TimeSpan duration)
    {
        if (WaveformCanvas == null || WaveformCanvas.ActualWidth < 2 || WaveformCanvas.ActualHeight < 2) return;
        WaveformCanvas.Children.Clear();
        double width = WaveformCanvas.ActualWidth;
        double height = WaveformCanvas.ActualHeight;
        double mid = height / 2;
        double progress = duration.TotalSeconds > 0 ? Math.Clamp(position.TotalSeconds / duration.TotalSeconds, 0, 1) : 0;
        int count = _waveformPeaks.Length;
        if (count == 0)
        {
            WaveformCanvas.Children.Add(new System.Windows.Shapes.Line { X1 = 0, X2 = width, Y1 = mid, Y2 = mid, Stroke = new SolidColorBrush(Color.FromRgb(100, 110, 119)), StrokeThickness = 1 });
            return;
        }
        double step = width / count;
        for (int i = 0; i < count; i++)
        {
            double amp = Math.Max(1.5, _waveformPeaks[i] * (height * .46));
            var bar = new System.Windows.Shapes.Rectangle
            {
                Width = Math.Max(1, step * .72), Height = amp * 2,
                RadiusX = 1, RadiusY = 1,
                Fill = (double)i / count <= progress
                    ? new SolidColorBrush(Color.FromRgb(75, 141, 206))
                    : new SolidColorBrush(Color.FromRgb(196, 201, 205))
            };
            Canvas.SetLeft(bar, i * step);
            Canvas.SetTop(bar, mid - amp);
            WaveformCanvas.Children.Add(bar);
        }
        WaveformCanvas.Children.Add(new System.Windows.Shapes.Line { X1 = progress * width, X2 = progress * width, Y1 = 0, Y2 = height, Stroke = new SolidColorBrush(Color.FromRgb(230, 235, 239)), StrokeThickness = 1 });
    }

    private void WaveformCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var duration = _player.NaturalDuration.HasTimeSpan ? _player.NaturalDuration.TimeSpan : TimeSpan.Zero;
        DrawWaveform(_mediaReady ? _player.Position : _selectedPosition, duration);
    }

    private void WaveformCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (CurrentTrack is null) return;
        var point = e.GetPosition(WaveformCanvas);
        double ratio = Math.Clamp(point.X / Math.Max(1, WaveformCanvas.ActualWidth), 0, 1);
        var duration = _player.NaturalDuration.HasTimeSpan ? _player.NaturalDuration.TimeSpan : TimeSpan.Zero;
        if (duration <= TimeSpan.Zero) return;
        _selectedPosition = TimeSpan.FromSeconds(duration.TotalSeconds * ratio);
        _seekRequested = true;
        _hasTimelineSelection = true;
        if (_mediaReady) CommitSeek();
        else UpdateTimelineUi();
        e.Handled = true;
    }

    private void UpdateCueUi()
    {
        if (CurrentTrack is null || CurrentTrack.CuePositionSeconds <= 0)
            CueTime.Text = "Точка старта: не задана";
        else
            CueTime.Text = $"Точка старта: {FormatTime(TimeSpan.FromSeconds(CurrentTrack.CuePositionSeconds))}  •  Ctrl+Пробел";
    }

    private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingTimeline || CurrentTrack is null) return;
        _isSeeking = true;
        var duration = _player.NaturalDuration.HasTimeSpan ? _player.NaturalDuration.TimeSpan : TimeSpan.Zero;
        if (duration > TimeSpan.Zero)
        {
            _selectedPosition = TimeSpan.FromSeconds(duration.TotalSeconds * TimelineSlider.Value / 1000.0);
            _seekRequested = true;
            _hasTimelineSelection = true;
            TimelineTime.Text = $"{FormatTime(_selectedPosition)} / {FormatTime(duration)}";
        }
    }

    private void TimelineSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => CommitSeek();
    private void TimelineSlider_PreviewKeyUp(object sender, KeyEventArgs e) => CommitSeek();

    private void CommitSeek()
    {
        _isSeeking = false;
        if (_seekRequested && _mediaReady)
        {
            _player.Position = ClampPosition(_selectedPosition);
            _seekRequested = false;
        }
        UpdateTimelineUi();
    }

    private void UpdateCurrentCover(string path)
    {
        try
        {
            var pictures = TagLib.File.Create(path).Tag.Pictures;
            if (pictures.Length == 0) { NowCoverImage.Source = null; return; }
            using var stream = new MemoryStream(pictures[0].Data.Data);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            NowCoverImage.Source = bitmap;
        }
        catch { NowCoverImage.Source = null; }
    }

    private void UpdatePlayerUi()
    {
        NowTitle.Text = CurrentTrack?.Name ?? "Ничего не играет";
        if (CurrentTrack == null) NowCoverImage.Source = null;
        NowSub.Text = CurrentTrack is null ? "Выбери трек в фонотеке или нажми горячую клавишу" : $"Трек №{CurrentTrack.Number}  •  {Path.GetFileName(CurrentTrack.Path)}";
        PlayButton.Content = _playing ? "Ⅱ" : "▶";
        if (PlaylistList != null && CurrentTrack != null && PlaylistList.SelectedItem != CurrentTrack)
            PlaylistList.SelectedItem = CurrentTrack;
        if (StatusText != null) StatusText.Text = _playing ? "ВОСПРОИЗВЕДЕНИЕ" : (CurrentTrack == null ? "ГОТОВ К РАБОТЕ" : "ТРЕК ВЫБРАН");
        UpdateCueUi();
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e) => TogglePlay();
    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _player.Stop(); _playing = false; _selectedPosition = TimeSpan.Zero; _seekRequested = false;
        UpdatePlayerUi(); UpdateTimelineUi();
    }
    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_player != null) _player.Volume = VolumeSlider.Value;
    }
    private void PlaylistList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PlaylistList.SelectedItem is Track track && track != CurrentTrack) SelectTrack(track, false);
    }
    private void PlaylistList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PlaylistList.SelectedItem is Track track) SelectTrack(track, true);
    }
    private void RestartButton_Click(object sender, RoutedEventArgs e) => Restart();
    private void NavHome_Click(object sender, RoutedEventArgs e) => ShowPage(new OverviewPage(this));
    private void NavFeed_Click(object sender, RoutedEventArgs e) => ShowPage(new FeedPage(this));
    private void NavLibrary_Click(object sender, RoutedEventArgs e) => ShowPage(new LibraryPage(this));
    private void AddFilesButton_Click(object sender, RoutedEventArgs e) => ShowPage(new LibraryPage(this, true));
    private void NavKeys_Click(object sender, RoutedEventArgs e) => ShowPage(new HotkeysPage(this));
    private void NavSettings_Click(object sender, RoutedEventArgs e) => ShowPage(new SettingsPage(this));

    private void RegisterKeys()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        for (int i = 0; i < 10; i++)
        {
            int vk = i == 0 ? 0x30 : 0x30 + i;
            TryRegisterHotkey(hwnd, 100 + i, MOD_NOREPEAT, (uint)vk);
            TryRegisterHotkey(hwnd, 110 + i, MOD_ALT | MOD_NOREPEAT, (uint)vk);
            TryRegisterHotkey(hwnd, 120 + i, MOD_CONTROL | MOD_NOREPEAT, (uint)vk);
            TryRegisterHotkey(hwnd, 130 + i, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)vk);
        }
        TryRegisterHotkey(hwnd, 200, MOD_NOREPEAT, 0x20);
        TryRegisterHotkey(hwnd, 201, MOD_NOREPEAT, 0x0D);
        TryRegisterHotkey(hwnd, 202, MOD_CONTROL | MOD_NOREPEAT, 0x20);
    }

    private void TryRegisterHotkey(IntPtr hwnd, int id, uint modifiers, uint key)
    {
        if (RegisterHotKey(hwnd, id, modifiers, key)) _registeredHotkeyIds.Add(id);
    }

    private void UnregisterKeys()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        foreach (int id in _registeredHotkeyIds) UnregisterHotKey(hwnd, id);
        _registeredHotkeyIds.Clear();
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase ||
            Keyboard.FocusedElement is PasswordBox || Keyboard.FocusedElement is ComboBox)
            return;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        int digit = key switch
        {
            Key.D0 or Key.NumPad0 => 0,
            >= Key.D1 and <= Key.D9 => (int)key - (int)Key.D0,
            >= Key.NumPad1 and <= Key.NumPad9 => (int)key - (int)Key.NumPad0,
            _ => -1
        };
        if (digit >= 0)
        {
            ModifierKeys mods = Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt);
            int group = mods switch
            {
                ModifierKeys.None => 0,
                ModifierKeys.Alt => 1,
                ModifierKeys.Control => 2,
                ModifierKeys.Control | ModifierKeys.Alt => 3,
                _ => -1
            };
            if (group >= 0)
            {
                int id = 100 + group * 10 + digit;
                if (!_registeredHotkeyIds.Contains(id))
                    LaunchNumber(group * 10 + (digit == 0 ? 10 : digit));
                e.Handled = true;
                return;
            }
        }

        if (key == Key.Space && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == ModifierKeys.None)
        {
            if (!_registeredHotkeyIds.Contains(200)) TogglePlay();
            e.Handled = true;
        }
        else if (key == Key.Enter && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == ModifierKeys.None)
        {
            if (!_registeredHotkeyIds.Contains(201)) Restart();
            e.Handled = true;
        }
        else if (key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (!_registeredHotkeyIds.Contains(202)) PlayFromCue();
            e.Handled = true;
        }
    }

    private void LaunchNumber(int number)
    {
        var track = Tracks.FirstOrDefault(t => t.Number == number);
        if (track != null) SelectTrack(track, true);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == 200) TogglePlay();
            else if (id == 201) Restart();
            else if (id == 202) PlayFromCue();
            else
            {
                int group = (id - 100) / 10;
                int digit = (id - 100) % 10;
                LaunchNumber(group * 10 + (digit == 0 ? 10 : digit));
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(File.Exists))
        {
            if (Tracks.Any(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
            int n = Enumerable.Range(1, 40).FirstOrDefault(candidate => !Tracks.Any(t => t.Number == candidate));
            if (n == 0) { n = 41; while (Tracks.Any(t => t.Number == n)) n++; }
            Tracks.Add(new Track { Number = n, Name = Path.GetFileNameWithoutExtension(path), Path = path });
        }
        SaveLibrary();
    }

    public void RemoveTrack(Track track)
    {
        if (CurrentTrack == track)
        {
            Pause(); CurrentTrack = null; _mediaReady = false; _pendingPlay = false;
            _player.Close(); _waveformPeaks = Array.Empty<double>();
            WaveformCanvas.Children.Clear(); NowCoverImage.Source = null;
            _selectedPosition = TimeSpan.Zero; _hasTimelineSelection = false;
            UpdatePlayerUi(); UpdateTimelineUi();
        }
        Tracks.Remove(track);
        PlaylistList.SelectedItem = null;
        SaveLibrary();
        PlaylistCount.Text = Tracks.Count.ToString();
    }

    public void SaveLibrary() => File.WriteAllText(_libraryFile, System.Text.Json.JsonSerializer.Serialize(Tracks));

    private void LoadLibrary()
    {
        try
        {
            if (File.Exists(_libraryFile))
            {
                var list = System.Text.Json.JsonSerializer.Deserialize<List<Track>>(File.ReadAllText(_libraryFile));
                if (list != null) foreach (var t in list.Where(t => File.Exists(t.Path))) Tracks.Add(t);
            }
        }
        catch { }
    }
}

public class Track
{
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public double CuePositionSeconds { get; set; }
    public override string ToString() => $"{Number:00}   {Name}";
}
