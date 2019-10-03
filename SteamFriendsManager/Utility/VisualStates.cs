using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace SteamFriendsManager.Utility
{
    internal static class VisualStates
    {
        private static FrameworkElement GetImplementationRoot(DependencyObject dependencyObject)
        {
            Debug.Assert(dependencyObject != null, "DependencyObject should not be null.");
            return VisualTreeHelper.GetChildrenCount(dependencyObject) == 1
                ? VisualTreeHelper.GetChild(dependencyObject, 0) as FrameworkElement
                : null;
        }

        public static VisualStateGroup TryGetVisualStateGroup(DependencyObject dependencyObject, string groupName)
        {
            var root = GetImplementationRoot(dependencyObject);
            if (root == null)
                return null;

            var vsg = VisualStateManager.GetVisualStateGroups(root);

            return
                vsg?.OfType<VisualStateGroup>()
                    .FirstOrDefault(group => string.CompareOrdinal(groupName, group.Name) == 0);
        }
    }
}