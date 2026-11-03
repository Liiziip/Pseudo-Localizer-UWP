using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.Storage.Streams;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Media.Imaging;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Animation;

namespace PseudolocalizeUWP
{
    public sealed partial class MainPage : Page
    {
        private bool _introPlayed = false;

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (_introPlayed) return;
            _introPlayed = true;

            var duration = TimeSpan.FromMilliseconds(700);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var sb = new Storyboard();

            // 1) rotate around y
            var rotate = new DoubleAnimation
            {
                From = -30,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(1000),
                EasingFunction = ease
            };
            Storyboard.SetTarget(rotate, ContentScroller);
            Storyboard.SetTargetProperty(rotate,
                "(UIElement.Projection).(PlaneProjection.RotationY)");

            var scale = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = ease
            };

            // 2) fade in 
            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = duration,
                EasingFunction = ease
            };
            Storyboard.SetTarget(fade, ContentScroller);
            Storyboard.SetTargetProperty(fade, "Opacity");

            sb.Children.Add(rotate);
            sb.Children.Add(fade);

            Storyboard.SetTarget(scale, ContentScroller);
            Storyboard.SetTargetProperty(scale,
                "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");
            sb.Children.Add(scale);

            sb.Begin();


        }

        private string _rawLocalized = "";
        private string _rawheadingLocalized = "";
        private StorageFile _pickedFile;

        private UnicodeEncoding _lastEncoding = UnicodeEncoding.Utf8;

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void btn1_Click(object sender, RoutedEventArgs e)
        {
            if (input1.Text == "")
            {
                await new ContentDialog { Title = "Oops :(",Content = "Try to type something...?", CloseButtonText = "OK" }.ShowAsync();
                return;
            }
            else
            {
                // input1 -> localize -> output1
                _rawLocalized = PseudoLocalizer.Localize(input1.Text);
                _rawheadingLocalized = PseudoLocalizer.Localize("Start");
                RefreshOutput1();
            }
            }

            private void btn2_Click(object sender, RoutedEventArgs e)
        {
            input1.Text = "";
            output1.Text = "";
            _rawLocalized = null;
            heading1.Text = "Start";
            _rawheadingLocalized = null;
        }

        private void RefreshOutput1()
        {
            string text = _rawLocalized ?? "";
            string heading = _rawheadingLocalized ?? "";

            // chkbox2：remove 5 letters ID
            // must do before chkbox1,or ID will be lost
            if (chkbox2.IsChecked == true)
            {
                text = System.Text.RegularExpressions.Regex.Replace(
                    text, @"\[[a-zA-Z0-9]{5}\]", "");
                heading = System.Text.RegularExpressions.Regex.Replace(
                    heading, @"\[[a-zA-Z0-9]{5}\]", "");
            }

            // chkbox1：remove[]
            if (chkbox1.IsChecked == true)
            {
                text = text.Replace("[", "").Replace("]", "");
                heading = heading.Replace("[", "").Replace("]", "");
            }

            output1.Text = text;
            heading1.Text = string.IsNullOrEmpty(heading) ? "Start" : heading;
        }

        private async void btn3_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileSavePicker();
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.SuggestedFileName = "Output";
                picker.FileTypeChoices.Add("Text file", new List<string> { ".txt",".xml"});

                StorageFile file = await picker.PickSaveFileAsync();
                if (file == null)
                    return; 

                CachedFileManager.DeferUpdates(file);
                await FileIO.WriteTextAsync(file, output1.Text);
                FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                if (status == FileUpdateStatus.Complete)                
                    await new ContentDialog { Title = "File Successfully Saved!", Content = file.Path,
                         CloseButtonText = "OK" }.ShowAsync();                
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private async void btn4_Click(object sender, RoutedEventArgs e)
        {
            var content = Clipboard.GetContent();
            if (content.Contains(StandardDataFormats.Text))
            {
                string text = await content.GetTextAsync();
                input1.Text = text;
            }
            else
            {
                await new ContentDialog { Title = "Oops :(", Content = "Your clipboard seems to be a bit lonely..." ,CloseButtonText = "OK"}.ShowAsync();
            }
        }

        private void btn5_Click(object sender, RoutedEventArgs e)
        {
            var datapackage = new DataPackage();
            datapackage.SetText(output1.Text);
            Clipboard.SetContent(datapackage);
        }

        private async void filepicker1_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".psd1");
            picker.FileTypeFilter.Add(".mfl");
            picker.FileTypeFilter.Add(".ini");
            picker.FileTypeFilter.Add(".xml");
            picker.FileTypeFilter.Add(".adml");
            picker.FileTypeFilter.Add(".resx");
            picker.FileTypeFilter.Add(".inf");
            picker.FileTypeFilter.Add(".txt");

            StorageFile file = await picker.PickSingleFileAsync();
            if (file == null)
                return;
            _pickedFile = file;
            fileinput1.Text = file.Path;

        }


        private async void btn6_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StorageFile file = _pickedFile;

                if (file == null)
                {
                    string path = (fileinput1.Text ?? string.Empty).Trim().Trim('"');
                    if (path.Length == 0)
                    {
                        await new ContentDialog
                        {
                            Title = "Oops :(",
                            Content = "Nothing is selected...",
                            CloseButtonText = "OK"
                        }.ShowAsync();
                        return;
                    }
                    file = await StorageFile.GetFileFromPathAsync(path);
                }

                var buffer = await FileIO.ReadBufferAsync(file);
                byte[] bytes = buffer.ToArray();
                _lastEncoding = DetectEncoding(bytes);
                string text = TextFile.Decode(bytes);
                output2.Text = FileLocalizers.LocalizeByExtension(file.FileType, text);
                _rawheadingLocalized = PseudoLocalizer.Localize("Start");
                RefreshOutput1();
            }
            catch (UnauthorizedAccessException)
            {
                await new ContentDialog
                {
                    Title = "ERROR",
                    Content = "UWP sandbox does not allow to read this file,browse again",
                    CloseButtonText = "OK"
                }.ShowAsync();
            }
            catch (Exception ex)
            {
                await new ContentDialog
                {
                    Title = "Localization Failed",
                    Content = ex.Message,
                    CloseButtonText = "OK"
                }.ShowAsync();
            }
        }

        /// <summary>按 BOM 判断源文件编码，保存时照原样写回。</summary>
        private static UnicodeEncoding DetectEncoding(byte[] bytes)
        {
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return UnicodeEncoding.Utf16LE;
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return UnicodeEncoding.Utf16BE;
            return UnicodeEncoding.Utf8;
        }

        private async void btn7_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(output2.Text))
            {
                await new ContentDialog
                {
                    Title = "Oops :(",
                    Content = "Nothing is output...",
                    CloseButtonText = "OK"
                }.ShowAsync();
                return;
            }

            try
            {
                string ext = (_pickedFile != null && !string.IsNullOrEmpty(_pickedFile.FileType))
                    ? _pickedFile.FileType
                    : ".txt";
                string baseName = (_pickedFile != null)
                    ? Path.GetFileNameWithoutExtension(_pickedFile.Name)
                    : "Output";

                var picker = new FileSavePicker();
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.SuggestedFileName = baseName + "_pseudo";
                picker.FileTypeChoices.Add("same as original (" + ext + ")", new List<string> { ext });
                if (!string.Equals(ext, ".txt", StringComparison.OrdinalIgnoreCase))
                    picker.FileTypeChoices.Add("Text", new List<string> { ".txt" });

                StorageFile file = await picker.PickSaveFileAsync();
                if (file == null)
                    return;

                CachedFileManager.DeferUpdates(file);
                await FileIO.WriteTextAsync(file, output2.Text, _lastEncoding);
                FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);

                if (status == FileUpdateStatus.Complete)
                {
                    await new ContentDialog
                    {
                        Title = "File Saved Successfully!",
                        Content = file.Path,
                        CloseButtonText = "OK"
                    }.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                await new ContentDialog
                {
                    Title = "ERROR",
                    Content = ex.Message,
                    CloseButtonText = "OK"
                }.ShowAsync();
            }
        }

        private void btn8_Click(object sender, RoutedEventArgs e)
        {
            fileinput1.Text = "";
            output2.Text = "";
            _pickedFile = null;
            _rawheadingLocalized = null;
            RefreshOutput1();
        }

        private void chkbox1_Checked(object sender, RoutedEventArgs e) => RefreshOutput1();
        private void chkbox1_Unchecked(object sender, RoutedEventArgs e) => RefreshOutput1();
        private void chkbox2_Checked(object sender, RoutedEventArgs e) => RefreshOutput1();
        private void chkbox2_Unchecked(object sender, RoutedEventArgs e) => RefreshOutput1();

        private static readonly Random _bgRng = new Random();
        private string _lastBgName = null;
        // true 表示当前显示的是 A 层，B 层隐藏
        private bool _bgIsShowingA = true;

        // 动画进行中忽略新的点击，避免两层状态错乱
        private bool _bgSwitching = false;

        private async void heading1_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (_bgSwitching) return;

            try
            {
                StorageFolder installFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
                StorageFolder assetsFolder = await installFolder.GetFolderAsync("Assets");
                StorageFolder bgFolder = await assetsFolder.GetFolderAsync("background");

                var pngFiles = (await bgFolder.GetFilesAsync())
                    .Where(f => f.FileType.Equals(".png", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (pngFiles.Count == 0)
                    return;

                StorageFile picked;
                if (pngFiles.Count > 1 && _lastBgName != null)
                {
                    var candidates = pngFiles.Where(f => f.Name != _lastBgName).ToList();
                    picked = candidates[_bgRng.Next(candidates.Count)];
                }
                else
                {
                    picked = pngFiles[_bgRng.Next(pngFiles.Count)];
                }
                _lastBgName = picked.Name;

                var bitmap = new BitmapImage(new Uri($"ms-appx:///Assets/background/{picked.Name}"));
                await CrossFadeBackgroundAsync(bitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"switch failed: {ex.Message}");
            }
        }

        private async Task CrossFadeBackgroundAsync(ImageSource newSource)
        {
            _bgSwitching = true;
            try
            {
                // 决定谁显示新图、谁淡出
                var toShow = _bgIsShowingA ? BgLayerB : BgLayerA;
                var toHide = _bgIsShowingA ? BgLayerA : BgLayerB;

                // 先把新图放到隐藏层，并确保它 Opacity=0 再开始动画
                toShow.Opacity = 0;
                toShow.Source = newSource;

                // 等图片解码完成，避免“空层先淡入、图片后出现”的突兀感
                var loadedTcs = new TaskCompletionSource<bool>();
                if (newSource is BitmapImage bmp)
                {
                    bmp.ImageOpened += (s, e) => loadedTcs.TrySetResult(true);
                    bmp.ImageFailed += (s, e) => loadedTcs.TrySetResult(false);
                }
                else
                {
                    loadedTcs.TrySetResult(true);
                }

                if (!await loadedTcs.Task)
                {
                    // 图片加载失败，不切换，保持原背景
                    toShow.Source = null;
                    return;
                }

                // 交叉淡化：旧层 1→0，新层 0→1
                var duration = TimeSpan.FromMilliseconds(1500);
                var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

                var sb = new Storyboard();

                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = duration,
                    EasingFunction = ease
                };
                Storyboard.SetTarget(fadeOut, toHide);
                Storyboard.SetTargetProperty(fadeOut, "Opacity");

                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = duration,
                    EasingFunction = ease
                };
                Storyboard.SetTarget(fadeIn, toShow);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");

                sb.Children.Add(fadeOut);
                sb.Children.Add(fadeIn);

                var doneTcs = new TaskCompletionSource<bool>();
                sb.Completed += (s, e) => doneTcs.TrySetResult(true);
                sb.Begin();
                await doneTcs.Task;

                // 交换“当前可见层”标记，为下一次切换做准备
                _bgIsShowingA = !_bgIsShowingA;
            }
            finally
            {
                _bgSwitching = false;
            }
        }
    }
}
