using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

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
    private readonly WrapPanel _tiles = new() { Margin = new Thickness(7, 7, 7, 12) };
    private readonly TextBox _rename = new() { MinWidth = 190, Width = 240, Padding = new Thickness(7, 4, 7, 4) };
    private readonly TextBlock _count = new() { Foreground = Theme.Muted, FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
    private Track? _selected;

    public LibraryPage(MainWindow app, bool openPicker = false)
    {
        _app = app;
        Background = Brushes.Transparent;
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _tiles };
        Grid.SetRow(scroll, 0);
        root.Children.Add(scroll);

        var editor = new Border { Background = Theme.Brush("#292E33"), BorderBrush = Theme.Brush("#414A53"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(9, 7, 9, 7) };
        var row = new DockPanel();
        _count.Text = $"{app.Tracks.Count} ФОНОГРАММ";
        DockPanel.SetDock(_count, Dock.Left);
        row.Children.Add(_count);
        var add = new Button { Content = "＋", Width = 30, Height = 27, Background = Theme.Accent, Foreground = Brushes.White, Margin = new Thickness(6, 0, 0, 0) };
        add.Click += (_, _) => AddFiles();
        DockPanel.SetDock(add, Dock.Right);
        row.Children.Add(add);
        var save = new Button { Content = "Сохранить", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(6, 0, 0, 0) };
        save.Click += (_, _) => { if (_selected != null && !string.IsNullOrWhiteSpace(_rename.Text)) { _selected.Name = _rename.Text.Trim(); _app.SaveLibrary(); Refresh(); } };
        DockPanel.SetDock(save, Dock.Right);
        row.Children.Add(save);
        var remove = new Button { Content = "Удалить", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(6, 0, 0, 0) };
        remove.Click += (_, _) => { if (_selected != null) { _app.RemoveTrack(_selected); _selected = null; _rename.Text = ""; Refresh(); } };
        DockPanel.SetDock(remove, Dock.Right);
        row.Children.Add(remove);
        _rename.ToolTip = "Название выбранной фонограммы";
        DockPanel.SetDock(_rename, Dock.Right);
        row.Children.Add(_rename);
        editor.Child = row;
        Grid.SetRow(editor, 1);
        root.Children.Add(editor);

        Content = root;
        Refresh();
        if (openPicker) Loaded += (_, _) => AddFiles();
    }

    private void AddFiles()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Filter = "Аудио|*.mp3;*.wav;*.wma;*.m4a;*.aac;*.mid;*.midi|Все файлы|*.*" };
        if (dialog.ShowDialog() == true) { _app.AddFiles(dialog.FileNames); Refresh(); }
    }

    private void Refresh()
    {
        _tiles.Children.Clear();
        foreach (var track in _app.Tracks.OrderBy(t => t.Number))
        {
            var cover = new Border
            {
                Height = 104, Width = 132, CornerRadius = new CornerRadius(2),
                Background = new LinearGradientBrush(
                    Color.FromRgb((byte)(37 + track.Number * 11 % 45), (byte)(52 + track.Number * 7 % 55), (byte)(68 + track.Number * 13 % 65)),
                    Color.FromRgb(24, 29, 34), 90)
            };
            var art = new Grid();
            art.Children.Add(new TextBlock { Text = "♫", FontSize = 42, Foreground = new SolidColorBrush(Color.FromArgb(190, 218, 226, 233)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            art.Children.Add(new TextBlock { Text = track.Number.ToString("00"), FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(7) });
            cover.Child = art;

            var name = new TextBlock { Text = track.Name, FontSize = 10, Foreground = Theme.White, TextWrapping = TextWrapping.Wrap, MaxHeight = 30, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(1, 6, 1, 1) };
            var details = new TextBlock { Text = System.IO.Path.GetExtension(track.Path).Trim('.').ToUpperInvariant() + "  •  STAGECUE", FontSize = 8, Foreground = Theme.Muted, Margin = new Thickness(1, 2, 1, 0) };
            var content = new StackPanel();
            content.Children.Add(cover);
            content.Children.Add(name);
            content.Children.Add(details);
            var button = new Button { Content = content, Width = 148, Height = 166, Padding = new Thickness(6), Margin = new Thickness(3), Background = Theme.Brush("#2B3035"), BorderBrush = Theme.Brush("#424A52"), BorderThickness = new Thickness(1), HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Top };
            button.Click += (_, _) => { _selected = track; _rename.Text = track.Name; _app.SelectTrack(track, false); };
            button.MouseDoubleClick += (_, _) => _app.SelectTrack(track, true);
            _tiles.Children.Add(button);
        }
        _count.Text = $"{_app.Tracks.Count} ФОНОГРАММ";
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
