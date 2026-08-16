using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Glitonea.Controls;
using Glitonea.Extensions;
using PropertyChanged;
using Saradomin.Infrastructure;

namespace Saradomin.View.Windows
{
    [DoNotNotify]
    public partial class ChoiceBox : WindowEx
    {
        public static readonly StyledProperty<string> MessageProperty =
            AvaloniaProperty.Register<ChoiceBox, string>(nameof(Message));

        public static readonly StyledProperty<string> PositiveTextProperty =
            AvaloniaProperty.Register<ChoiceBox, string>(nameof(PositiveText), "OK");

        public static readonly StyledProperty<string> NegativeTextProperty =
            AvaloniaProperty.Register<ChoiceBox, string>(nameof(NegativeText), "Cancel");

        public string Message
        {
            get => GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public string PositiveText
        {
            get => GetValue(PositiveTextProperty);
            set => SetValue(PositiveTextProperty, value);
        }

        public string NegativeText
        {
            get => GetValue(NegativeTextProperty);
            set => SetValue(NegativeTextProperty, value);
        }

        private TaskCompletionSource<bool> _tcs;

        public ChoiceBox()
        {
            InitializeComponent();
            DataContext = this;
        }

        public static async Task<bool> ShowAsync(
            string title,
            string message,
            string positiveText = "Update now",
            string negativeText = "Later")
        {
            var owner = Application.Current!.GetMainWindow();
            var box = new ChoiceBox
            {
                Title = title,
                Message = message,
                PositiveText = positiveText,
                NegativeText = negativeText,
                Owner = owner,
                _tcs = new TaskCompletionSource<bool>()
            };

            new NotificationBoxStateChangedMessage(true).Broadcast();
            box.ShowDialog(owner);
            var result = await box._tcs.Task;
            new NotificationBoxStateChangedMessage(false).Broadcast();
            return result;
        }

        private void OnPositive(object sender, RoutedEventArgs e)
        {
            _tcs?.TrySetResult(true);
            Close();
        }

        private void OnNegative(object sender, RoutedEventArgs e)
        {
            _tcs?.TrySetResult(false);
            Close();
        }
    }
}