// ./Models/Config.cs

using System;

namespace UrlLauncher.Models
{
	public class Config
	{
		public string CustomBrowserPath {get; set;} = string.Empty;
		public DateTime LastModified {get; set;} = DateTime.Now;
	}
}
