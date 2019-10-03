using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SteamFriendsManager.Utility
{
    public class AnimatedWrapPanel : Panel
    {
        //public static readonly DependencyProperty AnimationDurationProperty =
        //    DependencyProperty.Register("AnimationDuration",
        //        typeof (int), typeof (AnimatedWrapPanel), new PropertyMetadata(700));

        public static readonly DependencyProperty ItemGapProperty = DependencyProperty.Register("ItemGap",
            typeof(int), typeof(AnimatedWrapPanel), new PropertyMetadata(10));

        //private TimeSpan _animationLength = TimeSpan.FromMilliseconds(200);
        //private bool _firstArrangement = true;

        //public int AnimationDuration
        //{
        //    get { return (int) GetValue(AnimationDurationProperty); }
        //    set
        //    {
        //        _animationLength = TimeSpan.FromMilliseconds(value);
        //        SetValue(AnimationDurationProperty, value);
        //    }
        //}

        public int ItemGap
        {
            get => (int) GetValue(ItemGapProperty);
            set => SetValue(ItemGapProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var infiniteSize = new Size(double.PositiveInfinity, double.PositiveInfinity);
            double curX = 0, curY = 0, curLineHeight = 0;
            foreach (UIElement child in Children)
            {
                child.Measure(infiniteSize);

                double gapX = child.DesiredSize.Width > 0 ? ItemGap : 0;
                double gapY = child.DesiredSize.Height > 0 ? ItemGap : 0;
                if (curX + child.DesiredSize.Width > availableSize.Width)
                {
                    //Wrap to next line
                    curY += curLineHeight + gapY;
                    curX = 0;
                    curLineHeight = 0;
                }

                curX += child.DesiredSize.Width + gapX;
                if (child.DesiredSize.Height > curLineHeight)
                    curLineHeight = child.DesiredSize.Height;
            }

            curX -= ItemGap;
            curY += curLineHeight;

            var resultSize = new Size
            {
                Width = double.IsPositiveInfinity(availableSize.Width)
                    ? curX
                    : availableSize.Width,
                Height = double.IsPositiveInfinity(availableSize.Height)
                    ? curY
                    : availableSize.Height
            };

            return resultSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Children.Count == 0)
                return finalSize;

            double curX = 0, curY = 0, curLineHeight = 0;

            foreach (UIElement child in Children)
            {
                double gapX = child.DesiredSize.Width > 0 ? ItemGap : 0;
                double gapY = child.DesiredSize.Height > 0 ? ItemGap : 0;
                var trans = child.RenderTransform as TranslateTransform;
                if (trans == null)
                {
                    child.RenderTransformOrigin = new Point(0, 0);
                    trans = new TranslateTransform();
                    child.RenderTransform = trans;
                }

                if (curX + child.DesiredSize.Width > finalSize.Width)
                {
                    //Wrap to next line
                    curY += curLineHeight + gapY;
                    curX = 0;
                    curLineHeight = 0;
                }

                child.Arrange(new Rect(curX, curY, child.DesiredSize.Width,
                    child.DesiredSize.Height));

                /* Temporarily disable animation due to bad performance. */

                //child.Arrange(new Rect(0, 0, child.DesiredSize.Width,
                //    child.DesiredSize.Height));

                //if (!_firstArrangement && Math.Abs(trans.X - curX) > 0.01)
                //{
                //    trans.BeginAnimation(TranslateTransform.XProperty,
                //        new DoubleAnimation(curX, _animationLength)
                //        {
                //            EasingFunction = new CubicEase
                //            {
                //                EasingMode = EasingMode.EaseInOut
                //            }
                //        }, HandoffBehavior.SnapshotAndReplace);
                //}
                //else if (_firstArrangement)
                //    trans.X = curX;

                //if (!_firstArrangement && Math.Abs(trans.Y - curY) > 0.01)
                //{
                //    trans.BeginAnimation(TranslateTransform.YProperty,
                //        new DoubleAnimation(curY, _animationLength)
                //        {
                //            EasingFunction = new CubicEase
                //            {
                //                EasingMode = EasingMode.EaseInOut
                //            }
                //        }, HandoffBehavior.SnapshotAndReplace);
                //}
                //else if (_firstArrangement)
                //    trans.Y = curY;

                curX += child.DesiredSize.Width + gapX;
                if (child.DesiredSize.Height > curLineHeight)
                    curLineHeight = child.DesiredSize.Height;
            }

            //if (_firstArrangement)
            //    _firstArrangement = false;

            return finalSize;
        }
    }
}