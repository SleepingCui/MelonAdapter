using System;

namespace MelonLoader.Logging
{
    /// <summary>
    /// 32-bit ARGB color used by the MelonLoader logging API baseline.
    /// Named colors are provided for the full public name set of the baseline loader so that
    /// existing mods never fail with a missing member; the exact hue of rarely used names is a
    /// best-effort reproduction (see spec/SUPPORTED_API.md).
    /// </summary>
    public struct ColorARGB : IEquatable<ColorARGB>
    {
        public ColorARGB(byte a, byte r, byte g, byte b)
        {
            A = a; R = r; G = g; B = b;
        }

        public byte A { get; }
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }

        public static ColorARGB FromArgb(byte a, byte r, byte g, byte b) => new ColorARGB(a, r, g, b);
        public static ColorARGB FromArgb(byte r, byte g, byte b) => new ColorARGB(byte.MaxValue, r, g, b);
        public static ColorARGB FromArgb(byte a, ColorARGB color) => new ColorARGB(a, color.R, color.G, color.B);

        public static ColorARGB FromArgb(uint argb) =>
            new ColorARGB((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

        public static bool operator ==(ColorARGB left, ColorARGB right) => left.Equals(right);
        public static bool operator !=(ColorARGB left, ColorARGB right) => !left.Equals(right);

        public bool Equals(ColorARGB other) => R == other.R && G == other.G && B == other.B && A == other.A;
        public override bool Equals(object obj) => obj is ColorARGB other && Equals(other);
        public override int GetHashCode() => (A << 24) | (R << 16) | (G << 8) | B;
        public override string ToString() => "#" + A.ToString("X2") + R.ToString("X2") + G.ToString("X2") + B.ToString("X2");

        private static ColorARGB C(int r, int g, int b) => FromArgb(255, (byte)r, (byte)g, (byte)b);

        public static ColorARGB Transparent => new ColorARGB(0, 255, 255, 255);
        public static ColorARGB AliceBlue => C(0xF0, 0xF8, 0xFF);
        public static ColorARGB AntiqueWhite => C(0xFA, 0xEB, 0xD7);
        public static ColorARGB Aqua => C(0x00, 0xFF, 0xFF);
        public static ColorARGB Aquamarine => C(0x7F, 0xFF, 0xD4);
        public static ColorARGB Azure => C(0xF0, 0xFF, 0xFF);
        public static ColorARGB Beige => C(0xF5, 0xF5, 0xDC);
        public static ColorARGB Bisque => C(0xFF, 0xE4, 0xC4);
        public static ColorARGB Black => C(0x00, 0x00, 0x00);
        public static ColorARGB BlanchedAlmond => C(0xFF, 0xEB, 0xCD);
        public static ColorARGB Blue => C(0x00, 0x00, 0xFF);
        public static ColorARGB BlueViolet => C(0x8A, 0x2B, 0xE2);
        public static ColorARGB Brown => C(0xA5, 0x2A, 0x2A);
        public static ColorARGB BurlyWood => C(0xDE, 0xB8, 0x87);
        public static ColorARGB CadetBlue => C(0x5F, 0x9E, 0xA0);
        public static ColorARGB Chartreuse => C(0x7F, 0xFF, 0x00);
        public static ColorARGB Chocolate => C(0xD2, 0x69, 0x1E);
        public static ColorARGB Coral => C(0xFF, 0x7F, 0x50);
        public static ColorARGB CornflowerBlue => C(0x64, 0x95, 0xED);
        public static ColorARGB Cornsilk => C(0xFF, 0xF8, 0xDC);
        public static ColorARGB Crimson => C(0xDC, 0x14, 0x3C);
        public static ColorARGB Cyan => C(0x00, 0xFF, 0xFF);
        public static ColorARGB DarkBlue => C(0x00, 0x00, 0x8B);
        public static ColorARGB DarkCyan => C(0x00, 0x8B, 0x8B);
        public static ColorARGB DarkGoldenrod => C(0xB8, 0x86, 0x0B);
        public static ColorARGB DarkGray => C(0xA9, 0xA9, 0xA9);
        public static ColorARGB DarkGreen => C(0x00, 0x64, 0x00);
        public static ColorARGB DarkKhaki => C(0xBD, 0xB7, 0x6B);
        public static ColorARGB DarkMagenta => C(0x8B, 0x00, 0x8B);
        public static ColorARGB DarkOliveGreen => C(0x55, 0x6B, 0x2F);
        public static ColorARGB DarkOrange => C(0xFF, 0x8C, 0x00);
        public static ColorARGB DarkOrchid => C(0x99, 0x32, 0xCC);
        public static ColorARGB DarkRed => C(0x8B, 0x00, 0x00);
        public static ColorARGB DarkSalmon => C(0xE9, 0x96, 0x7A);
        public static ColorARGB DarkSeaGreen => C(0x8F, 0xBC, 0x8F);
        public static ColorARGB DarkSlateBlue => C(0x48, 0x3D, 0x8B);
        public static ColorARGB DarkSlateGray => C(0x2F, 0x4F, 0x4F);
        public static ColorARGB DarkTurquoise => C(0x00, 0xCE, 0xD1);
        public static ColorARGB DarkViolet => C(0x94, 0x00, 0xD3);
        public static ColorARGB DeepPink => C(0xFF, 0x14, 0x93);
        public static ColorARGB DeepSkyBlue => C(0x00, 0xBF, 0xFF);
        public static ColorARGB DimGray => C(0x69, 0x69, 0x69);
        public static ColorARGB DodgerBlue => C(0x1E, 0x90, 0xFF);
        public static ColorARGB Firebrick => C(0xB2, 0x22, 0x22);
        public static ColorARGB FloralWhite => C(0xFF, 0xFA, 0xF0);
        public static ColorARGB ForestGreen => C(0x22, 0x8B, 0x22);
        public static ColorARGB Fuchsia => C(0xFF, 0x00, 0xFF);
        public static ColorARGB Gainsboro => C(0xDC, 0xDC, 0xDC);
        public static ColorARGB GhostWhite => C(0xF8, 0xF8, 0xFF);
        public static ColorARGB Gold => C(0xFF, 0xD7, 0x00);
        public static ColorARGB Goldenrod => C(0xDA, 0xA5, 0x20);
        public static ColorARGB Gray => C(0x80, 0x80, 0x80);
        public static ColorARGB Green => C(0x00, 0x80, 0x00);
        public static ColorARGB GreenYellow => C(0xAD, 0xFF, 0x2F);
        public static ColorARGB Honeydew => C(0xF0, 0xFF, 0xF0);
        public static ColorARGB HotPink => C(0xFF, 0x69, 0xB4);
        public static ColorARGB IndianRed => C(0xCD, 0x5C, 0x5C);
        public static ColorARGB Indigo => C(0x4B, 0x00, 0x82);
        public static ColorARGB Ivory => C(0xFF, 0xFF, 0xF0);
        public static ColorARGB Khaki => C(0xF0, 0xE6, 0x8C);
        public static ColorARGB Lavender => C(0xE6, 0xE6, 0xFA);
        public static ColorARGB LavenderBlush => C(0xFF, 0xF0, 0xF5);
        public static ColorARGB LawnGreen => C(0x7C, 0xFC, 0x00);
        public static ColorARGB LemonChiffon => C(0xFF, 0xFA, 0xCD);
        public static ColorARGB LightBlue => C(0xAD, 0xD8, 0xE6);
        public static ColorARGB LightCoral => C(0xF0, 0x80, 0x80);
        public static ColorARGB LightCyan => C(0xE0, 0xFF, 0xFF);
        public static ColorARGB LightGoldenrodYellow => C(0xFA, 0xFA, 0xD2);
        public static ColorARGB LightGreen => C(0x90, 0xEE, 0x90);
        public static ColorARGB LightGray => C(0xD3, 0xD3, 0xD3);
        public static ColorARGB LightPink => C(0xFF, 0xB6, 0xC1);
        public static ColorARGB LightSalmon => C(0xFF, 0xA0, 0x7A);
        public static ColorARGB LightSeaGreen => C(0x20, 0xB2, 0xAA);
        public static ColorARGB LightSkyBlue => C(0x87, 0xCE, 0xFA);
        public static ColorARGB LightSlateGray => C(0x77, 0x88, 0x99);
        public static ColorARGB LightSteelBlue => C(0xB0, 0xC4, 0xDE);
        public static ColorARGB LightYellow => C(0xFF, 0xFF, 0xE0);
        public static ColorARGB Lime => C(0x00, 0xFF, 0x00);
        public static ColorARGB LimeGreen => C(0x32, 0xCD, 0x32);
        public static ColorARGB Linen => C(0xFA, 0xF0, 0xE6);
        public static ColorARGB Magenta => C(0xFF, 0x00, 0xFF);
        public static ColorARGB Maroon => C(0x80, 0x00, 0x00);
        public static ColorARGB MediumAquamarine => C(0x66, 0xCD, 0xAA);
        public static ColorARGB MediumBlue => C(0x00, 0x00, 0xCD);
        public static ColorARGB MediumOrchid => C(0xBA, 0x55, 0xD3);
        public static ColorARGB MediumPurple => C(0x93, 0x70, 0xDB);
        public static ColorARGB MediumSeaGreen => C(0x3C, 0xB3, 0x71);
        public static ColorARGB MediumSlateBlue => C(0x7B, 0x68, 0xEE);
        public static ColorARGB MediumSpringGreen => C(0x00, 0xFA, 0x9A);
        public static ColorARGB MediumTurquoise => C(0x48, 0xD1, 0xCC);
        public static ColorARGB MediumVioletRed => C(0xC7, 0x15, 0x85);
        public static ColorARGB MidnightBlue => C(0x19, 0x19, 0x70);
        public static ColorARGB MintCream => C(0xF5, 0xFF, 0xFA);
        public static ColorARGB MistyRose => C(0xFF, 0xE4, 0xE1);
        public static ColorARGB Moccasin => C(0xFF, 0xE4, 0xB5);
        public static ColorARGB NavajoWhite => C(0xFF, 0xDE, 0xAD);
        public static ColorARGB Navy => C(0x00, 0x00, 0x80);
        public static ColorARGB OldLace => C(0xFD, 0xF5, 0xE6);
        public static ColorARGB Olive => C(0x80, 0x80, 0x00);
        public static ColorARGB OliveDrab => C(0x6B, 0x8E, 0x23);
        public static ColorARGB Orange => C(0xFF, 0xA5, 0x00);
        public static ColorARGB OrangeRed => C(0xFF, 0x45, 0x00);
        public static ColorARGB Orchid => C(0xDA, 0x70, 0xD6);
        public static ColorARGB PaleGoldenrod => C(0xEE, 0xE8, 0xAA);
        public static ColorARGB PaleGreen => C(0x98, 0xFB, 0x98);
        public static ColorARGB PaleTurquoise => C(0xAF, 0xEE, 0xEE);
        public static ColorARGB PaleVioletRed => C(0xDB, 0x70, 0x93);
        public static ColorARGB PapayaWhip => C(0xFF, 0xEF, 0xD5);
        public static ColorARGB PeachPuff => C(0xFF, 0xDA, 0xB9);
        public static ColorARGB Peru => C(0xCD, 0x85, 0x3F);
        public static ColorARGB Pink => C(0xFF, 0xC0, 0xCB);
        public static ColorARGB Plum => C(0xDD, 0xA0, 0xDD);
        public static ColorARGB PowderBlue => C(0xB0, 0xE0, 0xE6);
        public static ColorARGB Purple => C(0x80, 0x00, 0x80);
        public static ColorARGB RebeccaPurple => C(0x66, 0x33, 0x99);
        public static ColorARGB Red => C(0xFF, 0x00, 0x00);
        public static ColorARGB RosyBrown => C(0xBC, 0x8F, 0x8F);
        public static ColorARGB RoyalBlue => C(0x41, 0x69, 0xE1);
        public static ColorARGB SaddleBrown => C(0x8B, 0x45, 0x13);
        public static ColorARGB Salmon => C(0xFA, 0x80, 0x72);
        public static ColorARGB SandyBrown => C(0xF4, 0xA4, 0x60);
        public static ColorARGB SeaGreen => C(0x2E, 0x8B, 0x57);
        public static ColorARGB SeaShell => C(0xFF, 0xF5, 0xEE);
        public static ColorARGB Sienna => C(0xA0, 0x52, 0x2D);
        public static ColorARGB Silver => C(0xC0, 0xC0, 0xC0);
        public static ColorARGB SkyBlue => C(0x87, 0xCE, 0xEB);
        public static ColorARGB SlateBlue => C(0x6A, 0x5A, 0xCD);
        public static ColorARGB SlateGray => C(0x70, 0x80, 0x90);
        public static ColorARGB Snow => C(0xFF, 0xFA, 0xFA);
        public static ColorARGB SpringGreen => C(0x00, 0xFF, 0x7F);
        public static ColorARGB SteelBlue => C(0x46, 0x82, 0xB4);
        public static ColorARGB Tan => C(0xD2, 0xB4, 0x8C);
        public static ColorARGB Teal => C(0x00, 0x80, 0x80);
        public static ColorARGB Thistle => C(0xD8, 0xBF, 0xD8);
        public static ColorARGB Tomato => C(0xFF, 0x63, 0x47);
        public static ColorARGB Turquoise => C(0x40, 0xE0, 0xD0);
        public static ColorARGB Violet => C(0xEE, 0x82, 0xEE);
        public static ColorARGB Wheat => C(0xF5, 0xDE, 0xB3);
        public static ColorARGB White => C(0xFF, 0xFF, 0xFF);
        public static ColorARGB WhiteSmoke => C(0xF5, 0xF5, 0xF5);
        public static ColorARGB Yellow => C(0xFF, 0xFF, 0x00);
        public static ColorARGB YellowGreen => C(0x9A, 0xCD, 0x32);
    }
}
