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
            // should not upgrade any value
            //Properties.Settings.Default.Upgrade();
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.Target)) TextBoxTargetFolder.Text = Properties.Settings.Default.Target;
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.Trash)) TextBoxTrashFolder.Text = Properties.Settings.Default.Trash;
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.Threshold)) TextBoxThreshold.Text = Properties.Settings.Default.Threshold;
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.FilterString)) TextBoxOutputFilter.Text = Properties.Settings.Default.FilterString;
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.DiffThreshold)) TextBoxDiffThreathold.Text = Properties.Settings.Default.DiffThreshold;
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.WaitTime)) TextBoxWaitMS.Text = Properties.Settings.Default.WaitTime;
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
            int diff;
            if (!int.TryParse(TextBoxDiffThreathold.Text, out diff))
            {
                MessageBox.Show("Invalid DiffThreshold Value");
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
                    Util.PictureSeiri(map, resultMap, n, diff);
                    Dispatcher.Invoke(() =>
                    {
#if DEBUG && false
                        foreach (var item in resultMap)
#else
                        foreach (var item in resultMap.Where(c => c.Count() > 1
                            && (filter.Length == 0 || c.Any(d => d.filename.Contains(filter))))
                            )
#endif
                        {
                            try
                            {
                                ListBoxSelect.Items.Add(item[0]);
                            }
                            catch (Exception e3)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    var filenames = string.Join(",", item.Select(c => c.filename));
                                    MessageBox.Show(filenames + " has error: " +
                                        e3.ToString());
                                });
                            }
                        }
                        wnd.Close();
                        UpdateItems();
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

        private void UpdateItems()
        {
            TextBlockItems.Text = ListBoxSelect.Items.Count.ToString() + " items";
        }

        private List<Action> deleteEvents = new List<Action>();
        private void ButtonMove_Click(object sender, RoutedEventArgs e)
        {
            if (DisableConfirmToDelete.IsChecked != true)
            {
                var dstdir = System.IO.Path.Combine(System.IO.Path.GetPathRoot(TextBoxTargetFolder.Text), TextBoxTrashFolder.Text);
                var r = MessageBox.Show("Are you sure to move checked files to " + dstdir + "?", "NearColorChecker", MessageBoxButton.YesNo);
                if (r != MessageBoxResult.Yes) return;
            }
            deleteItSub();
        }

        private void deleteItSub()
        {
            foreach (var item in deleteEvents.ToArray()) item();
            int waitTime;
            if (!int.TryParse(this.TextBoxWaitMS.Text, out waitTime)) waitTime = 1000;
            Task.Run(() =>
            {
                Task.Delay(waitTime).Wait();
                Dispatcher.Invoke(() =>
                {
                    if (ListBoxSelect.SelectedIndex < ListBoxSelect.Items.Count - 1)
                        ListBoxSelect.SelectedIndex++;
                });
            });
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
            Properties.Settings.Default.DiffThreshold = TextBoxDiffThreathold.Text;
            Properties.Settings.Default.WaitTime = TextBoxWaitMS.Text;
            Properties.Settings.Default.Save();
        }

        private List<CheckBox> allCheckboxes = new List<CheckBox>();
        private void ListBoxSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ListViewResult.Items.Clear();
                deleteEvents.Clear();
                allCheckboxes.Clear();
                var item = ListBoxSelect.SelectedItem;
                if (item == null) return;

                List<PictureInfo> target = resultMap.FirstOrDefault(c => c[0] == item);
                if (target == null) return;

                bool isFirstItem = false;
                foreach (var item2 in target)
                {
                    var wishToRemoveLVIs = new List<ListViewItem>();

                    var lvi = new ListViewItem();
                    wishToRemoveLVIs.Add(lvi);
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
                    allCheckboxes.Add(chbox);
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

                    var moveHere = new Button();
                    moveHere.Content = "move largest file here";
                    moveHere.Height = 50;
                    moveHere.Click += (sender2, evt) =>
                    {
                        var first = allCheckboxes.FirstOrDefault();
                        if (first == null) return;
                        var firstItem = target.FirstOrDefault();
                        if (firstItem == null) return;
                        var targetPath = System.IO.Path.GetDirectoryName(item2.filename);
                        var targetFileName = System.IO.Path.GetFileName(firstItem.filename);
                        var TargetFullPath = System.IO.Path.Combine(targetPath, targetFileName);
                        first.IsChecked = false;
                        foreach (var checkbox in allCheckboxes.Skip(1)) checkbox.IsChecked = true;
                        deleteItSub();
                        if (TargetFullPath != firstItem.filename)
                        {
                            File.Move(firstItem.filename, TargetFullPath);
                            firstItem.filename = TargetFullPath;
                        }
                    };
                    moveHere.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                    sp.Children.Add(moveHere);
                    
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
                    wishToRemoveLVIs.Add(lvi2);
                    lvi2.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                    var spf2 = new Grid();
                    spf2.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                    var img2 = new Image();
                    img2.Margin = new Thickness(0, 0, 100, 0);
                    img2.Source = Util.GetMosaicPicture(item2.color);
                    img2.Effect = null;
                    spf2.Children.Add(img2);
                    lvi2.Content = spf2;
                    ListViewResult.Items.Add(lvi2);

                    var lvi3 = new ListViewItem();
                    wishToRemoveLVIs.Add(lvi3);
                    lvi3.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                    var spf3 = new Grid();
                    spf3.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                    var img3 = new Image();
                    img3.Margin = new Thickness(0, 0, 100, 0);
                    var bm3 = Util.CreateMono(bm);
                    img3.Source = bm3;
                    img3.Effect = null;
                    spf3.Children.Add(img3);
                    lvi3.Content = spf3;
                    ListViewResult.Items.Add(lvi3);

                    var lvi4 = new ListViewItem();
                    wishToRemoveLVIs.Add(lvi4);
                    lvi4.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                    var spf4 = new Grid();
                    spf4.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                    var img4 = new Image();
                    img4.Margin = new Thickness(0, 0, 100, 0);
                    img4.Source = Util.CreateDiff(bm3);
                    img4.Effect = null;
                    spf4.Children.Add(img4);
                    lvi4.Content = spf4;
                    ListViewResult.Items.Add(lvi4);

                    var lvi5 = new ListViewItem();
                    wishToRemoveLVIs.Add(lvi5);
                    lvi5.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                    var spf5 = new Grid();
                    spf5.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                    var img5 = new Image();
                    img5.Margin = new Thickness(0, 0, 100, 0);
                    img5.Source = Util.GetMosaicPicture(item2.colorDiff);
                    img5.Effect = null;
                    spf5.Children.Add(img5);
                    lvi5.Content = spf5;
                    ListViewResult.Items.Add(lvi5);
#endif
                    Action act = null;
                    act = () =>
                    {
                        if (chbox.IsChecked == true)
                        {
                            var dstFileNameBase = System.IO.Path.Combine(
                                System.IO.Path.GetPathRoot(TextBoxTargetFolder.Text),
                                TextBoxTrashFolder.Text,
                                System.IO.Path.GetFileName(item2.filename));
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
                            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dstFileName));
                            File.Move(item2.filename, dstFileName);
                            foreach (var itemLvi in wishToRemoveLVIs) ListViewResult.Items.Remove(itemLvi);
                            var item3 = ListBoxSelect.SelectedItem;
                            if (item3 == null) return;
                            List<PictureInfo> target3 = resultMap.FirstOrDefault(c => c[0] == item3);
                            if (target3 != null) target3.Remove(item2);
                            deleteEvents.Remove(act);
                            UpdateItems();
                        }
                    };
                    deleteEvents.Add(act);
                }
            }
            catch (Exception e2)
            {
                TextBlockStatus.Text = e2.ToString();
            }
        }

        private void ListViewResult_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var borderWidth = SystemParameters.ResizeFrameVerticalBorderWidth;
            var scrollWidth = SystemParameters.VerticalScrollBarWidth;
            MyGridViewColumn.Width = Math.Max(1, ListViewResult.ActualWidth - borderWidth - scrollWidth);
            //ListViewResult.Items.Clear();
            //deleteEvents.Clear();
        }

        private void ButtonAuto_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
