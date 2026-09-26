using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace eScratch
{
    public static class Compatibility
    {
        // Same hash as the .NET 10 build, preserving the single-instance scope.
        public static string ProfileHash(string directory)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(
                Path.GetFullPath(directory).ToUpperInvariant()))).Replace("-", "").Substring(0, 16);
        }

        // Preserve the destination until a complete replacement is available.
        public static void ReplaceFile(string source, string destination)
        {
            if (File.Exists(destination)) File.Replace(source, destination, null, true);
            else File.Move(source, destination);
        }

        public static IEnumerable<int> CodePoints(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    yield return char.ConvertToUtf32(c, text[++i]);
                else yield return char.IsSurrogate(c) ? 0xFFFD : c;
            }
        }
    }
}

// C# record support is a compiler feature; no newer CLR is needed.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
