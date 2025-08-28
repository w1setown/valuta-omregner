using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq;

namespace CurrencyConverterApp.Services
{
    public class CurrencyService
    {
        private static readonly HttpClient httpClient = new();
        private Dictionary<string, decimal> exchangeRates = new();
        private Dictionary<string, string> currencyDescriptions = new();
        public DateTime LastUpdated { get; private set; }
        public string LastUpdateDate { get; private set; } = "";

        private const string ApiUrl = "https://www.nationalbanken.dk/api/currencyratesxml?lang=da";

        public async Task<bool> FetchExchangeRatesAsync()
        {
            try
            {
                using var response = await httpClient.GetAsync(ApiUrl);
                response.EnsureSuccessStatusCode();

                var xmlContent = await response.Content.ReadAsStringAsync();
                var xmlDoc = XDocument.Parse(xmlContent);

                exchangeRates.Clear();
                currencyDescriptions.Clear();

                // Add DKK as base currency
                exchangeRates["DKK"] = 1.0m;
                currencyDescriptions["DKK"] = "Danske kroner";

                // Find dailyrates element and extract date
                var dailyRates = xmlDoc.Element("exchangerates")?.Element("dailyrates");
                LastUpdateDate = dailyRates?.Attribute("id")?.Value ?? "";

                // Parse XML and extract currency rates
                foreach (var currency in xmlDoc.Descendants("currency"))
                {
                    var code = currency.Attribute("code")?.Value;
                    var desc = currency.Attribute("desc")?.Value;
                    var rateAttr = currency.Attribute("rate");

                    if (!string.IsNullOrEmpty(code) && rateAttr != null)
                    {
                        // Handle comma as decimal separator (Danish format)
                        var rateText = rateAttr.Value.Replace(',', '.');

                        if (decimal.TryParse(rateText, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal rate))
                        {
                            exchangeRates[code] = rate;
                            currencyDescriptions[code] = desc ?? "Ukendt valuta";
                        }
                    }
                }

                LastUpdated = DateTime.Now;
                return true;
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Network error: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unexpected error: {ex.Message}");
                return false;
            }
        }

        public decimal? ConvertAmount(decimal amount, string fromCurrency, string toCurrency)
        {
            if (!exchangeRates.ContainsKey(fromCurrency) || !exchangeRates.ContainsKey(toCurrency))
                return null;

            // Convert first to DKK (base currency)
            decimal dkkAmount;
            if (fromCurrency == "DKK")
            {
                dkkAmount = amount;
            }
            else
            {
                // Rate is given as DKK per 100 units of foreign currency
                dkkAmount = amount * (exchangeRates[fromCurrency] / 100m);
            }

            // Convert from DKK to target currency
            if (toCurrency == "DKK")
            {
                return dkkAmount;
            }
            else
            {
                // Convert DKK to foreign currency
                return dkkAmount * 100m / exchangeRates[toCurrency];
            }
        }

        public List<string> GetAvailableCurrencies() =>
            exchangeRates.Keys.OrderBy(c => c).ToList();

        public Dictionary<string, decimal> GetAllExchangeRates() =>
            new(exchangeRates);

        public Dictionary<string, string> GetCurrencyDescriptions() =>
            new(currencyDescriptions);

        public string GetCurrencyDescription(string currencyCode) =>
            currencyDescriptions.TryGetValue(currencyCode, out string description)
                ? description : "Ukendt valuta";

        public int GetCurrencyCount() => exchangeRates.Count;

        public bool IsDataLoaded() => exchangeRates.Count > 0;

        public (decimal rate, bool success) GetExchangeRate(string fromCurrency, string toCurrency)
        {
            var result = ConvertAmount(1m, fromCurrency, toCurrency);
            return result.HasValue ? (result.Value, true) : (0m, false);
        }
    }
}