using PasswordVaultApp.Data;
using PasswordVaultApp.Models;
using PasswordVaultApp.Security;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PasswordVaultApp
{
    public class VaultWindow : Window
    {
        private readonly User _currentUser;
        private readonly byte[] _masterKey;
        private readonly VaultRepository _repo = new VaultRepository();

        private TextBox _txtSite = null!;
        private TextBox _txtUsername = null!;
        private TextBox _txtPassword = null!;
        private ListBox _listVault = null!;
        private TextBlock _lblStatus = null!;

        public VaultWindow(User user, byte[] masterKey)
        {
            _currentUser = user;
            _masterKey = masterKey;

            Title = $"My Password Vault - {_currentUser.Email}";
            Width = 750;
            Height = 520;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowState=WindowState.Maximized;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F6F9"));

          

            //Clear the master key from RAM when the window is closed
            this.Closed += (s, e) => Array.Clear(_masterKey, 0, _masterKey.Length);

            BuildUI();
            _ = LoadItemsAsync();
        }

        private void BuildUI()
        {
            var mainGrid = new Grid { Margin = new Thickness(20) };
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // PANEL IZQUIERDO: Agregar nueva contraseña
            var leftCard = new Border
            {
                Background = Brushes.White,
                Padding = new Thickness(20),
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E4E8")),
                BorderThickness = new Thickness(1)
            };

            var formStack = new StackPanel();
            formStack.Children.Add(new TextBlock { Text = "New Credential", FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 15) });

            formStack.Children.Add(new TextBlock { Text = "Site / App Name:", FontWeight = FontWeights.SemiBold });
            _txtSite = new TextBox { Height = 30, Padding = new Thickness(5), Margin = new Thickness(0, 4, 0, 10) };
            formStack.Children.Add(_txtSite);

            formStack.Children.Add(new TextBlock { Text = "Username / Email:", FontWeight = FontWeights.SemiBold });
            _txtUsername = new TextBox { Height = 30, Padding = new Thickness(5), Margin = new Thickness(0, 4, 0, 10) };
            formStack.Children.Add(_txtUsername);

            formStack.Children.Add(new TextBlock { Text = "Password:", FontWeight = FontWeights.SemiBold });
            _txtPassword = new TextBox { Height = 30, Padding = new Thickness(5), Margin = new Thickness(0, 4, 0, 8) };
            formStack.Children.Add(_txtPassword);

            var btnGenerate = new Button
            {
                Content = "Generate Strong Password",
                Height = 30,
                Margin = new Thickness(0, 0, 0, 15),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnGenerate.Click += (s, e) => _txtPassword.Text = GenerateRandomPassword(18);
            formStack.Children.Add(btnGenerate);

            var btnSave = new Button
            {
                Content = "Save to Vault",
                Height = 38,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A")),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnSave.Click += BtnSave_Click;
            formStack.Children.Add(btnSave);

            leftCard.Child = formStack;
            Grid.SetColumn(leftCard, 0);
            mainGrid.Children.Add(leftCard);

            // PANEL DERECHO: Lista de contraseñas guardadas
            var rightCard = new Border
            {
                Background = Brushes.White,
                Padding = new Thickness(20),
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E4E8")),
                BorderThickness = new Thickness(1)
            };

            var rightGrid = new Grid();
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new TextBlock { Text = "Saved Accounts", FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(header, 0);
            rightGrid.Children.Add(header);

            _listVault = new ListBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent };
            Grid.SetRow(_listVault, 1);
            rightGrid.Children.Add(_listVault);

            _lblStatus = new TextBlock { Text = "Ready. Passwords remain encrypted in RAM.", Foreground = Brushes.Gray, Margin = new Thickness(0, 10, 0, 0), FontSize = 12 };
            Grid.SetRow(_lblStatus, 2);
            rightGrid.Children.Add(_lblStatus);

            rightCard.Child = rightGrid;
            Grid.SetColumn(rightCard, 2);
            mainGrid.Children.Add(rightCard);

            this.Content = mainGrid;
        }

        private async System.Threading.Tasks.Task LoadItemsAsync()
        {
            _listVault.Items.Clear();
            List<VaultItem> items = await _repo.GetVaultItemsAsync(_currentUser.Id);

            foreach (var item in items)
            {
                var row = new Grid { Margin = new Thickness(0, 5, 0, 5), Width = 380 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var info = new StackPanel();
                info.Children.Add(new TextBlock { Text = item.SiteName, FontWeight = FontWeights.Bold, FontSize = 14 });
                info.Children.Add(new TextBlock { Text = item.AccountUsername, Foreground = Brushes.Gray, FontSize = 12 });
                Grid.SetColumn(info, 0);
                row.Children.Add(info);

                var btnCopy = new Button
                {
                    Content = "Copy Pass",
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(5, 0, 5, 0),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                btnCopy.Click += (s, e) => CopyDecryptedPassword(item);
                Grid.SetColumn(btnCopy, 1);
                row.Children.Add(btnCopy);

                var btnDel = new Button
                {
                    Content = "Delete",
                    Padding = new Thickness(8, 5, 8, 5),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                btnDel.Click += async (s, e) =>
                {
                    await _repo.DeleteVaultItemAsync(item.Id, _currentUser.Id);
                    await LoadItemsAsync();
                };
                Grid.SetColumn(btnDel, 2);
                row.Children.Add(btnDel);

                _listVault.Items.Add(row);
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_txtSite.Text) || string.IsNullOrWhiteSpace(_txtUsername.Text) || string.IsNullOrEmpty(_txtPassword.Text))
            {
                MessageBox.Show("Fill all the fields.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await _repo.AddVaultItemAsync(_currentUser.Id, _txtSite.Text.Trim(), null, _txtUsername.Text.Trim(), _txtPassword.Text, _masterKey);

            _txtSite.Clear();
            _txtUsername.Clear();
            _txtPassword.Clear();
            _lblStatus.Text = "Credential encrypted with AES-256-GCM and saved to PostgreSQL.";
            await LoadItemsAsync();
        }

        private void CopyDecryptedPassword(VaultItem item)
        {
            string plainPassword = CryptoService.Decrypt(item.EncryptedPassword, item.Nonce, item.AuthTag, _masterKey);
            Clipboard.SetText(plainPassword);
            _lblStatus.Text = $"Password for {item.SiteName} copied! Clipboard will clear in 15 seconds.";

            // Temporizador de seguridad para borrar el portapapeles tras 15 segundos
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            timer.Tick += (s, e) =>
            {
            timer.Stop();
                if (Clipboard.GetText() == plainPassword)
                {
                    Clipboard.Clear();
                    _lblStatus.Text = "Clipboard cleared for security.";
                }
            };
            timer.Start();
        }

        private static string GenerateRandomPassword(int length)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*_-+=";
            return RandomNumberGenerator.GetString(chars, length);
        }
    }
}