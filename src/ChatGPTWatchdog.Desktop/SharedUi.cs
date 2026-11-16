using W=System.Windows;
using C=System.Windows.Controls;
using M=System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
namespace ChatGPTWatchdog;
internal sealed partial class ReviewWindow {
internal void ApplyTheme(string value){theme=value;dark=value!="Light";if(value=="System")dark=(int?)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1)==0;var map=new Dictionary<string,string>{{"202020","F5F5F5"},{"252525","EFEFEF"},{"262626","EAEAEA"},{"2B2B2B","FFFFFF"},{"303030","F0F0F0"},{"383838","E0E0E0"},{"454545","D2D2D2"},{"969696","646464"},{"B9B9B9","535353"},{"C1C1C1","454545"},{"F2F2F2","202020"},{"FFFFFF","202020"},{"888888","666666"}};var resources=W.Application.Current.Resources;foreach(var key in new[]{"202020","252525","262626","2B2B2B","303030","383838","414141","444444","454545","484848","4A4A4A","5A5A5A","888888","969696","A5A5A5","B9B9B9","C1C1C1","EEEEEE","F2F2F2","FFFFFF"}){var c=(M.Color)M.ColorConverter.ConvertFromString("#"+key);double b=(c.R*.2126+c.G*.7152+c.B*.0722);int v=(int)Math.Clamp(255-b,30,245);string color=dark?key:map.GetValueOrDefault(key,$"{v:X2}{v:X2}{v:X2}");resources["Theme"+key]=Brush("#"+color);}resources["DangerBackground"]=Brush(dark?"#392125":"#FFF1F3");resources["DangerBorder"]=Brush(dark?"#A64C5A":"#D58B97");resources["DangerAccent"]=Brush(dark?"#FFA5B3":"#9D1D32");resources["DangerText"]=Brush(dark?"#FFDEE4":"#6F2432");resources["Surface"]=resources["Theme2B2B2B"];resources["DgxPublisherForeground"]=Brush(dark?"#FFFFFF":"#162921");resources["Background"]=resources["Theme202020"];resources["Muted"]=resources["ThemeB9B9B9"];resources["DashboardAccent"]=Brush(dark?"#78B7FF":"#2468BB");resources["MenuShadowOpacity"]=dark?.30:.18;if(Window!=null)foreach(W.Window open in W.Application.Current.Windows)Caption(open);}
void Caption(W.Window target){if(W.SystemParameters.HighContrast)return;var h=new WindowInteropHelper(target).Handle;if(h==IntPtr.Zero)return;int value=dark?1:0;DwmSetWindowAttribute(h,20,ref value,4);value=dark?0x383838:0xE0E0E0;DwmSetWindowAttribute(h,34,ref value,4);value=dark?0x202020:0xF5F5F5;DwmSetWindowAttribute(h,35,ref value,4);value=dark?0xF2F2F2:0x202020;DwmSetWindowAttribute(h,36,ref value,4);}
C.Border WorkRule(){var rule=new C.Border{Height=1,Margin=new W.Thickness(0,16,0,16)};Resource(rule,C.Border.BackgroundProperty,"Theme454545");return rule;}
C.Button Standard(C.Button b){b.Height=36;b.MinWidth=88;b.Padding=new W.Thickness(12,0,12,0);b.Margin=new W.Thickness(8,0,0,0);return b;}
static C.TextBlock WorkText(string text,bool title=false){var t=Text(text,title?DgxUi.SubheadingSize:DgxUi.BodySize);t.FontWeight=title?W.FontWeights.SemiBold:W.FontWeights.Normal;t.Margin=new W.Thickness(0,3,0,5);Resource(t,C.TextBlock.ForegroundProperty,title?"ThemeF2F2F2":"Muted");return t;}
W.Window NoticeWindow(string title,string message,Action? confirm=null,string confirmLabel="Open GitHub"){
  var body=DgxUi.NoticeBody(title,message,out var actions);
  var d=Dialog(title,520,220,body);DgxUi.ConfigureNotice(d);
  var dismiss=Standard(new C.Button{Content=confirm==null?"Close":"Cancel",IsDefault=confirm==null,IsCancel=true});dismiss.Margin=new W.Thickness(0);actions.Children.Add(dismiss);
  if(confirm!=null)actions.Children.Add(Standard(Button(confirmLabel,()=>{confirm();d.DialogResult=true;})));
  return d;
 }
void Notice(string title,string message,Action? confirm=null){NoticeWindow(title,message,confirm).ShowDialog();}
static C.Image Glyph(string name,double size){var image=ReviewGlyphs.FluentImage(name,size,size);Resource(image,System.Windows.Documents.TextElement.ForegroundProperty,"ThemeF2F2F2");return image;}
static M.ImageSource BrandSource(){using var stream=typeof(ReviewWindow).Assembly.GetManifestResourceStream("brand.png");return BitmapFrame.Create(stream!,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);}
}

