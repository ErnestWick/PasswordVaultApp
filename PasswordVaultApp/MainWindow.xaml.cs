using PasswordVaultApp.Data;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PasswordVaultApp
{
    public partial class MainWindow : Window
    {
        private readonly VaultRepository _repo = new VaultRepository();
        private TextBox _txtEmail = null!;
        private PasswordBox _txtMasterPassword = null!;
        private Button _btnLogin = null!;
        private Button _btnRegister = null!;

        public MainWindow()
        {
            InitializeComponent();
            BuildUI();
        }

        private void BuildUI()
        {
            var border = new Border
            {
                Background = Brushes.White,
                Margin = new Thickness(25),
                Padding = new Thickness(25),
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E4E8")),
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            stack.Children.Add(new TextBlock
            {
                Text = "My Password Vault",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"))
            });

            stack.Children.Add(new TextBlock { Text = "Email:", FontWeight = FontWeights.SemiBold });
            _txtEmail = new TextBox { Height = 32, Padding = new Thickness(5), Margin = new Thickness(0, 5, 0, 12), VerticalContentAlignment = VerticalAlignment.Center };
            stack.Children.Add(_txtEmail);

            stack.Children.Add(new TextBlock { Text = "Master Password:", FontWeight = FontWeights.SemiBold });
            _txtMasterPassword = new PasswordBox { Height = 32, Padding = new Thickness(5), Margin = new Thickness(0, 5, 0, 20), VerticalContentAlignment = VerticalAlignment.Center };
            stack.Children.Add(_txtMasterPassword);

            _btnLogin = new Button
            {
                Content = "Unlock Vault",
                Height = 38,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            _btnLogin.Click += BtnLogin_Click;
            stack.Children.Add(_btnLogin);

            _btnRegister = new Button
            {
                Content = "Create Master Account",
                Height = 32,
                Margin = new Thickness(0, 10, 0, 0),
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            _btnRegister.Click += BtnRegister_Click;
            stack.Children.Add(_btnRegister);

            border.Child = stack;
            this.Content = border;
        }

        private async void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            string email = _txtEmail.Text.Trim();
            string password = _txtMasterPassword.Password;

            if (string.IsNullOrWhiteSpace(email) || password.Length < 8)
            {
                
                MessageBox.Show("Please enter a valid email and a master password of at least 8 characters.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _btnRegister.IsEnabled = false;
                bool created = await _repo.RegisterUserAsync(email, password);
                if (created)
                {
                    MessageBox.Show("Master account created successfully. Now click 'Unlock Vault'.", "Registration Successful", MessageBoxButton.OK, MessageBoxImage.Information);

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Registration error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            }
            finally
            {
                _btnRegister.IsEnabled = true;
            }
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string email = _txtEmail.Text.Trim();
            string password = _txtMasterPassword.Password;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please fill in both fields.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);

                return;
            }

            try
            {
                _btnLogin.IsEnabled = false;
                var (user, masterKey) = await _repo.LoginAsync(email, password);

                if (user == null || masterKey == null)
                {
                    MessageBox.Show("Incorrect email or master password.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);

                    return;
                }

                _txtMasterPassword.Clear();
                // MessageBox.Show($"¡Bóveda desbloqueada para {user.Email}!\nLlave AES-256 lista en memoria RAM.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                var vaultWindow = new VaultWindow(user, masterKey);
                vaultWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error of connection: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _btnLogin.IsEnabled = true;
            }
        }
    }
}
