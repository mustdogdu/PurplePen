﻿/* Copyright (c) 2008, Peter Golde
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without 
 * modification, are permitted provided that the following conditions are 
 * met:
 * 
 * 1. Redistributions of source code must retain the above copyright
 * notice, this list of conditions and the following disclaimer.
 * 
 * 2. Redistributions in binary form must reproduce the above copyright
 * notice, this list of conditions and the following disclaimer in the
 * documentation and/or other materials provided with the distribution.
 * 
 * 3. Neither the name of Peter Golde, nor "Purple Pen", nor the names
 * of its contributors may be used to endorse or promote products
 * derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE
 * USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY
 * OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Drawing = System.Drawing;
#if WPF
using PointF = System.Drawing.PointF;
using RectangleF = System.Drawing.RectangleF;
using SizeF = System.Drawing.SizeF;
using Matrix = System.Drawing.Drawing2D.Matrix;
using WpfMatrix = System.Windows.Media.Matrix;
#endif
#if WPF
using System.Windows;
using System.Windows.Media;
#else
using System.Drawing;
using System.Drawing.Drawing2D;
#endif

namespace PurplePen.MapModel
{
    public interface IGraphicsBrush : IDisposable
    {
        Brush Brush { get; }
    }

    public interface IGraphicsPen : IDisposable
    { }
    public interface IGraphicsPath : IDisposable
    { }
    public interface IGraphicsFont : IDisposable
    { }

    public enum GraphicsPathPartKind { Start, Lines, Beziers, Close };

    public struct GraphicsPathPart {
        public readonly GraphicsPathPartKind Kind;
        public readonly PointF[] Points;

        public GraphicsPathPart(GraphicsPathPartKind kind, PointF[] points)
        {
            this.Kind = kind;
            this.Points = points;
        }
    }

    public interface IGraphicsTarget_X: IDisposable
    {
        // Prepend a transform to the graphics drawing target.
        void PushTransform(Matrix matrix);
        void PopTransform();

        // Set a clip on the graphics drawing target.
        void PushClip(IGraphicsPath geometry);
        void PopClip();

        // Create paths.
        IGraphicsPath CreatePath(IEnumerable<GraphicsPathPart> parts, Drawing.Drawing2D.FillMode windingMode);
        IGraphicsPath CreateMultiPath(IEnumerable<IEnumerable<GraphicsPathPart>> multiParts, Drawing.Drawing2D.FillMode windingMode);

        // Create brushes and pens
        IGraphicsBrush CreateSolidBrush(Color color);
        IBrushTarget CreatePatternBrush(SizeF size, int bitmapWidth, int bitmapHeight);
        IGraphicsPen CreatePen(IGraphicsBrush brush, float width, Drawing.Drawing2D.LineCap caps, Drawing.Drawing2D.LineJoin join, float miterLimit);

        // Create font
        IGraphicsFont CreateFont(string familyName, float emHeight, bool bold, bool italic);

        // Draw an line with a pen.
        void DrawLine(IGraphicsPen pen, PointF start, PointF finish);

        // Draw an ellipse with a pen.
        void DrawEllipse(IGraphicsPen pen, PointF center, float radiusX, float radiusY);

        // Fill an ellipse with a pen.
        void FillEllipse(IGraphicsBrush brush, PointF center, float radiusX, float radiusY);

        // Draw a rectangle with a pen.
        void DrawRectangle(IGraphicsPen pen, RectangleF rect);

        // Fill a rectangle with a brush.
        void FillRectangle(IGraphicsBrush brush, RectangleF rect);

        // Fill a polygon with a brush
        void FillPolygon(IGraphicsBrush brush, PointF[] pts, Drawing.Drawing2D.FillMode windingMode);

        // Draw text with upper-left corner of text at the given locations.
        void DrawText(string text, IGraphicsBrush brush, PointF upperLeft);

        // Draw text with upper-left corner of text at upper-left corner of rectangle, clipped.
        void DrawClippedText(string text, IGraphicsBrush brush, RectangleF rect);
    }

    public interface IBrushTarget: IGraphicsTarget_X
    {
        IGraphicsBrush FinishBrush();
    }


    // A GraphicsTarget encapsulates either a Graphics (for WinForms) or a DrawingContext (for WPF)
    public struct GraphicsTarget
    {
#if WPF
        public DrawingContext DrawingContext;
        private int pushLevel;      // How many pushes have we done?

        public IGraphicsBrush CreateSolidBrush(Color color)
        {
            return new WPF_Brush(color);
        }

        public GraphicsTarget(DrawingContext dc)
        {
            this.DrawingContext = dc;
            pushLevel = 0;
        }

        public object Save()
        {
            return pushLevel;
        }

        public void Restore(object state)
        {
            int desiredLevel = (int) state;

            if (pushLevel < desiredLevel)
                throw new InvalidOperationException("GraphicsTarget.Restore done with invalid state");

            while (pushLevel > desiredLevel) {
                DrawingContext.Pop();
                --pushLevel;
            }
        }

        // Prepend a transform to the graphics drawing target.
        public void Transform(Matrix matrix)
        {
            DrawingContext.PushTransform(new MatrixTransform(GraphicsUtil.GetWpfMatrix(matrix)));
            ++pushLevel;
        }

        // Set a clip on the graphics drawing target.
        public void SetClip(SymPathWithHoles path)
        {
            DrawingContext.PushClip(path.Geometry);
            ++pushLevel;
        }

        // Draw an line with a pen.
        public void DrawLine(Pen pen, PointF start, PointF finish)
        {
            DrawingContext.DrawLine(pen, new Point(start.X, start.Y), new Point(finish.X, finish.Y));
        }

        // Draw an ellipse with a pen.
        public void DrawEllipse(Pen pen, PointF center, float radiusX, float radiusY)
        {
            DrawingContext.DrawEllipse(null, pen, new Point(center.X, center.Y), radiusX, radiusY);
        }

        // Fill an ellipse with a pen.
        public void FillEllipse(Brush brush, PointF center, float radiusX, float radiusY)
        {
            DrawingContext.DrawEllipse(brush, null, new Point(center.X, center.Y), radiusX, radiusY);
        }

        // Draw a rectangle with a pen.
        public void DrawRectangle(Pen pen, RectangleF rect)
        {
            DrawingContext.DrawRectangle(null, pen, new Rect(rect.X, rect.Y, rect.Width, rect.Height));
        }

        // Fill a rectangle with a brush.
        public void FillRectangle(Brush brush, RectangleF rect)
        {
            DrawingContext.DrawRectangle(brush, null, new Rect(rect.X, rect.Y, rect.Width, rect.Height));
        }

        // Fill a polygon with a brush
        public void FillPolygon(Brush brush, PointF[] pts, bool winding)
        {
            Point[] points = new Point[pts.Length];
            for (int i = 0; i < pts.Length; ++i)
                points[i] = new Point(pts[i].X, pts[i].Y);

            PathSegment segment = new PolyLineSegment(points, true);
            PathFigure figure = new PathFigure(points[points.Length - 1], new PathSegment[] { segment }, true);
            PathGeometry geometry = new PathGeometry(new PathFigure[] { figure }, winding ? FillRule.Nonzero : FillRule.EvenOdd, System.Windows.Media.Transform.Identity);
            DrawingContext.DrawGeometry(brush, null, geometry);
        }

        // Fill a polygon with a brush
        public void FillPolygon(IGraphicsBrush brush, PointF[] pts, Drawing.Drawing2D.FillMode windingMode)
        {
            Point[] points = new Point[pts.Length];
            for (int i = 0; i < pts.Length; ++i)
                points[i] = new Point(pts[i].X, pts[i].Y);

            PathSegment segment = new PolyLineSegment(points, true);
            PathFigure figure = new PathFigure(points[points.Length - 1], new PathSegment[] { segment }, true);
            PathGeometry geometry = new PathGeometry(new PathFigure[] { figure }, windingMode == Drawing.Drawing2D.FillMode.Winding ? FillRule.Nonzero : FillRule.EvenOdd, System.Windows.Media.Transform.Identity);
            DrawingContext.DrawGeometry((brush as WPF_Brush).Brush, null, geometry);
        }


#else
        public Graphics Graphics;

        public IGraphicsBrush CreateSolidBrush(Color color)
        {
            return new GDIPlus_Brush(color);
        }

        public GraphicsTarget(Graphics g)
        {
            this.Graphics = g;
        }

        // Save state of transform, clip region.
        public object Save()
        {
            return Graphics.Save();
        }

        // Restore state
        public void Restore(object state)
        {
            Graphics.Restore((GraphicsState) state);
        }

        // Prepend a transform to the graphics drawing target.
        public void Transform(Matrix matrix)
        {
            Graphics.MultiplyTransform(matrix, MatrixOrder.Prepend);
        }

        // Set a clip on the graphics drawing target.
        public void SetClip(SymPathWithHoles path)
        {
            Graphics.IntersectClip(new Region(path.GetPath()));
        }

        // Draw an line with a pen.
        public void DrawLine(Pen pen, PointF start, PointF finish)
        {
            Graphics.DrawLine(pen, start, finish);
        }

        // Draw an ellipse with a pen.
        public void DrawEllipse(Pen pen, PointF center, float radiusX, float radiusY)
        {
            Graphics.DrawEllipse(pen, center.X - radiusX, center.Y - radiusY, 2 * radiusX, 2 * radiusY);
        }

        // Fill an ellipse with a pen.
        public void FillEllipse(Brush brush, PointF center, float radiusX, float radiusY)
        {
            Graphics.FillEllipse(brush, center.X - radiusX, center.Y - radiusY, 2 * radiusX, 2 * radiusY);
        }

        // Draw a rectangle with a pen.
        public void DrawRectangle(Pen pen, RectangleF rect)
        {
            Graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
        }

        // Fill a rectangle with a brush.
        public void FillRectangle(Brush brush, RectangleF rect)
        {
            Graphics.FillRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);
        }

        // Fill a polygon with a brush
        public void FillPolygon(Brush brush, PointF[] pts, bool windingMode)
        {
            Graphics.FillPolygon(brush, pts, windingMode ? FillMode.Winding : FillMode.Alternate);
        }

        // Fill a polygon with a brush
        public void FillPolygon(IGraphicsBrush brush, PointF[] pts, FillMode windingMode)
        {
            Graphics.FillPolygon((brush as GDIPlus_Brush).Brush, pts, windingMode);
        }

#endif
    }

#if WPF
    public class WPF_Brush : IGraphicsBrush
    {
        private Brush brush;

        public WPF_Brush(Color color)
        {
            brush = new SolidColorBrush(color);
            brush.Freeze();
        }

        public Brush Brush
        {
            get { return brush; }
        }

        public void Dispose()
        {
            brush = null;
        }
    }
#else
    public class GDIPlus_Brush : IGraphicsBrush
    {
        private Brush brush;

        public Brush Brush
        {
            get { return brush; }
        }

        public GDIPlus_Brush(Color color)
        {
            brush = new SolidBrush(color);
        }

        public void Dispose()
        {
            brush.Dispose();
        }
    }
#endif

    public static class GraphicsUtil
    {
        public static float MITER_LIMIT = 5.0F;

        // Transform points according to a matrix. Does NOT change the input points.
        public static PointF[] TransformPoints(PointF[] pts, Matrix matrix)
        {
            PointF[] xformedPts = (PointF[]) pts.Clone();
            matrix.TransformPoints(xformedPts);
            return xformedPts;
        }

        // Transform one point according to a matrix. 
        public static PointF TransformPoint(PointF pt, Matrix matrix)
        {
            PointF[] xformedPts = new PointF[1] { pt };
            matrix.TransformPoints(xformedPts);
            return xformedPts[0];
        }

#if WPF
        public static WpfMatrix GetWpfMatrix(Matrix source)
        {
            float[] elements = source.Elements;
            return new WpfMatrix(elements[0], elements[1], elements[2], elements[3], elements[4], elements[5]);
        }
#endif

#if false
        // Create a solid brush.
        public static Brush CreateSolidBrush(Color color)
        {
#if WPF
            Brush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
#else
            return new SolidBrush(color);
#endif
        }
#endif

        // Create a solid pen
        public static Pen CreateSolidPen(Color color, float thickness, LineStyle style)
        {
#if WPF
            Pen pen = new Pen(new SolidColorBrush(color), thickness);
            if (style == LineStyle.Rounded) {
                pen.StartLineCap = pen.EndLineCap = PenLineCap.Round;
                pen.LineJoin = PenLineJoin.Round;
            }
            else if (style == LineStyle.Beveled) {
                pen.StartLineCap = pen.EndLineCap = PenLineCap.Flat;
                pen.LineJoin = PenLineJoin.Bevel;
            }
            else if (style == LineStyle.Mitered) {
                pen.StartLineCap = pen.EndLineCap = PenLineCap.Flat;
                pen.LineJoin = PenLineJoin.Miter;
                pen.MiterLimit = MITER_LIMIT;
            }
            else if (style == LineStyle.FlatRounded) {
                pen.StartLineCap = pen.EndLineCap = PenLineCap.Flat;
                pen.LineJoin = PenLineJoin.Round;
            }

            pen.Freeze();
            return pen;
#else
            Pen pen = new Pen(color, thickness);
            if (style == LineStyle.Rounded) {
                pen.SetLineCap(LineCap.Round, LineCap.Round, DashCap.Flat);
                pen.LineJoin = LineJoin.Round;
            }
            else if (style == LineStyle.Beveled) {
                pen.SetLineCap(LineCap.Flat, LineCap.Flat, DashCap.Flat);
                pen.LineJoin = LineJoin.Bevel;
            }
            else if (style == LineStyle.Mitered) {
                pen.SetLineCap(LineCap.Flat, LineCap.Flat, DashCap.Flat);
                pen.LineJoin = LineJoin.Miter;
                pen.MiterLimit = MITER_LIMIT;
            }
            else if (style == LineStyle.FlatRounded) {
                pen.SetLineCap(LineCap.Flat, LineCap.Flat, DashCap.Flat);
                pen.LineJoin = LineJoin.Round;
            }

            return pen;
#endif
        }

        // Dispose of a pen
        public static void DisposePen(Pen pen)
        {
#if !WPF
            pen.Dispose();
#endif
        }

        // Dispose of a brush
        public static void DisposeBrush(Brush brush)
        {
#if !WPF
            brush.Dispose();
#endif
        }

        // Multiple two matrixes, giving a third
        public static Matrix Multiply(Matrix m1, Matrix m2)
        {
            Matrix result = m1.Clone();
            result.Multiply(m2, System.Drawing.Drawing2D.MatrixOrder.Append);
            return result;
        }

    }
}
