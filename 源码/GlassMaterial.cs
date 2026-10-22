using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace QingMonitor {
    internal static class GlassMaterial {
        [StructLayout(LayoutKind.Sequential)]private struct AccentPolicy {public int State,Flags,GradientColor,Animation;}
        [StructLayout(LayoutKind.Sequential)]private struct CompositionData {public int Attribute;public IntPtr Data;public IntPtr Size;}
        [DllImport("user32.dll")]private static extern int SetWindowCompositionAttribute(IntPtr hwnd,ref CompositionData data);
        [DllImport("dwmapi.dll")]private static extern int DwmIsCompositionEnabled(out bool enabled);
        [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
        internal static bool RoundNative(IntPtr window,int radius){
            try{int preference=radius==0?1:radius<=8?3:2;int border=unchecked((int)0xfffffffe);DwmSetWindowAttribute(window,34,ref border,4);return DwmSetWindowAttribute(window,33,ref preference,4)==0;}
            catch(EntryPointNotFoundException){return false;}catch(DllNotFoundException){return false;}
        }
        internal static bool Apply(IntPtr window,bool enabled,int frost,int opacity){
            IntPtr data=IntPtr.Zero;
            try{
                bool composition;if(DwmIsCompositionEnabled(out composition)!=0||!composition)return false;
                int alpha=Math.Min(220,48+frost*100/100+opacity*65/100);
                var policy=new AccentPolicy{State=enabled?4:0,Flags=0,GradientColor=unchecked((alpha<<24)|(232<<16)|(225<<8)|220)};
                data=Marshal.AllocHGlobal(Marshal.SizeOf(policy));Marshal.StructureToPtr(policy,data,false);
                var attribute=new CompositionData{Attribute=19,Data=data,Size=new IntPtr(Marshal.SizeOf(policy))};
                return SetWindowCompositionAttribute(window,ref attribute)!=0&&enabled;
            }catch(EntryPointNotFoundException){return false;}catch(DllNotFoundException){return false;}
            finally{if(data!=IntPtr.Zero)Marshal.FreeHGlobal(data);}
        }
        internal static GraphicsPath Outline(RectangleF bounds,float radius){
            radius=Math.Max(0,Math.Min(radius,Math.Min(bounds.Width,bounds.Height)/2f));var path=new GraphicsPath();
            if(radius<0.5f){path.AddRectangle(bounds);return path;}
            float d=radius*2;path.AddArc(bounds.Left,bounds.Top,d,d,180,90);path.AddArc(bounds.Right-d,bounds.Top,d,d,270,90);
            path.AddArc(bounds.Right-d,bounds.Bottom-d,d,d,0,90);path.AddArc(bounds.Left,bounds.Bottom-d,d,d,90,90);path.CloseFigure();return path;
        }
    }
}
