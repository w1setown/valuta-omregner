using CurrencyConverterApp.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace CurrencyConverterAndroid;

public partial class MainPage : ContentPage
{
    private readonly CurrencyService currencyService = new();
    private bool isLoading = false;

    // Data collections for the UI
    private ObservableCollection<CurrencyInfo> allCurrencies = new();
    private ObservableCollection<CurrencyInfo> filteredCurrencies = new();
    private ObservableCollection<RateInfo> allRates = new();
    private ObservableCollection<RateInfo> filteredRates = new();

    // Current active tab
    private string currentTab = "converter";

    public MainPage()
    {
        InitializeComponent();
        CurrenciesCollectionView.ItemsSource = filteredCurrencies;
        RatesCollectionView.ItemsSource = filteredRates;
        LoadCurrencies();
    }

    private async void LoadCurrencies()
    {
        if (isLoading) return;
        isLoading = true;

        UpdateStatus("Opdaterer...", "");
        SetTabEnabled(false);

        try
        {
            if (await currencyService.FetchExchangeRatesAsync())
            {
                var currencies = currencyService.GetAvailableCurrencies();
                var descriptions = currencyService.GetCurrencyDescriptions();

                // Setup pickers
                FromPicker.ItemsSource = currencies;
                ToPicker.ItemsSource = currencies;

                // Set default selections
                if (currencies.Contains("DKK"))
                    FromPicker.SelectedItem = "DKK";
                if (currencies.Contains("EUR"))
                    ToPicker.SelectedItem = "EUR";

                // Setup currency collection
                allCurrencies.Clear();
                foreach (var currency in currencies)
                {
                    allCurrencies.Add(new CurrencyInfo
                    {
                        Code = currency,
                        Description = descriptions.TryGetValue(currency, out string? desc) ? desc : "Ukendt valuta"
                    });
                }

                // Setup rates collection
                var rates = currencyService.GetAllExchangeRates();
                allRates.Clear();
                foreach (var rate in rates.Where(r => r.Key != "DKK").OrderBy(r => r.Key))
                {
                    var desc = descriptions.TryGetValue(rate.Key, out string? description) ? description : "Ukendt valuta";
                    allRates.Add(new RateInfo
                    {
                        Code = rate.Key,
                        Description = desc,
                        Rate = rate.Value.ToString("N4"),
                        RateDescription = $"1 DKK = {(100m / rate.Value):N4} {rate.Key}"
                    });
                }

                // Update filtered collections
                RefreshFilteredCollections();

                UpdatedLabel.Text = $"Opdateret: {currencyService.LastUpdated:dd-MM-yyyy HH:mm:ss}";
                RatesDateLabel.Text = $"Kurser fra: {currencyService.LastUpdateDate}";
                UpdateStatus("Klar", $"{currencies.Count} valutaer");
            }
            else
            {
                await DisplayAlert("Fejl", "Kunne ikke hente valutakurser fra Danmarks Nationalbank", "OK");
                UpdateStatus("Fejl", "");
            }
        }
        finally
        {
            isLoading = false;
            SetTabEnabled(true);
        }
    }

    private void UpdateStatus(string status, string info)
    {
        StatusLabel.Text = status;
        CurrencyCountLabel.Text = info;
    }

    private void SetTabEnabled(bool enabled)
    {
        RefreshTabButton.IsEnabled = enabled;
        RefreshTabButton.Text = enabled ? "Opdater" : "Venter...";
    }

    private void RefreshFilteredCollections()
    {
        // Update currencies
        filteredCurrencies.Clear();
        var currencySearch = CurrencySearchEntry?.Text?.ToLower() ?? "";
        var currenciesToShow = string.IsNullOrEmpty(currencySearch)
            ? allCurrencies
            : allCurrencies.Where(c => c.Code.ToLower().Contains(currencySearch) ||
                                      c.Description.ToLower().Contains(currencySearch));

        foreach (var currency in currenciesToShow)
            filteredCurrencies.Add(currency);

        // Update rates
        filteredRates.Clear();
        var rateSearch = RateSearchEntry?.Text?.ToLower() ?? "";
        var ratesToShow = string.IsNullOrEmpty(rateSearch)
            ? allRates
            : allRates.Where(r => r.Code.ToLower().Contains(rateSearch) ||
                                 r.Description.ToLower().Contains(rateSearch));

        foreach (var rate in ratesToShow)
            filteredRates.Add(rate);
    }

    // Tab Navigation
    private void OnConverterTabClicked(object sender, EventArgs e)
    {
        SetActiveTab("converter");
    }

    private void OnCurrenciesTabClicked(object sender, EventArgs e)
    {
        SetActiveTab("currencies");
        RefreshFilteredCollections();
    }

    private void OnRatesTabClicked(object sender, EventArgs e)
    {
        SetActiveTab("rates");
        RefreshFilteredCollections();
    }

    private void OnRefreshTabClicked(object sender, EventArgs e)
    {
        LoadCurrencies();
    }

    private void SetActiveTab(string tabName)
    {
        currentTab = tabName;

        // Hide all views
        ConverterView.IsVisible = false;
        CurrenciesView.IsVisible = false;
        RatesView.IsVisible = false;

        // Reset tab button colors
        ResetTabButtonColors();

        // Show selected view and highlight button
        switch (tabName)
        {
            case "converter":
                ConverterView.IsVisible = true;
                SetActiveTabButton(ConverterTabButton);
                break;
            case "currencies":
                CurrenciesView.IsVisible = true;
                SetActiveTabButton(CurrenciesTabButton);
                break;
            case "rates":
                RatesView.IsVisible = true;
                SetActiveTabButton(RatesTabButton);
                break;
        }
    }

    private void ResetTabButtonColors()
    {
        var inactiveColor = Color.FromArgb("#E5E7EB");
        var inactiveTextColor = Color.FromArgb("#6B7280");

        ConverterTabButton.BackgroundColor = inactiveColor;
        ConverterTabButton.TextColor = inactiveTextColor;

        CurrenciesTabButton.BackgroundColor = inactiveColor;
        CurrenciesTabButton.TextColor = inactiveTextColor;

        RatesTabButton.BackgroundColor = inactiveColor;
        RatesTabButton.TextColor = inactiveTextColor;
    }

    private void SetActiveTabButton(Button button)
    {
    // Use the same brown/cream theme as the default
    button.BackgroundColor = Color.FromArgb("#8B6F47"); // brown
    button.TextColor = Color.FromArgb("#FBF8F1"); // cream
    }

    // Search functionality
    private void OnCurrencySearchChanged(object sender, TextChangedEventArgs e)
    {
        RefreshFilteredCollections();
    }

    private void OnRateSearchChanged(object sender, TextChangedEventArgs e)
    {
        RefreshFilteredCollections();
    }

    // Currency conversion
    private void OnConvertClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AmountEntry.Text))
        {
            ShowError("Indtast venligst et beløb");
            return;
        }

        var amountText = AmountEntry.Text.Replace(',', '.');

        if (!decimal.TryParse(amountText, System.Globalization.NumberStyles.Float,
                             System.Globalization.CultureInfo.InvariantCulture, out decimal amount))
        {
            ShowError("Indtast venligst et gyldigt beløb");
            return;
        }

        if (amount <= 0)
        {
            ShowError("Beløbet skal være større end 0");
            return;
        }

        if (FromPicker.SelectedItem is not string from)
        {
            ShowError("Vælg venligst fra-valuta");
            return;
        }

        if (ToPicker.SelectedItem is not string to)
        {
            ShowError("Vælg venligst til-valuta");
            return;
        }

        var result = currencyService.ConvertAmount(amount, from, to);
        if (result.HasValue)
        {
            ResultLabel.Text = $"{amount:N2} {from} = {result.Value:N2} {to}";

            var rate = currencyService.ConvertAmount(1, from, to);
            if (rate.HasValue)
            {
                ExchangeRateLabel.Text = $"Kurs: 1 {from} = {rate.Value:N4} {to}";
            }

            ResultSection.IsVisible = true;
        }
        else
        {
            ShowError("Kunne ikke omregne valuta. Kontroller at begge valutaer er gyldige.");
        }
    }

    private void OnSwapClicked(object sender, EventArgs e)
    {
        if (FromPicker.SelectedItem != null && ToPicker.SelectedItem != null)
        {
            var temp = FromPicker.SelectedItem;
            FromPicker.SelectedItem = ToPicker.SelectedItem;
            ToPicker.SelectedItem = temp;

            if (!string.IsNullOrWhiteSpace(AmountEntry.Text))
            {
                OnConvertClicked(this, e);
            }
        }
    }

    private async void ShowError(string message)
    {
        await DisplayAlert("Fejl", message, "OK");
    }
}

// Data models for the collections
public class CurrencyInfo
{
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
}

public class RateInfo
{
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public string Rate { get; set; } = "";
    public string RateDescription { get; set; } = "";
}