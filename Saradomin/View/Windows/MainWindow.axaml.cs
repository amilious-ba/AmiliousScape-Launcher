using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Glitonea.Mvvm.Messaging;
using PropertyChanged;
using Saradomin.Infrastructure;

namespace Saradomin.View.Windows
{
    [DoNotNotify]
    public partial class MainWindow : Window
    {
        private bool _logStickToBottom = true;
        private bool _logHooked;
        private bool _autoScrolling;

        private const int LogTabIndex = 3;

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            new MainViewLoadedMessage().Broadcast();

            HookLogAutoScroll();

            Message.Subscribe<LogTabActivatedMessage>(this, _ =>
            {
                ForceLogScrollToEndAfterLayout();
            });

            Message.Subscribe<LogScrollRequestedMessage>(this, _ =>
            {
                Dispatcher.UIThread.Post(ScrollLogToEndIfNeeded, DispatcherPriority.Background);
            });
        }

        private void HookLogAutoScroll()
        {
            if (_logHooked || LogScrollViewer == null)
                return;

            _logHooked = true;

            // Only user scrolling (wheel) can disable stick-to-bottom
            LogScrollViewer.PointerWheelChanged += (_, _) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (LogScrollViewer == null)
                        return;

                    const double threshold = 40;
                    var max = Math.Max(0, LogScrollViewer.Extent.Height - LogScrollViewer.Viewport.Height);
                    _logStickToBottom = max <= 1 || LogScrollViewer.Offset.Y >= max - threshold;
                }, DispatcherPriority.Input);
            };
        }

        private void ScrollLogToEndIfNeeded()
        {
            if (!_logStickToBottom || LogScrollViewer == null)
                return;

            _autoScrolling = true;
            try
            {
                var max = Math.Max(0, LogScrollViewer.Extent.Height - LogScrollViewer.Viewport.Height);
                LogScrollViewer.Offset = new Vector(0, max);
            }
            finally
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (LogScrollViewer != null && _logStickToBottom)
                    {
                        var max = Math.Max(0, LogScrollViewer.Extent.Height - LogScrollViewer.Viewport.Height);
                        LogScrollViewer.Offset = new Vector(0, max);
                    }
                    _autoScrolling = false;
                }, DispatcherPriority.Loaded);
            }
        }

        public void ForceLogScrollToEndAfterLayout()
        {
            _logStickToBottom = true;

            Dispatcher.UIThread.Post(() =>
            {
                ScrollLogToEndIfNeeded();
                Dispatcher.UIThread.Post(ScrollLogToEndIfNeeded, DispatcherPriority.Loaded);
            }, DispatcherPriority.Background);
        }

        private void TitleBar_MouseDown(object _, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        }
    }
}