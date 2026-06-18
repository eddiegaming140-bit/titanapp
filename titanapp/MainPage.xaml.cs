using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;

namespace titanapp;

public partial class MainPage : ContentPage
{
    public struct HrItem
    {
        public string Id { get; set; }
        public string Timestamp { get; set; }
        public string Address { get; set; }
        public string Description { get; set; }
    }

    private readonly Color ActiveBlue = Color.FromArgb("#00467C");
    private readonly Color NormalGreen = Color.FromArgb("#64895A");
    private readonly Color RequestYellow = Color.FromArgb("#B8AA0C");

    private IDispatcherTimer? _timer;
    private IDispatcherTimer? _mapUpdateTimer;
    private CancellationTokenSource? _anropsTokenSource;
    private bool _isWaitingForConfirmation = false;
    private double _currentHeading = 0;

    private List<HrItem> _hrDatabase = new List<HrItem>();
    private int _currentHrIndex = 0;

    private Location? _lastLocation;
    private DateTime? _lastLocationTime;

    public MainPage()
    {
        InitializeComponent();
        SeedInitialHrData();
        SetRightPanelUI("Rakel"); // Default tab
        StartClock();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status == PermissionStatus.Granted)
            {
                StartSensors();
                StartLocationTracking();
            }
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine("Location permissions exception handled safely.");
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width > 0 && height > 0)
        {
            double designWidth = 1180;
            double designHeight = 820;
            double scale = Math.Min(width / designWidth, height / designHeight);

            MainContainer.Scale = scale;
            MainContainer.WidthRequest = designWidth;
            MainContainer.HeightRequest = designHeight;
        }
    }

    private void StartSensors()
    {
        if (Compass.Default.IsSupported && !Compass.Default.IsMonitoring)
        {
            Compass.Default.ReadingChanged += (s, e) =>
            {
                _currentHeading = e.Reading.HeadingMagneticNorth;
            };
            Compass.Default.Start(SensorSpeed.UI);
        }
    }

    private async void StartLocationTracking()
    {
        try
        {
            Geolocation.Default.LocationChanged += Geolocation_LocationChanged;
            var request = new GeolocationListeningRequest(GeolocationAccuracy.Best, TimeSpan.FromMilliseconds(250));
            await Geolocation.Default.StartListeningForegroundAsync(request);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GPS Initialisation Error: {ex.Message}");
        }

        _mapUpdateTimer = Dispatcher.CreateTimer();
        _mapUpdateTimer.Interval = TimeSpan.FromMilliseconds(200);
        _mapUpdateTimer.Tick += async (s, e) =>
        {
            if (_lastLocation != null)
            {
                string lat = _lastLocation.Latitude.ToString(CultureInfo.InvariantCulture);
                string lng = _lastLocation.Longitude.ToString(CultureInfo.InvariantCulture);
                string heading = _currentHeading.ToString(CultureInfo.InvariantCulture);

                // Update Left Map (3D Nav)
                string jsMain = $"updateUserLocation({lat}, {lng}, {heading});";
                await MapWebView.EvaluateJavaScriptAsync(jsMain);

                // Update Right Map (2D Tactical)
                string jsOverview = $"if (typeof updateOverviewLocation === 'function') updateOverviewLocation({lat}, {lng});";
                await OverviewMapWebView.EvaluateJavaScriptAsync(jsOverview);
            }
        };
        _mapUpdateTimer.Start();
    }

    private void Geolocation_LocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
    {
        var location = e.Location;
        double speedKmh = 0;

        if (location.Speed.HasValue)
        {
            speedKmh = location.Speed.Value * 3.6;
        }
        else if (_lastLocation != null && _lastLocationTime.HasValue)
        {
            double distance = Location.CalculateDistance(_lastLocation, location, DistanceUnits.Kilometers);
            double timeElapsedHours = (DateTime.Now - _lastLocationTime.Value).TotalHours;

            if (timeElapsedHours > 0 && distance > 0.0005)
            {
                speedKmh = distance / timeElapsedHours;
            }
        }

        _lastLocation = location;
        _lastLocationTime = DateTime.Now;

        Dispatcher.Dispatch(() =>
        {
            CurrentSpeedLabel.Text = Math.Round(speedKmh).ToString();
        });
    }

    private void OnHemClicked(object? sender, EventArgs e) => SetRightPanelUI("Rakel");
    private void OnNavigationClicked(object? sender, EventArgs e) => SetRightPanelUI("Navigation");

    private void OnRakelClicked(object? sender, EventArgs e) => SetRightPanelUI("Rakel");
    private void OnHrClicked(object? sender, EventArgs e) => SetRightPanelUI("HR");
    private void OnKartaClicked(object? sender, EventArgs e) => SetRightPanelUI("Karta");

    private void SetRightPanelUI(string activeTab)
    {
        bool showRakel = activeTab == "Rakel";
        bool showHr = activeTab == "HR";
        bool showKarta = activeTab == "Karta";

        // View Toggles
        HrPageOverlay.IsVisible = showHr;
        KartaPageOverlay.IsVisible = showKarta;

        RakelFrame.IsVisible = showRakel;
        status_btn.IsVisible = showRakel;
        port_btn.IsVisible = showRakel;
        sds_btn.IsVisible = showRakel;
        mer_btn.IsVisible = showRakel;
        insats_btn.IsVisible = showRakel;
        raps_btn.IsVisible = showRakel;
        sok_btn.IsVisible = showRakel;
        index_btn.IsVisible = showRakel;
        foregaende_btn.IsVisible = showRakel;
        normal_btn.IsVisible = showRakel;
        lamna_btn.IsVisible = showRakel;
        paborj_btn.IsVisible = showRakel;
        anrops_btn.IsVisible = showRakel;

        // Bottom border tracking colors
        if (rakel_border != null) rakel_border.BackgroundColor = showRakel ? ActiveBlue : Colors.Transparent;
        if (hr_border != null) hr_border.BackgroundColor = showHr ? ActiveBlue : Colors.Transparent;
        if (karta_border != null) karta_border.BackgroundColor = showKarta ? ActiveBlue : Colors.Transparent;
    }

    private async void OnRecenterMapClicked(object? sender, EventArgs e)
    {
        try
        {
            await MapWebView.EvaluateJavaScriptAsync("recenterMap();");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Map Recenter Error: {ex.Message}");
        }
    }

    private async void OnAnropsbegaranClicked(object? sender, EventArgs e)
    {
        if (!_isWaitingForConfirmation)
        {
            _isWaitingForConfirmation = true;
            anrops_btn.BackgroundColor = RequestYellow;
            anrops_label.Text = "Skicka anropsbegäran";
            _anropsTokenSource = new CancellationTokenSource();
            try
            {
                await Task.Delay(3000, _anropsTokenSource.Token);
                ResetAnropsButton();
            }
            catch (TaskCanceledException) { }
        }
        else
        {
            _anropsTokenSource?.Cancel();
            _isWaitingForConfirmation = false;
            anrops_btn.BackgroundColor = NormalGreen;
            anrops_label.Text = "Anropsbegäran skickas";
            await Task.Delay(2000);
            ResetAnropsButton();
        }
    }

    private void ResetAnropsButton()
    {
        _isWaitingForConfirmation = false;
        anrops_btn.BackgroundColor = NormalGreen;
        anrops_label.Text = "Anropsbegäran";
    }

    private void SeedInitialHrData()
    {
        _hrDatabase.Add(new HrItem
        {
            Id = "240000101900051",
            Timestamp = "2025-10-02, 13:01:20",
            Address = "Ica Maxi, Grafiska Vägen 16, Krokslätt, Göteborg",
            Description = "stöld I Butik, Stöld I Butik"
        });
        UpdateHrDisplayFields();
    }

    private void OnMenyClicked(object? sender, EventArgs e) => CreateHrPopup.IsVisible = true;

    private void OnClosePopupClicked(object? sender, EventArgs e)
    {
        PopupAddressInput.Text = string.Empty;
        PopupDescriptionInput.Text = string.Empty;
        CreateHrPopup.IsVisible = false;
    }

    private void OnSubmitHrClicked(object? sender, EventArgs e)
    {
        string addressStr = string.IsNullOrWhiteSpace(PopupAddressInput.Text) ? "Okänd Adress" : PopupAddressInput.Text;
        string descStr = string.IsNullOrWhiteSpace(PopupDescriptionInput.Text) ? "Ingen beskrivning tillgänglig." : PopupDescriptionInput.Text;

        Random rand = new Random();
        string generatedId = $"240000{rand.Next(100000, 999999)}00051";
        string currentTimestamp = DateTime.Now.ToString("yyyy-MM-dd, HH:mm:ss");

        _hrDatabase.Add(new HrItem
        {
            Id = generatedId,
            Timestamp = currentTimestamp,
            Address = addressStr,
            Description = descStr
        });

        _currentHrIndex = _hrDatabase.Count - 1;
        UpdateHrDisplayFields();
        OnClosePopupClicked(this, EventArgs.Empty);
        SetRightPanelUI("HR");
    }

    private void UpdateHrDisplayFields()
    {
        if (_hrDatabase.Count == 0) return;
        var selectedItem = _hrDatabase[_currentHrIndex];
        HrIdLabel.Text = $"Från: {selectedItem.Id}";
        HrTimeLabel.Text = selectedItem.Timestamp;
        HrDescriptionLabel.Text = selectedItem.Description;

        if (selectedItem.Address.Contains(","))
        {
            var parts = selectedItem.Address.Split(new[] { ',' }, 2);
            HrAddressLabel1.Text = parts[0].Trim() + ",";
            HrAddressLabel2.Text = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        }
        else
        {
            HrAddressLabel1.Text = selectedItem.Address;
            HrAddressLabel2.Text = string.Empty;
        }
        HrPaginationLabel.Text = $"{_currentHrIndex + 1}/{_hrDatabase.Count}";
    }

    private void OnHrUpClicked(object? sender, EventArgs e)
    {
        if (_currentHrIndex > 0)
        {
            _currentHrIndex--;
            UpdateHrDisplayFields();
        }
    }

    private void OnHrDownClicked(object? sender, EventArgs e)
    {
        if (_currentHrIndex < _hrDatabase.Count - 1)
        {
            _currentHrIndex++;
            UpdateHrDisplayFields();
        }
    }

    private void StartClock()
    {
        UpdateTimeAndDate();
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (s, e) => UpdateTimeAndDate();
        _timer.Start();
    }

    private void UpdateTimeAndDate()
    {
        var now = DateTime.Now;
        time_label.Text = now.ToString("HH:mm");
        var culture = new CultureInfo("sv-SE");
        string dateText = now.ToString("ddd d MMM", culture).TrimEnd('.');
        if (!string.IsNullOrEmpty(dateText))
        {
            date_label.Text = char.ToUpper(dateText[0]) + dateText.Substring(1) + ".";
        }
    }
}