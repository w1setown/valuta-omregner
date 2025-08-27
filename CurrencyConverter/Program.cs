// <version> 0.1.0 </version>
// Author: Gabriel Visby Søgaard Ganderup

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks; 
using System.Xml.Linq;
using System.Linq;

namespace CurrencyConverter
{
    public class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static Dictionary<string, decimal> exchangeRates = new Dictionary<string, decimal>();
        private static DateTime lastUpdated;
        private const string ApiUrl = "https://www.nationalbanken.dk/api/currencyratesxml?lang=da";

        static async Task Main(string[] args)
        {
            Console.WriteLine("VALUTAOMREGNER - Danmarks Nationalbank API");

            // Hent valutakurser ved opstart
            if (!await FetchExchangeRatesAsync())
            {
                Console.WriteLine("Fejl: Kunne ikke hente valutakurser. Programmet afsluttes.");
                Console.WriteLine("Tryk på en vilkårlig tast for at afslutte...");
                Console.ReadKey();
                return;
            }

            await RunMainLoopAsync();
        }

        private static async Task<bool> FetchExchangeRatesAsync()
        {
            try
            {
                Console.WriteLine("Henter aktuelle valutakurser fra Danmarks Nationalbank...");

                using var response = await httpClient.GetAsync(ApiUrl);
                response.EnsureSuccessStatusCode();

                var xmlContent = await response.Content.ReadAsStringAsync();
                var xmlDoc = XDocument.Parse(xmlContent);

                exchangeRates.Clear();
                exchangeRates.Add("DKK", 1.0m);

                // Find dailyrates element og udtræk dato
                var dailyRates = xmlDoc.Element("exchangerates")?.Element("dailyrates");
                var dateId = dailyRates?.Attribute("id")?.Value;

                if (dateId != null)
                {
                    Console.WriteLine($"Kurser for dato: {dateId}");
                }

                // Parse XML og udtræk valutakurser
                var currencies = xmlDoc.Descendants("currency");

                foreach (var currency in currencies)
                {
                    var code = currency.Attribute("code")?.Value;
                    var desc = currency.Attribute("desc")?.Value;
                    var rateAttribute = currency.Attribute("rate");

                    if (!string.IsNullOrEmpty(code) && rateAttribute != null)
                    {
                        // Håndter komma som decimalseparator (dansk format)
                        var rateText = rateAttribute.Value.Replace(',', '.');

                        if (decimal.TryParse(rateText, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal rate))
                        {
                            // Kursen er angivet per 1 DKK, så vi gemmer den direkte
                            exchangeRates[code] = rate;
                        }
                    }
                }

                lastUpdated = DateTime.Now;
                Console.WriteLine($"Hentet kurser for {exchangeRates.Count} valutaer");
                return true;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Netværksfejl: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Uventet fejl: {ex.Message}");
                return false;
            }
        }

        private static async Task RunMainLoopAsync()
        {
            while (true)
            {
                DisplayMainMenu();
                var choice = Console.ReadLine()?.Trim();

                switch (choice)
                {
                    case "1":
                        await ConvertCurrencyAsync();
                        break;
                    case "2":
                        DisplayAvailableCurrencies();
                        break;
                    case "3":
                        DisplayExchangeRates();
                        break;
                    case "4":
                        await FetchExchangeRatesAsync();
                        break;
                    case "5":
                        Console.WriteLine("Tak for at bruge valutaomregneren!");
                        return;
                    default:
                        Console.WriteLine("Ugyldigt valg. Prøv igen.");
                        break;
                }

                Console.WriteLine("\nTryk på en vilkårlig tast for at fortsætte...");
                Console.ReadKey();
                Console.Clear();
            }
        }

        private static void DisplayMainMenu()
        {
            Console.WriteLine($"\nSidst opdateret: {lastUpdated:dd-MM-yyyy HH:mm:ss}");
            Console.WriteLine("\nVælg en handling:");
            Console.WriteLine("1. Omregn valuta");
            Console.WriteLine("2. Vis tilgængelige valutaer");
            Console.WriteLine("3. Vis aktuelle kurser");
            Console.WriteLine("4. Opdater kurser");
            Console.WriteLine("5. Afslut");
            Console.Write("\nDit valg (1-5): ");
        }

        private static async Task ConvertCurrencyAsync()
        {
            Console.WriteLine("\nVALUTAOMREGNING");

            // Få beløb fra bruger
            decimal amount = GetAmountFromUser();
            if (amount <= 0) return;

            // Få fra-valuta
            string fromCurrency = GetCurrencyFromUser("Omregn fra (f.eks. DKK): ");
            if (string.IsNullOrEmpty(fromCurrency)) return;

            // Få til-valuta
            string toCurrency = GetCurrencyFromUser("Omregn til (f.eks. EUR): ");
            if (string.IsNullOrEmpty(toCurrency)) return;

            // Udfør omregning
            var result = ConvertAmount(amount, fromCurrency, toCurrency);
            if (result.HasValue)
            {
                Console.WriteLine($"\n✓ Resultat:");
                Console.WriteLine($"   {amount:N2} {fromCurrency} = {result.Value:N2} {toCurrency}");

                // Vis også omvendt kurs
                var reverseResult = ConvertAmount(1, fromCurrency, toCurrency);
                if (reverseResult.HasValue)
                {
                    Console.WriteLine($"   Kurs: 1 {fromCurrency} = {reverseResult.Value:N4} {toCurrency}");
                }
            }
            else
            {
                Console.WriteLine("Kunne ikke omregne valuta. Kontroller at begge valutaer er gyldige.");
            }
        }

        private static decimal GetAmountFromUser()
        {
            while (true)
            {
                Console.Write("Indtast beløb: ");
                var input = Console.ReadLine()?.Replace(',', '.');

                if (decimal.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal amount) && amount > 0)
                {
                    return amount;
                }

                Console.WriteLine("Indtast venligst et gyldigt beløb (større end 0).");
            }
        }

        private static string GetCurrencyFromUser(string prompt)
        {
            Console.Write(prompt);
            var currency = Console.ReadLine()?.Trim().ToUpper();

            if (string.IsNullOrEmpty(currency))
            {
                Console.WriteLine("Ingen valuta indtastet.");
                return null;
            }

            if (!exchangeRates.ContainsKey(currency))
            {
                Console.WriteLine($"Valuta '{currency}' blev ikke fundet. Brug kommando 2 for at se tilgængelige valutaer.");
                return null;
            }

            return currency;
        }

        private static decimal? ConvertAmount(decimal amount, string fromCurrency, string toCurrency)
        {
            if (!exchangeRates.ContainsKey(fromCurrency) || !exchangeRates.ContainsKey(toCurrency))
                return null;

            // Omregn først til DKK (basis valuta)
            decimal dkkAmount;
            if (fromCurrency == "DKK")
            {
                dkkAmount = amount;
            }
            else
            {
                dkkAmount = amount * (exchangeRates[fromCurrency] / 100m);
            }

            // Omregn fra DKK til målvaluta
            if (toCurrency == "DKK")
            {
                return dkkAmount;
            }
            else
            {
                return dkkAmount * 100m / exchangeRates[toCurrency];
            }
        }

        private static void DisplayAvailableCurrencies()
        {
            Console.WriteLine("\nTILGÆNGELIGE VALUTAER");

            var currencies = exchangeRates.Keys.OrderBy(c => c).ToList();

            for (int i = 0; i < currencies.Count; i++)
            {
                Console.Write($"{currencies[i],-8}");
                if ((i + 1) % 6 == 0)
                    Console.WriteLine();
            }

            if (currencies.Count % 6 != 0)
                Console.WriteLine();

            Console.WriteLine($"\nTotal: {currencies.Count} valutaer");
        }

        private static void DisplayExchangeRates()
        {
            Console.WriteLine("\nAKTUELLE VALUTAKURSER");
            Console.WriteLine($"Opdateret: {lastUpdated:dd-MM-yyyy HH:mm:ss}");
            Console.WriteLine($"{"Valuta",-8} {"Beskrivelse",-25} {"Kurs",-15}");

            // Hent beskrivelser fra XML hvis muligt
            var sortedRates = exchangeRates
                .Where(kvp => kvp.Key != "DKK")
                .OrderBy(kvp => kvp.Key);

            foreach (var rate in sortedRates)
            {
                string description = GetCurrencyDescription(rate.Key);
                Console.WriteLine($"{rate.Key,-8} {description,-25} {rate.Value,15:N4}");
            }
        }

        private static string GetCurrencyDescription(string currencyCode)
        {
            var descriptions = new Dictionary<string, string>
            {
                {"USD", "Amerikanske dollar"},
                {"EUR", "Euro"},
                {"GBP", "Britiske pund"},
                {"SEK", "Svenske kroner"},
                {"NOK", "Norske kroner"},
                {"CHF", "Schweiziske franc"},
                {"JPY", "Japanske yen"},
                {"AUD", "Australske dollar"},
                {"CAD", "Canadiske dollar"},
                {"CNY", "Kinesiske Yuan"},
                {"INR", "Indiske rupee"},
                {"BRL", "Brasilianske real"},
                {"MXN", "Mexicanske peso"},
                {"ZAR", "Sydafrikanske rand"},
                {"KRW", "Sydkoreanske won"},
                {"SGD", "Singapore dollar"},
                {"HKD", "Hongkong dollar"},
                {"NZD", "New Zealandske dollar"},
                {"THB", "Thailandske baht"},
                {"MYR", "Malaysiske ringgit"},
                {"PHP", "Filippinske peso"},
                {"IDR", "Indonesiske rupiah"},
                {"TRY", "Tyrkiske lira"},
                {"PLN", "Polske zloty"},
                {"CZK", "Tjekkiske koruna"},
                {"HUF", "Ungarske forint"},
                {"RON", "Rumænske lei"},
                {"BGN", "Bulgarske lev"},
                {"HRK", "Kroatiske kuna"},
                {"RUB", "Russiske rubel"},
                {"ISK", "Islandske kroner"},
                {"ILS", "Israelske shekel"},
                {"XDR", "SDR (Beregnet)"}
            };

            return descriptions.TryGetValue(currencyCode, out string desc) ? desc : "Ukendt valuta";
        }
    }
}