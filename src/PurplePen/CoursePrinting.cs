/* Copyright (c) 2006-2007, Peter Golde
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
using System.ComponentModel;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Diagnostics;

namespace PurplePen
{
    // Class to print out courses.
    class CoursePrinting: BasicPrinting
    {
        // Layout of a single page that is being print. Might be all of a course or just part.
        class CoursePage
        {
            public Id<Course> courseId;             // course to print
            public RectangleF mapRectangle;      // rectangle to print in map coordinates
            public RectangleF printRectangle;     // rectangle to print to on page, in hundredth of inch.
            public bool landscape;                       // true if page should be printed in landscape orientation
        }

        // Encapsulate the layout of one dimension of a page layout.
#if TEST
        internal
#endif
        struct DimensionLayout
        {
            public float startMap;                   // start and length in the map in map coords.
            public float lengthMap;
            public float startPage;                 // start and length on the printed page, in hundreths of a inch
            public float lengthPage;

            public DimensionLayout(float startMap, float lengthMap, float startPage, float lengthPage)
            {
                this.startMap = startMap;
                this.lengthMap = lengthMap;
                this.startPage = startPage;
                this.lengthPage = lengthPage;
            }
        }

        // List of all the pages, constructed during layout.
        private List<CoursePage> pages = new List<CoursePage>();

        private CoursePrintSettings coursePrintSettings;
        private EventDB eventDB;
        private SymbolDB symbolDB;
        private Controller controller;
        private MapDisplay mapDisplay;

        // mapDisplay is a MapDisplay that contains the correct map. All other features of the map display need to be customized.
        public CoursePrinting(EventDB eventDB, SymbolDB symbolDB, Controller controller, MapDisplay mapDisplay, CoursePrintSettings coursePrintSettings)
            : base(QueryEvent.GetEventTitle(eventDB, " "), coursePrintSettings.PageSettings)
        {
            this.eventDB = eventDB;
            this.symbolDB = symbolDB;
            this.controller = controller;
            this.mapDisplay = mapDisplay;
            this.coursePrintSettings = coursePrintSettings;

            // Set default features for printing.
            mapDisplay.MapIntensity = 1.0F;
            mapDisplay.AntiAlias = false;
        }

        // Layout all the pages, return the total number of pages.
        protected override int LayoutPages(PageSettings pageSettings, SizeF printArea)
        {
            pages.Clear();

            // Go through each course and lay it out, then add to the page list.
            foreach (Id<Course> courseId in coursePrintSettings.CourseIds) {
                // Get the layout for the course.
                List<CoursePage> coursePages = LayoutOptimizedCourse(pageSettings, courseId);

                // Add as many copies as desired.
                for (int i = 0; i < coursePrintSettings.Count; ++i)
                    pages.AddRange(coursePages);
            }

            return pages.Count;            // total number of pages.
        }

        // Layout a single course onto one or more pages.
        // Optimize onto portrait or landscape.
        List<CoursePage> LayoutOptimizedCourse(PageSettings pageSettings, Id<Course> courseId)
        {
            List<CoursePage> portraitLayout, landscapeLayout;
            bool saveLandscape;

            // Layout in both portrait and landscape, and use the one which uses the least pages.
            // Note the strange fact that some printer drivers return different printable areas for landscale and 
            // portrait.
            saveLandscape = pageSettings.Landscape;
            pageSettings.Landscape = false;
            portraitLayout = LayoutCourse(pageSettings, courseId);
            pageSettings.Landscape = true;
            landscapeLayout = LayoutCourse(pageSettings, courseId);
            pageSettings.Landscape = saveLandscape;

            bool useLandscape;

            // Figure out which layout is best. Best layout is the one with the least number of pages. If they have the same
            // number of pages, then the most similar layout.
            if (portraitLayout.Count < landscapeLayout.Count) 
                useLandscape = false;
            else if (portraitLayout.Count > landscapeLayout.Count) {
                useLandscape = true;
            }
            else {
                useLandscape = false;
                if (landscapeLayout.Count > 0 && landscapeLayout[0].printRectangle.Width > landscapeLayout[0].printRectangle.Height) 
                    useLandscape = true;
            }

            // Return the layout that was best.
            if (useLandscape) {
                // Landscape is better.
                return landscapeLayout;
            }
            else {
                // Portrait is better.
                return portraitLayout;
            }
        }

        // Layout a course onto one or more pages.
        List<CoursePage> LayoutCourse(PageSettings pageSettings, Id<Course> courseId)
        {
            List<CoursePage> pageList = new List<CoursePage>();

            // Get the area of the map we want to print, in map coordinates, and the ratio between print scale and map scale.
            float scaleRatio;
            RectangleF mapArea = GetPrintAreaForCourse(courseId, out scaleRatio);

            // Get the available page size on the page. Note the PrintableArea member of PageSettings does not automatically does the
            // landscape into account.
            RectangleF printableArea = pageSettings.PrintableArea;
            if (pageSettings.Landscape)
                printableArea = new RectangleF(printableArea.Top, printableArea.Left, printableArea.Height, printableArea.Width);  // reverse rectangle for landscape
            SizeF pageSizeAvailable = printableArea.Size;

            // Layout both page dimensions, iterate through them to get all the pages we have.
            foreach (DimensionLayout verticalLayout in LayoutPageDimension(mapArea.Top, mapArea.Height, printableArea.Top, printableArea.Height, scaleRatio))
                foreach (DimensionLayout horizontalLayout in LayoutPageDimension(mapArea.Left, mapArea.Width, printableArea.Left, printableArea.Width, scaleRatio)) 
                {
                    CoursePage page = new CoursePage();
                    page.courseId = courseId;
                    page.landscape = pageSettings.Landscape;
                    page.mapRectangle = new RectangleF(horizontalLayout.startMap, verticalLayout.startMap, horizontalLayout.lengthMap, verticalLayout.lengthMap);
                    page.printRectangle = new RectangleF(horizontalLayout.startPage, verticalLayout.startPage, horizontalLayout.lengthPage, verticalLayout.lengthPage);
                    pageList.Add(page);
                }

            return pageList;
        }

        // Lays out a page in one dimension, determine if it fits on one page or more than one page, and how the mapping goes.
        // Yields an enumeration of the page layouts in that dimensions.
#if TEST
        internal
#endif
        static IEnumerable<DimensionLayout> LayoutPageDimension(float mapStart, float mapLength, float printableAreaStart, float printableAreaLength, float scaleRatio)
        {
            // Map coordinates are in mm, so there are 0.2544 map units per page unit.
            float mmPerPageUnit = (0.254F * scaleRatio);

            // Figure out the length this map part will need on the page, in 1/100 of an inch, given the scale ratio.
            float pageLengthNeeded = mapLength / mmPerPageUnit;

            // If it fits in the printable area, just center it and a single page suffices.
            if (pageLengthNeeded <= printableAreaLength) {
                float borderAmount = (printableAreaLength - pageLengthNeeded) / 2F;
                yield return new DimensionLayout(mapStart, mapLength, printableAreaStart + borderAmount, pageLengthNeeded);
            }
            else {
                // Doesn't fit on one page. How many pages will be needed?

                // The minimum amount of overlap is either 1 inch, or 1/6th of the printable area.
                float minOverlap = Math.Min(100F, printableAreaLength / 6);

                // How many pages?
                int numberOfPages = (int) Math.Ceiling((pageLengthNeeded - minOverlap) / (printableAreaLength - minOverlap));
                Debug.Assert(numberOfPages >= 2);

                // How much overlap will there be between pages (in page units)?
                float overlapPage = (numberOfPages * printableAreaLength - pageLengthNeeded) / (numberOfPages - 1);

                // And create the pages.
                float mapAdvance = (printableAreaLength - overlapPage) * mmPerPageUnit;
                for (int i = 0; i < numberOfPages; ++i) {
                    yield return new DimensionLayout(mapStart + i * mapAdvance, printableAreaLength * mmPerPageUnit, printableAreaStart, printableAreaLength);
                }
            }
        }

        // Get the area of the map we want to print, in map coordinates, and the print scale.
        // if the courseId is None, do all controls.
        RectangleF GetPrintAreaForCourse(Id<Course> courseId, out float scaleRatio)
        {
            // Get the course view to get the scale ratio.
            CourseView courseView = CourseView.CreatePrintingCourseView(eventDB, courseId);
            scaleRatio = courseView.ScaleRatio;

            return controller.GetPrintArea(courseId);
        }

        // Set landscape/portrait and margins for a particular page.
        protected override void ChangePageSettings(int pageNumber, ref bool landscape, Margins margins)
        {
            landscape = pages[pageNumber].landscape;
            margins.Left = margins.Right = margins.Top = margins.Bottom = 0;
        }

        // The core printing routine. The origin of the graphics is the upper-left of the margins,
        // and the printArea in the size to draw into (in hundreths of an inch).
        protected override void DrawPage(Graphics g, int pageNumber, SizeF printArea, int dpi)
        {
            CoursePage page = pages[pageNumber];

            // Get the course view for the course we are printing.
            CourseView courseView = CourseView.CreatePrintingCourseView(eventDB, page.courseId);

            // Get the correct purple color to print the course in.
            short ocadId;
            float purpleC, purpleM, purpleY, purpleK;
            if (! FindPurple.FindPurpleColor(mapDisplay.GetMapColors(), out ocadId, out purpleC, out purpleM, out purpleY, out purpleK)) {
                // Use a default purple.
                ocadId = CourseAppearance.courseOcadId;
                purpleC = CourseAppearance.courseColorC;
                purpleM = CourseAppearance.courseColorM;
                purpleY = CourseAppearance.courseColorY;
                purpleK = CourseAppearance.courseColorK;
            }

            // Create a course layout from the view.
            CourseLayout layout = new CourseLayout();
            layout.SetLayerColor(CourseLayer.Descriptions, CourseAppearance.blackColorOcadId, CourseAppearance.blackColorName, CourseAppearance.blackColorC, CourseAppearance.blackColorM, CourseAppearance.blackColorY, CourseAppearance.blackColorK);
            layout.SetLayerColor(CourseLayer.MainCourse, ocadId, CourseAppearance.courseColorName, purpleC, purpleM, purpleY, purpleK);
            CourseFormatter.FormatCourseToLayout(symbolDB, courseView, layout, CourseLayer.MainCourse);

            // Set the course layout into the map display
            mapDisplay.SetCourse(layout);

            // Save and restore state so we can mess with stuff.
            GraphicsState graphicsState = g.Save();

            // Set the transform, and the clip.
            Matrix transform = Util.CreateRectangleTransform(page.mapRectangle, page.printRectangle, true);
            g.IntersectClip(page.printRectangle);
            g.MultiplyTransform(transform);

            // Determine the resolution in map coordinates.
            Matrix inverseTransform = transform.Clone();
            inverseTransform.Invert();
            float minResolutionPage = 100F / dpi;
            float minResolutionMap = Util.TransformDistance(minResolutionPage, inverseTransform);
            if (minResolutionMap > 0.01F)
                minResolutionMap = 0.01F;     // always draw with at least 0.01F resolution. This prevents bitmap glyphs from being used in patterned areas, and always makes sure the quality is good.

            // And draw.
            mapDisplay.Draw(g, page.mapRectangle, minResolutionMap);   

            // restore state.
            g.Restore(graphicsState);
        }

    }

    // All the information needed to print courses.
    class CoursePrintSettings
    {
        private PageSettings pageSettings;

        public PageSettings PageSettings
        {
            get
            {
                if (pageSettings == null) {
                    pageSettings = new PageSettings();
                }

                pageSettings.Landscape = false;                                // Set to not landscape. We change this on a page by page basis later.
                pageSettings.Margins = new Margins(0, 0, 0, 0);        // use no margins. We check the hard margins of the printer during layout.
                return pageSettings;
            }
            set
            {
                pageSettings = value;
            }
        }

        public Id<Course>[] CourseIds;          // Courses to print, None is all controls.

        public int Count = 1;                         // count of copies to print
    }
}
