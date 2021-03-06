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

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Explicit)]
public struct Block {
    [FieldOffset(0)]
    public int SpriteID;

    [FieldOffset(4)]
    public short TypeID;

    [FieldOffset(8)]
    public bool Selected;

    [FieldOffset(9)]
    public byte Height;
}