// ./ViewModels/MainWindowViewModel.cs 

using System;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UrlLauncher.Models;

namespace UrlLauncher.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        // 数据相关的定义
        private readonly string _dataFilePath;
        private readonly string _configFilePath;

        [ObservableProperty]
        private ObservableCollection<UrlItem> _urlItems = new();

        [ObservableProperty]
        private string _newName = string.Empty;

        [ObservableProperty]
        private string _newUrl = string.Empty;

        [ObservableProperty]
        private string _newCategory = string.Empty;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<UrlItem> _filteredItems = new();

        [ObservableProperty]
        private string _customBrowserPath = string.Empty;// 输入框绑定的临时路径

        [ObservableProperty]
		private string _currentBrowserPath = string.Empty;// 现在使用的路径
		
		[ObservableProperty]
		private string _currentBrowserDisplay = "None(use Chrome as defaut)";

        // 构造函数
        public MainWindowViewModel()
        {
			string assemblyPath = Assembly.GetExecutingAssembly().Location;
			string baseDir = Path.GetDirectoryName(assemblyPath);

            _dataFilePath = Path.Combine(baseDir, "urls.json");
            _configFilePath = Path.Combine(baseDir, "config.json");

            LoadData();
            LoadConfig();
            UpdateFilteredItems();
			UpdateBrowserDisplay();
        }

        // 实现函数声明于[ObservableProperty], 变化的时候自动调用
        partial void OnSearchTextChanged(string value)
        {
            UpdateFilteredItems();
        }

        // 更新公有列表, 过滤后或者完全展示
        private void UpdateFilteredItems()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredItems = new ObservableCollection<UrlItem>(UrlItems);
            }
            else
            {
                var searchLower = SearchText.ToLower();
                FilteredItems = new ObservableCollection<UrlItem>(
                    UrlItems.Where(item => 
                        item.Name.ToLower().Contains(searchLower) ||
                        item.Url.ToLower().Contains(searchLower) ||
                        item.Category.ToLower().Contains(searchLower)
                    )
                );
            }
        }

        // 必须输入名字和url
        [RelayCommand]
        private void AddUrl()
        {
            if (string.IsNullOrWhiteSpace(NewName) || string.IsNullOrWhiteSpace(NewUrl))
                return;
            if (string.IsNullOrWhiteSpace(NewCategory))
            {
                var urlCategory = ExtractDomainPrefix(NewUrl);
                if (!string.IsNullOrWhiteSpace(urlCategory))
                {
                    NewCategory = urlCategory;
                }
            }
            var newItem = new UrlItem
            {
                Name = NewName,
                Url = NewUrl,
                Category = NewCategory
            };

            UrlItems.Add(newItem);
            UpdateFilteredItems();
            SaveData();

            // 清空输入框
            NewName = string.Empty;
            NewUrl = string.Empty;
            NewCategory = string.Empty;
        }

        // 点击打开按钮后调用的
        [RelayCommand]
        private async Task OpenUrl(UrlItem item)
        {
            if (item == null) return;

            try
            {
				//throw new Exception("test");
                // 优先使用自定义浏览器路径
                var browserPath = GetBrowserPath();
                var browserProcess = new ProcessStartInfo
                {
                    FileName = browserPath,
                    Arguments = item.Url,
                    UseShellExecute = false
                };
                Process.Start(browserProcess);
            }
            catch (Exception ex)
            {
                // 浏览器不可用，让shell自己找浏览器来开
                Console.WriteLine($"error: 无法使用指定浏览器打开 URL：{ex.Message}");    
                try
                {
				    //throw new Exception("test");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = item.Url,
                        UseShellExecute = true
                    });
                }
                catch (Exception innerEx)
                {
                    Console.WriteLine($"error: 无法自己找到浏览器打开 URL：{innerEx.Message}");
					var box = MessageBoxManager.GetMessageBoxStandard(
							"Error",
							$"无法自己找到浏览器打开URL: {item.Url}",
							ButtonEnum.Ok,
							Icon.Error
					);
					await box.ShowAsync();
				}
            }
        }
		
		// 点击删除URL按钮后执行的
        [RelayCommand]
        private void DeleteUrl(UrlItem item)
        {
            if (item == null) return;
            
            UrlItems.Remove(item);
            UpdateFilteredItems();
            SaveData();
        }

        // 输入浏览器路径并点击保存时
        [RelayCommand]
        private void SaveBrowserConfig()
        {
            CurrentBrowserPath = CustomBrowserPath;
			SaveConfig();
			UpdateBrowserDisplay();
			CustomBrowserPath = string.Empty;
        }

        // 点击清除默认浏览器路径
        [RelayCommand]
        private void ClearBrowserPath()
        {
            CustomBrowserPath = string.Empty;
			CurrentBrowserPath = string.Empty;
            SaveConfig();
			UpdateBrowserDisplay();
        }
		
		private void UpdateBrowserDisplay()
		{
			if(string.IsNullOrWhiteSpace(CurrentBrowserPath))
			{
				CurrentBrowserDisplay = "None(use Chrome as defaut)";
			}
			else
			{
				CurrentBrowserDisplay = CurrentBrowserPath;
			}
		}

        // x.com->x    
        private string ExtractDomainPrefix(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                var host = uri.Host;
                // 移除 www. 前缀
                if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                {
                    host = host.Substring(4);
                }

                // 查找第一个点号 '.', 截取前面的字符
                var firstDotIndex = host.IndexOf('.');
                if (firstDotIndex > 0)
                {
                    return host.Substring(0, firstDotIndex).ToLowerInvariant();
                }

                // 如果没有点号（例如 "localhost"），则直接返回整个 host
                return host.ToLowerInvariant();
            }
            // 如果 URL 无法解析（例如不是一个完整的 URL），则返回空字符串
            return string.Empty;
        }

        // 获取浏览器路径 (优先使用自定义路径)
        private string GetBrowserPath()
        {
            if (!string.IsNullOrWhiteSpace(CurrentBrowserPath) && File.Exists(CurrentBrowserPath))
            {
                Console.WriteLine("use custom browser");
                return CurrentBrowserPath;
            }
            Console.WriteLine("use chrome as default");    
            return GetChromePath();
        }

        // 默认使用chrome
        private string GetChromePath()
        {
            // 尝试常见的 Chrome 安装路径
            string[] chromePaths = 
            {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            };

            foreach (var path in chromePaths)
            {
                if (File.Exists(path))
                    return path;
            }

            // 如果找不到，返回 chrome，让系统shell自己找
			Console.WriteLine("return chrome, let shell find it");
            return "chrome";
        }

        // 把整个列表重新写入json当中
        private void SaveData()
        {
            try
            {
                var json = JsonSerializer.Serialize(UrlItems, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(_dataFilePath, json);
            }
            catch
            {
                Console.WriteLine("error: fail to save data");    
            }
        }

        // 解序列化json给共有的UrlItems
        private void LoadData()
        {
            try
            {
                if (File.Exists(_dataFilePath))
                {
                    var json = File.ReadAllText(_dataFilePath);
                    var items = JsonSerializer.Deserialize<ObservableCollection<UrlItem>>(json);
                    if (items != null)
                    {
                        UrlItems = items;
                    }
                }
            }
            catch
            {
                // 如果加载失败，使用空列表
                UrlItems = new ObservableCollection<UrlItem>();
            }
        }

        // 保存配置
        private void SaveConfig()
        {
            try
            {
                var config = new Config
                {
                    CustomBrowserPath = CurrentBrowserPath,
					LastModified = DateTime.Now
                };
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error: 保存配置失败: {ex.Message}");
            }
        }

        // 加载配置
        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    var config = JsonSerializer.Deserialize<Config>(json);
                    
                    if (config != null)
                    {
                        CurrentBrowserPath = config.CustomBrowserPath ?? string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error: 加载配置失败: {ex.Message}");
				CurrentBrowserPath = string.Empty;
            }
        }
    }
}
