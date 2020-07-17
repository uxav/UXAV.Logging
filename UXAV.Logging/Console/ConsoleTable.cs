using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UXAV.Logging.Console
{
    public class ConsoleTable
    {
        private readonly List<string> _headers;
        private readonly int[] _columnWidths;
        private readonly List<IEnumerable<string>> _rows = new List<IEnumerable<string>>();

        public ConsoleTable(params string[] headers)
        {
            _headers = new List<string>(headers);
            _columnWidths = new int[headers.Count()];

            var col = 0;
            foreach (var header in _headers)
            {
                _columnWidths[col] = header.Length;
                col++;
            }
        }

        public int TotalWidth
        {
            get { return _columnWidths.Aggregate(1, (current, width) => current + (width + 3)); }
        }

        public void AddRow(params object[] items)
        {
            var values = new List<string>();
            var col = 0;
            foreach (var item in items)
            {
                if (item == null)
                {
                    values.Add(string.Empty);
                    col++;
                    continue;
                }

                var s = item.ToString();
                values.Add(s);
                var replaced = Regex.Replace(s, @"\u001b.*?m", string.Empty);
                if (replaced.Length > _columnWidths[col])
                    _columnWidths[col] = replaced.Length;
                col++;
            }

            _rows.Add(values);
        }

        public override string ToString()
        {
            return ToString(false);
        }

        public string ToString(bool useColor)
        {
            var sb = new StringBuilder();

            var colTag1 = useColor ? AnsiColors.Yellow : string.Empty;
            var colTag2 = useColor ? AnsiColors.Green : string.Empty;
            var colTagClose = useColor ? AnsiColors.Reset : string.Empty;

            sb.Append('|');
            var divLine = "|";

            var col = 0;
            foreach (var header in _headers)
            {
                var s = colTag1 + header.PadRight(_columnWidths[col]) + colTagClose;
                sb.Append(" " + s + " |");
                var dashes = string.Empty;
                for (var i = 0; i < _columnWidths[col]; i++)
                {
                    dashes = dashes + "-";
                }

                divLine = divLine + " " + dashes + " |";
                col++;
            }

            sb.AppendLine();

            sb.AppendLine(divLine);

            foreach (var row in _rows)
            {
                col = 0;
                var items = row as string[] ?? row.ToArray();
                sb.Append('|');
                foreach (var item in items)
                {
                    var s = item.PadRight(_columnWidths[col]);
                    if (col == 0)
                    {
                        s = colTag2 + s + colTagClose;
                    }

                    sb.Append(" " + s + " |");
                    col++;
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
