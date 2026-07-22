using MyTimetable.Models;
using System.Text;
using System.Text.Json;

namespace MyTimetable.Rendering
{
    public sealed class CliRenderer
    {
        public string Render(CalendarSchedule schedule, int slotCount)
        {
            // Column widths: start at 10, grow to fit
            int[] cw = new int[slotCount + 1];
            for (int i = 0; i <= slotCount; i++) cw[i] = 10;

            foreach (var day in schedule)
            {
                string date = day.Date.ToString("yyyy-MM-dd");
                if (date.Length > cw[0]) cw[0] = date.Length;

                var cells = day.Cells;
                for (int s = 0; s < slotCount && s < cells.Length; s++)
                {
                    string text = FormatCell(cells[s]);
                    if (text.Length > cw[s + 1]) cw[s + 1] = text.Length;
                }
            }

            var sb = new StringBuilder();

            sb.AppendLine(MakeBar("╔═", "═", "═╦═", "═╗", cw));
            sb.AppendLine(MakeRow(CreateHeader(slotCount), cw));
            sb.AppendLine(MakeBar("╠═", "═", "═╬═", "═╣", cw));

            foreach (var day in schedule)
            {
                var parts = new string[slotCount + 1];
                parts[0] = day.Date.ToString("yyyy-MM-dd");
                for (int s = 0; s < slotCount && s < day.Cells.Length; s++)
                    parts[s + 1] = FormatCell(day.Cells[s]);
                sb.AppendLine(MakeRow(parts, cw));
            }

            sb.Append(MakeBar("╚═", "═", "═╩═", "═╝", cw));
            return sb.ToString();
        }

        private static string FormatCell(Cell cell)
        {
            // Custom lessons take priority — they were explicitly placed here.
            // Hidden defaults underneath are invisible by design.
            if (cell.CustomLesson is { } cl)
            {
                string sn = ShortName(cl.Title);
                string type = cl.LessonType.Length > 0 ? cl.LessonType[0].ToString() : "?";
                return $"!{sn} {type}";
            }
            if (cell.DefaultLesson is { } dl && !cell.Hidden)
            {
                string sn = ShortName(dl.Title);
                string type = dl.LessonType.Length > 0 ? dl.LessonType[0].ToString() : "?";
                if (!string.IsNullOrEmpty(dl.Room))
                    return $"{sn} {type} [{dl.Room}]";
                return $"{sn} {type}";
            }
            return "-";
        }

        private static string ShortName(string t)
        {
            if (t.Contains("Физическая культура") || t.Contains("Элективные дисциплины")) return "Физра";
            if (t.Contains("Алгебра и геометрия")) return "Алгем";
            if (t.Contains("Математический анализ")) return "Матан";
            if (t.Contains("Основы программирования")) return "Прога";
            if (t.Contains("Дискретные структуры")) return "МКН2";
            if (t.Contains("Иностранный язык")) return "Английский";
            if (t.Contains("История России")) return "История";
            return t;
        }

        private static string[] CreateHeader(int slotCount)
        {
            var hdr = new string[slotCount + 1];
            hdr[0] = "Date";
            for (int s = 1; s <= slotCount; s++) hdr[s] = s.ToString();
            return hdr;
        }

        private static string MakeBar(string l, string h, string j, string r, int[] cw)
        {
            var b = new StringBuilder();
            b.Append(l);
            for (int i = 0; i < cw.Length; i++)
            {
                if (i > 0) b.Append(j);
                b.Append(new string(h[0], cw[i]));
            }
            b.Append(r);
            return b.ToString();
        }

        private static string MakeRow(string[] parts, int[] cw)
        {
            var row = new StringBuilder();
            row.Append("║ ");
            row.Append((parts[0] ?? "-").PadRight(cw[0]));
            for (int i = 1; i < cw.Length; i++)
            {
                row.Append(" ║ ");
                var v = parts[i] ?? "-";
                row.Append(v.Length <= cw[i] ? v.PadRight(cw[i]) : v[..cw[i]]);
            }
            row.Append(" ║");
            return row.ToString();
        }
    }
}
