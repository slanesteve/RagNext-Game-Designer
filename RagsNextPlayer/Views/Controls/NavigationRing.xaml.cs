using System;
using System.Collections.Generic;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;

namespace RagsNextPlayer.Views.Controls
{
    public partial class NavigationRing : ContentView
    {
        public static readonly BindableProperty ExitsProperty =
            BindableProperty.Create(
                nameof(Exits),
                typeof(Dictionary<string, Guid>),
                typeof(NavigationRing),
                null,
                propertyChanged: OnExitsChanged);

        public static readonly BindableProperty DirectionClickedCommandProperty =
            BindableProperty.Create(
                nameof(DirectionClickedCommand),
                typeof(ICommand),
                typeof(NavigationRing),
                null);

        public Dictionary<string, Guid>? Exits
        {
            get => (Dictionary<string, Guid>?)GetValue(ExitsProperty);
            set => SetValue(ExitsProperty, value);
        }

        public ICommand? DirectionClickedCommand
        {
            get => (ICommand?)GetValue(DirectionClickedCommandProperty);
            set => SetValue(DirectionClickedCommandProperty, value);
        }

        // Map direction name → (border button, indicator dot)
        private Dictionary<string, (Border btn, Ellipse dot)>? _dirControls;

        public NavigationRing()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object? sender, EventArgs e)
        {
            // Build the direction→control map after XAML is ready
            _dirControls = new Dictionary<string, (Border, Ellipse)>(StringComparer.OrdinalIgnoreCase)
            {
                ["North"] = (Btn_North, Dot_North),
                ["South"] = (Btn_South, Dot_South),
                ["East"]  = (Btn_East,  Dot_East),
                ["West"]  = (Btn_West,  Dot_West),
                ["Up"]    = (Btn_Up,    Dot_Up),
                ["Down"]  = (Btn_Down,  Dot_Down),
                ["In"]    = (Btn_In,    Dot_In),
                ["Out"]   = (Btn_Out,   Dot_Out),
            };

            StartCorePulse();
            UpdateExitButtons();
        }

        private static void OnExitsChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is NavigationRing ring)
                ring.UpdateExitButtons();
        }

        /// <summary>
        /// Updates every direction button to active/inactive based on the current Exits dictionary.
        /// Active exits glow cyan with a bright indicator dot.
        /// </summary>
        public void UpdateExitButtons()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_dirControls is null) return; // not yet loaded

                var exits = Exits;
                var activeStyle   = (Style)Resources["ExitActiveStyle"];
                var inactiveStyle = (Style)Resources["ExitInactiveStyle"];
                var activeDot     = (Color)Resources["ActiveDotColor"];
                var inactiveDot   = (Color)Resources["InactiveDotColor"];

                foreach (var (dir, (btn, dot)) in _dirControls)
                {
                    bool hasExit = false;
                    if (exits is not null)
                    {
                        foreach (var key in exits.Keys)
                        {
                            if (string.Equals(key, dir, StringComparison.OrdinalIgnoreCase))
                            {
                                hasExit = true;
                                break;
                            }
                        }
                    }

                    btn.Style = hasExit ? activeStyle : inactiveStyle;
                    dot.Fill  = new SolidColorBrush(hasExit ? activeDot : inactiveDot);

                    // Pulse animation on newly-active dots
                    if (hasExit)
                    {
                        dot.AbortAnimation("dotPulse");
                        var pulse = new Animation(v => dot.Scale = v, 1.0, 1.5, Easing.SinInOut);
                        pulse.Commit(dot, "dotPulse", length: 900, repeat: () => true,
                            finished: (_, __) => dot.Scale = 1.0);
                    }
                    else
                    {
                        dot.AbortAnimation("dotPulse");
                        dot.Scale = 1.0;
                    }
                }
            });
        }

        /// <summary>Slow breathing animation on the center core ring while the game is active.</summary>
        private void StartCorePulse()
        {
            if (CorePulseRing is null) return;
            var anim = new Animation(v => CorePulseRing.Opacity = v, 0.15, 0.6, Easing.SinInOut);
            anim.Commit(CorePulseRing, "corePulse", length: 1800, repeat: () => true);
        }

        private void OnDirectionTapped(object? sender, TappedEventArgs e)
        {
            if (sender is not Border border) return;
            
            // Try getting direction from parameter first, fallback to controls mapping lookup
            var direction = e.Parameter as string;
            if (string.IsNullOrWhiteSpace(direction) && _dirControls is not null)
            {
                foreach (var (dir, (btn, dot)) in _dirControls)
                {
                    if (btn == border)
                    {
                        direction = dir;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(direction)) return;

            var exits = Exits;
            if (exits is null) return;

            Guid targetRoomId = Guid.Empty;
            bool found = false;

            foreach (var pair in exits)
            {
                if (string.Equals(pair.Key, direction, StringComparison.OrdinalIgnoreCase))
                {
                    targetRoomId = pair.Value;
                    found = true;
                    break;
                }
            }

            if (!found) return;

            // Brief scale-tap animation feedback
            border.ScaleTo(0.88, 80, Easing.CubicIn)
                  .ContinueWith(_ => border.ScaleTo(1.0, 120, Easing.CubicOut));

            if (DirectionClickedCommand?.CanExecute(direction) == true)
            {
                DirectionClickedCommand.Execute(direction);
            }
        }
    }
}
