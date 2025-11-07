using Microsoft.Maui.Controls;

namespace RagNext.Behaviors
{
    public sealed class ScrollAwareBehavior : Behavior<ScrollView>
    {
        public static readonly BindableProperty HasMoreAboveProperty =
            BindableProperty.Create(nameof(HasMoreAbove), typeof(bool), typeof(ScrollAwareBehavior), false);

        public static readonly BindableProperty HasMoreBelowProperty =
            BindableProperty.Create(nameof(HasMoreBelow), typeof(bool), typeof(ScrollAwareBehavior), false);

        public static readonly BindableProperty ProgressProperty =
            BindableProperty.Create(nameof(Progress), typeof(double), typeof(ScrollAwareBehavior), 0.0);

        public bool HasMoreAbove
        {
            get => (bool)GetValue(HasMoreAboveProperty);
            set => SetValue(HasMoreAboveProperty, value);
        }

        public bool HasMoreBelow
        {
            get => (bool)GetValue(HasMoreBelowProperty);
            set => SetValue(HasMoreBelowProperty, value);
        }

        public double Progress
        {
            get => (double)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        ScrollView? _sv;

        protected override void OnAttachedTo(ScrollView bindable)
        {
            base.OnAttachedTo(bindable);
            _sv = bindable;
            bindable.Scrolled += OnScrolled;
            Device.StartTimer(TimeSpan.FromMilliseconds(50), () =>
            {
                UpdateState(); // initial layout pass
                return _sv != null;
            });
        }

        protected override void OnDetachingFrom(ScrollView bindable)
        {
            bindable.Scrolled -= OnScrolled;
            _sv = null;
            base.OnDetachingFrom(bindable);
        }

        void OnScrolled(object? sender, ScrolledEventArgs e) => UpdateState();

        void UpdateState()
        {
            if (_sv is null) return;
            var contentHeight = (_sv.Content as VisualElement)?.Height ?? 0;
            var viewport = _sv.Height;

            if (contentHeight <= 0 || viewport <= 0)
            {
                HasMoreAbove = false;
                HasMoreBelow = false;
                Progress = 0;
                return;
            }

            var maxScroll = Math.Max(0, contentHeight - viewport);
            var y = _sv.ScrollY;

            HasMoreAbove = y > 4;
            HasMoreBelow = y < maxScroll - 4;
            Progress = maxScroll == 0 ? 1.0 : Math.Min(1.0, Math.Max(0.0, y / maxScroll));
        }
    }
}