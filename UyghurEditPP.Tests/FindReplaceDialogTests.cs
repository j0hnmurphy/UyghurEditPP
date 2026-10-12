using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UyghurEditPP.FindReplace;

namespace UyghurEditPP.Tests
{
	[TestClass]
	public class FindReplaceDialogTests
	{
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

		// Review finding 5: "replace all" overwrote the message about a bad pattern with the count.
		[TestMethod]
		public void ReplaceAll_BadPattern_KeepsTheMessage_AndChangesNothing()
		{
			RunOnSta(() => {
				TextEditor ed = new TextEditor();
				ed.Text = "a (b) c";
				FindReplaceDialog dlg = new FindReplaceDialog(ed);
				try{
					dlg.cbRegex.IsChecked = true;
					dlg.txtFind.Text = "(";
					dlg.txtReplace.Text = "x";
					dlg.ReplaceAll();
					Assert.AreEqual(MainForm.gLang.GetText("The regular expression is not valid."), dlg.labKun.Text);
					Assert.AreEqual("a (b) c", ed.Text);
				}
				finally{
					dlg.Close();
				}
			});
		}
	}
}
