using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace BootLauncher
{
    static class ShellIcon
    {
        const uint SHGFI_SYSICONINDEX = 0x4000;
        const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        const int SHIL_JUMBO = 0x4;
        const int ILD_TRANSPARENT = 0x1;
        static readonly Guid IID_IImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("shell32.dll")]
        static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr hIcon);

        public static Image GetLargeIcon(string path)
        {
            try
            {
                var info = new SHFILEINFO();
                uint cb = (uint)Marshal.SizeOf(typeof(SHFILEINFO));
                if (SHGetFileInfo(path, 0, ref info, cb, SHGFI_SYSICONINDEX) == IntPtr.Zero)
                    return null;
                if (info.iIcon < 0) return null;

                IImageList imageList;
                var iid = IID_IImageList;
                if (SHGetImageList(SHIL_JUMBO, ref iid, out imageList) != 0)
                    return null;

                IntPtr hIcon = IntPtr.Zero;
                imageList.GetIcon(info.iIcon, ILD_TRANSPARENT, ref hIcon);
                if (hIcon == IntPtr.Zero) return null;
                try
                {
                    using (var icon = Icon.FromHandle(hIcon))
                    {
                        return new Bitmap(icon.ToBitmap());
                    }
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
            catch
            {
                return null;
            }
        }

        public static Image Resize(Image source, int size)
        {
            using (var cropped = CropToContent(source))
            {
                var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    float scale = Math.Min((float)size / cropped.Width, (float)size / cropped.Height);
                    int w = Math.Max(1, (int)Math.Round(cropped.Width * scale));
                    int h = Math.Max(1, (int)Math.Round(cropped.Height * scale));
                    g.DrawImage(cropped, (size - w) / 2, (size - h) / 2, w, h);
                }
                return bmp;
            }
        }

        // 有些 exe 没有大图标，系统会把小图标原样放在 256px 透明画布的左上角，
        // 直接缩放就会只剩角落一小块；先裁出非透明内容再缩放。
        static Image CropToContent(Image source)
        {
            using (var bmp = new Bitmap(source))
            {
                var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    var buf = new byte[Math.Abs(data.Stride) * bmp.Height];
                    Marshal.Copy(data.Scan0, buf, 0, buf.Length);
                    int minX = bmp.Width, minY = bmp.Height, maxX = -1, maxY = -1;
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        int row = y * data.Stride;
                        for (int x = 0; x < bmp.Width; x++)
                        {
                            if (buf[row + x * 4 + 3] <= 12) continue;
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                    if (maxX < 0) return new Bitmap(source);
                    var rect = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
                    return bmp.Clone(rect, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
            }
        }
    }

    [ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IImageList
    {
        [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
        [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, ref int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
        [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
        [PreserveSig] int Draw(IntPtr pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, int flags, ref IntPtr picon);
    }
}
