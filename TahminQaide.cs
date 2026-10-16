/*
 * The rules of the word prediction list in the editor (MainForm): which word the caret is
 * at the end of, which candidates to show, and what a key does while the list is open.
 */
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace UyghurEditPP
{
	public enum TahminHerket { Pass, Move, Choose, Close, CloseAndPass }

	public static class TahminQaide
	{
		public const int NamzatSani = 5;   // owner's decision, 2026-10-16
		public const int EngQisqa = 2;     // the list shows from this many letters

		/// <summary>
		/// Where the word that ends exactly at orun starts in qur, or -1: no word there, or the
		/// caret is inside a word (letters follow it).
		/// </summary>
		public static int SozBashi(string qur, int orun, Regex finder)
		{
			for(Match m = finder.Match(qur); m.Success; m = m.NextMatch()){
				if(m.Index >= orun) break;
				int ayagh = m.Index + m.Length;
				if(ayagh == orun) return m.Index;
				if(ayagh > orun) return -1;
			}
			return -1;
		}

		/// <summary>
		/// Up to n dictionary words that start with bash, most frequent first, without bash
		/// itself; a capital first letter of bash is kept (as the spelling menu does).
		/// </summary>
		public static List<string> Namzatlar(SozTahmini tahmin, string bash, int n)
		{
			string soz = SozTahmini.Normalize(bash);
			List<string> netije = new List<string>(n);
			foreach(string namzat in tahmin.Find(soz, n + 1)){
				if(namzat == soz) continue;
				if(netije.Count == n) break;
				netije.Add(char.IsUpper(bash[0]) ? char.ToUpperInvariant(namzat[0]) + namzat.Substring(1) : namzat);
			}
			return netije;
		}

		/// <summary>
		/// What a key does while the list is open. tallanghan is the selected candidate (-1:
		/// none); netije is the new selection (Move) or the chosen candidate (Choose).
		/// </summary>
		public static TahminHerket Kunupka(Key key, ModifierKeys mods, int tallanghan, int sani, out int netije)
		{
			netije = tallanghan;
			if(mods != ModifierKeys.None) return TahminHerket.Pass;
			int reqem = -1;
			if(key >= Key.D1 && key <= Key.D9) reqem = key - Key.D1;
			else if(key >= Key.NumPad1 && key <= Key.NumPad9) reqem = key - Key.NumPad1;
			if(reqem >= 0){
				if(reqem >= sani) return TahminHerket.Pass;
				netije = reqem;
				return TahminHerket.Choose;
			}
			switch(key){
				case Key.Down:
					netije = Math.Min(tallanghan + 1, sani - 1);
					return TahminHerket.Move;
				case Key.Up:
					if(tallanghan < 0) return TahminHerket.CloseAndPass;  // nothing selected: the caret goes up
					netije = tallanghan - 1;
					return TahminHerket.Move;
				case Key.Tab:
					netije = Math.Max(tallanghan, 0);   // nothing selected: the first candidate
					return TahminHerket.Choose;
				case Key.Enter:
					return tallanghan < 0 ? TahminHerket.CloseAndPass : TahminHerket.Choose;  // nothing selected: a new line
				case Key.Escape:
					return TahminHerket.Close;
				default:
					return TahminHerket.Pass;
			}
		}
	}
}
