using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Omni.Windows;

public sealed class ConfirmWindow : Window
{
    readonly TaskCompletionSource<bool> answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ConfirmWindow(Window owner, string title, string message, string confirm)
    {
        Owner = owner; Title = title; Width = 500; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None; ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = owner.Background; Foreground = owner.Foreground; Resources = owner.Resources;
        var body = new StackPanel { Margin = new Thickness(24) };
        body.Children.Add(new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeights.SemiBold });
        body.Children.Add(new TextBlock { Name = "ConfirmationMessage", Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 22) });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Name = "CancelConfirmation", Content = "取消", IsCancel = true, MinWidth = 90 };
        var accept = new Button { Name = "AcceptConfirmation", Content = confirm, MinWidth = 110, Background = (Brush)FindResource("Accent") };
        cancel.Click += (_, _) => Close(); accept.Click += (_, _) => { answer.TrySetResult(true); Close(); };
        buttons.Children.Add(cancel); buttons.Children.Add(accept); body.Children.Add(buttons);
        Content = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(53, 74, 97)), BorderThickness = new Thickness(1), Child = body };
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
        Closed += (_, _) => answer.TrySetResult(false);
        Loaded += (_, _) => cancel.Focus();
    }
    public Task<bool> Ask() { ShowDialog(); return answer.Task; }
}
