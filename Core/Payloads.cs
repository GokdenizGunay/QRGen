using System.Globalization;
using System.Text;

namespace QRGen.Core;

public static class Payloads
{
    static string EscWifi(string s) => new StringBuilder(s)
        .Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace(":", "\\:").Replace("\"", "\\\"").ToString();

    static string EscV(string s) => s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r", "").Replace("\n", "\\n");

    public static string Url(string url)
    {
        url = url.Trim();
        if (url.Length > 0 && !url.Contains("://") && !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;
        return url;
    }

    public static string Wifi(string ssid, string password, string security, bool hidden)
    {
        var sb = new StringBuilder("WIFI:");
        sb.Append("T:").Append(security).Append(';');
        sb.Append("S:").Append(EscWifi(ssid)).Append(';');
        if (security != "nopass") sb.Append("P:").Append(EscWifi(password)).Append(';');
        if (hidden) sb.Append("H:true;");
        sb.Append(';');
        return sb.ToString();
    }

    public static string VCard(string first, string last, string org, string title, string phone, string mobile,
                               string email, string url, string street, string city, string zip, string country, string note)
    {
        var sb = new StringBuilder();
        sb.Append("BEGIN:VCARD\nVERSION:3.0\n");
        sb.Append($"N:{EscV(last)};{EscV(first)};;;\n");
        sb.Append($"FN:{EscV((first + " " + last).Trim())}\n");
        if (org != "") sb.Append($"ORG:{EscV(org)}\n");
        if (title != "") sb.Append($"TITLE:{EscV(title)}\n");
        if (phone != "") sb.Append($"TEL;TYPE=WORK,VOICE:{phone}\n");
        if (mobile != "") sb.Append($"TEL;TYPE=CELL:{mobile}\n");
        if (email != "") sb.Append($"EMAIL:{email}\n");
        if (url != "") sb.Append($"URL:{url}\n");
        if (street + city + zip + country != "")
            sb.Append($"ADR;TYPE=WORK:;;{EscV(street)};{EscV(city)};;{EscV(zip)};{EscV(country)}\n");
        if (note != "") sb.Append($"NOTE:{EscV(note)}\n");
        sb.Append("END:VCARD");
        return sb.ToString();
    }

    public static string Email(string to, string subject, string body)
    {
        var q = new List<string>();
        if (subject != "") q.Add("subject=" + Uri.EscapeDataString(subject));
        if (body != "") q.Add("body=" + Uri.EscapeDataString(body));
        return "mailto:" + to.Trim() + (q.Count > 0 ? "?" + string.Join("&", q) : "");
    }

    public static string Sms(string number, string message) => $"SMSTO:{number.Trim()}:{message}";

    public static string Phone(string number) => "tel:" + number.Trim().Replace(" ", "");

    public static string WhatsApp(string number, string message)
    {
        var digits = new string(number.Where(char.IsAsciiDigit).ToArray());
        return $"https://wa.me/{digits}" + (message != "" ? "?text=" + Uri.EscapeDataString(message) : "");
    }

    public static string Geo(string lat, string lon)
    {
        static string N(string s) => double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v.ToString(CultureInfo.InvariantCulture) : "0";
        return $"geo:{N(lat)},{N(lon)}";
    }

    public static string Event(string title, string location, string desc, DateTime start, DateTime end, bool allDay)
    {
        string F(DateTime d) => allDay ? d.ToString("yyyyMMdd") : d.ToString("yyyyMMdd'T'HHmmss");
        var sb = new StringBuilder("BEGIN:VEVENT\n");
        sb.Append($"SUMMARY:{EscV(title)}\n");
        sb.Append(allDay ? $"DTSTART;VALUE=DATE:{F(start)}\n" : $"DTSTART:{F(start)}\n");
        sb.Append(allDay ? $"DTEND;VALUE=DATE:{F(end)}\n" : $"DTEND:{F(end)}\n");
        if (location != "") sb.Append($"LOCATION:{EscV(location)}\n");
        if (desc != "") sb.Append($"DESCRIPTION:{EscV(desc)}\n");
        sb.Append("END:VEVENT");
        return sb.ToString();
    }
}
