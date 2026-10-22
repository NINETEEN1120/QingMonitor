using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;

internal static class RenderIconPreview
{
    private static void ExportIcon(string path)
    {
        int[] sizes={16,24,32,48,64,128,256};var frames=new List<byte[]>();
        foreach(int size in sizes)using(Bitmap bitmap=DrawIcon(size))
        {
            if(size==256){using(var png=new MemoryStream()){bitmap.Save(png,ImageFormat.Png);frames.Add(png.ToArray());}continue;}
            using(var memory=new MemoryStream())using(var frame=new BinaryWriter(memory))
            {
                int maskStride=((size+31)/32)*4;byte[] mask=new byte[maskStride*size];
                frame.Write(40);frame.Write(size);frame.Write(size*2);frame.Write((ushort)1);frame.Write((ushort)32);frame.Write(0);frame.Write(size*size*4);frame.Write(0);frame.Write(0);frame.Write(0);frame.Write(0);
                for(int y=size-1;y>=0;y--)for(int x=0;x<size;x++)
                {
                    Color pixel=bitmap.GetPixel(x,y);frame.Write(pixel.B);frame.Write(pixel.G);frame.Write(pixel.R);frame.Write(pixel.A);
                    if(pixel.A==0)mask[(size-1-y)*maskStride+x/8]|=(byte)(0x80>>(x%8));
                }
                frame.Write(mask);frames.Add(memory.ToArray());
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        using(var writer=new BinaryWriter(File.Create(path)))
        {
            writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++)
            {
                writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);
                writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(frames[i].Length);writer.Write(offset);offset+=frames[i].Length;
            }
            foreach(byte[] frame in frames)writer.Write(frame);
        }
    }
    private static Bitmap DrawIcon(int size)
    {
        Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.ScaleTransform(size / 256f, size / 256f);
            using (GraphicsPath tile = new GraphicsPath())
            {
                tile.AddArc(16, 16, 96, 96, 180, 90);
                tile.AddArc(144, 16, 96, 96, 270, 90);
                tile.AddArc(144, 144, 96, 96, 0, 90);
                tile.AddArc(16, 144, 96, 96, 90, 90);
                tile.CloseFigure();
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(25, 41, 56)))
                using (Pen border = new Pen(Color.FromArgb(59, 81, 105), 3))
                {
                    g.FillPath(fill, tile);
                    g.DrawPath(border, tile);
                }
            }
            using (SolidBrush outline = new SolidBrush(Color.FromArgb(105, 201, 243)))
            using (SolidBrush interior = new SolidBrush(Color.FromArgb(25, 41, 56)))
            using (SolidBrush mercury = new SolidBrush(Color.FromArgb(255, 181, 71)))
            using (Pen liquid = new Pen(Color.FromArgb(255, 181, 71), 12))
            using (Pen ticks = new Pen(Color.FromArgb(105, 201, 243), 6))
            {
                FillCapsule(g, outline, 94, 52, 44, 142);
                g.FillEllipse(outline, 80, 138, 72, 72);
                FillCapsule(g, interior, 105, 63, 22, 123);
                g.FillEllipse(interior, 91, 149, 50, 50);
                liquid.StartCap = LineCap.Round;
                liquid.EndCap = LineCap.Round;
                g.DrawLine(liquid, 116, 110, 116, 174);
                g.FillEllipse(mercury, 97, 155, 38, 38);
                ticks.StartCap = LineCap.Round;
                ticks.EndCap = LineCap.Round;
                if (size >= 24)
                {
                    g.DrawLine(ticks, 155, 78, 183, 78);
                    g.DrawLine(ticks, 155, 103, 172, 103);
                    g.DrawLine(ticks, 155, 128, 183, 128);
                }
            }
        }
        return bitmap;
    }

    private static void FillCapsule(Graphics g, Brush brush, int x, int y, int width, int height)
    {
        using (GraphicsPath shape = new GraphicsPath())
        {
            shape.AddArc(x, y, width, width, 180, 180);
            shape.AddArc(x, y + height - width, width, width, 0, 180);
            shape.CloseFigure();
            g.FillPath(brush, shape);
        }
    }

    private static void DrawActual(Graphics g, int size, int x, int y)
    {
        using (Bitmap icon = DrawIcon(size))
            g.DrawImageUnscaled(icon, x, y);
    }

    public static void Main(string[] args)
    {
        string output = args[0];
        Directory.CreateDirectory(output);
        if(args.Length>1)ExportIcon(args[1]);
        using (Bitmap icon = DrawIcon(256))
            icon.Save(Path.Combine(output, "QingMonitor-thermometer-256.png"), ImageFormat.Png);
        using (Bitmap sheet = new Bitmap(760, 380))
        using (Graphics g = Graphics.FromImage(sheet))
        using (Font title = new Font("Microsoft YaHei", 19, FontStyle.Bold, GraphicsUnit.Pixel))
        using (Font label = new Font("Microsoft YaHei", 14, FontStyle.Regular, GraphicsUnit.Pixel))
        using (Font caption = new Font("Microsoft YaHei", 12, FontStyle.Regular, GraphicsUnit.Pixel))
        using (SolidBrush ink = new SolidBrush(Color.FromArgb(36, 48, 63)))
        using (SolidBrush muted = new SolidBrush(Color.FromArgb(92, 106, 122)))
        using (SolidBrush trayDark = new SolidBrush(Color.FromArgb(26, 30, 36)))
        using (SolidBrush trayLight = new SolidBrush(Color.FromArgb(227, 232, 239)))
        {
            g.Clear(Color.FromArgb(247, 249, 252));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.DrawString("性能悬浮窗 · 温度计图标预览", title, ink, 26, 22);
            DrawActual(g, 256, 26, 70);
            g.DrawString("主图标", label, muted, 129, 331);
            g.DrawString("桌面 64 px", label, ink, 326, 80);
            DrawActual(g, 64, 337, 117);
            g.DrawString("文件列表 32 px", label, ink, 483, 80);
            DrawActual(g, 32, 512, 133);
            g.DrawString("托盘实际尺寸 · 深色 / 浅色背景", label, ink, 326, 217);
            g.FillRectangle(trayDark, 328, 251, 156, 60);
            g.FillRectangle(trayLight, 502, 251, 156, 60);
            DrawActual(g, 16, 357, 273);
            DrawActual(g, 24, 422, 269);
            DrawActual(g, 16, 531, 273);
            DrawActual(g, 24, 596, 269);
            g.DrawString("16 px          24 px", caption, muted, 351, 320);
            g.DrawString("16 px          24 px", caption, muted, 525, 320);
            sheet.Save(Path.Combine(output, "图标预览-温度计.png"), ImageFormat.Png);
        }
    }
}
