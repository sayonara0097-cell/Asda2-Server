using System;
using System.Linq;
using System.Text;
using WCell.Core.Network;

namespace WCell.Core
{
  public static class Asda2EncodingHelper
  {
    private static readonly char[] RuChars =
      "йцукенгшщзхъфывапролджэячсмитьбюЙЦУКЕНГШЩЗХЪФЫВАПРОЛДЖЭЯЧСМИТЬБЮёЁ".ToArray();

    private static readonly char[] EngTranslitChars =
      "ycukeng#%zh'f@vaproldj394smit[bwYCUKENG#%ZH]F@VAPROLDJ394SMIT'BW<<".ToArray();

    private static readonly byte[] RuEncode =
      "E9 F6 F3 EA E5 ED E3 F8 F9 E7 F5 FA F4 FB E2 E0 EF F0 EE EB E4 E6 FD FF F7 F1 EC E8 F2 FC E1 FE C9 D6 D3 CA C5 CD C3 D8 D9 C7 D5 DA D4 DB C2 C0 CF D0 CE CB C4 C6 DD DF D7 D1 CC C8 D2 DC C1 DE B8 A8"
        .AsBytes();

    private static readonly char[] ArChars =
      "ضصثقفغعهخحجدشسيبلاتنمكطئءؤرلاىةوزظذلآآ,.><أإ،؛؟ـپچژگک".ToArray();

    private static readonly char[] EngTranslitCharsForAr =
      "ycukeng#%zh'f@vaproldj394smit[bwYCUKENG#%ZH]F@VAPROLDJ394SMIT'BW<<".ToArray();

    private static readonly byte[] ArEncode =
      "D6 D5 CB DE DD DB DA E5 CE CD CC CF D4 D3 ED C8 E1 C7 CA E4 E3 DF D8 C6 C1 C4 D1 E1 C7 EC C9 E6 D2 D9 D0 E1 C2 C2 2C 2E 3E 3C C3 C5 A1 BA BF DC 81 8D 8E 90 98"
        .AsBytes();

    private static readonly byte[] RuEncodeTranslit = Encoding.ASCII.GetBytes(EngTranslitChars);
    private static readonly byte[] ArEncodeTranslit = Encoding.ASCII.GetBytes(EngTranslitCharsForAr);
    public static char[] RuCharacters = new char[256];
    public static char[] ArCharacters = new char[256];
    public static byte[] ArCharactersReversed = new byte[ushort.MaxValue];
    public static byte[] ArCharactersReversedTranslit = new byte[ushort.MaxValue];
    public static byte[] RuCharactersReversed = new byte[ushort.MaxValue];
    public static byte[] RuCharactersReversedTranslit = new byte[ushort.MaxValue];
    public static char[] ForReverseTranslit = new char[ushort.MaxValue];
    public static bool[] AllowedEnglishSymbols = new bool[ushort.MaxValue];
    public static bool[] AllowedEnglishNameSymbols = new bool[ushort.MaxValue];
    public static bool[] AllowedArabicSymbols = new bool[ushort.MaxValue];
    public static bool[] AllowedArabicNameSymbols = new bool[ushort.MaxValue];

    public static string AllowedEnglishSymbolsStr =
      "`1234567890-=qwertyuiop[]asdfghjkl;'zxcvbnm,./~!@#$%^&*()_+QWERTYUIOP{}ASDFGHJKL:\"ZXCVBNM<>?; \\";

    public static string AllowedEnglishNameSymbolsStr =
      "qwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM1234567890";

    public static string AllowedArabicSymbolsStr =
      "`1234567890-=ضصثقفغعهخحجدذشسيبلاتنمكطظزوةىلارؤءئإپچژگکـ[];',./~!@#$%^&*()_+{}:\"<>?; ،؛؟";

    public static string AllowedArabicNameSymbolsStr =
      " ضصثقفغعهخحجدشسيبلاتنمكطذئءؤرلاىةوزظإپچژگک.123456789";

    static Asda2EncodingHelper()
    {
      for(int index = 0; index < 256; ++index)
      {
        RuCharacters[index] = (char) index;
        ArCharacters[index] = (char) index;
      }
      for(int index = 0; index < RuEncode.Length; ++index)
        RuCharacters[RuEncode[index]] =
          RuChars[index];
      for(int index = 0; index < ArEncode.Length; ++index)
        ArCharacters[ArEncode[index]] =
          ArChars[index];
      for(int index = 0; index < RuCharactersReversed.Length; ++index)
      {
        if(index >= 256)
        {
          RuCharactersReversed[index] = 63;
          RuCharactersReversedTranslit[index] = 63;
          ForReverseTranslit[index] = '?';
        }
        else
        {
          RuCharactersReversed[index] = (byte) index;
          RuCharactersReversedTranslit[index] = (byte) index;
          ForReverseTranslit[index] = (char) index;
        }
      }
      for(int index = 0; index < ArCharactersReversed.Length; ++index)
      {
        if(index >= 256)
        {
          ArCharactersReversed[index] = 63;
          ArCharactersReversedTranslit[index] = 63;
          ForReverseTranslit[index] = '?';
        }
        else
        {
          ArCharactersReversed[index] = (byte) index;
          ArCharactersReversedTranslit[index] = (byte) index;
          ForReverseTranslit[index] = (char) index;
        }
      }

      for(int index = 0; index < RuChars.Length; ++index)
      {
        RuCharactersReversed[RuChars[index]] =
          RuEncode[index];
        RuCharactersReversedTranslit[RuChars[index]] =
          RuEncodeTranslit[index];
      }
      for(int index = 0; index < ArChars.Length; ++index)
      {
        ArCharactersReversed[ArChars[index]] =
          ArEncode[index];
        ArCharactersReversedTranslit[ArChars[index]] =
          ArEncodeTranslit[index];
      }

      for(int index = 0; index < EngTranslitChars.Length; ++index)
        ForReverseTranslit[EngTranslitChars[index]] =
          RuChars[index];
      InitAllowedEnglishSymbols();
      InitAllowedArabicSymbols();
    }

    public static string Decode(byte[] data, Locale locale)
    {
      char[] chars = new char[data.Length];
      char[] source = locale == Locale.Ru ? RuCharacters : ArCharacters;
      for(int index = 0; index < data.Length; ++index)
        chars[index] = source[data[index]];
      return new string(chars);
    }

    public static byte[] Encode(string s, Locale locale)
    {
      byte[] bytes = new byte[s.Length];
      byte[] source = locale == Locale.Ru ? RuCharactersReversed : ArCharactersReversed;
      for(int index = 0; index < s.Length; ++index)
        bytes[index] = source[s[index]];
      return bytes;
    }

    public static byte[] EncodeTranslit(string s)
    {
      byte[] numArray = new byte[s.Length];
      for(int index = 0; index < s.Length; ++index)
        numArray[index] = RuCharactersReversedTranslit[s[index]];
      return numArray;
    }

    public static string Translit(string name)
    {
      char[] chArray = new char[name.Length];
      for(int index = 0; index < name.Length; ++index)
        chArray[index] = (char) RuCharactersReversedTranslit[name[index]];
      return new string(chArray);
    }

    public static string ReverseTranslit(string name)
    {
      char[] chArray = new char[name.Length];
      for(int index = 0; index < name.Length; ++index)
        chArray[index] = ForReverseTranslit[name[index]];
      return new string(chArray);
    }

    private static void InitAllowedEnglishSymbols()
    {
      foreach(char ch in AllowedEnglishSymbolsStr)
        AllowedEnglishSymbols[ch] = true;
      foreach(char ch in AllowedEnglishNameSymbolsStr)
        AllowedEnglishNameSymbols[ch] = true;
    }

    private static void InitAllowedArabicSymbols()
    {
      foreach(char ch in AllowedArabicSymbolsStr)
        AllowedArabicSymbols[ch] = true;
      foreach(char ch in AllowedArabicNameSymbolsStr)
        AllowedArabicNameSymbols[ch] = true;
    }

    public static bool IsPrueEnglish(string s)
    {
      return s.All(c => AllowedEnglishSymbols[c]);
    }

    public static bool IsPrueEnglishName(string s)
    {
      return s.All(c => AllowedEnglishNameSymbols[c]);
    }

    public static bool IsPrueArabic(string s)
    {
      return s.All(c => AllowedArabicSymbols[c]);
    }

    public static bool IsPrueArabicName(string s)
    {
      return s.All(c => AllowedArabicNameSymbols[c]);
    }

    public static Locale MinimumAvailableLocale(Locale clientLocale, string message)
    {
      bool flag = IsPrueEnglish(message);
      Locale locale = Locale.Start;
      if(!flag)
        locale = clientLocale;
      return locale;
    }
  }
}
