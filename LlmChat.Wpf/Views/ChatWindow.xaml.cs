using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

namespace LlmChat.Wpf.Views
{
    public partial class ChatWindow : Window
    {
        private readonly ViewModels.ChatViewModel _vm;
        private bool _autoScroll = true;

        // Accept ChatViewModel via DI so DataContext is set correctly and bindings work.
        public ChatWindow(ViewModels.ChatViewModel vm)
        {
            InitializeComponent();
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            DataContext = _vm;

            // Hook collection changed to auto-scroll when new messages appear
            Loaded += (_, _) =>
            {
                _vm.Messages.CollectionChanged += Messages_CollectionChanged;
                InputBox.Focus();
            };

            Unloaded += (_, _) => _vm.Messages.CollectionChanged -= Messages_CollectionChanged;
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
            {
                e.Handled = true;
                if (DataContext is ViewModels.ChatViewModel vm && vm.SendCommand.CanExecute(null))
                {
                    vm.SendCommand.Execute(null);
                    ChatScrollViewer.ScrollToEnd();
                }
            }
        }

        private void Messages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Auto-scroll only if user is near the bottom
            if (_autoScroll)
                ChatScrollViewer.ScrollToEnd();

            // Keep input focused for fast conversation
            InputBox.Dispatcher.BeginInvoke(() => InputBox.Focus());
        }

        private void ChatScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var sv = (ScrollViewer)sender;
            // User is at bottom if extentHeight - viewportHeight - verticalOffset is small
            var distanceToEnd = sv.ExtentHeight - sv.ViewportHeight - sv.VerticalOffset;
            _autoScroll = distanceToEnd < 12; // pixel threshold
        }
    }
}
