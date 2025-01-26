using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WashBotWPF
{
    public partial class SettingsWindow : Window
    {
        public Config CurrentConfig { get; private set; }
        private readonly Dictionary<CheckBox, DayOfWeek> dayCheckBoxes;

        public SettingsWindow(Config config)
        {
            InitializeComponent();
            CurrentConfig = new Config
            {
                Login = config.Login,
                Password = config.Password,
                SiteOpenTime = config.SiteOpenTime,
                BookingTime = config.BookingTime,
                AutoStart = config.AutoStart,
                SaveCredentials = config.SaveCredentials,
                BookingMode = config.BookingMode,
                ScheduledDays = new List<DayOfWeek>(config.ScheduledDays)
            };

            // Инициализация словаря чекбоксов дней недели
            dayCheckBoxes = new Dictionary<CheckBox, DayOfWeek>
            {
                { MondayCheck, DayOfWeek.Monday },
                { TuesdayCheck, DayOfWeek.Tuesday },
                { WednesdayCheck, DayOfWeek.Wednesday },
                { ThursdayCheck, DayOfWeek.Thursday },
                { FridayCheck, DayOfWeek.Friday },
                { SaturdayCheck, DayOfWeek.Saturday },
                { SundayCheck, DayOfWeek.Sunday }
            };

            // Заполняем поля текущими значениями
            LoginTextBox.Text = config.Login;
            PasswordBox.Password = config.Password;
            SaveCredentialsCheckBox.IsChecked = config.SaveCredentials;
            SiteOpenTimeTextBox.Text = config.SiteOpenTime;
            BookingTimeTextBox.Text = config.BookingTime;
            AutostartCheckBox.IsChecked = config.AutoStart;

            // Устанавливаем режим работы
            SingleBookingRadio.IsChecked = config.BookingMode == BookingMode.SingleBooking;
            ScheduledBookingRadio.IsChecked = config.BookingMode == BookingMode.ScheduledBooking;

            // Устанавливаем выбранные дни недели
            foreach (var pair in dayCheckBoxes)
            {
                pair.Key.IsChecked = config.ScheduledDays.Contains(pair.Value);
            }

            // Добавляем обработчики событий
            SingleBookingRadio.Checked += BookingMode_Changed;
            ScheduledBookingRadio.Checked += BookingMode_Changed;
            UpdateDaysGroupBoxState();
        }

        private void BookingMode_Changed(object sender, RoutedEventArgs e)
        {
            UpdateDaysGroupBoxState();
        }

        private void UpdateDaysGroupBoxState()
        {
            bool isScheduledMode = ScheduledBookingRadio.IsChecked ?? false;
            DaysGroupBox.IsEnabled = isScheduledMode;

            // Если режим не по расписанию, снимаем все галочки
            if (!isScheduledMode)
            {
                foreach (var checkBox in dayCheckBoxes.Keys)
                {
                    checkBox.IsChecked = false;
                }
            }
        }

        private bool ValidateSettings()
        {
            // Проверка времени
            if (!TimeSpan.TryParse(SiteOpenTimeTextBox.Text, out TimeSpan openTime) ||
                !TimeSpan.TryParse(BookingTimeTextBox.Text, out TimeSpan bookingTime))
            {
                MessageBox.Show(
                    "Пожалуйста, введите корректное время в формате ЧЧ:мм:сс",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            // Проверка интервала между временем открытия и записи
            if (bookingTime <= openTime)
            {
                MessageBox.Show(
                    "Время записи должно быть позже времени открытия сайта",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (bookingTime - openTime < TimeSpan.FromSeconds(5))
            {
                MessageBox.Show(
                    "Интервал между открытием и записью должен быть не менее 5 секунд",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            // Проверка выбранных дней для режима по расписанию
            if (ScheduledBookingRadio.IsChecked == true &&
                !dayCheckBoxes.Any(pair => pair.Key.IsChecked == true))
            {
                MessageBox.Show(
                    "Выберите хотя бы один день недели для записи по расписанию",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateSettings())
                return;

            // Сохраняем основные настройки
            CurrentConfig.Login = LoginTextBox.Text;
            CurrentConfig.Password = PasswordBox.Password;
            CurrentConfig.SaveCredentials = SaveCredentialsCheckBox.IsChecked ?? false;
            CurrentConfig.SiteOpenTime = SiteOpenTimeTextBox.Text;
            CurrentConfig.BookingTime = BookingTimeTextBox.Text;
            CurrentConfig.AutoStart = AutostartCheckBox.IsChecked ?? false;

            // Сохраняем режим работы
            CurrentConfig.BookingMode = SingleBookingRadio.IsChecked == true
                ? BookingMode.SingleBooking
                : BookingMode.ScheduledBooking;

            // Сохраняем выбранные дни недели
            CurrentConfig.ScheduledDays.Clear();
            foreach (var pair in dayCheckBoxes)
            {
                if (pair.Key.IsChecked == true)
                {
                    CurrentConfig.ScheduledDays.Add(pair.Value);
                }
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TimeTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // Разрешаем только цифры и двоеточие
            e.Handled = !IsTimeCharacter(e.Text);
        }

        private bool IsTimeCharacter(string text)
        {
            return text.All(c => char.IsDigit(c) || c == ':');
        }

        private void TimeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Проверяем и форматируем время
            if (TimeSpan.TryParse(textBox.Text, out TimeSpan time))
            {
                textBox.Text = time.ToString(@"hh\:mm\:ss");
            }
        }
    }
}