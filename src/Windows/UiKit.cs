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
    public static readonly Dictionary<string,string> Dark=new(){["Ink"]="#111923",["Surface"]="#18222D",["Input"]="#17222F",["Raised"]="#222F40",["Text"]="#EDF2FF",["Muted"]="#91A5C2",["Line"]="#34465B",["Selected"]="#273554",["Hover"]="#222E44",["Header"]="#202D3B"};
    public static readonly Dictionary<string,string> Light=new(){["Ink"]="#EDF1F8",["Surface"]="#FFFFFF",["Input"]="#F5F7FC",["Raised"]="#E6ECF5",["Text"]="#182536",["Muted"]="#51647A",["Line"]="#C9D4E3",["Selected"]="#DBE3FF",["Hover"]="#EBEFFF",["Header"]="#E8EDF6"};
    public static void Apply(Window owner,Preferences p){Language=p.Language;bool light=p.Theme=="light"||p.Theme=="system"&&(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",0) is int n&&n!=0);foreach(var pair in light?Light:Dark)owner.Resources[pair.Key]=Brush(pair.Value);}
}
