/*
 * Created by SharpDevelop.
 * User: Gheyret Kenji
 * Date: 01/15/2021
 * Time: 17:08
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Media;

namespace UyghurEditPP.FindReplace
{
	/// <summary>
	/// Interaction logic for FindReplaceDialog.xaml
	/// </summary>
	public partial class FindReplaceDialog : Window
	{
		private static string textToFind = "";
		private static bool caseSensitive = false;
		private static bool wholeWord = true;
		private static bool useRegex = false;
		private static bool useWildcards = false;
		private static bool searchUp = false;
		private static bool allTabs = false;
		private static bool crossScript = false;
		private TextEditor  curEditor;
		private int         repCount = 0;
		KUNUPKA       gKunupka = KUNUPKA.System;
		
		bool          gLastIsFind = true;
		
		public FindReplaceDialog(TextEditor editor)
		{
			InitializeComponent();
			Editor = editor;
			cbCaseSensitive.IsChecked = caseSensitive;
			cbWholeWord.IsChecked = wholeWord;
			cbRegex.IsChecked = useRegex;
			cbWildcards.IsChecked = useWildcards;
			cbSearchUp.IsChecked = searchUp;
			cbAllTabs.IsChecked = allTabs;
			cbCrossScript.IsChecked = crossScript;
			
			this.FontFamily = AppFonts.Wpf(AppFonts.UkijTuz);
			this.FontSize = 14;
			repCount = 0;
			this.txtFind.PreviewTextInput += UserControl_TextInput;
			this.txtReplace.PreviewTextInput += UserControl_TextInput;
			UpdateMessages();
		}

		public void UpdateMessages(){
			this.Title          = MainForm.gLang.GetText("Izdesh we Almashturush");
			this.labIzde1.Text = MainForm.gLang.GetText("Izdeydighan tékist:");
			this.labRep.Text   = MainForm.gLang.GetText("Orunbasar tékist:");
			this.butFind1.Content = MainForm.gLang.GetText("Izde");
			this.butCount.Content = MainForm.gLang.GetText("Sani");
			
			this.butRep.Content = MainForm.gLang.GetText("Almashtur");
			this.butRepAll.Content = MainForm.gLang.GetText("Hemmini Almashtur");
			
			this.cbCaseSensitive.Content = MainForm.gLang.GetText("Chong-kichik yézilishini perqlendürsun");
			this.cbWholeWord.Content     = MainForm.gLang.GetText("Pütün söz");
			this.cbSearchUp.Content     = MainForm.gLang.GetText("Üstige qarap izdisun");
			this.cbAllTabs.Content      = MainForm.gLang.GetText("Search in all open tabs");
			this.cbCrossScript.Content  = MainForm.gLang.GetText("Match other Uyghur scripts too");

			this.cbRegex.Content     = MainForm.gLang.GetText("Muntizim ipade")+" (Regular Expression)";
			this.cbWildcards.Content     = MainForm.gLang.GetText("Alahide belgiler") + "(\\t,\\n,\\r...)";
			this.cbNormal.Content = MainForm.gLang.GetText("Normal");
			
			this.ToolTip = "<Ctrl>+<K> " + MainForm.gLang.GetText("ni bassa kunupka almiship, Uyghurche kirgüzgili bolidu");
			this.labKun.Text = "<Ctrl>+<K> " + MainForm.gLang.GetText("ni bassa kunupka almishidu. Uyghurche kirgüzgili bolidu");
		}
		
		void window1_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			System.Diagnostics.Debug.WriteLine(e.Key.ToString());
			if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
			{
				if(gKunupka == KUNUPKA.System){
					gKunupka = KUNUPKA.Uyghur;
					this.labKun.Text = "ئۇيغۇرچە";
				}
				else if(gKunupka == KUNUPKA.Uyghur){
					gKunupka = KUNUPKA.UyghurLY;
					this.labKun.Text = "Uyghurche";
				}
				else
				{
					gKunupka = KUNUPKA.System;
					this.labKun.Text = "System";
				}
				e.Handled = true;
			}
			else if(e.Key == Key.Return){
				e.Handled = true;
				FindNextClick(null,null);
			}
			//this.labKun.Text = MainForm.gLang.GetText("<Ctrl>+<K> ni bassa kunupka almishidu. Uyghurche kirgüzgili bolidu");
		}
		
		
		private void UserControl_TextInput(object sender,TextCompositionEventArgs e)
		{
			string newtxt="";
			TextBox curBox = sender as TextBox;
			if(InputLanguageManager.Current.CurrentInputLanguage.ThreeLetterISOLanguageName.Equals("uig")){
				
			}
			else if(gKunupka == KUNUPKA.Uyghur){
				newtxt = Uyghur.KeyToUEY(e.Text);
				if(newtxt.Length>0)
				{
					e.Handled = true;
					InputText(curBox,newtxt);
				}
			}
			else if(gKunupka == KUNUPKA.UyghurLY){
				newtxt = Uyghur.KeyToULY(e.Text);
				if(newtxt.Length>0)
				{
					e.Handled = true;
					InputText(curBox,newtxt);
				}
			}
			else if(curEditor.RightToLeft){
				e.Handled = true;
				InputText(curBox,e.Text);
			}
		}
		
		private void InputText(TextBox curBox,string newtxt)
		{
			int oldpos = curBox.CaretIndex;
			string txt = curBox.Text;
			if(curBox.SelectionLength>0){
				oldpos = curBox.SelectionStart;
				txt = txt.Remove(oldpos,curBox.SelectionLength);
				curBox.SelectionLength = 0;
			}
			
			if(Uyghur.IsUSozuq(newtxt[0]))
			{
				if((oldpos==0) || (oldpos>0 && (Uyghur.IsUSozuq(txt[oldpos-1])||!Uyghur.IsUyghurcheHerp(txt[oldpos-1])))
				  )
				{
					//txt = txt.Insert(txtFind.CaretIndex,Uyghur.UYG_UN_HM_6+"");
					newtxt = Uyghur.UYG_UN_HM_6 + newtxt;
				}
			}
			newtxt = Uyghur.Tirnaqlar(newtxt,curEditor.RightToLeft);
			txt = txt.Insert(oldpos,newtxt);
			curBox.Text = txt;
			curBox.CaretIndex =  oldpos+newtxt.Length;
		}
		
		/// <summary>The editors of all open tabs, in tab order (set by the main window).</summary>
		public Func<List<TextEditor>> GetEditors;

		/// <summary>Makes the tab of an editor the selected one (set by the main window).</summary>
		public Action<TextEditor> ShowEditor;

		public TextEditor Editor{
			set{
				curEditor=value;
				repCount = 0;
			}
			get{
				return curEditor;
			}
		}
		
		
		public void ShowMe()
		{
			window1.FlowDirection = MainForm.gLang.LanguaID.Equals("uey")?FlowDirection.RightToLeft:FlowDirection.LeftToRight;
			txtFind.FlowDirection = window1.FlowDirection;
			txtReplace.FlowDirection = window1.FlowDirection;
			UpdateMessages();
			if(curEditor.SelectionLength>0){
				txtFind.Text=curEditor.SelectedText;
			}
			txtFind.SelectAll();
			txtFind.Focus();
			Show();
			Topmost=true;
			Activate();
		}
		
		public void HideMe()
		{
			this.Hide();
			repCount = 0;
		}
		
		private void Window_Closed(object sender, System.EventArgs e)
		{
			textToFind = txtFind.Text;
			caseSensitive = (cbCaseSensitive.IsChecked == true);
			wholeWord = (cbWholeWord.IsChecked == true);
			useRegex = (cbRegex.IsChecked == true);
			useWildcards = (cbWildcards.IsChecked == true);
			searchUp = (cbSearchUp.IsChecked == true);
			allTabs = (cbAllTabs.IsChecked == true);
			crossScript = (cbCrossScript.IsChecked == true);
		}

		// Why a search could not be done; the text is shown in the window.
		sealed class SearchException : Exception
		{
			public SearchException(string message) : base(message) {}
		}

		// Runs a search or replace; a pattern that is not valid or takes too long is reported
		// in the window instead of as an error. Returns false then.
		bool Guard(System.Action action)
		{
			try{
				action();
				return true;
			}
			catch(SearchException ee){
				labKun.Text = ee.Message;
			}
			catch(RegexMatchTimeoutException){
				labKun.Text = MainForm.gLang.GetText("The search took too long and was stopped.");
			}
			SystemSounds.Beep.Play();
			return false;
		}

		public void FindNextClick(object sender, RoutedEventArgs e)
		{
			gLastIsFind = true;
			Guard(() => {
				if (!FindNext(txtFind.Text)){
					SystemSounds.Beep.Play();
				}
			});
		}


		public void CountClick(object sender, RoutedEventArgs e)
		{
			Guard(() => {
				Regex regex = GetRegEx(txtFind.Text, true);
				int count = 0;
				foreach(TextEditor ed in EditorsToSearch()){
					count += regex.Matches(ed.Text, 0).Count;
				}
				labKun.Text = MainForm.gLang.GetText("Sani: ") + count;
			});
		}

		public void ContinueLastOperation(){
			if(gLastIsFind){
				FindNextClick(null,null);
			}
			else{
				ReplaceClick(null,null);
			}
		}

		// The editors a search covers: the current one, or every open tab.
		List<TextEditor> EditorsToSearch()
		{
			List<TextEditor> editors = new List<TextEditor>();
			if(cbAllTabs.IsChecked == true && GetEditors != null){
				editors.AddRange(GetEditors());
			}
			if(!editors.Contains(Editor)){
				editors.Insert(0, Editor);
			}
			return editors;
		}

		// The replacement text: \t, \n ... are understood; a backslash that starts no escape
		// (for example in a path) is taken as it is.
		string ReplacementText()
		{
			try{
				return Regex.Unescape(txtReplace.Text);
			}
			catch(ArgumentException){
				return txtReplace.Text;
			}
		}

		private void ReplaceClick(object sender, RoutedEventArgs e)
		{
			gLastIsFind = false;
			Guard(() => {
				Regex regex = GetRegEx(txtFind.Text);
				//string input = editor.Text.Substring(editor.SelectionStart, editor.SelectionLength);
				string repText = ReplacementText();

				Match match = regex.Match(Editor.Text,Editor.SelectionStart);
				if (match.Success)
				{
					Editor.Document.Replace(match.Index, match.Length, repText);
					Editor.CaretOffset = match.Index+repText.Length;
					Editor.BringCaretToView();
					Editor.Focus();
					repCount++;
					labKun.Text = MainForm.gLang.GetText("Jemiy ") + repCount + MainForm.gLang.GetText(" qétim almashturuldi");
				}

				if (!FindNext(txtFind.Text) && !match.Success){
					SystemSounds.Beep.Play();
				}
			});
		}

		private void ReplaceAllClick(object sender, RoutedEventArgs e)
		{
			string msg = MainForm.gLang.GetText("Rastla barliq «") + txtFind.Text + MainForm.gLang.GetText("» ni «") +  txtReplace.Text + MainForm.gLang.GetText("» gha alamshturamsiz?");
			if(cbAllTabs.IsChecked == true){
				msg += "\n" + MainForm.gLang.GetText("All open tabs will be changed.");
			}
			// Owned by (and centered on) this find window, so it stays above it and the
			// window cannot be used while the question is open.
			System.Windows.Forms.IWin32Window findWindow = CenteredMessageBox.Wrap(new System.Windows.Interop.WindowInteropHelper(this).Handle);
			// The caption is Uyghur text, so it is not marked as a left-to-right run.
			if (CenteredMessageBox.ShowHere(findWindow ?? CenteredMessageBox.MainWindow, msg, MainForm.gLang.GetText("Hemmini Almashturush"),
				System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
			{
				ReplaceAll();
			}
		}

		// The replacing of "replace all", once confirmed.
		internal void ReplaceAll()
		{
			Cursor old = Mouse.OverrideCursor;
			Mouse.OverrideCursor= Cursors.Wait;
			int before = repCount;
			bool done = false;
			try{
				done = Guard(() => {
					Regex regex = GetRegEx(txtFind.Text, true);
					string repText = ReplacementText();
					TextEditor current = Editor;
					foreach(TextEditor ed in EditorsToSearch()){
						ReplaceAllIn(ed, regex, repText, ed == current);
					}
				});
			}
			finally{
				string count = MainForm.gLang.GetText("Jemiy ") + repCount + MainForm.gLang.GetText(" qétim almashturuldi");
				// When it stopped (bad pattern, time-out), Guard's message stays; what was replaced
				// before a time-out is added to it.
				labKun.Text = done ? count : repCount > before ? labKun.Text + " " + count : labKun.Text;
				Mouse.OverrideCursor = old;
			}
		}

		// Replaces every match in one editor. All matches are found in the text as it is, then
		// replaced from the end, so that the replacement text is never searched again.
		void ReplaceAllIn(TextEditor ed, Regex regex, string repText, bool showResult)
		{
			MatchCollection matches = regex.Matches(ed.Text, 0);
			if(matches.Count == 0){
				return;
			}
			int caret = 0;
			int shift = 0;
			foreach(Match m in matches){
				caret = m.Index + shift + repText.Length;
				shift += repText.Length - m.Length;
			}
			ed.BeginChange();
			try{
				for(int i = matches.Count - 1; i >= 0; i--){
					ed.Document.Replace(matches[i].Index, matches[i].Length, repText);
				}
			}
			finally{
				ed.EndChange();
			}
			repCount += matches.Count;
			if(showResult){
				ed.CaretOffset = caret;
				ed.BringCaretToView();
			}
		}

		private bool FindNext(string textToFind)
		{
			Regex regex = GetRegEx(textToFind);
			bool up = regex.Options.HasFlag(RegexOptions.RightToLeft);
			int start = up ? Editor.SelectionStart : Editor.SelectionStart + Editor.SelectionLength;
			Match match = regex.Match(Editor.Text, start);

			if (!match.Success && cbAllTabs.IsChecked == true && ShowEditor != null)
			{
				// The next tabs, in tab order and round to the first, one after the other.
				List<TextEditor> editors = EditorsToSearch();
				int here = editors.IndexOf(Editor);
				for (int k = 1; k < editors.Count && !match.Success; k++)
				{
					TextEditor other = editors[(((up ? here - k : here + k) % editors.Count) + editors.Count) % editors.Count];
					string text = other.Text;
					Match found = regex.Match(text, up ? text.Length : 0);
					if (found.Success)
					{
						match = found;
						ShowEditor(other);
						Editor = other;
						Activate();   // showing the tab moves the focus to the editor
					}
				}
			}

			if (!match.Success)  // start again from beginning or end
			{
				SystemSounds.Beep.Play();
				if (up)
					match = regex.Match(Editor.Text, Editor.Text.Length);
				else
					match = regex.Match(Editor.Text, 0);
			}

			if (match.Success)
			{
				Editor.Select(match.Index, match.Length);
				Editor.CaretOffset = match.Index;
				Editor.BringCaretToView();
			}
			else{
				SystemSounds.Beep.Play();
				this.labKun.Text=MainForm.gLang.GetText("Tépilmidi");
			}
			return match.Success;
		}

		private Regex GetRegEx(string textToFind, bool leftToRight = false)
		{
			FindMode mode = cbNormal.IsChecked == true ? FindMode.Normal
				: cbWildcards.IsChecked == true ? FindMode.Wildcards : FindMode.RegularExpression;
			try{
				return FindPattern.Build(textToFind, mode, cbWholeWord.IsChecked == true, cbCaseSensitive.IsChecked == true,
					cbSearchUp.IsChecked == true && !leftToRight, FindPattern.Timeout, cbCrossScript.IsChecked == true);
			}
			catch(ArgumentException){
				throw new SearchException(MainForm.gLang.GetText("The regular expression is not valid."));
			}
		}
	}
}
