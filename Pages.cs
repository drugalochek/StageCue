using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StageCue;

internal static class Theme
{
    public static readonly Brush Panel = Brush("#252B32");
    public static readonly Brush Panel2 = Brush("#303943");
    public static readonly Brush Line = Brush("#46515D");
    public static readonly Brush Muted = Brush("#9BA8B5");
    public static readonly Brush Accent = Brush("#76A9D8");
    public static readonly Brush White = Brush("#E8EDF2");
    public static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    public static TextBlock Text(string value, double size=13, bool bold=false, Brush? color=null) =>
        new() { Text=value, FontSize=size, FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal, Foreground=color??White, TextWrapping=TextWrapping.Wrap };
    public static Border Card(UIElement child, Thickness? margin=null, Thickness? padding=null) =>
        new() { Background=Panel, BorderBrush=Line, BorderThickness=new Thickness(1), CornerRadius=new CornerRadius(12),
            Padding=padding??new Thickness(17), Margin=margin??new Thickness(0,0,0,12), Child=child };
    public static Button Button(string text, RoutedEventHandler click, bool accent=false)
    {
        var b=new Button { Content=text, Background=accent?Accent:Panel2, Foreground=accent?Brush("#17212B"):White,
            Padding=new Thickness(14,10,14,10), Margin=new Thickness(0,0,8,0), FontWeight=FontWeights.SemiBold };
        b.Click+=click; return b;
    }
}
public abstract class BasePage : Page
{
    protected readonly MainWindow AppWindow;
    protected StackPanel Root { get; } = new();
    protected BasePage(MainWindow app, string title, string subtitle)
    {
        AppWindow=app; Background=Brushes.Transparent;
        Root.Margin=new Thickness(2,2,5,10);
        Root.Children.Add(Theme.Text(title,27,true));
        Root.Children.Add(Theme.Text(subtitle,12,false,Theme.Muted));
        ((TextBlock)Root.Children[1]).Margin=new Thickness(0,5,0,22);
        Content=new ScrollViewer { VerticalScrollBarVisibility=ScrollBarVisibility.Auto, Content=Root };
    }
    protected void Section(string text) { var t=Theme.Text(text.ToUpperInvariant(),10,true,Theme.Muted); t.Margin=new Thickness(1,3,0,10); Root.Children.Add(t); }
}
public class OverviewPage : BasePage
{
    public OverviewPage(MainWindow app):base(app,"Главная","Быстрый доступ к фонограммам и управлению выступлением")
    {
        var hero=new Grid { MinHeight=150 };
        hero.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1.4,GridUnitType.Star)});
        hero.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        var left=new StackPanel { VerticalAlignment=VerticalAlignment.Center };
        left.Children.Add(Theme.Text("STAGE CONTROL",10,true,Theme.Accent));
        left.Children.Add(Theme.Text("Твой звук.\nТвой момент.",29,true));
        left.Children.Add(Theme.Text("Фонотека и быстрые команды для сцены.",12,false,Theme.Muted));
        var add=Theme.Button("＋  Добавить фонограммы",(_,_)=>app.ShowPage(new LibraryPage(app,true)),true); add.Margin=new Thickness(0,17,0,0); add.HorizontalAlignment=HorizontalAlignment.Left;
        left.Children.Add(add); hero.Children.Add(left);
        var stats=new StackPanel { VerticalAlignment=VerticalAlignment.Center, Margin=new Thickness(25,0,0,0) };
        stats.Children.Add(Theme.Text("ТРЕКОВ В ФОНОТЕКЕ",10,true,Theme.Muted));
        stats.Children.Add(Theme.Text(app.Tracks.Count.ToString(),42,true,Theme.Accent));
        stats.Children.Add(Theme.Text("из них назначено по номерам 1–40",11,false,Theme.Muted));
        Grid.SetColumn(stats,1); hero.Children.Add(stats); Root.Children.Add(Theme.Card(hero));
        Section("Быстрый запуск");
        var grid=new UniformGrid { Columns=5 };
        for(int n=1;n<=10;n++)
        {
            var track=app.Tracks.FirstOrDefault(t=>t.Number==n);
            var content=new StackPanel();
            content.Children.Add(Theme.Text(n.ToString("00"),18,true,Theme.Accent));
            content.Children.Add(Theme.Text(track?.Name??"Пусто",10,false,track is null?Theme.Muted:Theme.White));
            var b=new Button { Content=content, Background=Theme.Panel2, BorderBrush=Theme.Line, BorderThickness=new Thickness(1), Margin=new Thickness(0,0,8,8), Padding=new Thickness(10), HorizontalContentAlignment=HorizontalAlignment.Left };
            if(track!=null) b.Click+=(_,_)=>app.SelectTrack(track,true);
            grid.Children.Add(b);
        }
        Root.Children.Add(Theme.Card(grid));
        Section("Управление");
        var hint=new Grid(); hint.ColumnDefinitions.Add(new ColumnDefinition()); hint.ColumnDefinitions.Add(new ColumnDefinition());
        hint.Children.Add(Theme.Text("ПРОБЕЛ\nИграть / пауза",13,true));
        var h2=Theme.Text("CTRL + ПРОБЕЛ\nСохранить точку старта и запустить",13,true); Grid.SetColumn(h2,1); hint.Children.Add(h2);
        Root.Children.Add(Theme.Card(hint));
    }
}
public class FeedPage : BasePage
{
    public FeedPage(MainWindow app):base(app,"Моя лента","Закреплённые и быстрые фонограммы для текущего выступления")
    {
        Section("Номера быстрого доступа");
        var grid=new UniformGrid{Columns=4};
        for(int n=1;n<=40;n++)
        {
            int number=n; var track=app.Tracks.FirstOrDefault(t=>t.Number==number);
            var stack=new StackPanel();
            stack.Children.Add(Theme.Text(number.ToString("00"),20,true,Theme.Accent));
            stack.Children.Add(Theme.Text(track?.Name??"Не назначено",11,true));
            stack.Children.Add(Theme.Text(track is null?"Пустой слот":"НАЖМИ ДЛЯ ЗАПУСКА",8,true,Theme.Muted));
            var b=new Button{Content=stack,Background=Theme.Panel,BorderBrush=Theme.Line,BorderThickness=new Thickness(1),Padding=new Thickness(12),Margin=new Thickness(0,0,9,9),HorizontalContentAlignment=HorizontalAlignment.Left};
            if(track!=null) b.Click+=(_,_)=>app.SelectTrack(track,true);
            grid.Children.Add(b);
        }
        Root.Children.Add(grid);
    }
}
public class LibraryPage : Page
{
    private readonly MainWindow _app;
    private readonly WrapPanel _tiles = new() { Margin = new Thickness(8) };
    private readonly TextBox _search = new() { Width = 210, Padding = new Thickness(7, 4, 7, 4), ToolTip = "Поиск по названию и пути" };
    private readonly TextBox _rename = new() { Width = 190, Padding = new Thickness(7, 4, 7, 4), ToolTip = "Название фонограммы" };
    private readonly TextBox _number = new() { Width = 48, Padding = new Thickness(6, 4, 6, 4), ToolTip = "Номер быстрого запуска 1–40" };
    private readonly TextBlock _count = new() { Foreground = Theme.Muted, FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
    private Track? _selected;

    public LibraryPage(MainWindow app, bool openPicker = false)
    {
        _app = app;
        Background = Brushes.Transparent;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var top = new DockPanel { LastChildFill = true, Margin = new Thickness(9, 8, 9, 4) };
        _count.Text = "ЛОКАЛЬНЫЕ ФАЙЛЫ";
        DockPanel.SetDock(_count, Dock.Left);
        top.Children.Add(_count);
        _search.Width = 210;
        _search.TextChanged += (_, _) => Refresh();
        DockPanel.SetDock(_search, Dock.Right);
        top.Children.Add(_search);
        Grid.SetRow(top, 0);
        root.Children.Add(top);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _tiles
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        var editor = new Border
        {
            Background = Theme.Brush("#292929"),
            BorderBrush = Theme.Brush("#414141"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(9, 7, 9, 7)
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var add = new Button { Content = "＋ Добавить", Background = Theme.Accent, Foreground = Brushes.White, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0) };
        add.Click += (_, _) => AddFiles();
        row.Children.Add(add);
        row.Children.Add(_rename);
        var assign = new Button { Content = "Сохранить", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(6, 0, 8, 0) };
        assign.Click += (_, _) => SaveSelected();
        row.Children.Add(assign);
        row.Children.Add(_number);
        var setNumber = new Button { Content = "Назначить №", Padding = new Thickness(9, 5, 9, 5), Margin = new Thickness(6, 0, 8, 0) };
        setNumber.Click += (_, _) => SetNumber();
        row.Children.Add(setNumber);
        var remove = new Button { Content = "Удалить", Padding = new Thickness(10, 5, 10, 5), Background = Theme.Brush("#49343A") };
        remove.Click += (_, _) => RemoveSelected();
        row.Children.Add(remove);
        editor.Child = row;
        Grid.SetRow(editor, 2);
        root.Children.Add(editor);

        Content = root;
        Refresh();
        if (openPicker) Loaded += (_, _) => AddFiles();
    }

    private void AddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Аудиофайлы|*.mp3;*.wav;*.wma;*.m4a;*.aac;*.flac;*.ogg;*.aiff|Все файлы|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            _app.AddFiles(dialog.FileNames);
            Refresh();
        }
    }

    private void SaveSelected()
    {
        if (_selected == null) return;
        if (!string.IsNullOrWhiteSpace(_rename.Text))
            _selected.Name = _rename.Text.Trim();
        _app.SaveLibrary();
        Refresh();
    }

    private void SetNumber()
    {
        if (_selected == null) return;
        if (!int.TryParse(_number.Text, out int number) || number < 1 || number > 40)
        {
            MessageBox.Show("Номер быстрого запуска должен быть от 1 до 40.", "StageCue");
            return;
        }
        if (_app.Tracks.Any(t => t != _selected && t.Number == number))
        {
            MessageBox.Show($"Номер {number} уже назначен другой фонограмме.", "StageCue");
            return;
        }
        _selected.Number = number;
        _app.SaveLibrary();
        Refresh();
    }

    private void RemoveSelected()
    {
        if (_selected == null) return;
        var answer = MessageBox.Show(
            $"Убрать «{_selected.Name}» из фонотеки? Сам аудиофайл на диске останется.",
            "Удаление фонограммы",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        _app.RemoveTrack(_selected);
        _selected = null;
        _rename.Text = "";
        _number.Text = "";
        Refresh();
    }

    private static ImageSource? ReadCover(string path)
    {
        try
        {
            var pictures = TagLib.File.Create(path).Tag.Pictures;
            if (pictures.Length == 0) return null;
            using var stream = new MemoryStream(pictures[0].Data.Data);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch { return null; }
    }

    public void Refresh()
    {
        _tiles.Children.Clear();
        string query = _search.Text.Trim();
        var tracks = _app.Tracks
            .Where(t => string.IsNullOrWhiteSpace(query) ||
                        t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        t.Path.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Number)
            .ToList();

        foreach (var track in tracks)
        {
            var art = new Grid { Width = 142, Height = 118, Background = Theme.Brush("#202020"), ClipToBounds = false };
            var cover = ReadCover(track.Path);
            if (cover != null)
                art.Children.Add(new Image { Source = cover, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch });
            else
                art.Children.Add(new TextBlock { Text = "♫", FontSize = 43, Foreground = Theme.Brush("#8E969D"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });

            var numberBadge = new Border { Background = Theme.Brush("#202020"), Opacity = .92, Padding = new Thickness(5, 2, 5, 2), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(5) };
            numberBadge.Child = new TextBlock { Text = track.Number is >= 1 and <= 40 ? track.Number.ToString("00") : "—", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Theme.Accent };
            art.Children.Add(numberBadge);

            var title = new TextBlock { Text = track.Name, FontSize = 11, Foreground = Theme.White, TextWrapping = TextWrapping.Wrap, MaxHeight = 34, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(1, 6, 1, 1) };
            var details = new TextBlock { Text = System.IO.Path.GetExtension(track.Path).Trim('.').ToUpperInvariant() + "  •  " + System.IO.Path.GetFileName(track.Path), FontSize = 8, Foreground = Theme.Muted, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(1, 3, 1, 0) };
            var stack = new StackPanel();
            stack.Children.Add(art);
            stack.Children.Add(title);
            stack.Children.Add(details);
            var card = new Button
            {
                Content = stack,
                Width = 160,
                MinHeight = 174,
                Padding = new Thickness(7),
                Margin = new Thickness(3),
                Background = Theme.Brush(_selected == track ? "#394B5C" : "#292929"),
                BorderBrush = Theme.Brush(_selected == track ? "#6D9FCB" : "#414141"),
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Top,
                ToolTip = track.Path
            };
            card.Click += (_, _) =>
            {
                _selected = track;
                _rename.Text = track.Name;
                _number.Text = track.Number.ToString();
                _app.SelectTrack(track, false);
                Refresh();
            };
            card.MouseDoubleClick += (_, _) => _app.SelectTrack(track, true);
            _tiles.Children.Add(card);
        }
        _count.Text = $"{tracks.Count} ИЗ {_app.Tracks.Count} ФОНОГРАММ";
    }
}
public class HotkeysPage : BasePage
{
    public HotkeysPage(MainWindow app):base(app,"Бинды клавиш","Схема быстрого запуска фонограмм с клавиатуры")
    {
        var rows=new[]{("1–9 · 0","Фонограммы 01–10"),("ALT + 1–9 · 0","Фонограммы 11–20"),("CTRL + 1–9 · 0","Фонограммы 21–30"),("CTRL + ALT + 1–9 · 0","Фонограммы 31–40"),("ПРОБЕЛ","Играть / пауза"),("CTRL + ПРОБЕЛ","Сохранить точку старта и запустить с неё"),("ENTER","Запустить текущий трек с начала")};
        foreach(var (key,desc) in rows)
        {
            var g=new Grid{Margin=new Thickness(0,0,0,8)}; g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(210)});g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(Theme.Card(Theme.Text(key,12,true,Theme.Accent),new Thickness(0),new Thickness(13)));
            var d=Theme.Text(desc,12,false,Theme.White);d.VerticalAlignment=VerticalAlignment.Center;d.Margin=new Thickness(17,0,0,0);Grid.SetColumn(d,1);g.Children.Add(d);Root.Children.Add(g);
        }
        Root.Children.Add(Theme.Card(Theme.Text("Глобальные сочетания работают, пока StageCue запущен. Если сочетание занято Windows или другой программой, оно может быть недоступно.",11,false,Theme.Muted)));
    }
}
public class SettingsPage : BasePage
{
    public SettingsPage(MainWindow app):base(app,"Настройки","Хранение данных и поведение StageCue")
    {
        Root.Children.Add(Theme.Card(Theme.Text("БИБЛИОТЕКА\nПути к фонограммам, названия, номера и контрольные точки сохраняются локально. Сами аудиофайлы не перемещаются.",13)));
        var open=Theme.Button("Открыть папку данных",(_,_)=>{var p=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StageCue");Directory.CreateDirectory(p);Process.Start(new ProcessStartInfo("explorer.exe",p){UseShellExecute=true});},true);
        open.HorizontalAlignment=HorizontalAlignment.Left;Root.Children.Add(open);
        Root.Children.Add(Theme.Card(Theme.Text("АУДИОФОРМАТЫ\nПоддержка зависит от кодеков Windows. Для надёжного воспроизведения используй MP3 или WAV.",12,false,Theme.Muted),new Thickness(0,15,0,0)));
    }
}
