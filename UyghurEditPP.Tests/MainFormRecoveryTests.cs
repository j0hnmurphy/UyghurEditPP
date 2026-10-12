using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UyghurEditPP.Tests
{
	// The recovery copy as MainForm keeps it while a file is saved. The data folder (settings,
	// recovery copies, logs) is a temporary one, so the user's own folder is not touched.
	[TestClass]
	public class MainFormRecoveryTests
	{
		string gFolder;
		Action<string, string, string> gOriginalReplace;
		Action<Exception> gOriginalLog;

		[TestInitialize]
		public void SetUp()
		{
			gFolder = Path.Combine(Path.GetTempPath(), "UyghurEditPP.Tests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(gFolder);
			gOriginalReplace = SafeFile.ReplaceFile;
			gOriginalLog = SafeFile.Log;
			SafeFile.Log = ee => {};
			AppPaths.DataFolderOverride = gFolder;
			Assert.IsTrue(AppPaths.DataFolder.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase));
		}

		[TestCleanup]
		public void TearDown()
		{
			AppPaths.DataFolderOverride = null;
			SafeFile.ReplaceFile = gOriginalReplace;
			SafeFile.Log = gOriginalLog;
			Directory.Delete(gFolder, true);
		}

		static object Field(MainForm form, string name)
		{
			return typeof(MainForm).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
		}

		static object Call(MainForm form, string name, params object[] args)
		{
			try{
				return typeof(MainForm).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, args);
			}
			catch(TargetInvocationException ee){
				throw ee.InnerException;
			}
		}

		string CopyText()
		{
			return Recovery.FindLeftovers(Path.Combine(gFolder, "recovery"), (pid, started) => false).Single().Items.Single().ReadText();
		}

		// Review finding 2: when saving fails, the copy is brought up to date at once (the
		// edits since the last copy would otherwise wait for the timer).
		[TestMethod]
		public void FailedSave_WritesTheCopyAgainAtOnce_AndASavedFileDeletesIt()
		{
			string file = Path.Combine(gFolder, "a.txt");
			File.WriteAllText(file, "old");
			RunOnSta(() => {
				using(MainForm form = new MainForm()){
					form.ShowInTaskbar = false;
					form.Show();   // loads the settings and starts the recovery, in the temporary folder
					Assert.IsNotNull(Field(form, "gRecovery"));
					form.OpenaFile(file);
					TextEditor ed = form.gEditor;
					ed.Document.Insert(0, "first ");   // the first change makes the copy at once
					Assert.AreEqual("first old", CopyText());
					ed.Document.Insert(0, "second ");  // later changes wait for the timer
					Assert.AreEqual("first old", CopyText());

					// Only the document's file cannot be written (the copies still can).
					SafeFile.ReplaceFile = (source, destination, backup) => {
						if(string.Equals(Path.GetFullPath(destination), Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase)){
							throw new UnauthorizedAccessException("read-only");
						}
						gOriginalReplace(source, destination, backup);
					};
					Assert.ThrowsException<UnauthorizedAccessException>(() => Call(form, "SaveEditor", ed, file));
					Assert.AreEqual("second first old", CopyText());

					SafeFile.ReplaceFile = gOriginalReplace;
					Call(form, "SaveEditor", ed, file);
					Assert.AreEqual(0, Recovery.FindLeftovers(Path.Combine(gFolder, "recovery"), (pid, started) => false).Count);
				}
			});
			Assert.AreEqual("second first old", File.ReadAllText(file));
		}

		// Re-review: a restored document over the limit would get no copy of its own, so its
		// leftover would be offered again at every start. A copy is never longer than the limit
		// (Recovery.Save refuses longer texts), so the longest possible one is checked here.
		[TestMethod]
		public void RestoringTheLongestPossibleCopy_GetsACopyOfItsOwn_SoTheLeftoverCanGo()
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder(Recovery.MaxChars);
			string line = new string('a', 99) + "\n";
			while(sb.Length < Recovery.MaxChars){
				sb.Append(line);
			}
			string text = sb.ToString(0, Recovery.MaxChars);
			string folder = Path.Combine(gFolder, "recovery");
			Assert.IsTrue(new Recovery(folder, 1, new DateTime(5, DateTimeKind.Utc)).Save(Recovery.NewId(), null, "long", 65001, false, text));
			RunOnSta(() => {
				using(MainForm form = new MainForm()){
					form.ShowInTaskbar = false;
					form.Show();
					Recovery.Leftover left = Recovery.FindLeftovers(folder, (pid, started) => pid != 1).Single();
					bool restored = (bool)Call(form, "RestoreLeftover", left, new System.Collections.Generic.List<string>());
					Assert.IsTrue(restored, "the leftover is deleted after this, not offered again");
					Assert.AreEqual(Recovery.MaxChars, form.gEditor.Document.TextLength);
				}
			});
		}

		static void RunOnSta(Action action)
		{
			Exception error = null;
			Thread t = new Thread(() => {
				try{
					action();
				}
				catch(Exception e){
					error = e;
				}
			});
			t.SetApartmentState(ApartmentState.STA);
			t.Start();
			t.Join();
			if(error != null){
				throw new AssertFailedException(error.ToString());
			}
		}
	}
}
