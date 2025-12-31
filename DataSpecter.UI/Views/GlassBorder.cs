using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace DataSpecter.UI.Views
{
    // FIX: Inherit from ContentControl, not Border, to support Templates
    public class GlassBorder : ContentControl
    {
        private VisualBrush _glassBrush;
        private TranslateTransform _translateTransform;

        // 1. Scope Property (The element to blur)
        public static readonly DependencyProperty ScopeProperty =
            DependencyProperty.Register("Scope", typeof(UIElement), typeof(GlassBorder),
                new PropertyMetadata(null, OnScopeChanged));

        public UIElement Scope
        {
            get { return (UIElement)GetValue(ScopeProperty); }
            set { SetValue(ScopeProperty, value); }
        }

        // 2. CornerRadius Property (ContentControl doesn't have this by default)
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(GlassBorder),
                new PropertyMetadata(new CornerRadius(0)));

        public CornerRadius CornerRadius
        {
            get { return (CornerRadius)GetValue(CornerRadiusProperty); }
            set { SetValue(CornerRadiusProperty, value); }
        }

        public GlassBorder()
        {
            _translateTransform = new TranslateTransform();
            _glassBrush = new VisualBrush
            {
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top,
                Transform = _translateTransform
            };

            // Set the brush as the background (accessed via TemplateBinding later)
            this.Background = _glassBrush;
            this.LayoutUpdated += OnLayoutUpdated;
        }

        private static void OnScopeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var border = (GlassBorder)d;
            if (e.NewValue is UIElement scope)
            {
                border._glassBrush.Visual = scope;
            }
        }

        private void OnLayoutUpdated(object sender, EventArgs e)
        {
            if (Scope == null) return;

            try
            {
                var transform = this.TransformToVisual(Scope);
                Point offset = transform.Transform(new Point(0, 0));

                _translateTransform.X = -offset.X;
                _translateTransform.Y = -offset.Y;
            }
            catch
            {
                // Visual tree not ready
            }
        }
    }
}