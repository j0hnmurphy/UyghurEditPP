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

		// Ponytail review finding 2: if Windows shuts down while "Save?" is shown, the copy must
		// hold the latest edits, not those of the last timer tick.
		[TestMethod, Timeout(60000)]
		public void Closing_RefreshesTheCopy_BeforeAskingToSave()
		{
			string file = Path.Combine(gFolder, "a.txt");
			File.WriteAllText(file, "old");
			string copyWhileAsking = null;
			RunOnSta(() => {
				using(MainForm form = new MainForm()){
					form.ShowInTaskbar = false;
					form.Show();
					form.OpenaFile(file);
					form.gEditor.Document.Insert(0, "first ");
					form.gEditor.Document.Insert(0, "second ");
					Assert.AreEqual("first old", CopyText());
					using(System.Windows.Forms.Timer answer = new System.Windows.Forms.Timer{Interval = 100}){
						answer.Tick += (s, a) => {
							ThemedMessageBox box = System.Windows.Forms.Application.OpenForms.OfType<ThemedMessageBox>().FirstOrDefault();
							if(box == null){
								return;
							}
							answer.Stop();
							copyWhileAsking = CopyText();
							box.DialogResult = System.Windows.Forms.DialogResult.Cancel;
						};
						answer.Start();
						form.Close();   // "Save?" answered Cancel: the window stays open
					}
				}
			});
			Assert.AreEqual("second first old", copyWhileAsking);
		}

		// Ponytail review finding 3: one tab too long to copy stopped the immediate first copy
		// of every other document. The long text itself is not written, and is logged once.
		[TestMethod]
		public void TooLongTab_IsNotCopied_AndDoesNotStopTheCopiesOfOtherTabs()
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder(Recovery.MaxChars + 100);
			string line = new string('a', 99) + "\n";
			while(sb.Length <= Recovery.MaxChars){
				sb.Append(line);
			}
			System.Collections.Generic.List<Exception> logged = new System.Collections.Generic.List<Exception>();
			Action<Exception> originalLog = Recovery.Log;
			Recovery.Log = ee => logged.Add(ee);
			try{
				RunOnSta(() => {
					using(MainForm form = new MainForm()){
						form.ShowInTaskbar = false;
						form.Show();
						form.gEditor.Document.Insert(0, sb.ToString());
						Call(form, "MenuYengiClick", null, EventArgs.Empty);
						form.gEditor.Document.Insert(0, "x");
						Assert.AreEqual("x", CopyText());
					}
				});
			}
			finally{
				Recovery.Log = originalLog;
			}
			Assert.AreEqual(1, logged.Count);
		}

		// Ponytail review finding 1: the first window saved its stale recent files over those a
		// later window had saved (opened from Explorer and closed first), losing them.
		[TestMethod]
		public void FirstWindowClosing_KeepsTheRecentFilesALaterWindowSaved_AndItsOwnOpenFiles()
		{
			string mine = Path.Combine(gFolder, "mine.txt");
			string other = Path.Combine(gFolder, "other.txt");
			File.WriteAllText(mine, "m");
			File.WriteAllText(other, "o");
			string settings = AppPaths.DataFile(AppSettings.FileName);
			string gone = Path.Combine(gFolder, "gone.txt");   // dropped from the list at the start
			System.Collections.Hashtable atStart = new System.Collections.Hashtable();
			atStart["IZLAR"] = new[] { gone };
			AppSettings.Save(settings, atStart);
			bool first = true;
			RunOnSta(() => {
				using(MainForm form = new MainForm()){
					form.ShowInTaskbar = false;
					form.Show();
					first = Field(form, "gFirstWindow") != null;
					if(!first){
						return;   // the user's own program holds the first window: nothing to check
					}
					form.OpenaFile(mine);
					// Saved meanwhile by a later window.
					System.Collections.Hashtable later = new System.Collections.Hashtable();
					later["IZLAR"] = new[] { other, gone };
					later["ORUNLAR"] = new System.Collections.Generic.Dictionary<string, int>{ { other, 42 } };
					later["ACHIQ"] = new[] { "stale.txt" };
					AppSettings.Save(settings, later);
					form.Close();
				}
			});
			if(!first){
				Assert.Inconclusive("another UyghurEdit++ holds the first window lock");
			}
			System.Collections.Hashtable saved = AppSettings.Load(settings, null);
			CollectionAssert.Contains((string[])saved["IZLAR"], other);
			CollectionAssert.Contains((string[])saved["IZLAR"], mine);
			CollectionAssert.DoesNotContain((string[])saved["IZLAR"], gone);
			Assert.AreEqual(42, ((System.Collections.Generic.Dictionary<string, int>)saved["ORUNLAR"])[other]);
			CollectionAssert.AreEqual(new[] { mine }, (string[])saved["ACHIQ"]);
		}

		// Re-review: a restored document over the limit would get no copy of its own, so its
		// leftover would be offered again at every start. A copy is never longer than the limit
		// (MainForm does not copy longer texts), so the longest possible one is checked here.
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
			new Recovery(folder, 1, new DateTime(5, DateTimeKind.Utc)).Save(Recovery.NewId(), null, "long", 65001, false, text);
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

		// Real-machine check: two restored untitled documents and the empty first tab were all
		// called Namsiz_1.txt; a new tab could then take the name of a restored one too.
		[TestMethod]
		public void RestoredAndNewUntitledTabs_AllGetDifferentNames()
		{
			string folder = Path.Combine(gFolder, "recovery");
			new Recovery(folder, 1, new DateTime(5, DateTimeKind.Utc)).Save(Recovery.NewId(), null, "Namsiz_1.txt", 65001, false, "one");
			new Recovery(folder, 2, new DateTime(6, DateTimeKind.Utc)).Save(Recovery.NewId(), null, "Namsiz_2.txt", 65001, false, "two");
			new Recovery(folder, 3, new DateTime(7, DateTimeKind.Utc)).Save(Recovery.NewId(), null, "Namsiz_1.txt", 65001, false, "three");
			RunOnSta(() => {
				using(MainForm form = new MainForm()){
					form.ShowInTaskbar = false;
					form.Show();   // its first tab is Namsiz_1.txt
					foreach(Recovery.Leftover left in Recovery.FindLeftovers(folder, (pid, started) => pid > 3)){
						Assert.IsTrue((bool)Call(form, "RestoreLeftover", left, new System.Collections.Generic.List<string>()));
					}
					Call(form, "MenuYengiClick", null, EventArgs.Empty);
					System.Windows.Forms.TabControl tabs = (System.Windows.Forms.TabControl)Field(form, "mainTab");
					string[] names = tabs.TabPages.Cast<System.Windows.Forms.TabPage>().Select(p => p.Text.TrimStart('*')).ToArray();
					Assert.AreEqual(5, names.Length);
					Assert.AreEqual(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count(), string.Join(", ", names));
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
