using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace SteamFriendsManager.Utility
{
    public class DragSelectionHelper : DependencyObject
    {
        #region IsDragSelectionEnabledProperty

        public static bool GetIsDragSelectionEnabled(DependencyObject obj)
        {
            return (bool) obj.GetValue(IsDragSelectionEnabledProperty);
        }

        public static void SetIsDragSelectionEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(IsDragSelectionEnabledProperty, value);
        }

        public static readonly DependencyProperty IsDragSelectionEnabledProperty =
            DependencyProperty.RegisterAttached("IsDragSelectingEnabled", typeof(bool), typeof(DragSelectionHelper),
                new UIPropertyMetadata(false, IsDragSelectingEnabledPropertyChanged));

        private static void IsDragSelectingEnabledPropertyChanged(DependencyObject o,
            DependencyPropertyChangedEventArgs e)
        {
            var listBox = o as ListBox;

            if (listBox == null)
                return;

            // if DragSelection is enabled
            if (GetIsDragSelectionEnabled(listBox))
            {
                // set the listbox's selection mode to multiple ( didn't work with extended )
                listBox.SelectionMode = SelectionMode.Multiple;

                // and subscribe to the required events to handle the drag selection and the attached properties
                listBox.PreviewMouseRightButtonDown += listBox_PreviewMouseRightButtonDown;

                listBox.PreviewMouseLeftButtonDown += listBox_PreviewMouseLeftButtonDown;
                listBox.PreviewMouseLeftButtonUp += listBox_PreviewMouseLeftButtonUp;

                listBox.PreviewKeyDown += listBox_PreviewKeyDown;
                listBox.PreviewKeyUp += listBox_PreviewKeyUp;
            }
            else // is selection is disabled
            {
                // set selection mode to the default
                listBox.SelectionMode = SelectionMode.Extended;

                // unsuscribe from the events
                listBox.PreviewMouseRightButtonDown -= listBox_PreviewMouseRightButtonDown;

                listBox.PreviewMouseLeftButtonDown -= listBox_PreviewMouseLeftButtonDown;
                listBox.PreviewMouseLeftButtonUp -= listBox_PreviewMouseLeftButtonUp;

                listBox.PreviewKeyDown -= listBox_PreviewKeyDown;
                listBox.PreviewKeyUp += listBox_PreviewKeyUp;
            }
        }

        private static void listBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null)
                return;

            if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
                SetIsDragSelectionEnabled(listBox, false);
        }

        private static void listBox_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null)
                return;

            if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
                SetIsDragSelectionEnabled(listBox, true);
        }

        private static void listBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // to prevent the listbox from selecting / deselecting wells on right click
            e.Handled = true;
        }

        private static void listBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SetIsDragClickStarted(sender as DependencyObject, true);
        }

        private static void listBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            SetIsDragClickStarted(sender as DependencyObject, false);
        }

        public static DependencyObject GetParent(DependencyObject obj)
        {
            if (obj == null)
                return null;

            var ce = obj as ContentElement;
            if (ce == null) return VisualTreeHelper.GetParent(obj);

            var parent = ContentOperations.GetParent(ce);
            if (parent != null)
                return parent;

            var fce = ce as FrameworkContentElement;
            return fce != null ? fce.Parent : null;
        }

        #endregion IsDragSelectionEnabledProperty

        #region IsDragSelectingProperty

        public static bool GetIsDragSelecting(DependencyObject obj)
        {
            return (bool) obj.GetValue(IsDragSelectingProperty);
        }

        public static void SetIsDragSelecting(DependencyObject obj, bool value)
        {
            obj.SetValue(IsDragSelectingProperty, value);
        }

        public static readonly DependencyProperty IsDragSelectingProperty =
            DependencyProperty.RegisterAttached("IsDragSelecting", typeof(bool), typeof(DragSelectionHelper),
                new UIPropertyMetadata(false, IsDragSelectingPropertyChanged));

        private static void IsDragSelectingPropertyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var listBoxItem = o as ListBoxItem;

            if (listBoxItem == null)
                return;

            if (!GetIsDragClickStarted(listBoxItem)) return;

            if (GetIsDragSelecting(listBoxItem))
                listBoxItem.IsSelected = true;
        }

        #endregion IsDragSelectingProperty

        #region IsDragClickStartedProperty

        public static bool GetIsDragClickStarted(DependencyObject obj)
        {
            return (bool) obj.GetValue(IsDragClickStartedProperty);
        }

        public static void SetIsDragClickStarted(DependencyObject obj, bool value)
        {
            obj.SetValue(IsDragClickStartedProperty, value);
        }

        public static readonly DependencyProperty IsDragClickStartedProperty =
            DependencyProperty.RegisterAttached("IsDragClickStarted", typeof(bool), typeof(DragSelectionHelper),
                new FrameworkPropertyMetadata(false, IsDragClickStartedPropertyChanged) {Inherits = true});

        private static void IsDragClickStartedPropertyChanged(DependencyObject obj,
            DependencyPropertyChangedEventArgs e)
        {
            var listBox = obj as ListBox;

            if (listBox == null)
                return;

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                return;

            var hitTestResult = VisualTreeHelper.HitTest(listBox, Mouse.GetPosition(listBox));
            if (hitTestResult == null)
                return;

            var element = hitTestResult.VisualHit;
            while (element != null)
            {
                var scrollBar = element as ScrollBar;
                if (scrollBar != null)
                    return;
                element = VisualTreeHelper.GetParent(element);
            }

            if (GetIsDragClickStarted(listBox))
                listBox.SelectedItems.Clear();
        }

        #endregion IsDragClickInitiatedProperty
    }
}