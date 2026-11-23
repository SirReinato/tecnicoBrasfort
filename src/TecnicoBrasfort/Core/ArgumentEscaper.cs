using System;
using System.Collections.Generic;
using System.Text;

namespace TecnicoBrasfort.Core
{
    public static class ArgumentEscaper
    {
        public static string JoinArguments(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return "";
            }

            List<string> escaped = new List<string>(args.Length);
            for (int i = 0; i < args.Length; i++)
            {
                escaped.Add(Escape(args[i]));
            }
            return string.Join(" ", escaped.ToArray());
        }

        public static string Escape(string arg)
        {
            if (arg == null)
            {
                throw new ArgumentNullException("arg");
            }

            for (int i = 0; i < arg.Length; i++)
            {
                char c = arg[i];
                if (c == '\r' || c == '\n' || c == '\0')
                {
                    throw new ArgumentException("Argumento contem caracteres de controle invalidos.", "arg");
                }
            }

            if (arg.Length == 0)
            {
                return "\"\"";
            }

            bool needsQuotes = false;
            for (int i = 0; i < arg.Length; i++)
            {
                char c = arg[i];
                if (char.IsWhiteSpace(c) || c == '"')
                {
                    needsQuotes = true;
                    break;
                }
            }

            if (!needsQuotes)
            {
                return arg;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append('"');
            int backslashes = 0;

            for (int i = 0; i < arg.Length; i++)
            {
                char c = arg[i];
                if (c == '\\')
                {
                    backslashes++;
                }
                else if (c == '"')
                {
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                    backslashes = 0;
                }
                else
                {
                    if (backslashes > 0)
                    {
                        sb.Append('\\', backslashes);
                        backslashes = 0;
                    }
                    sb.Append(c);
                }
            }

            if (backslashes > 0)
            {
                sb.Append('\\', backslashes * 2);
            }

            sb.Append('"');
            return sb.ToString();
        }
    }
}
