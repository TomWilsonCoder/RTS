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
using System.IO;
using System.Diagnostics;

public static partial class Globals {
    public static readonly int BUILD;


    static Globals() {
        //detect if the we are debugging (just look for VSHost)
        Process ps = Process.GetCurrentProcess();
        bool isDebug = ps.ProcessName.ToLower().EndsWith(".vshost");
        
        /*attempt to read build file*/
        int buffer = 60; /*based off known builds at the time of writing this.*/

        const string buildFilename = "../currentBuild";
        if (File.Exists(buildFilename)) {
            buffer = Convert.ToInt32(File.ReadAllText(buildFilename));
        }

        //incriment and save new build
        if (isDebug) {
            buffer++;
            File.WriteAllText(buildFilename, buffer.ToString());
        }

        BUILD = buffer;
    }
}