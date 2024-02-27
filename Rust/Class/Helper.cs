using System.Globalization;

namespace Rust
{
    public partial class Helper
    {
        public static decimal? ToPrice(string _)
        {
            string[] Split = _.Split(' ');

            if (Split.Length > 1)
            {
                string? Last = Split.LastOrDefault();
                string? First = Split.FirstOrDefault();

                if (!string.IsNullOrEmpty(Last) && !string.IsNullOrEmpty(First))
                {
                    if (decimal.TryParse(First, NumberStyles.Currency,

                        Last == "USD" ? CultureInfo.GetCultureInfo("en-US") :
                        Last == "pуб." ? CultureInfo.GetCultureInfo("ru-RU") :
                        Last == "TL" ? CultureInfo.GetCultureInfo("tr-TR") :

                        CultureInfo.CurrentCulture, out decimal Price))
                    {
                        return Math.Ceiling(Price * 100);
                    }
                }
            }

            return null;
        }

        public static DateTime ConvertFromUnixTime(long _)
        {
            var DateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

            return DateTime.AddSeconds(_);
        }
    }
}
