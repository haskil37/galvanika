using System.Windows;

namespace Galvanika
{
	public partial class CustomMessageBox : Window
    {
        private MessageBoxResult _result = MessageBoxResult.None;
        public CustomMessageBox()
        {
            this.InitializeComponent();
            _ok.Focus();
        }
        public MessageBoxResult MessageBoxResult
        {
            get { return this._result; }
            private set
            {
                this._result = value;
                this.DialogResult = true;
            }
        }
        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string),
                typeof(CustomMessageBox), new UIPropertyMetadata(string.Empty));

        public static MessageBoxResult Show(string messageBoxText)
        {
            CustomMessageBox messageBox = new CustomMessageBox();
            messageBox.Message = messageBoxText;
            if (false == messageBox.ShowDialog())
                return MessageBoxResult.OK;
            return messageBox.MessageBoxResult;
        }
        private void ok_Click(object sender, RoutedEventArgs e)
        {
            this.MessageBoxResult = MessageBoxResult.OK;
        }
    }
}