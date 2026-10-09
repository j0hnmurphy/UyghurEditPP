using System;
using System.Collections;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UyghurEditPP.Tests
{
	[TestClass]
	public class ThemeTests
	{
		string gFolder;

		[TestInitialize]
		public void SetUp()
		{
			gFolder = Path.Combine(Path.GetTempPath(), "UyghurEditPP.Tests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(gFolder);
		}

		// The caption color is passed to DWM as a COLORREF: 0x00BBGGRR.
		[TestMethod]
		public void ColorRef_IsBlueGreenRed()
		{
			Assert.AreEqual(0x00332211, UiTheme.ColorRef(System.Drawing.Color.FromArgb(0x11, 0x22, 0x33)));
			Assert.AreEqual(0x00F5F5F5, UiTheme.ColorRef(UiTheme.Light.Bar));
			Assert.AreEqual(0x002B2B2B, UiTheme.ColorRef(UiTheme.Dark.Bar));
		}

		[TestCleanup]
		public void TearDown()
		{
			Directory.Delete(gFolder, true);
		}

		[TestMethod]
		public void ThemeSetting_SurvivesSaveAndLoad()
		{
			foreach(string setting in new[] { UiTheme.LightSetting, UiTheme.DarkSetting, UiTheme.SystemSetting }){
				string json = Path.Combine(gFolder, setting + ".json");
				Hashtable h = new Hashtable();
				h["LANG"] = "uly";
				h["TEMA"] = setting;
				AppSettings.Save(json, h);

				Hashtable loaded = AppSettings.Load(json, null);
				Assert.AreEqual(setting, loaded["TEMA"]);
				StringAssert.Contains(File.ReadAllText(json), "\"theme\": \"" + setting + "\"");
			}
		}

		[TestMethod]
		public void NoThemeSetting_IsNotWrittenAndStaysMissing()
		{
			string json = Path.Combine(gFolder, "none.json");
			Hashtable h = new Hashtable();
			h["LANG"] = "uly";
			AppSettings.Save(json, h);

			Assert.IsFalse(File.ReadAllText(json).Contains("theme"));
			Assert.IsFalse(AppSettings.Load(json, null).ContainsKey("TEMA"));
		}

		[TestMethod]
		public void FromSetting_LightAndDark()
		{
			Assert.AreSame(UiTheme.Light, UiTheme.FromSetting(UiTheme.LightSetting));
			Assert.AreSame(UiTheme.Dark, UiTheme.FromSetting(UiTheme.DarkSetting));
			Assert.AreSame(UiTheme.Light, UiTheme.FromSetting(null));
			Assert.AreSame(UiTheme.Light, UiTheme.FromSetting("something else"));
			Assert.AreSame(UiTheme.WindowsUsesDarkMode() ? UiTheme.Dark : UiTheme.Light, UiTheme.FromSetting(UiTheme.SystemSetting));
		}
	}
}
