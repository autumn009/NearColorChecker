using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Windows.Media;

namespace NearColorChecker001
{
    static class Constants
    {
        internal const int ColorMapX = 16;
        internal const int ColorMapY = 16;
    }

    class PictureInfo
    {
        internal Color[,] color;
        internal string filename;
        internal int width;
        internal int height;
        internal int size;
        public PictureInfo()
        {
            color = new Color[Constants.ColorMapX, Constants.ColorMapY];
        }
        public override string ToString()
        {
            return color[0, 0].ToString();
        }
    }

    static class Util
    {
        internal static void FileWalker(string root, Action<string> doit)
        {
            foreach (var item in Directory.EnumerateFiles(root, "*.jpg", SearchOption.AllDirectories))
            {
                doit(item);
            }
        }

        private static double calcDistance(double x0,double y0,double x1,double y1)
        {
            return Math.Sqrt((x0 - x1) * (x0 - x1) + (y0 - y1) * (y0 - y1));
        }

        internal static PictureInfo CalcScore(string filename)
        {
            //var waiter = new AutoResetEvent(false);
            BitmapImage bm;
            try
            {
                bm = new BitmapImage(new Uri(filename));
            }
            catch (NotSupportedException)
            {
                return null;
            }
            bm.CacheOption = BitmapCacheOption.OnLoad;
            var bmw = new WriteableBitmap(bm);

            //waiter.WaitOne();
            var size = File.ReadAllBytes(filename).Length;
            var buf = new byte[bm.PixelWidth * bm.PixelHeight * 4];
            //bm.StreamSource.Read(buf,0,size);

            bmw.CopyPixels(buf, bm.PixelWidth * 4, 0);

            var pi = new PictureInfo();
            pi.filename = filename;
            pi.width = bm.PixelWidth;
            pi.height = bm.PixelHeight;
            pi.size = size;
            byte[,] b = new byte[Constants.ColorMapX, Constants.ColorMapY];
            byte[,] g = new byte[Constants.ColorMapX, Constants.ColorMapY];
            byte[,] r = new byte[Constants.ColorMapX, Constants.ColorMapY];
            int xunit = pi.width / Constants.ColorMapX;
            int yunit = pi.height / Constants.ColorMapY;
            int distanceBase = Math.Max(xunit,yunit);
            for (int y = 0; y < Constants.ColorMapY; y++)
            {
                for (int x = 0; x < Constants.ColorMapX; x++)
                {
                    double centerX = (x+0.5) * xunit;
                    double centerY = (y+0.5) * yunit;
                    double rsum = 0.0, gsum = 0.0, bsum = 0.0;
                    for (int y0 = 0; y0 < yunit; y0++)
                    {
                        int i = ((y * yunit + y0) * pi.width + x * xunit) * 4;
                        for (int x0 = 0; x0 < xunit; x0++)
                        {
                            double distance = calcDistance(centerX, centerY, x * xunit + x0, y * yunit + y0);
                            double stronglevel = 1.0 - distance / distanceBase;
                            if (stronglevel < 0.0) continue;
                            bsum = bsum * (1.0 - stronglevel) + buf[i] * stronglevel;
                            gsum = gsum * (1.0 - stronglevel) + buf[i + 1] * stronglevel;
                            rsum = rsum * (1.0 - stronglevel) + buf[i + 2] * stronglevel;
                            i += 4;
                        }
                    }
                    b[x, y] = (byte)bsum;
                    g[x, y] = (byte)gsum;
                    r[x, y] = (byte)rsum;
                    //System.Diagnostics.Debug.WriteLine("b=" + b[x, y] + " g=" + g[x, y] + " r=" + r[x, y]);
                }
            }

            for (int y = 0; y < Constants.ColorMapY; y++)
            {
                for (int x = 0; x < Constants.ColorMapX; x++)
                {
                    pi.color[x, y] = Color.FromRgb(r[x, y], g[x, y], b[x, y]);
                }
            }
            return pi;
        }

        private static int calcDiffColor(Color a, Color b)
        {
            //System.Diagnostics.Debug.WriteLine("a=" + a.ToString() + " b" + b.ToString() + " diff=" + (Math.Abs(a.B - b.B) + Math.Abs(a.G - b.G) + Math.Abs(a.R - b.R)));
            return Math.Abs(a.B - b.B) + Math.Abs(a.G - b.G) + Math.Abs(a.R - b.R);
        }

        private static bool TestThreshold(Color[,] a, Color[,] b, int threshold)
        {
            for (int y = 0; y < Constants.ColorMapY; y++)
            {
                for (int x = 0; x < Constants.ColorMapX; x++)
                {
                    if (calcDiffColor(a[x, y], b[x, y]) > threshold) return false;
                }
            }
            return true;
        }

        internal static void PictureSeiri(List<PictureInfo> map, List<List<PictureInfo>> resultMap, int threshold)
        {
            resultMap.Clear();
            for (; ; )
            {
                if (map.Count() == 0) return;
                var target = map.First();
                var list = new List<PictureInfo>();
                foreach (var item in map.ToArray())
                {
                    if (!TestThreshold(target.color, item.color, threshold)) continue;
                    map.Remove(item);
                    list.Add(item);
                }
                resultMap.Add(list.OrderByDescending(c => c.size).ToList());
            }
        }

        internal static bool IsNetworkDrive(string path)
        {
            if (path.StartsWith(@"\\")) return true;
            if (path.Length < 2) return false;
            if (path[1] != ':') return false;

            System.IO.DriveInfo drive = new System.IO.DriveInfo(path[0].ToString());
            return drive.DriveType == System.IO.DriveType.Network;
        }

        internal static bool IsSameDrive(string path1, string path2)
        {
            return path1.Length > 1
                && path2.Length > 1
                && char.ToLower(path1[0]) == char.ToLower(path2[0])
                && path1[1] == ':'
                && path2[1] == ':';
        }
    }
}
