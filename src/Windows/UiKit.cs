using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Omni.Core;
namespace Omni.Windows;
public static class UiKit
{
    public static string Language="zh-Hant";
    public static string T(string zh,string en)=>Language=="en"?en:zh;
    public static Brush Brush(string hex)=>new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    public static TextBlock Text(string text,double size=15)=>new(){Text=text,FontSize=size,FontWeight=size>=20?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};
    public static Button Button(string text,Action click,bool accent=false){var b=new Button{Content=text,Padding=new Thickness(15,10,15,10)};if(accent){b.SetResourceReference(Control.BackgroundProperty,"Accent");b.Foreground=Brushes.White;}b.Click+=(_,_)=>click();return b;}
    public static Border Card(UIElement child,int padding=18){var b=new Border{Child=child,Padding=new Thickness(padding),Margin=new Thickness(4,6,4,6),CornerRadius=new CornerRadius(9),BorderThickness=new Thickness(1)};b.SetResourceReference(Border.BackgroundProperty,"Surface");b.SetResourceReference(Border.BorderBrushProperty,"Line");return b;}
    public static FrameworkElement Icon(string name, double size=22)
    {
        var path=name switch{
"network"=>"M12,2 A10,10 0 1 1 11.99,2 M2,12 L22,12 M4,6 L20,6 M4,18 L20,18 M12,2 C5,8 5,16 12,22 C19,16 19,8 12,2",
"notification"=>"M5,17 L19,17 L17,14 L17,8 C17,1 7,1 7,8 L7,14 Z M10,21 L14,21",
"download"=>"M12,2 L12,16 M6,10 L12,16 L18,10 M3,17 L3,22 L21,22 L21,17",
"info"=>"M12,2 A10,10 0 1 1 11.99,2 M12,10 L12,18 M12,6 L12.01,6",
"settings"=>"M12,3 L15,6 L19,6 L19,10 L22,12 L19,15 L19,19 L15,19 L12,22 L9,19 L5,19 L5,15 L2,12 L5,9 L5,5 L9,5 Z M12,8 A4,4 0 1 1 11.99,8",
"mp4"=>"M12,2 A10,10 0 1 1 11.99,2 M9,7 L17,12 L9,17 Z",
"mp3"=>"M9,18 L9,5 L21,2 L21,15 M9,8 L21,5 M9,18 C9,22 2,22 2,19 C2,16 9,15 9,18 M21,15 C21,19 14,19 14,16 C14,13 21,12 21,15",

            "complete"=>"M12,2 A10,10 0 1 1 11.99,2 M6,12 L10,16 L18,8",
            "video"=>"M3,4 L21,4 L21,20 L3,20 Z M7,4 L7,20 M17,4 L17,20 M3,9 L7,9 M17,9 L21,9 M3,15 L7,15 M17,15 L21,15",
            "audio"=>"M9,18 L9,5 L21,2 L21,15 M9,8 L21,5 M9,18 C9,22 2,22 2,19 C2,16 9,15 9,18 M21,15 C21,19 14,19 14,16 C14,13 21,12 21,15",
            "delete"=>"M3,6 L21,6 M8,6 L8,3 L16,3 L16,6 M5,6 L6,22 L18,22 L19,6 M10,10 L10,18 M14,10 L14,18",
            "folder"=>"M2,6 L9,6 L11,9 L22,9 L22,21 L2,21 Z",
            "add"=>"M12,3 L12,21 M3,12 L21,12",
            "back"=>"M14,4 L6,12 L14,20 M6,12 L23,12",
            "play"=>"M6,3 L21,12 L6,21 Z",
            "size"=>"M4,3 L20,3 L22,10 L22,21 L2,21 L2,10 Z M2,10 L22,10 M16,16 L19,16",
            "search"=>"M10,2 A8,8 0 1 1 9.99,2 M16,16 L23,23",
            "tag"=>"M2,3 L12,3 L23,14 L14,23 L2,11 Z M7,7 L7.1,7",
            "save"=>"M3,3 L18,3 L22,7 L22,22 L3,22 Z M7,3 L7,10 L17,10 L17,3 M7,22 L7,15 L18,15 L18,22",
            _=>"M4,12 L20,12 M12,4 L12,20"};
        var v=new System.Windows.Shapes.Path{Data=Geometry.Parse(path),StrokeThickness=1.8,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Width=size,Height=size,Stretch=Stretch.Uniform};
        if(name is "mp3" or "mp4")v.Stroke=new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#28BADD"),(Color)ColorConverter.ConvertFromString("#D421EA"),45);else if(name=="complete")v.Stroke=Brush("#29C978");else v.Stroke=Brush(name switch{"delete"=>"#F2788D","audio" or "tag"=>"#AB8AFF","network" or "video"=>"#39B9DD","notification"=>"#E9B65C",_=>"#6EABF2"});return v;
    }
    public static StackPanel IconLabel(string name,string label){var p=new StackPanel{Orientation=Orientation.Horizontal};p.Children.Add(Icon(name));var t=Text(label);t.Margin=new Thickness(9,0,0,0);p.Children.Add(t);return p;}
    public static Button IconButton(string name,string label,Action action,bool accent=false){var b=Button(label,action,accent);var content=IconLabel(name,label);if(accent&&content.Children[0] is System.Windows.Shapes.Path path)path.Stroke=Brushes.White;b.Content=content;System.Windows.Automation.AutomationProperties.SetName(b,label);return b;}
    public static Grid Hint(TextBox input,string hint){var g=new Grid();g.Children.Add(input);var t=Text(hint,13);t.SetResourceReference(TextBlock.ForegroundProperty,"Muted");t.Margin=new Thickness(16,10,14,10);t.IsHitTestVisible=false;g.Children.Add(t);void Update()=>t.Visibility=string.IsNullOrEmpty(input.Text)?Visibility.Visible:Visibility.Collapsed;input.TextChanged+=(_,_)=>Update();Update();System.Windows.Automation.AutomationProperties.SetHelpText(input,hint);return g;}
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DependencyObject,BaseFont> Fonts=new();
    sealed class BaseFont {public double Value;}
    public static void Typography(DependencyObject root,int percent){if(root is TextBlock or Control){var dp=root is TextBlock?TextBlock.FontSizeProperty:Control.FontSizeProperty;var local=root.ReadLocalValue(dp);if(local is double){var original=Fonts.GetValue(root,k=>new BaseFont{Value=(double)k.GetValue(dp)});root.SetValue(dp,original.Value*percent/100.0);}}for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)Typography(VisualTreeHelper.GetChild(root,i),percent);}
    public static readonly Dictionary<string,string> Dark=new(){["Ink"]="#111923",["Surface"]="#18222D",["Input"]="#17222F",["Raised"]="#222F40",["Text"]="#EDF2FF",["Muted"]="#91A5C2",["Line"]="#34465B",["Selected"]="#273554",["Hover"]="#222E44",["Header"]="#202D3B"};
    public static readonly Dictionary<string,string> Light=new(){["Ink"]="#EDF1F8",["Surface"]="#FFFFFF",["Input"]="#F5F7FC",["Raised"]="#E6ECF5",["Text"]="#182536",["Muted"]="#51647A",["Line"]="#C9D4E3",["Selected"]="#DBE3FF",["Hover"]="#EBEFFF",["Header"]="#E8EDF6"};
    public static void Apply(Window owner,Preferences p){Language=p.Language;bool light=p.Theme=="light"||p.Theme=="system"&&(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",0) is int n&&n!=0);foreach(var pair in light?Light:Dark)owner.Resources[pair.Key]=Brush(pair.Value);owner.FontSize=15*p.TextScale/100.0;if(owner.FindName("AppRoot") is FrameworkElement root)root.LayoutTransform=new ScaleTransform(p.UiScale/100.0,p.UiScale/100.0);owner.Dispatcher.BeginInvoke(()=>{if(owner.Content is DependencyObject content)Typography(content,p.TextScale);},System.Windows.Threading.DispatcherPriority.Loaded);}
}
