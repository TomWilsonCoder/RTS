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
using System.Drawing;
using System.Windows.Forms;


using System.Collections.Generic;

static class Init {
    [STAThread]
    static void Main(string[] args) {
        Application.EnableVisualStyles();

        new System.Threading.Thread(new System.Threading.ThreadStart(delegate{
            GameWindow wnd = new GameWindow();
            Game game = new Game(wnd);
            Application.Run(wnd);
        }), 1024 * 1024 * 4).Start();

    }
}