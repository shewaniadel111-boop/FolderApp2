using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace FolderApp
{
    public class AppItem
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
    }

    public class Folder
    {
        public string Name { get; set; } = "";
        public List<AppItem> Items { get; set; } = new();
    }

    public partial class MainWindow : Window
    {
        private static readonly string SavePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FolderApp", "folders.json");

        // Starting folders for the very first run only
        private List<Folder> folders = new()
        {
            new Folder { Name = "Games" },
            new Folder { Name = "School" },
            new Folder { Name = "Music" }
        };

        private Folder currentFolder;

        public MainWindow()
        {
            InitializeComponent();
            Load();
            ShowFolders();
        }

        // ---------- saving and loading ----------

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SavePath));
                string json = JsonSerializer.Serialize(
                    folders, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SavePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't save:\n" + ex.Message);
            }
        }

        private void Load()
        {
            if (!File.Exists(SavePath)) return;

            try
            {
                string json = File.ReadAllText(SavePath);

                try
                {
                    // New format: a list of folders
                    var loaded = JsonSerializer.Deserialize<List<Folder>>(json);
                    if (loaded != null) folders = loaded;
                }
                catch (JsonException)
                {
                    // Old format: a dictionary of name -> apps. Convert it.
                    File.Copy(SavePath, SavePath + ".bak", true);   // safety copy
                    var old = JsonSerializer.Deserialize<Dictionary<string, List<AppItem>>>(json);
                    if (old != null)
                    {
                        folders = old.Select(pair => new Folder
                        {
                            Name = pair.Key,
                            Items = pair.Value ?? new List<AppItem>()
                        }).ToList();
                        Save();
                    }
                }
            }
            catch
            {
                // Unreadable file: start with the default folders
            }

            foreach (Folder folder in folders)
                folder.Items ??= new List<AppItem>();
        }

        // ---------- the folder tiles on the main screen ----------

        private void ShowFolders()
        {
            FoldersPanel.Children.Clear();

            foreach (Folder folder in folders)
            {
                var tile = new Button
                {
                    Style = (Style)FindResource("TileButton"),
                    Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x40)),
                    Content = new TextBlock
                    {
                        Text = folder.Name,
                        FontSize = 14,
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        MaxWidth = 76
                    },
                    Tag = folder,
                    Width = 90,
                    Height = 90,
                    Margin = new Thickness(10)
                };
                tile.Click += Folder_Click;

                var rename = new MenuItem { Header = "Rename", Tag = folder };
                rename.Click += RenameFolder_Click;
                var delete = new MenuItem { Header = "Delete", Tag = folder };
                delete.Click += DeleteFolder_Click;
                var menu = new ContextMenu();
                menu.Items.Add(rename);
                menu.Items.Add(delete);
                tile.ContextMenu = menu;

                FoldersPanel.Children.Add(tile);
            }

            // The "+" tile at the end
            var addTile = new Button
            {
                Style = (Style)FindResource("TileButton"),
                Content = "+",
                FontSize = 32,
                Width = 90,
                Height = 90,
                Margin = new Thickness(10)
            };
            addTile.Click += NewFolder_Click;
            FoldersPanel.Children.Add(addTile);
        }

        private bool NameTaken(string name, Folder ignore = null)
        {
            return folders.Any(f => f != ignore &&
                f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            string name = AskName("New folder", "");
            if (name == null) return;

            if (NameTaken(name))
            {
                MessageBox.Show("A folder with that name already exists.");
                return;
            }

            folders.Add(new Folder { Name = name });
            Save();
            ShowFolders();
        }

        private void RenameFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = (Folder)((MenuItem)sender).Tag;
            string newName = AskName("Rename folder", folder.Name);
            if (newName == null || newName == folder.Name) return;

            if (NameTaken(newName, folder))
            {
                MessageBox.Show("A folder with that name already exists.");
                return;
            }

            folder.Name = newName;
            Save();
            ShowFolders();
        }

        private void DeleteFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = (Folder)((MenuItem)sender).Tag;

            var answer = MessageBox.Show(
                "Delete \"" + folder.Name + "\" and its " + folder.Items.Count +
                " app shortcut(s)?\n(The programs themselves are not deleted.)",
                "Delete folder", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            folders.Remove(folder);
            Save();
            ShowFolders();
        }

        // A small "type a name" window, built in code
        private string AskName(string title, string initial)
        {
            var box = new TextBox
            {
                Text = initial,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 12)
            };
            var ok = new Button
            {
                Content = "OK",
                IsDefault = true,
                Width = 80,
                Padding = new Thickness(6),
                Margin = new Thickness(0, 0, 8, 0)
            };
            var cancel = new Button
            {
                Content = "Cancel",
                IsCancel = true,
                Width = 80,
                Padding = new Thickness(6)
            };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var layout = new StackPanel { Margin = new Thickness(16) };
            layout.Children.Add(box);
            layout.Children.Add(buttons);

            var window = new Window
            {
                Title = title,
                Content = layout,
                Width = 320,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            ok.Click += (s, args) => window.DialogResult = true;
            window.Loaded += (s, args) => { box.Focus(); box.SelectAll(); };

            if (window.ShowDialog() == true)
            {
                string text = box.Text.Trim();
                if (text.Length > 0) return text;
            }
            return null;
        }

        // ---------- opening and closing a folder ----------

        private void Folder_Click(object sender, RoutedEventArgs e)
        {
            var tile = (Button)sender;
            currentFolder = (Folder)tile.Tag;

            ShowItems();

            Point center = tile.TranslatePoint(
                new Point(tile.ActualWidth / 2, tile.ActualHeight / 2), Root);
            Overlay.RenderTransformOrigin = new Point(
                (center.X - Overlay.Margin.Left) / Overlay.ActualWidth,
                (center.Y - Overlay.Margin.Top) / Overlay.ActualHeight);

            Overlay.IsHitTestVisible = true;
            Animate(1.0, 1.0);
        }

        private void Overlay_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Overlay.IsHitTestVisible = false;
            Animate(0.1, 0.0);
        }

        // ---------- the apps inside a folder ----------

        private void ShowItems()
        {
            ItemsPanel.Children.Clear();
            foreach (AppItem item in currentFolder.Items)
            {
                var content = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                ImageSource icon = GetIcon(item.Path);
                if (icon != null)
                {
                    content.Children.Add(new Image
                    {
                        Source = icon,
                        Width = 48,
                        Height = 48,
                        Margin = new Thickness(0, 0, 0, 6)
                    });
                }

                content.Children.Add(new TextBlock
                {
                    Text = item.Name,
                    FontSize = 12,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 70
                });

                var button = new Button
                {
                    Style = (Style)FindResource("TileButton"),
                    Content = content,
                    Tag = item,
                    Width = 78,
                    Height = 90,
                    Margin = new Thickness(5),
                    ToolTip = item.Name
                };
                button.Click += AppItem_Click;

                var removeItem = new MenuItem { Header = "Remove", Tag = item };
                removeItem.Click += Remove_Click;
                var menu = new ContextMenu();
                menu.Items.Add(removeItem);
                button.ContextMenu = menu;

                ItemsPanel.Children.Add(button);
            }
        }

        private ImageSource GetIcon(string path)
        {
            try
            {
                using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(path))
                {
                    if (icon == null) return null;

                    BitmapSource bitmap = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch
            {
                return null;
            }
        }

        private void AppItem_Click(object sender, RoutedEventArgs e)
        {
            var item = (AppItem)((Button)sender).Tag;

            try
            {
                Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't open " + item.Name + ":\n" + ex.Message);
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose an app or shortcut",
                Filter = "Apps and shortcuts|*.exe;*.lnk|All files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
                currentFolder.Items.Add(new AppItem { Name = name, Path = dialog.FileName });
                Save();
                ShowItems();
            }
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            var item = (AppItem)((MenuItem)sender).Tag;
            currentFolder.Items.Remove(item);
            Save();
            ShowItems();
        }

        // ---------- animation ----------

        private void Animate(double scaleTo, double opacityTo)
        {
            var time = TimeSpan.FromMilliseconds(250);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var scaleAnim = new DoubleAnimation(scaleTo, time) { EasingFunction = ease };
            Scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            Scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            Overlay.BeginAnimation(OpacityProperty, new DoubleAnimation(opacityTo, time));
        }
    }
}