using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SourceTranslator
{
    class Program
    {
        enum TranslationMode
        {
            KeepOriginalTextBeforeTranslation = 0,
            TranslationOnly = 1,
            KeepOriginalTextAfterTranslation = 2
        }

        static void Main(string[] args)
        {
            try
            {
                Console.BackgroundColor = ConsoleColor.Black;
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Source Comment Translator v1.0 © 2020 by Johannes Fetz");
                Console.WriteLine("ReversoTranslation © 2020 Reverso-Softissimo. All rights reserved.");
                Console.WriteLine();
#if !DEBUG
                if (args.Length != 3)
                {
                    Console.WriteLine("Usage: [PATH] [REVERSO_DIRECTION] [MODE]");
                    Console.WriteLine("   - PATH : C style source file path");
                    Console.WriteLine(string.Format("   - REVERSO_DIRECTION : {0}", Program.GetAvailableDirections()));
                    Console.WriteLine("   - MODE : 0 to keep orginal text before translation");
                    Console.WriteLine("   - MODE : 1 translation only");
                    Console.WriteLine("   - MODE : 2 to keep orginal text after translation");
                    Console.WriteLine();
                    Console.WriteLine("Example: MY_HEADER.H jpn-eng 2");
                    return;
                }
#endif
                string path = args[0];
                if (!File.Exists(path))
                    Program.Error(string.Format("{0} not found.", path));
                string outputPath = Path.Combine(Path.GetDirectoryName(path), String.Concat(Path.GetFileNameWithoutExtension(path), ".TRANSLATED", Path.GetExtension(path)));

                string direction = args[1].ToLowerInvariant();
                if (!Program.AvailableReversoDirections.Contains(direction))
                    Program.Error(string.Format("Available Reverso direction : {0}", Program.GetAvailableDirections()));

                int modeInt;
                if (!int.TryParse(args[2], out modeInt))
                    Program.Error("Invalid mode.");
                TranslationMode mode = (TranslationMode)modeInt;

                byte[] raw = File.ReadAllBytes(path);
                Encoding sourceEncoding = direction.StartsWith("jpn") ? Encoding.GetEncoding(932) : Encoding.UTF8;
                Encoding destinationEncoding = direction.EndsWith("jpn") ? Encoding.GetEncoding(932) : Encoding.UTF8;
                string contents = sourceEncoding.GetString(raw);

                direction += "-5"; // We append the "-5" direction suffix for reverso;
                string res = Program.TranslateComments(contents, mode, direction);

                File.WriteAllText(outputPath, res, destinationEncoding);

                Console.WriteLine();
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0} generated.", Path.GetFileName(outputPath)));
            }
            catch (Exception ex)
            {
                Program.Error(ex.Message);
            }
            finally
            {
                Console.BackgroundColor = ConsoleColor.Black;
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private static void Error(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Environment.Exit(-1);
        }

        private static void ConsoleProgressBar(int progress, int total)
        {
            Console.CursorLeft = 0;
            Console.Write("[");
            Console.CursorLeft = 32;
            Console.Write("]");
            Console.CursorLeft = 1;
            float onechunk = 30.0f / total;
            int position = 1;
            for (int i = 0; i < onechunk * progress; i++)
            {
                Console.BackgroundColor = ConsoleColor.Gray;
                Console.CursorLeft = position++;
                Console.Write(" ");
            }
            for (int i = position; i <= 31; i++)
            {
                Console.BackgroundColor = ConsoleColor.Green;
                Console.CursorLeft = position++;
                Console.Write(" ");
            }
            Console.CursorLeft = 35;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Write(progress.ToString() + " of " + total.ToString() + " comments    ");
        }

        private const string CommentRegexPattern = @"(@(?:""[^""]*"")+|""(?:[^""\n\\]+|\\.)*""|'(?:[^'\n\\]+|\\.)*')|//.*|/\*(?s:.*?)\*/";

        private static readonly Regex CommentRegex = new Regex(Program.CommentRegexPattern, RegexOptions.Compiled | RegexOptions.Multiline);

        private static string TranslateComments(string code, TranslationMode mode, string direction, bool useCorrector = true, int maxTranslationChars = 800)
        {
            MatchCollection collection = Program.CommentRegex.Matches(code);
            if (collection.Count <= 0)
                return code;
            int step = 0;
            StringBuilder result = new StringBuilder();
            int index = 0;
            foreach (Match match in collection)
            {
                Program.ConsoleProgressBar(++step, collection.Count);
                string toTranslate = match.Value.Trim('\\', '/', '-', '*', '_', ' ', '\t', '\n', '\r');
                if (toTranslate.StartsWith("\""))
                    continue;
                string translation = Program.ReversoTranslation(toTranslate, direction, useCorrector, maxTranslationChars);
                if (string.IsNullOrWhiteSpace(translation) || string.Equals(toTranslate.Trim('.', '_'), translation.Trim('.', '_'), StringComparison.InvariantCultureIgnoreCase))
                    continue;
                result.Append(code.Substring(index, match.Index - index));
                string output;
                switch (mode)
                {
                    case TranslationMode.KeepOriginalTextAfterTranslation:
                        output = string.Format(CultureInfo.InvariantCulture, "{0} - {1}", translation, toTranslate);
                        break;
                    case TranslationMode.KeepOriginalTextBeforeTranslation:
                        output = string.Format(CultureInfo.InvariantCulture, "{0} - {1}", toTranslate, translation);
                        break;
                    case TranslationMode.TranslationOnly:
                    default:
                        output = translation;
                        break;
                }
                result.Append(match.Value.Replace(toTranslate, output));
                index = match.Index + match.Length;
            }
            if (index < code.Length)
                result.Append(code.Substring(index, code.Length - index));
            return result.ToString();
        }

        #region REVERSO

        private static string GetAvailableDirections()
        {
            return Program.AvailableReversoDirections.Aggregate(new StringBuilder(),
                         (current, next) => current.Append(current.Length == 0 ? "" : ", ").Append(next))
                         .ToString();
        }

        private static readonly string[] AvailableReversoDirections = new string[] { "jpn-eng", "eng-jpg", "jpn-fra", "fra-jpg", "eng-fra", "fra-eng" };

        private static readonly Uri ReversoWebserviceUri = new Uri("https://async5.reverso.net/WebReferences/WSAJAXInterface.asmx/TranslateCorrWS");

        private static string BuildReversoPayload(string input, string direction, bool useCorrector = true, int maxTranslationChars = 800)
        {
            return string.Format("{{'searchText': '{0}', 'direction': '{1}', 'maxTranslationChars':'{2}', 'usecorr':'{3}'}}",
                input.Replace("'", "\'"),
                direction,
                maxTranslationChars,
                useCorrector ? "true" : "false");
        }

        private const string BeginOfResult = "\"result\":\"";
        private const string EndOfResult = "\",\"";

        private static string ExtractTranslationFromResponse(string response)
        {
            int resultBeginIndex = response.IndexOf(Program.BeginOfResult);
            if (resultBeginIndex < 0)
                return null;
            resultBeginIndex += Program.BeginOfResult.Length;

            int resultEndIndex = response.IndexOf(Program.EndOfResult);
            if (resultEndIndex < 0)
                return null;
            return response.Substring(resultBeginIndex, resultEndIndex - resultBeginIndex);
        }

        private static string ReversoTranslation(string input, string direction, bool useCorrector = true, int maxTranslationChars = 800)
        {
            HttpWebRequest http = (HttpWebRequest)WebRequest.Create(Program.ReversoWebserviceUri);
            http.Accept = "application/json";
            http.ContentType = "application/json; charset=utf-8";
            http.Method = "POST";

            string payload = BuildReversoPayload(input, direction, useCorrector, maxTranslationChars);
            UTF8Encoding encoding = new UTF8Encoding();
            Byte[] bytes = encoding.GetBytes(payload);

            using (Stream newStream = http.GetRequestStream())
            {
                newStream.Write(bytes, 0, bytes.Length);
                newStream.Close();

                using (WebResponse response = http.GetResponse())
                {
                    using (Stream stream = response.GetResponseStream())
                    {
                        using (StreamReader sr = new StreamReader(stream))
                        {
                            return Program.ExtractTranslationFromResponse(sr.ReadToEnd());
                        }
                    }
                }
            }
        }

        #endregion
    }
}
