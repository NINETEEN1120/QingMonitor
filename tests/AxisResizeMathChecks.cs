using System;
using System.Drawing;
using QingMonitor;

internal static class AxisResizeMathChecks {
    static int passed;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
    static Rectangle Axis(Rectangle start,Rectangle proposed,int edge,Size min,Size max){
        return OverlayResize.AxisBounds(start,proposed,edge,min,max);
    }
    static int Main(){try{
        var start=new Rectangle(100,200,600,90);var min=new Size(300,60);var max=new Size(1000,400);
        var proposed=new Rectangle(70,190,840,126);
        Check(Axis(start,proposed,1,min,max)==new Rectangle(-140,200,840,90),"left changes width, preserves height and right anchor");
        Check(Axis(start,proposed,2,min,max)==new Rectangle(100,200,840,90),"right changes width, preserves height and left anchor");
        Check(Axis(start,proposed,3,min,max)==new Rectangle(100,164,600,126),"top changes height, preserves width and bottom anchor");
        Check(Axis(start,proposed,6,min,max)==new Rectangle(100,200,600,126),"bottom changes height, preserves width and top anchor");
        proposed=new Rectangle(0,0,1,1);
        Check(Axis(start,proposed,1,min,max)==new Rectangle(400,200,300,90),"left minimum keeps right anchor");
        Check(Axis(start,proposed,2,min,max)==new Rectangle(100,200,300,90),"right minimum keeps left anchor");
        Check(Axis(start,proposed,3,min,max)==new Rectangle(100,230,600,60),"top minimum keeps bottom anchor");
        Check(Axis(start,proposed,6,min,max)==new Rectangle(100,200,600,60),"bottom minimum keeps top anchor");
        proposed=new Rectangle(-100,-100,9000,9000);
        Check(Axis(start,proposed,1,min,max)==new Rectangle(-300,200,1000,90),"left maximum keeps right anchor");
        Check(Axis(start,proposed,2,min,max)==new Rectangle(100,200,1000,90),"right maximum keeps left anchor");
        Check(Axis(start,proposed,3,min,max)==new Rectangle(100,-110,600,400),"top maximum keeps bottom anchor");
        Check(Axis(start,proposed,6,min,max)==new Rectangle(100,200,600,400),"bottom maximum keeps top anchor");
        for(int edge=4;edge<=8;edge++)if(edge!=6){
            Check(OverlayResize.ScaleFor(start,new Rectangle(0,0,840,126),edge,100)==140,"corner scale 140 percent "+edge);
            Check(OverlayResize.ScaleFor(start,new Rectangle(0,0,1,1),edge,100)==70,"corner lower scale clamp "+edge);
            Check(OverlayResize.ScaleFor(start,new Rectangle(0,0,9000,9000),edge,100)==200,"corner upper scale clamp "+edge);
        }
        Check(OverlayResize.Anchor(start,new Size(840,126),4)==new Rectangle(-140,164,840,126),"top left proportional anchor");
        Check(OverlayResize.Anchor(start,new Size(840,126),5)==new Rectangle(100,164,840,126),"top right proportional anchor");
        Check(OverlayResize.Anchor(start,new Size(840,126),7)==new Rectangle(-140,200,840,126),"bottom left proportional anchor");
        Check(OverlayResize.Anchor(start,new Size(840,126),8)==new Rectangle(100,200,840,126),"bottom right proportional anchor");
        Console.WriteLine("Passed="+passed);return 0;
    }catch(Exception e){Console.WriteLine(e);return 1;}}
}
