using System;
using System.Drawing;

namespace QingMonitor {
    internal static class OverlayResize {
        internal static int HitTest(Point point,Size size,int border,bool enabled) {
            if(!enabled||point.X<0||point.Y<0||point.X>=size.Width||point.Y>=size.Height)return 0;
            bool left=point.X<border,right=point.X>=size.Width-border,top=point.Y<border,bottom=point.Y>=size.Height-border;
            if(top)return left?13:right?14:12;
            if(bottom)return left?16:right?17:15;
            return left?10:right?11:0;
        }
        internal static int ScaleFor(Rectangle start,Rectangle proposed,int edge,int percent) {
            double x=proposed.Width/(double)Math.Max(1,start.Width),y=proposed.Height/(double)Math.Max(1,start.Height);
            double factor=edge==1||edge==2?x:edge==3||edge==6?y:Math.Abs(x-1)>Math.Abs(y-1)?x:y;
            return Math.Max(70,Math.Min(200,(int)Math.Round(percent*factor,MidpointRounding.AwayFromZero)));
        }
        internal static Rectangle AxisBounds(Rectangle start,Rectangle proposed,int edge,Size min,Size max) {
            if(edge==1||edge==2){
                int width=Math.Max(min.Width,Math.Min(max.Width,proposed.Width));
                return new Rectangle(edge==1?start.Right-width:start.Left,start.Top,width,start.Height);
            }
            if(edge==3||edge==6){
                int height=Math.Max(min.Height,Math.Min(max.Height,proposed.Height));
                return new Rectangle(start.Left,edge==3?start.Bottom-height:start.Top,start.Width,height);
            }
            return start;
        }
        internal static Rectangle Anchor(Rectangle start,Size size,int edge) {
            bool left=edge==1||edge==4||edge==7,top=edge==3||edge==4||edge==5;
            return new Rectangle(left?start.Right-size.Width:start.Left,top?start.Bottom-size.Height:start.Top,size.Width,size.Height);
        }
    }
}
