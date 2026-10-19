/*
 * The words the user chose from the prediction list, with how often, kept in a file of
 * their own ("word count" lines, like uyghur_imla.txt). Only words and counts: no text
 * around them. Forgetting them deletes the file.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UyghurEditPP
{
	public class TahminIshletkuchi
	{
		readonly string gHojjet;
		Dictionary<string, int> gSozler;

		/// <summary>Reads the file; a file that cannot be read counts as empty (and is logged).</summary>
		public TahminIshletkuchi(string hojjet)
		{
			gHojjet = hojjet;
			try{
				gSozler = Oqu(hojjet);
			}
			catch(Exception ee){
				if(!(ee is IOException || ee is UnauthorizedAccessException)) throw;
				ErrorLog.Write(ee);
				gSozler = new Dictionary<string, int>(StringComparer.Ordinal);
			}
		}

		/// <summary>
		/// Counts one more choice of soz. The file is read again first, so what another window
		/// learned is kept; if it cannot be read, nothing is written (the exception goes up).
		/// </summary>
		public void Qosh(string soz)
		{
			soz = SozTahmini.Normalize(soz);
			if(soz.Length == 0 || soz.IndexOf(' ') >= 0) return;
			Dictionary<string, int> sozler = Oqu(gHojjet);
			int tekrar;
			sozler.TryGetValue(soz, out tekrar);
			sozler[soz] = tekrar + 1;
			SafeFile.Write(gHojjet, stream => {
				using(StreamWriter sw = new StreamWriter(stream, new UTF8Encoding(true), 4096, true)){  // SafeFile closes the stream itself
					foreach(KeyValuePair<string, int> kv in sozler){
						sw.Write(kv.Key + " " + kv.Value + "\r\n");
					}
				}
			});
			gSozler = sozler;
		}

		/// <summary>
		/// Up to n learned words that start with bash: more often chosen first, then the same
		/// tie-break as SozTahmini (shorter, then ordinal).
		/// </summary>
		public List<string> Find(string bash, int n)
		{
			bash = SozTahmini.Normalize(bash ?? "");
			List<string> netije = new List<string>();
			if(bash.Length == 0) return netije;
			// shortcut: looks at every learned word; fine for the few hundred a person chooses, index them if it grows to many thousands.
			foreach(string soz in gSozler.Keys){
				if(soz.StartsWith(bash, StringComparison.Ordinal)) netije.Add(soz);
			}
			netije.Sort((a, b) => {
				int c = gSozler[b].CompareTo(gSozler[a]);
				if(c == 0) c = a.Length.CompareTo(b.Length);
				return c != 0 ? c : string.CompareOrdinal(a, b);
			});
			if(netije.Count > n) netije.RemoveRange(n, netije.Count - n);
			return netije;
		}

		/// <summary>Forgets every learned word and deletes the file.</summary>
		public void Ochur()
		{
			File.Delete(gHojjet);
			gSozler = new Dictionary<string, int>(StringComparer.Ordinal);
		}

		static Dictionary<string, int> Oqu(string hojjet)
		{
			Dictionary<string, int> sozler = new Dictionary<string, int>(StringComparer.Ordinal);
			if(!File.Exists(hojjet)) return sozler;
			foreach(string qur in File.ReadAllLines(hojjet, Encoding.UTF8)){
				string[] qisimlar = qur.Split(new[]{ ' ' }, StringSplitOptions.RemoveEmptyEntries);
				int tekrar;
				if(qisimlar.Length != 2 || !int.TryParse(qisimlar[1], out tekrar) || tekrar <= 0) continue;
				sozler[SozTahmini.Normalize(qisimlar[0])] = tekrar;
			}
			return sozler;
		}
	}
}
