/*
 *  THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
 *  KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
 *  IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR
 *  PURPOSE. IT CAN BE DISTRIBUTED FREE OF CHARGE AS LONG AS THIS HEADER 
 *  REMAINS UNCHANGED.
 *
 *  REPO: http://www.github.com/tomwilsoncoder/RTS
*/
using System;


public static partial class OpenGL {

    private class OpenGLFont : IFont {
        public int HASH;
        public IntPtr HFONT;
        public uint LIST;

        public ABC[] GLYPHINFO;
        public TEXTMETRIC METRIC;
     
        public void Dispose() { }
        public override int GetHashCode() {
            return HASH;
        }
    }

}