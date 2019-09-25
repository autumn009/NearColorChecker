using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace NearColorChecker001
{
    // came from https://memoteu.hatenablog.com/entry/2018/08/26/150739
    static class MyImageUtil
    {
        // ビットマップイメージ作成
        public static BitmapImage CreateImage(string path)
        {
            BitmapImage result = null;

            try
            {
                byte[] buff = LoadSubAppsegmentJpegFile(path);
                if (buff == null)
                {
                    buff = File.ReadAllBytes(path);
                }

                using (var Stream = new MemoryStream(buff))
                {
                    result = new BitmapImage();
                    result.BeginInit();
                    result.StreamSource = Stream;
                    result.CreateOptions = BitmapCreateOptions.None;
                    result.CacheOption = BitmapCacheOption.OnLoad;
                    result.EndInit();

                    if (result.CanFreeze)
                    {
                        result.Freeze();
                    }
                }

            }
            catch
            {
                throw;
            }

            return result;
        }
        private static byte[] LoadSubAppsegmentJpegFile(string path)
        {

            if (string.IsNullOrEmpty(path)) { throw new ArgumentNullException("path is Null or Empty"); }
            if (!File.Exists(path)) { throw new FileNotFoundException(path + " Not Found"); }

            long resultSize = 0;
            byte[] result = null;

            // Key   : Applicationセグメントのポジション
            // Value : Applicationセグメントのサイズ
            Dictionary<long, long> segmentList = null;

            try
            {
                using (Stream stream = File.OpenRead(path))
                {
                    // Appセグメントの位置とサイズを取得
                    segmentList = MakeAppsegmentDictionary(stream);
                    if (segmentList != null)
                    {

                        // Appセグメントを除くサイズを計測
                        resultSize = stream.Length;
                        foreach (var segment in segmentList)
                        {
                            resultSize -= segment.Value;
                        }

                        result = new byte[resultSize];

                        stream.Seek(0, SeekOrigin.Begin);

                        long current = 0;
                        int dat = 0;

                        while (true)
                        {
                            // Appセグメント位置に来たらサイズ分カーソルを進める
                            if (segmentList.ContainsKey(stream.Position))
                            {
                                stream.Seek(segmentList[stream.Position], SeekOrigin.Current);
                            }
                            else
                            {
                                // １バイトづつ読み込む
                                dat = stream.ReadByte();

                                // 終端に到達したら処理を終了
                                if (dat == -1)
                                {
                                    break;
                                }

                                result[current] = (byte)dat;
                                current++;

                            }
                        }

                    }
                }
            }
            catch { throw; }


            return result;
        }

        // ストリームを走査して全てのApplicationセグメントの位置とサイズを返す
        private static Dictionary<long, long> MakeAppsegmentDictionary(Stream stream)
        {

            long current = stream.Position;
            stream.Seek(0, SeekOrigin.Begin);

            const int MarkerExt = 0xFF;
            const int MarkerNon = 0x00;
            const int MarkerRestart0 = 0xD0;
            const int MarkerRestart7 = 0xD7;

            const int SegmentStartOfImage = 0xFFD8;
            const int SegmentEndOfImage = 0xFFD9;
            const int SegmentStartOfScan = 0xFFDA;
            const int SegmentApp0 = 0xFFE0;
            const int SegmentApp15 = 0xFFEF;

            int mark, size;
            bool isImageData = false;

            mark = Read2ByteData(stream);

            Dictionary<long, long> result = null;

            // ストリームの先頭2バイトがJpeg識別子である
            if (mark == SegmentStartOfImage)
            {

                result = new Dictionary<long, long>();

                while (true)
                {
                    if (isImageData)
                    {
                        int h = 0;
                        int l = 0;

                        while (true)
                        {
                            h = stream.ReadByte();

                            if (h == -1)
                            {
                                throw new EndOfStreamException("ファイルが壊れている可能性があります。");
                            }
                            else if (h == MarkerExt)
                            {
                                l = stream.ReadByte();

                                if (l == -1)
                                {
                                    throw new EndOfStreamException("ファイルが壊れている可能性があります。");
                                }
                                else if (l != MarkerNon && !(l >= MarkerRestart0 && l <= MarkerRestart7))
                                {
                                    break;
                                }
                                else if (l == MarkerExt)
                                {
                                    stream.Seek(-1, SeekOrigin.Current);
                                }
                            }
                        }

                        stream.Seek(-2, SeekOrigin.Current);

                        isImageData = false;
                    }
                    else
                    {
                        mark = Read2ByteData(stream);

                        if (mark == SegmentEndOfImage)
                        {
                            break;
                        }

                        size = Read2ByteData(stream);

                        if (mark >= SegmentApp0 && mark <= SegmentApp15)
                        {
                            result.Add(stream.Position - 4, size + 2);
                        }
                        else if (mark == SegmentStartOfScan)
                        {
                            isImageData = true;

                        }
                        stream.Seek(size - 2, SeekOrigin.Current);

                    }
                }
            }

            stream.Seek(current, SeekOrigin.Begin);

            return result;

        }

        // ストリームから2バイト読み込む
        private static int Read2ByteData(Stream stream)
        {
            int h = stream.ReadByte();
            int l = stream.ReadByte();

            if (h == -1 || l == -1)
            {
                throw new EndOfStreamException("ファイルが壊れている可能性があります。");
            }

            return (h << 8 | l);
        }
    }
}
