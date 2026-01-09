using System.Windows;
namespace SAM.Picker.Modern {
  public partial class WarningWindow : Window {
    public bool IsConfirmed { get; private set; }
    public WarningWindow(string message, string confirmText = "Okay", string cancelText = "Nope!", string title = "Warning") {
      InitializeComponent();
      TitleText.Text = title;
      MessageText.Text = message;
      ConfirmButton.Content = confirmText;
      if (string.IsNullOrEmpty(cancelText)) CancelButton.Visibility = Visibility.Collapsed;
      else CancelButton.Content = cancelText;
      Owner = Application.Current.MainWindow;
    }
    private void Close_Click(object sender, RoutedEventArgs e) {
      IsConfirmed = false;
      Close();
    }
    private void Confirm_Click(object sender, RoutedEventArgs e) {
      IsConfirmed = true;
      Close();
    }
  }
}