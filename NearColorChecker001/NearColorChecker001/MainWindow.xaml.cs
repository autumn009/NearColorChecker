using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO.IsolatedStorage;
using System.IO;

namespace NearColorChecker001
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Properties.Settings.Default.Upgrade();
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.Target)) TextBoxTargetFolder.Text = Properties.Settings.Default.Target;
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.Trash)) TextBoxTrashFolder.Text = Properties.Settings.Default.Trash;
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.Threshold)) TextBoxThreshold.Text = Properties.Settings.Default.Threshold;
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.FilterString)) TextBoxOutputFilter.Text = Properties.Settings.Default.FilterString;
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.MonoThreshold)) TextBoxMonoThreathold.Text = Properties.Settings.Default.MonoThreshold;
        }

        private List<List<PictureInfo>> resultMap = new List<List<PictureInfo>>();
        private void ButtonSearch_Click(object sender, RoutedEventArgs e)
        {
            var root = TextBoxTargetFolder.Text;
            if (!Directory.Exists(root)) return;
            int n;
            if (!int.TryParse(TextBoxThreshold.Text, out n))
            {
                MessageBox.Show("Invalid Threshold Value");
                return;
            }
            int monoThreashold;
            if (!int.TryParse(TextBoxMonoThreathold.Text, out monoThreashold))
            {
                MessageBox.Show("Invalid MonoThreshold Value");
                return;
            }
            var wnd = new WorkingWindow();
            wnd.Owner = this;
            wnd.Show();
            this.IsEnabled = false;
            ListBoxSelect.Items.Clear();
            string filter = TextBoxOutputFilter.Text;
            Task.Run(() =>
            {
                try
                {
                    var map = new List<PictureInfo>();
                    int count = 0;
                    Util.FileWalker(root, (filename) =>
                    {
                        var pi = Util.CalcScore(filename);
                        if (pi == null) return;
                        map.Add(pi);
                        count++;
                        if (count % 10 == 0)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                TextBlockStatus.Text = count.ToString();
                            });
                        }
                    });
                    Dispatcher.Invoke(() =>
                    {
                        TextBlockStatus.Text = "Grouping";
                    });
                    Util.PictureSeiri(map, resultMap, n);
                    Dispatcher.Invoke(() =>
                    {
                        foreach (var item in resultMap.Where(c => c.Count() > 1 && !Util.IsMonoTone(c[0].color[0,0],monoThreashold) && (filter.Length == 0 || c.Any(d => d.filename.Contains(filter)))))
                        {
                            ListBoxSelect.Items.Add(item[0]);
                        }
                        wnd.Close();
                        this.IsEnabled = true;
                        TextBlockStatus.Text = "Done";
                    });
                }
                catch (Exception e2)
                {
                    Dispatcher.Invoke(() =>
                    {
                        TextBlockStatus.Text = e2.ToString();
                    });
                }
            });
        }

        private List<Action> deleteEvents = new List<Action>();
        private void ButtonMove_Click(object sender, RoutedEventArgs e)
        {
            if (TextBoxTrashFolder.Text.Length > 1
                && TextBoxTrashFolder.Text.Length > 1
                && char.ToLower(TextBoxTargetFolder.Text[0]) == char.ToLower(TextBoxTrashFolder.Text[0])
                && TextBoxTargetFolder.Text[1] == ':'
                && TextBoxTrashFolder.Text[1] == ':')
            {
                if (DisableConfirmToDelete.IsChecked != true)
                {
                    var r = MessageBox.Show("Are you sure to move checked files?", "NearColorChecker", MessageBoxButton.YesNo);
                    if (r != MessageBoxResult.Yes) return;
                }
                foreach (var item in deleteEvents.ToArray()) item();
                Task.Run(() =>
                {
                    Task.Delay(1000).Wait();
                    Dispatcher.Invoke(() =>
                    {
                        if (ListBoxSelect.SelectedIndex < ListBoxSelect.Items.Count - 1)
                            ListBoxSelect.SelectedIndex++;
                    });
                });
            }
            else
            {
                MessageBox.Show("You must specify same drive for two directories", "NearColorChecker");
            }
        }

        private void ButtonSkip_Click(object sender, RoutedEventArgs e)
        {
            if (ListBoxSelect.SelectedIndex < ListBoxSelect.Items.Count - 1)
                ListBoxSelect.SelectedIndex++;
        }

        private void ButtonTargetFolderSelect_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.ShowNewFolderButton = true;
                dialog.SelectedPath = TextBoxTargetFolder.Text;
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                TextBoxTargetFolder.Text = dialog.SelectedPath;
            }
        }

        private void ButtonTrashFolderSelect_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.ShowNewFolderButton = true;
                dialog.SelectedPath = TextBoxTrashFolder.Text;
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                TextBoxTrashFolder.Text = dialog.SelectedPath;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Properties.Settings.Default.Target = TextBoxTargetFolder.Text;
            Properties.Settings.Default.Trash = TextBoxTrashFolder.Text;
            Properties.Settings.Default.Threshold = TextBoxThreshold.Text;
            Properties.Settings.Default.FilterString = TextBoxOutputFilter.Text;
            Properties.Settings.Default.MonoThreshold = TextBoxMonoThreathold.Text;
            Properties.Settings.Default.Save();
        }

        private void ListBoxSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListViewResult.Items.Clear();
            deleteEvents.Clear();
            var item = ListBoxSelect.SelectedItem;
            if (item == null) return;

            List<PictureInfo> target = resultMap.FirstOrDefault(c => c[0] == item);
            bool isFirstItem = false;
            foreach (var item2 in target)
            {
                var lvi = new ListViewItem();
                lvi.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                //var spf = new StackPanel();
                //spf.Orientation = Orientation.Horizontal;
                var spf = new Grid();
                spf.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                var sp = new StackPanel();
                sp.Orientation = Orientation.Vertical;
                sp.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                var chbox = new CheckBox();
                chbox.IsChecked = isFirstItem;
                chbox.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                isFirstItem = true;
                chbox.Content = string.Format("{0}x{1}", item2.width, item2.height);
                sp.Children.Add(chbox);
                var open = new Button();
                open.Content = "view large";
                open.Height = 50;
                open.Click += (sender2, evt) =>
                {
                    System.Diagnostics.Process.Start(item2.filename);
                };
                open.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                sp.Children.Add(open);
                var status = new Button();
                status.Content = "file info";
                status.Height = 50;
                status.Click += (sender2, evt) =>
                {
                    System.Diagnostics.Process.Start("explorer.exe", "/select," + item2.filename);
                };
                status.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                sp.Children.Add(status);
                var nameTextBlock = new TextBlock();
                nameTextBlock.Text = item2.filename.Substring(TextBoxTargetFolder.Text.Length + 1);
                nameTextBlock.TextWrapping = TextWrapping.Wrap;
                nameTextBlock.Width = 100;
                nameTextBlock.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                sp.Children.Add(nameTextBlock);
#if DEBUG
                var viewmap = new Button();
                viewmap.Content = "view map";
                viewmap.Click += (sender2, evt) =>
                {
                    var sb = new StringBuilder();
                    for (int y = 0; y < Constants.ColorMapY; y++)
                    {
                        for (int x = 0; x < Constants.ColorMapX; x++)
                        {
                            sb.Append(item2.color[x, y]);
                            sb.Append(",");
                        }
                        sb.Append("\r\n");
                    }
                    MessageBox.Show(sb.ToString());
                };
                sp.Children.Add(viewmap);
#endif
                var img = new Image();
                img.Margin = new Thickness(0, 0, 100, 0);
                //img.Width = 150;
                //img.Height = 150;
                var bm = new BitmapImage();
                bm.BeginInit();
                bm.CacheOption = BitmapCacheOption.OnLoad;
                bm.UriSource = Util.CreateFileUri(item2.filename);
                bm.EndInit();
                img.Source = bm;
                spf.Children.Add(img);
                spf.Children.Add(sp);
                lvi.Content = spf;
                ListViewResult.Items.Add(lvi);
#if DEBUG
                var lvi2 = new ListViewItem();
                lvi2.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                var spf2 = new Grid();
                spf2.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                var img2 = new Image();
                img2.Margin = new Thickness(0, 0, 100, 0);
                img2.Source = Util.GetMosaicPicture(item2);
                img2.Effect = null;
                spf2.Children.Add(img2);
                lvi2.Content = spf2;
                ListViewResult.Items.Add(lvi2);
#endif
                Action act = null;
                act = () =>
                {
                    if (chbox.IsChecked == true)
                    {
                        var dstFileNameBase = System.IO.Path.Combine(TextBoxTrashFolder.Text, System.IO.Path.GetFileName(item2.filename));
                        var dstFileName = dstFileNameBase;
                        int count = 0;
                        while (File.Exists(dstFileName))
                        {
                            var ext = System.IO.Path.GetExtension(dstFileNameBase);
                            var name = System.IO.Path.GetFileNameWithoutExtension(dstFileName);
                            var dir = System.IO.Path.GetDirectoryName(dstFileName);
                            dstFileName = System.IO.Path.Combine(dir, name + count.ToString() + ext);
                            count++;
                        }
                        File.Move(item2.filename, dstFileName);
                        ListViewResult.Items.Remove(lvi);
                        var item3 = ListBoxSelect.SelectedItem;
                        if (item3 == null) return;
                        List<PictureInfo> target3 = resultMap.FirstOrDefault(c => c[0] == item3);
                        if (target3 != null) target3.Remove(item2);
                        deleteEvents.Remove(act);
                    }
                };
                deleteEvents.Add(act);
            }
        }

        private void ListViewResult_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var borderWidth = SystemParameters.ResizeFrameVerticalBorderWidth;
            var scrollWidth = SystemParameters.VerticalScrollBarWidth;
            MyGridViewColumn.Width = Math.Max(1,ListViewResult.ActualWidth - borderWidth - scrollWidth);
            //ListViewResult.Items.Clear();
            //deleteEvents.Clear();
        }
    }
}
