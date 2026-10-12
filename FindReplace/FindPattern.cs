/*
 * Builds the regular expression for the Find and Replace window from what was typed
 * and the options chosen. Kept apart from the window so that it can be tested.
 */
using System;
using System.Text.RegularExpressions;

namespace UyghurEditPP.FindReplace
{
	public enum FindMode
	{
		Normal,             // the text is searched as typed
		Wildcards,          // \t \n \r ... and * ? are understood
		RegularExpression
	}

	public static class FindPattern
	{
		/// <summary>
		/// The longest a single search may take (a pattern such as (a+)+$ can take for ever).
		/// </summary>
		public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

		/// <summary>
		/// Throws ArgumentException when the pattern is not a valid regular expression
		/// (only possible in the wildcard and regular expression modes). Matching throws
		/// RegexMatchTimeoutException when it takes longer than <paramref name="timeout"/>.
		/// </summary>
		public static Regex Build(string text, FindMode mode, bool wholeWord, bool caseSensitive, bool searchUp, TimeSpan timeout)
		{
			RegexOptions options = RegexOptions.None;
			if(searchUp){
				options |= RegexOptions.RightToLeft;
			}
			if(!caseSensitive){
				options |= RegexOptions.IgnoreCase | RegexOptions.CultureInvariant; // ULY I/i, never Turkish İ/ı
			}

			string pattern = text;
			if(mode == FindMode.Normal){
				pattern = Regex.Escape(pattern);
			}
			else if(mode == FindMode.Wildcards){
				pattern = pattern.Replace("\\*", ".*").Replace("\\?", ".");
			}

			if(wholeWord){
				pattern = "\\b" + pattern + "\\b";
			}
			return new Regex(pattern, options, timeout);
		}

		public static Regex Build(string text, FindMode mode, bool wholeWord, bool caseSensitive, bool searchUp)
		{
			return Build(text, mode, wholeWord, caseSensitive, searchUp, Timeout);
		}
	}
}
