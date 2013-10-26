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
        internal const int ColorMapX = 4;
        internal const int ColorMapY = 4;
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

        private static double calcDistance(double x0, double y0, double x1, double y1)
        {
            return Math.Sqrt((x0 - x1) * (x0 - x1) + (y0 - y1) * (y0 - y1));
        }

        internal static Uri CreateFileUri(string filename)
        {
            //System.Diagnostics.Debug.WriteLine(filename);
            var uri = new Uri("file:///" + filename.Replace("%", "%25"));
            //System.Diagnostics.Debug.WriteLine(filename);
            return uri;
        }

        internal static PictureInfo CalcScore(string filename)
        {
            //var waiter = new AutoResetEvent(false);
            BitmapImage bm;
            try
            {
                bm = new BitmapImage(CreateFileUri(filename));
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (FileFormatException)
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
            double distanceBaseX = xunit;
            double distanceBaseY = yunit;
            for (int y = 0; y < Constants.ColorMapY; y++)
            {
                for (int x = 0; x < Constants.ColorMapX; x++)
                {
                    double centerX = (x + 0.5) * xunit;
                    double centerY = (y + 0.5) * yunit;
                    double rsum = 0.0, gsum = 0.0, bsum = 0.0;
                    for (int y0 = 0; y0 < yunit; y0++)
                    {
                        int i = ((y * yunit + y0) * pi.width + x * xunit) * 4;
                        for (int x0 = 0; x0 < xunit; x0++)
                        {
                            double distance = calcDistance(centerX / distanceBaseX,
                                centerY / distanceBaseY,
                                (x * xunit + x0) / distanceBaseX,
                                (y * yunit + y0) / distanceBaseY);
                            double stronglevel = distance;
                            //System.Diagnostics.Debug.WriteLine(stronglevel.ToString());
                            //System.Diagnostics.Debug.Write(distance.ToString());
                            //System.Diagnostics.Debug.Write(" ");
                            //System.Diagnostics.Debug.WriteLine(string.Format("{0} {1} {2} {3}={4}", centerX / distanceBaseX, centerY / distanceBaseY, (x * xunit + x0) / distanceBaseX, (y * yunit + y0) / distanceBaseY, distance));
                            if (stronglevel < 0.0) continue;
                            if (stronglevel > 1.0) continue;
                            bsum = bsum + (255 - (255 - buf[i]) * stronglevel);
                            gsum = gsum + (255 - (255 - buf[i + 1]) * stronglevel);
                            rsum = rsum + (255 - (255 - buf[i + 2]) * stronglevel);
                            i += 4;
                        }
                    }
                    b[x, y] = (byte)(bsum / (xunit * yunit));
                    g[x, y] = (byte)(gsum / (xunit * yunit));
                    r[x, y] = (byte)(rsum / (xunit * yunit));
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

        private static bool calcDiffColor(Color a, Color b, int threshold)
        {
            System.Diagnostics.Debug.WriteLine("a=" + a.ToString() + " b" + b.ToString() + " diffb=" + Math.Abs(a.B - b.B) + " diffg=" + Math.Abs(a.G - b.G) + " diffr=" + Math.Abs(a.R - b.R) + " thresold=" + threshold);
            return Math.Abs(a.B - b.B) > threshold
                || Math.Abs(a.G - b.G) > threshold
                || Math.Abs(a.R - b.R) > threshold;
        }

        private static bool TestThreshold(Color[,] a, Color[,] b, int threshold)
        {
            for (int y = 0; y < Constants.ColorMapY; y++)
            {
                for (int x = 0; x < Constants.ColorMapX; x++)
                {
                    if (calcDiffColor(a[x, y], b[x, y], threshold / 3))
                    {
                        //System.Diagnostics.Debug.WriteLine("x=" + x + " y=" + y + " a=" + a[x, y].ToString() + " b" + b[x, y].ToString() + " diff=" + (Math.Abs(a[x, y].B - b[x, y].B) + Math.Abs(a[x, y].G - b[x, y].G) + Math.Abs(a[x, y].R - b[x, y].R)));
                        return false;
                    }
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
                    var t = threshold;
                    //if (target.width < 200 || target.height < 200 || item.width < 200 || item.height < 200)
                    //{
                    //t *= 3; // boost threshold if picture is very small
                    //}
                    if (!TestThreshold(target.color, item.color, t)) continue;
                    map.Remove(item);
                    list.Add(item);
                }
                resultMap.Add(list.OrderByDescending(c => c.width * c.height).ThenByDescending(c => c.size).ToList());
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

        internal static bool IsMonoTone(Color col, int threshold)
        {
            return Math.Abs(col.B - col.G) < threshold &&
                Math.Abs(col.G - col.R) < threshold &&
                Math.Abs(col.R - col.B) < threshold;
        }
        internal static bool IsMonoToneAll(PictureInfo pictureInfo, int monoThreashold)
        {
            for (int y = 0; y < pictureInfo.color.GetLength(1); y++)
            {
                for (int x = 0; x < pictureInfo.color.GetLength(0); x++)
                {
                    if (!IsMonoTone(pictureInfo.color[x,y],monoThreashold)) return false;
                }
            }
            return true;
        }

        internal static WriteableBitmap GetMosaicPicture(PictureInfo info)
        {
            const int scalefactor = 50;
            var bm = new WriteableBitmap(info.color.GetLength(0) * scalefactor, info.color.GetLength(1) * scalefactor, 96, 96, PixelFormats.Bgr32, null);
            for (int y = 0; y < info.color.GetLength(1); y++)
            {
                for (int x = 0; x < info.color.GetLength(0); x++)
                {
                    //byte[] col = { info.color[x, y].B, info.color[x, y].G, info.color[x, y].R, info.color[x, y].A };
                    byte[] col = new byte[scalefactor*scalefactor*4];
                    for (int i = 0; i < scalefactor*scalefactor*4; i+=4)
                    {
                        col[i] = info.color[x, y].B;
                        col[i + 1] = info.color[x, y].G;
                        col[i + 2] = info.color[x, y].R;
                        col[i + 3] = info.color[x, y].A;
                    }

                    bm.WritePixels(new System.Windows.Int32Rect(x * scalefactor, y * scalefactor, scalefactor, scalefactor), col, 4 * scalefactor, 0);
                }
            }
            return bm;
        }

    }
}
