using System.Text;

namespace AgentBoard;

public static class AnsiDecoder
{
    public static IReadOnlyList<AnsiSpan> Decode(string text)
    {
        var spans = new List<AnsiSpan>();
        var bold = false;
        var underline = false;
        var reverse = false;
        int? fg = null;
        int? bg = null;
        var buf = new StringBuilder();

        void Flush()
        {
            if (buf.Length == 0)
            {
                return;
            }

            spans.Add(new AnsiSpan(buf.ToString(), bold, underline, reverse, fg, bg));
            buf.Clear();
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\u001b' && i + 1 < text.Length && text[i + 1] == '[')
            {
                Flush();
                var j = i + 2;
                while (j < text.Length && text[j] != 'm')
                {
                    j++;
                }

                if (j >= text.Length)
                {
                    break;
                }

                var codes = text[(i + 2)..j].Split(';', StringSplitOptions.RemoveEmptyEntries);
                if (codes.Length == 0)
                {
                    bold = underline = reverse = false;
                    fg = bg = null;
                }

                foreach (var code in codes)
                {
                    if (!int.TryParse(code, out var n))
                    {
                        continue;
                    }

                    switch (n)
                    {
                        case 0:
                            bold = underline = reverse = false;
                            fg = bg = null;
                            break;
                        case 1:
                            bold = true;
                            break;
                        case 4:
                            underline = true;
                            break;
                        case 7:
                            reverse = true;
                            break;
                        case 22:
                            bold = false;
                            break;
                        case 24:
                            underline = false;
                            break;
                        case 27:
                            reverse = false;
                            break;
                        case >= 30 and <= 37:
                            fg = n - 30;
                            break;
                        case >= 40 and <= 47:
                            bg = n - 40;
                            break;
                        case 39:
                            fg = null;
                            break;
                        case 49:
                            bg = null;
                            break;
                    }
                }

                i = j;
                continue;
            }

            buf.Append(text[i]);
        }

        Flush();
        return spans;
    }
}
