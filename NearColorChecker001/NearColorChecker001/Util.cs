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
            var size = bm.PixelWidth * bm.PixelHeight * 4;
            var buf = new byte[size];
            //bm.StreamSource.Read(buf,0,size);

            bmw.CopyPixels(buf, bm.PixelWidth * 4, 0);

            var pi = new PictureInfo();
            pi.filename = filename;
            pi.width = bm.PixelWidth;
            pi.height = bm.PixelHeight;
            pi.size = size;
            long[,] b = new long[Constants.ColorMapX, Constants.ColorMapY];
            long[,] g = new long[Constants.ColorMapX, Constants.ColorMapY];
            long[,] r = new long[Constants.ColorMapX, Constants.ColorMapY];
            int xunit = pi.width / Constants.ColorMapX;
            int yunit = pi.height / Constants.ColorMapY;
            for (int y = 0; y < Constants.ColorMapY; y++)
            {
                for (int x = 0; x < Constants.ColorMapX; x++)
                {
                    for (int y0 = 0; y0 < yunit; y0++)
                    {
                        int i = ((y * yunit + y0) * pi.width + x * xunit) * 4;
                        for (int x0 = 0; x0 < xunit; x0++)
                        {
                            b[x, y] += buf[i];
                            g[x, y] += buf[i + 1];
                            r[x, y] += buf[i + 2];
                            i += 4;
                        }
                    }
                }
            }

            long div = (pi.width / Constants.ColorMapX) * (pi.width / Constants.ColorMapY);
            for (int y = 0; y < Constants.ColorMapY; y++)
            {
                for (int x = 0; x < Constants.ColorMapX; x++)
                {
                    pi.color[x, y] = Color.FromRgb((byte)(r[x, y] / div), (byte)(g[x, y] / div), (byte)(b[x, y] / div));
                }
            }
            return pi;
        }

        private static int calcDiffColor(Color a, Color b)
        {
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
    }
}
