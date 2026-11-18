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
        private TextBlock _lblFormTitle = null!;
        private Button _btnSave = null!;
        private Button _btnCancelEdit = null!;
        private VaultItem? _editingItem = null;

        //Navigation controls for the vault items
        private List<VaultItem> _allItems = new List<VaultItem>();
        private int _currentIndex = -1;
        private Button _btnPrev = null!;
        private Button _btnNext = null!;
        private TextBox _txtCurrentRecord = null!;
        private TextBlock _lblTotalRecords = null!;

        public VaultWindow(User user, byte[] masterKey)
        {
            _currentUser = user;
            _masterKey = masterKey;

            Title = $"My Password Vault - {_currentUser.Email}";
            this.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(
            new Uri("pack://application:,,,/vault.ico", UriKind.RelativeOrAbsolute)
            );
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

            _lblFormTitle = new TextBlock { Text = "New Credential", FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 15) };
            formStack.Children.Add(_lblFormTitle);

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

            _btnSave = new Button
            {
                Content = "Save to Vault",
                Height = 38,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A")),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            _btnSave.Click += BtnSave_Click;
            formStack.Children.Add(_btnSave);

            _btnCancelEdit = new Button
            {
                Content = "Cancel Edit",
                Height = 32,
                Margin = new Thickness(0, 8, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed
            };
            _btnCancelEdit.Click += (s, e) => ResetForm();
            formStack.Children.Add(_btnCancelEdit);

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

            // Barra de navegación entre registros (<< [ 1 ] of [ 10 ] >>)
            var navPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 5)
            };

            _btnPrev = new Button
            {
                Content = "<<",
                Width = 36,
                Height = 28,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            _btnPrev.Click += (s, e) => NavigateToRecord(_currentIndex - 1);

            _txtCurrentRecord = new TextBox
            {
                Text = "0",
                Width = 42,
                Height = 28,
                Margin = new Thickness(8, 0, 8, 0),
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            // Permite escribir un número y dar Enter para saltar directo a ese registro
            _txtCurrentRecord.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && int.TryParse(_txtCurrentRecord.Text, out int target))
                {
                    NavigateToRecord(target - 1);
                }
            };

            var lblOf = new TextBlock
            {
                Text = "of",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 8, 0),
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Gray
            };

            var totalBox = new Border
            {
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Width = 42,
                Height = 28,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"))
            };
            _lblTotalRecords = new TextBlock
            {
                Text = "0",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            totalBox.Child = _lblTotalRecords;

            _btnNext = new Button
            {
                Content = ">>",
                Width = 36,
                Height = 28,
                Margin = new Thickness(8, 0, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            _btnNext.Click += (s, e) => NavigateToRecord(_currentIndex + 1);

            navPanel.Children.Add(_btnPrev);
            navPanel.Children.Add(_txtCurrentRecord);
            navPanel.Children.Add(lblOf);
            navPanel.Children.Add(totalBox);
            navPanel.Children.Add(_btnNext);

            rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(navPanel, 2);
            rightGrid.Children.Add(navPanel);

            _lblStatus = new TextBlock { Text = "Ready. Passwords remain encrypted in RAM.", Foreground = Brushes.Gray, Margin = new Thickness(0, 5, 0, 0), FontSize = 12 };
            Grid.SetRow(_lblStatus, 3);
            rightGrid.Children.Add(_lblStatus);

            rightCard.Child = rightGrid;
            Grid.SetColumn(rightCard, 2);
            mainGrid.Children.Add(rightCard);


            this.Content = mainGrid;
        }

        private async System.Threading.Tasks.Task LoadItemsAsync()
        {
            _listVault.Items.Clear();
            _allItems = await _repo.GetVaultItemsAsync(_currentUser.Id);

            foreach (var item in _allItems)
            {
                var row = new Grid { Margin = new Thickness(0, 5, 0, 5), Width = 440 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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

                var btnEdit = new Button
                {
                    Content = "Edit",
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(0, 0, 5, 0),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706")),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                btnEdit.Click += (s, e) =>
                {
                    int idx = _allItems.IndexOf(item);
                    NavigateToRecord(idx);
                };
                Grid.SetColumn(btnEdit, 2);
                row.Children.Add(btnEdit);

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
                    if (_editingItem?.Id == item.Id) ResetForm();
                    await _repo.DeleteVaultItemAsync(item.Id, _currentUser.Id);
                    await LoadItemsAsync();
                };
                Grid.SetColumn(btnDel, 3);
                row.Children.Add(btnDel);

                _listVault.Items.Add(row);
            }

            // Actualiza el indicador total (ej. 0 of 10)
            _lblTotalRecords.Text = _allItems.Count.ToString();
            if (_allItems.Count == 0)
            {
                _currentIndex = -1;
                _txtCurrentRecord.Text = "0";
            }
            else if (_currentIndex >= _allItems.Count)
            {
                _currentIndex = _allItems.Count - 1;
                _txtCurrentRecord.Text = (_currentIndex + 1).ToString();
            }
            else
            {
                _txtCurrentRecord.Text = (_currentIndex >= 0 ? _currentIndex + 1 : 0).ToString();
            }

            UpdateNavButtons();
        }

        private void NavigateToRecord(int index)
        {
            if (_allItems.Count == 0) return;

            // Limita el índice para no salir del rango (0 hasta total - 1)
            if (index < 0) index = 0;
            if (index >= _allItems.Count) index = _allItems.Count - 1;

            _currentIndex = index;
            _txtCurrentRecord.Text = (_currentIndex + 1).ToString();
            _listVault.SelectedIndex = _currentIndex;
            _listVault.ScrollIntoView(_listVault.SelectedItem);

            StartEditing(_allItems[_currentIndex]);
            UpdateNavButtons();
        }

        private void UpdateNavButtons()
        {
            _btnPrev.IsEnabled = _currentIndex > 0;
            _btnNext.IsEnabled = _allItems.Count > 0 && _currentIndex < _allItems.Count - 1;
        }

        private void StartEditing(VaultItem item)
        {
            _editingItem = item;

            _txtSite.Text = item.SiteName;
            _txtUsername.Text = item.AccountUsername;
            _txtPassword.Text = CryptoService.Decrypt(item.EncryptedPassword, item.Nonce, item.AuthTag, _masterKey);

            _lblFormTitle.Text = "Edit Credential";
            _btnSave.Content = "Update Credential";
            _btnSave.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
            _btnCancelEdit.Visibility = Visibility.Visible;
            _lblStatus.Text = $"Editing {item.SiteName}...";
        }

        private void ResetForm()
        {
            _editingItem = null;
            _txtSite.Clear();
            _txtUsername.Clear();
            _txtPassword.Clear();

            _lblFormTitle.Text = "New Credential";
            _btnSave.Content = "Save to Vault";
            _btnSave.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));
            _btnCancelEdit.Visibility = Visibility.Collapsed;
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_txtSite.Text) || string.IsNullOrWhiteSpace(_txtUsername.Text) || string.IsNullOrEmpty(_txtPassword.Text))
            {
                MessageBox.Show("Fill all the fields.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_editingItem != null)
            {
                await _repo.UpdateVaultItemAsync(
                    _editingItem.Id,
                    _currentUser.Id,
                    _txtSite.Text.Trim(),
                    _editingItem.SiteUrl,
                    _txtUsername.Text.Trim(),
                    _txtPassword.Text,
                    _masterKey
                );

                _lblStatus.Text = $"Credential for {_txtSite.Text.Trim()} re-encrypted and updated.";
                ResetForm();
            }
            else
            {
                await _repo.AddVaultItemAsync(_currentUser.Id, _txtSite.Text.Trim(), null, _txtUsername.Text.Trim(), _txtPassword.Text, _masterKey);

                _txtSite.Clear();
                _txtUsername.Clear();
                _txtPassword.Clear();
                _lblStatus.Text = "Credential encrypted with AES-256-GCM and saved to PostgreSQL.";
            }

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