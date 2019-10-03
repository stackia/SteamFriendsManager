using System;
using System.Net.Cache;
using System.Windows;
using System.Windows.Media.Imaging;

namespace SteamFriendsManager.Utility
{
    public class Image : System.Windows.Controls.Image
    {
        public static readonly DependencyProperty ImageUrlProperty = DependencyProperty.Register("ImageUrl",
            typeof(string), typeof(Image), new PropertyMetadata("", ImageUrlPropertyChanged));

        static Image()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Image),
                new FrameworkPropertyMetadata(typeof(Image)));
        }

        public string ImageUrl
        {
            get => (string) GetValue(ImageUrlProperty);
            set => SetValue(ImageUrlProperty, value);
        }

        private static void ImageUrlPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            var url = e.NewValue as string;

            if (string.IsNullOrEmpty(url))
                return;

            var cachedImage = (Image) obj;
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.UriSource = new Uri(url);
            bitmapImage.UriCachePolicy = new RequestCachePolicy(RequestCacheLevel.Default);
            bitmapImage.EndInit();
            cachedImage.Source = bitmapImage;
        }
    }
}