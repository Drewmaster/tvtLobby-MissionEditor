using System;
namespace TvtLobby.MissionEditor { public sealed class MapStyle {
 public string ImageSet {get;set;}
 public string Quad {get;set;}
 public string Tint {get;set;}
 public int SizeMode {get;set;}
 public int ShapeSizeMode {get;set;}
 public double PixelSize {get;set;}
 public double WorldSize {get;set;}
 public double MinPixels {get;set;}
 public double MaxPixels {get;set;}
 public int StrokePattern {get;set;}
 public string StrokeTexture {get;set;}
 public int FillPattern {get;set;}
 public string FillTexture {get;set;}
 public int FillOutside {get;set;}
 public double TextureScale {get;set;}
 public int LabelVisible {get;set;}
 public string LabelText {get;set;}
 public string LabelColor {get;set;}
 public double LabelSize {get;set;}
 public int LabelPosition {get;set;}
 public double LabelX {get;set;}
 public double LabelY {get;set;}
 public int ZOrder {get;set;}
 public MapStyle(){ImageSet="";Quad="";Tint="1 1 1 1";SizeMode=0;ShapeSizeMode=1;PixelSize=56;WorldSize=70;MinPixels=16;MaxPixels=192;StrokePattern=0;StrokeTexture="";FillPattern=1;FillTexture="";FillOutside=0;TextureScale=0.05;LabelVisible=1;LabelText="";LabelColor="1 1 1 1";LabelSize=16;LabelPosition=1;LabelX=0;LabelY=2;ZOrder=0;}
 // Same units and calculation as tvtLobby_MapStyle.ShapeScale in the game.
 // A missing style is a map-sized shape too; old maps need no manual conversion.
 public static double ShapeScale(MapStyle style,double pixelsPerMetre)
 {return style!=null&&style.ShapeSizeMode==0?1:Math.Max(.000001,pixelsPerMetre);}
}}
