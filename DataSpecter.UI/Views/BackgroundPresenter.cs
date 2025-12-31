using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DataSpecter.UI.Views
{
    // A special control that mirrors everything behind it
    public class BackgroundPresenter : FrameworkElement
    {
        private static readonly FieldInfo _drawingContentOfUIElement = typeof(UIElement)
            .GetField("_drawingContent", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static readonly FieldInfo _contentOfDrawingVisual = typeof(DrawingVisual)
            .GetField("_content", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static readonly FieldInfo _offsetOfVisual = typeof(Visual)
            .GetField("_offset", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static readonly Func<UIElement, DrawingContext> _renderOpenMethod = typeof(UIElement)
            .GetMethod("RenderOpen", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<UIElement, DrawingContext>>();

        private static readonly Action<UIElement, DrawingContext> _onRenderMethod = typeof(UIElement)
            .GetMethod("OnRender", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Action<UIElement, DrawingContext>>();

        private static readonly MethodInfo _methodGetContentBounds = typeof(VisualBrush)
            .GetMethod("GetContentBounds", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private readonly Stack<UIElement> _parentStack = new Stack<UIElement>();

        private static void ForceRender(UIElement target)
        {
            if (target == null) return;
            try
            {
                using (DrawingContext drawingContext = _renderOpenMethod(target))
                {
                    _onRenderMethod.Invoke(target, drawingContext);
                }
            }
            catch { /* Ignore render errors during layout updates */ }
        }

        private static void DrawVisual(DrawingContext drawingContext, Visual visual, Point relatedXY, Size renderSize)
        {
            if (visual == null) return;
            try
            {
                var visualBrush = new VisualBrush(visual) { Stretch = Stretch.None };
                var visualOffset = (Vector)_offsetOfVisual.GetValue(visual)!;

                // Invoke private GetContentBounds
                object[] args = new object[] { null }; // out Rect
                _methodGetContentBounds.Invoke(visualBrush, args);
                Rect contentBounds = (Rect)args[0];

                relatedXY -= visualOffset;

                drawingContext.DrawRectangle(
                    visualBrush, null,
                    new Rect(relatedXY.X + contentBounds.X, relatedXY.Y + contentBounds.Y, contentBounds.Width, contentBounds.Height));
            }
            catch { }
        }

        protected override Geometry GetLayoutClip(Size layoutSlotSize)
        {
            return new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));
        }

        protected override void OnVisualParentChanged(DependencyObject oldParentObject)
        {
            base.OnVisualParentChanged(oldParentObject);

            if (oldParentObject is UIElement oldParent)
                oldParent.LayoutUpdated -= ParentLayoutUpdated;

            if (VisualTreeHelper.GetParent(this) is UIElement newParent)
                newParent.LayoutUpdated += ParentLayoutUpdated;
        }

        private void ParentLayoutUpdated(object sender, EventArgs e)
        {
            // Force this element to re-render when the parent changes
            ForceRender(this);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            // Draw everything behind this element onto this element
            DrawBackground(drawingContext, this, _parentStack, 10, false);
        }

        private static void DrawBackground(
            DrawingContext drawingContext, UIElement self,
            Stack<UIElement> parentStackStorage,
            int maxDepth,
            bool throwExceptionIfParentArranging)
        {
            var parent = VisualTreeHelper.GetParent(self) as UIElement;

            // 1. Walk up the tree to find ancestors
            while (parent != null && parentStackStorage.Count < maxDepth)
            {
                if (!parent.IsVisible) { parentStackStorage.Clear(); return; }
                parentStackStorage.Push(parent);
                parent = VisualTreeHelper.GetParent(parent) as UIElement;
            }

            var selfRect = new Rect(0, 0, self.RenderSize.Width, self.RenderSize.Height);

            // 2. Walk down and draw ancestors + siblings
            while (parentStackStorage.Count > 0)
            {
                var currentParent = parentStackStorage.Pop();
                UIElement breakElement = (parentStackStorage.Count > 0) ? parentStackStorage.Peek() : self;

                // Draw Parent's own content
                if (_drawingContentOfUIElement.GetValue(currentParent) is object parentDrawingContent)
                {
                    var drawingVisual = new DrawingVisual();
                    _contentOfDrawingVisual.SetValue(drawingVisual, parentDrawingContent);
                    var parentRelatedXY = currentParent.TranslatePoint(new Point(0, 0), self);
                    DrawVisual(drawingContext, drawingVisual, parentRelatedXY, currentParent.RenderSize);
                }

                // Draw Siblings (Children of parent that are behind 'self')
                if (currentParent is Panel parentPanel)
                {
                    foreach (UIElement child in parentPanel.Children)
                    {
                        if (child == breakElement) break; // Stop when we reach the branch containing 'self'

                        if (child.IsVisible)
                        {
                            var childRelatedXY = child.TranslatePoint(new Point(0, 0), self);
                            var childRect = new Rect(childRelatedXY, child.RenderSize);

                            if (selfRect.IntersectsWith(childRect))
                            {
                                DrawVisual(drawingContext, child, childRelatedXY, child.RenderSize);
                            }
                        }
                    }
                }
            }
        }
    }
}