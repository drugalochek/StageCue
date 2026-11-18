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
public class LibraryPage : BasePage
{
    private readonly ListBox _list=new();
    private readonly TextBox _rename=new();
    private readonly TextBlock _count;
    public LibraryPage(MainWindow app,bool openPicker=false):base(app,"Фонотека","Все аудиофайлы, номера запуска и названия фонограмм")
    {
        var top=new DockPanel();
        _count=Theme.Text($"{app.Tracks.Count} ФОНОГРАММ",10,true,Theme.Muted); DockPanel.SetDock(_count,Dock.Left); top.Children.Add(_count);
        var add=Theme.Button("＋ Добавить файлы",(_,_)=>AddFiles(),true); DockPanel.SetDock(add,Dock.Right); top.Children.Add(add); Root.Children.Add(top);
        _list.Height=340; _list.Margin=new Thickness(0,13,0,13); _list.SelectionChanged+=(_,_)=>{if(SelectedTrack() is Track t){_rename.Text=t.Name;app.SelectTrack(t,false);}};
        Refresh(); Root.Children.Add(Theme.Card(_list,new Thickness(0,0,0,12),new Thickness(7)));
        Root.Children.Add(Theme.Text("НАЗВАНИЕ ВЫБРАННОЙ ФОНОГРАММЫ",10,true,Theme.Muted));
        _rename.Margin=new Thickness(0,8,0,10); Root.Children.Add(_rename);
        var row=new StackPanel{Orientation=Orientation.Horizontal};
        row.Children.Add(Theme.Button("Сохранить название",(_,_)=>{if(SelectedTrack() is Track t&&!string.IsNullOrWhiteSpace(_rename.Text)){t.Name=_rename.Text.Trim();app.SaveLibrary();Refresh(t);}},true));
        row.Children.Add(Theme.Button("Удалить",(_,_)=>{if(SelectedTrack() is Track t){app.RemoveTrack(t);Refresh();}},false));
        Root.Children.Add(row);
        if(openPicker) Loaded+=(_,_)=>AddFiles();
    }
    private Track? SelectedTrack() => (_list.SelectedItem as ListBoxItem)?.Tag as Track;
    private void AddFiles()
    {
        var d=new OpenFileDialog{Multiselect=true,Filter="Аудио|*.mp3;*.wav;*.wma;*.m4a;*.aac;*.mid;*.midi|Все файлы|*.*"};
        if(d.ShowDialog()==true){AppWindow.AddFiles(d.FileNames);Refresh();}
    }
    private void Refresh(Track? selected=null)
    {
        _list.Items.Clear();
        foreach(var t in AppWindow.Tracks.OrderBy(t=>t.Number))
        {
            var row=new Grid{Margin=new Thickness(4,3,4,3)};
            row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(48)});
            row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            var num=Theme.Text(t.Number.ToString("00"),14,true,Theme.Accent); num.VerticalAlignment=VerticalAlignment.Center; row.Children.Add(num);
            var name=Theme.Text(t.Name,13,true); name.VerticalAlignment=VerticalAlignment.Center; Grid.SetColumn(name,1); row.Children.Add(name);
            _list.Items.Add(new ListBoxItem{Content=row,Tag=t,Padding=new Thickness(8),Background=Brushes.Transparent});
        }
        _count.Text=$"{AppWindow.Tracks.Count} ФОНОГРАММ";
        if(selected!=null) foreach(ListBoxItem item in _list.Items) if(ReferenceEquals(item.Tag,selected)) _list.SelectedItem=item;
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
