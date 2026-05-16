using System.Globalization;
using System.Text;
using BarbershopCrm.Domain.Enums;

namespace BarbershopCrm.Infrastructure.Analytics;

/// <summary>
/// CSV-экспорт журнала записей для Excel/LibreOffice (ru-RU локаль).
/// Использует разделитель «;» (как ждёт Excel в русской локали),
/// UTF-8 с BOM — Excel автоматически определяет кодировку по BOM (EF BB BF)
/// и корректно отображает кириллицу без дополнительных настроек.
/// Даты в формате <c>dd.MM.yyyy HH:mm</c>, числа с запятой как десятичным разделителем.
/// </summary>
public static class CsvExporter
{
    private const char Separator = ';';

    private static readonly CultureInfo RuRu = CultureInfo.GetCultureInfo("ru-RU");

    private static readonly string[] Header =
    {
        "ID записи",
        "Филиал",
        "Мастер",
        "Услуга",
        "Клиент",
        "Дата и время",
        "Длительность, мин",
        "Стоимость, руб",
        "Статус",
        "Источник",
        "Итог визита, руб",
        "Причина отмены",
    };

    // UTF-8 с BOM: Excel читает BOM (EF BB BF) и автоматически
    // определяет кодировку — кириллица отображается корректно.
    private static readonly Encoding Utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public static byte[] BuildBookingsCsv(IEnumerable<BookingExportRow> rows)
    {
        var sb = new StringBuilder();
        // sep= подсказывает Excel какой разделитель использовать
        sb.Append("sep=").Append(Separator).Append("\r\n");
        sb.Append(string.Join(Separator, Header)).Append("\r\n");

        foreach (var r in rows)
        {
            var fields = new[]
            {
                r.BookingId.ToString(RuRu),
                Escape(r.BranchName),
                Escape(r.MasterName),
                Escape(r.ServiceName),
                Escape(r.ClientName),
                r.StartDateTime.ToString("dd.MM.yyyy HH:mm", RuRu),
                r.DurationMinutes.ToString(RuRu),
                r.PriceSnapshot.ToString("0.00", RuRu),
                Escape(StatusLabels.BookingStatus(r.Status)),
                Escape(StatusLabels.BookingSource(r.Source)),
                r.VisitTotalAmount?.ToString("0.00", RuRu) ?? "",
                Escape(r.CancelReason ?? ""),
            };
            sb.Append(string.Join(Separator, fields)).Append("\r\n");
        }

        // GetBytes не всегда добавляет BOM в начало буфера в зависимости от версии API;
        // явно добавляем стандартный UTF-8 preamble для Excel.
        var body = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(sb.ToString());
        var preamble = Utf8Bom.GetPreamble();
        if (preamble.Length == 0) return body;
        var result = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
        return result;
    }

    public static byte[] BuildClientsCsv(IEnumerable<ClientAnalyticsRow> rows)
    {
        var sb = new StringBuilder();
        sb.Append("sep=").Append(Separator).Append("\r\n");
        
        var header = new[]
        {
            "ID клиента",
            "ФИО",
            "Телефон",
            "Email",
            "Всего визитов",
            "Общая выручка, руб",
            "Средний чек, руб",
            "Первый визит",
            "Последний визит",
            "Дней с последнего визита",
            "ABC-категория",
            "Уровень",
            "Предпочитаемый мастер",
            "Предпочитаемая услуга",
            "Источник"
        };
        sb.Append(string.Join(Separator, header)).Append("\r\n");

        foreach (var r in rows)
        {
            var fields = new[]
            {
                r.ClientId.ToString(RuRu),
                Escape(r.ClientName),
                Escape(r.Phone ?? ""),
                Escape(r.Email ?? ""),
                r.TotalVisits.ToString(RuRu),
                r.TotalRevenue.ToString("0.00", RuRu),
                r.AverageTicket.ToString("0.00", RuRu),
                r.FirstVisitDate.ToString("dd.MM.yyyy", RuRu),
                r.LastVisitDate.ToString("dd.MM.yyyy", RuRu),
                r.DaysSinceLastVisit.ToString(RuRu),
                r.AbcCategory.ToString(),
                TierLabel(r.Tier),
                Escape(r.PreferredMaster ?? ""),
                Escape(r.PreferredService ?? ""),
                Escape(r.Source ?? "")
            };
            sb.Append(string.Join(Separator, fields)).Append("\r\n");
        }

        var body = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(sb.ToString());
        var preamble = Utf8Bom.GetPreamble();
        if (preamble.Length == 0) return body;
        var result = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
        return result;
    }

    private static string TierLabel(ClientTier tier) => tier switch
    {
        ClientTier.New => "Новый",
        ClientTier.Regular => "Постоянный",
        ClientTier.Loyal => "Лояльный",
        ClientTier.VIP => "VIP",
        _ => tier.ToString()
    };

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var needsQuote = value.IndexOfAny(new[] { Separator, '"', '\r', '\n' }) >= 0;
        var v = value.Replace("\"", "\"\"");
        return needsQuote ? $"\"{v}\"" : v;
    }
}
