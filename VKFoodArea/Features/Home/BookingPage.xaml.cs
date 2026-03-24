using System.Collections.ObjectModel;
using System.Collections.Specialized;
using VKFoodArea.Models;
using VKFoodArea.Repositories;

namespace VKFoodArea.Features.Home;

public partial class BookingPage : ContentPage
{
    private readonly Poi _poi;
    private readonly FoodRepository _foodRepository;
    private bool _isLoaded;

    public Poi Poi => _poi;

    public ObservableCollection<FoodItem> FoodItems { get; } = new();
    public ObservableCollection<FoodItem> SelectedFoods { get; } = new();

    public bool IsMenuEmpty => FoodItems.Count == 0;

    public string PhoneDisplay =>
        string.IsNullOrWhiteSpace(Poi.PhoneNumber)
            ? "Chưa cập nhật số điện thoại"
            : $"Liên hệ: {Poi.PhoneNumber}";

    public string SelectedFoodsSummary =>
        SelectedFoods.Count == 0
            ? "Chưa chọn món nào"
            : $"{SelectedFoods.Count} món • Tổng tạm tính: {SelectedFoods.Sum(x => x.Price):N0} đ";

    public BookingPage(Poi poi, FoodRepository foodRepository)
    {
        InitializeComponent();

        _poi = poi;
        _foodRepository = foodRepository;

        BindingContext = this;
        SelectedFoods.CollectionChanged += OnSelectedFoodsChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_isLoaded)
            return;

        _isLoaded = true;
        await LoadFoodsAsync();
    }

    private async Task LoadFoodsAsync()
    {
        FoodItems.Clear();

        var foods = await _foodRepository.GetByRestaurantAsync(_poi.Name);

        foreach (var food in foods)
            FoodItems.Add(food);

        OnPropertyChanged(nameof(IsMenuEmpty));
    }

    private async void OnCallClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_poi.PhoneNumber))
        {
            await DisplayAlert("Thông báo", "Quán này chưa cập nhật số điện thoại.", "OK");
            return;
        }

        try
        {
            PhoneDialer.Default.Open(_poi.PhoneNumber);
        }
        catch
        {
            await DisplayAlert("Lỗi", "Không thể gọi điện trên thiết bị này.", "OK");
        }
    }

    private void OnAddFoodClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn)
            return;

        if (btn.CommandParameter is not FoodItem food)
            return;

        SelectedFoods.Add(food);
    }

    private void OnRemoveFoodClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn)
            return;

        if (btn.CommandParameter is not FoodItem food)
            return;

        SelectedFoods.Remove(food);
    }

    private void OnSelectedFoodsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedFoodsSummary));
    }

    private void OnGuestChanged(object sender, ValueChangedEventArgs e)
    {
        GuestLabel.Text = $"Số người: {(int)e.NewValue}";
    }

    private async void OnConfirmBookingClicked(object sender, EventArgs e)
    {
        var customerName = NameEntry.Text?.Trim() ?? string.Empty;
        var customerPhone = PhoneEntry.Text?.Trim() ?? string.Empty;
        var guestCount = (int)GuestStepper.Value;
        var selectedDate = DatePicker.Date ?? DateTime.Today;
        var bookingDateTime = selectedDate.Date + TimePicker.Time;
        var totalPrice = SelectedFoods.Sum(x => x.Price);

        if (string.IsNullOrWhiteSpace(customerName))
        {
            await DisplayAlert("Thiếu thông tin", "Vui lòng nhập tên người đặt.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(customerPhone))
        {
            await DisplayAlert("Thiếu thông tin", "Vui lòng nhập số điện thoại.", "OK");
            return;
        }

        await DisplayAlert(
            "Đặt bàn thành công",
            $"Quán: {_poi.Name}\n" +
            $"Người đặt: {customerName}\n" +
            $"SĐT: {customerPhone}\n" +
            $"Thời gian: {bookingDateTime:dd/MM/yyyy HH:mm}\n" +
            $"Số người: {guestCount}\n" +
            $"Số món đã chọn: {SelectedFoods.Count}\n" +
            $"Tổng tạm tính: {totalPrice:N0} đ",
            "OK");
    }
}