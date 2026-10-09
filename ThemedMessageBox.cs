/*
 * A message box drawn by the program, in the colors of the light or dark theme.
 *
 * The system MessageBox cannot be themed (it stays light in the dark theme) and on
 * Windows 11 its caption band and body do not line up. This form keeps what the system
 * box did: the button sets, results, default button, Enter/Esc, Alt access keys in the
 * button texts, the icon and its sound, Ctrl+C to copy, and right-to-left layout in the
 * UEY UI. CenteredMessageBox.Show centers it on its owner.
 */
using System;
using System.Drawing;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace UyghurEditPP
{
	public sealed class ThemedMessageBox : Form
	{
		/// <summary>A button of the box: its langdata key and the result it gives.</summary>
		internal struct ButtonSpec
		{
			public readonly string Key;
			public readonly DialogResult Result;
			public ButtonSpec(string key, DialogResult result) { Key = key; Result = result; }
		}

		/// <summary>
		/// The buttons of a button set, in reading order, like the system MessageBox; null
		/// for a set this box does not draw (AbortRetryIgnore is not used by the program).
		/// </summary>
		internal static ButtonSpec[] Buttons(MessageBoxButtons buttons)
		{
			switch(buttons){
				case MessageBoxButtons.OK:
					return new[] { new ButtonSpec("OK", DialogResult.OK) };
				case MessageBoxButtons.OKCancel:
					return new[] { new ButtonSpec("OK", DialogResult.OK), new ButtonSpec("Cancel", DialogResult.Cancel) };
				case MessageBoxButtons.YesNo:
					return new[] { new ButtonSpec("Yes", DialogResult.Yes), new ButtonSpec("No", DialogResult.No) };
				case MessageBoxButtons.YesNoCancel:
					return new[] { new ButtonSpec("Yes", DialogResult.Yes), new ButtonSpec("No", DialogResult.No), new ButtonSpec("Cancel", DialogResult.Cancel) };
				case MessageBoxButtons.RetryCancel:
					return new[] { new ButtonSpec("Retry", DialogResult.Retry), new ButtonSpec("Cancel", DialogResult.Cancel) };
				default:
					return null;
			}
		}

		/// <summary>
		/// What Esc and the close box give, as in the system MessageBox: Cancel when there is
		/// a Cancel button, OK for an OK-only box, and nothing (the box cannot be closed that
		/// way) for Yes/No.
		/// </summary>
		internal static DialogResult EscapeResult(MessageBoxButtons buttons)
		{
			switch(buttons){
				case MessageBoxButtons.OK:
					return DialogResult.OK;
				case MessageBoxButtons.OKCancel:
				case MessageBoxButtons.YesNoCancel:
				case MessageBoxButtons.RetryCancel:
					return DialogResult.Cancel;
				default:
					return DialogResult.None;
			}
		}

		/// <summary>
		/// The width of the area for the text: wide enough for the text and for the row of
		/// buttons, but between min and max.
		/// </summary>
		internal static int FitWidth(int textWidth, int buttonsWidth, int min, int max)
		{
			int w = Math.Max(textWidth, buttonsWidth);
			return Math.Max(min, Math.Min(w, max));
		}

		/// <summary>The sound the system MessageBox plays for an icon (none without an icon).</summary>
		internal static SystemSound Sound(MessageBoxIcon icon)
		{
			switch(icon){
				case MessageBoxIcon.Error: return SystemSounds.Hand;          // also Hand, Stop
				case MessageBoxIcon.Warning: return SystemSounds.Exclamation; // also Exclamation
				case MessageBoxIcon.Question: return SystemSounds.Question;
				case MessageBoxIcon.Information: return SystemSounds.Asterisk; // also Asterisk
				default: return null;
			}
		}

		// The icon resource in user32.dll that the system message box icon comes from (0 for
		// none). Checked on Windows 11 26200: at 32 pixels these are the same images as
		// SystemIcons.Warning/Question/Error/Information. (LoadIconWithScaleDown would need
		// comctl32 version 6, which the program's P/Invoke calls do not get.)
		static int IconId(MessageBoxIcon icon)
		{
			switch(icon){
				case MessageBoxIcon.Warning: return 101;
				case MessageBoxIcon.Question: return 102;
				case MessageBoxIcon.Error: return 103;
				case MessageBoxIcon.Information: return 104;
				default: return 0;
			}
		}

		/// <summary>
		/// The system icon drawn at size pixels, from the icon's own image of that size (or the
		/// nearest one), so it stays sharp at 125-200%; a scaled 32-pixel copy when that fails.
		/// </summary>
		internal static Bitmap IconBitmap(MessageBoxIcon icon, int size)
		{
			int id = IconId(icon);
			if(id == 0){
				return null;
			}
			IntPtr hicon = LoadImage(GetModuleHandle("user32.dll"), new IntPtr(id), IMAGE_ICON, size, size, 0);
			if(hicon != IntPtr.Zero){
				try{
					using(Icon i = Icon.FromHandle(hicon)){
						return i.ToBitmap();
					}
				}
				finally{
					DestroyIcon(hicon);
				}
			}
			Bitmap scaled = new Bitmap(size, size);
			using(Graphics g = Graphics.FromImage(scaled)){
				g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
				g.DrawIcon(SystemIcon(icon), new Rectangle(0, 0, size, size));
			}
			return scaled;
		}

		static Icon SystemIcon(MessageBoxIcon icon)
		{
			switch(icon){
				case MessageBoxIcon.Error: return SystemIcons.Error;
				case MessageBoxIcon.Warning: return SystemIcons.Warning;
				case MessageBoxIcon.Question: return SystemIcons.Question;
				case MessageBoxIcon.Information: return SystemIcons.Information;
				default: return null;
			}
		}

		// Sizes at 96 DPI.
		const int Margin96 = 16, IconSize96 = 32, Gap96 = 12, MinText96 = 200, MaxText96 = 520;
		const int ButtonMinWidth96 = 88, ButtonHeight96 = 28, ButtonGap96 = 8, BandPadding96 = 12;

		readonly string gText;
		readonly MessageBoxButtons gButtons;
		readonly MessageBoxIcon gIcon;
		readonly UiTheme gTheme;
		readonly bool gRtl;
		readonly Panel gBody = new Panel();
		readonly Panel gBand = new Panel();
		readonly Label gLabel = new Label();
		readonly PictureBox gPicture = new PictureBox();
		readonly Button[] gButtonControls;
		readonly float gPointSize;
		readonly string gFontName;
		readonly IntPtr gOwner;
		Font gFont;

		ThemedMessageBox(IntPtr owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, bool rtl, UiTheme theme)
		{
			gOwner = owner;
			gText = text ?? "";
			gButtons = buttons;
			gIcon = icon;
			gTheme = theme;
			gRtl = rtl;

			Text = caption ?? "";
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = false;
			MinimizeBox = false;
			ShowIcon = false;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.Manual;
			AutoScaleMode = AutoScaleMode.None;   // laid out in pixels for the monitor's DPI (Relayout)
			KeyPreview = true;
			if(rtl){
				RightToLeft = RightToLeft.Yes;
				RightToLeftLayout = true;
			}

			// The menu font of the program: Windows' message font for English and Japanese,
			// UKIJ Tuz for the Uyghur scripts (as the menus).
			string lang = MainForm.gLang != null ? MainForm.gLang.LanguaID : "uly";
			if("eng".Equals(lang) || "jpn".Equals(lang)){
				using(Font f = SystemFonts.MessageBoxFont){   // a new Font on every call
					gFontName = f.Name;
					gPointSize = f.SizeInPoints;
				}
			}
			else{
				gFontName = "UKIJ Tuz";
				gPointSize = 12f;
			}

			gLabel.UseMnemonic = false;   // a path or message may contain "&"
			gLabel.AutoSize = false;
			gLabel.Text = gText;
			gLabel.BackColor = Color.Transparent;
			gPicture.SizeMode = PictureBoxSizeMode.Zoom;
			gPicture.BackColor = Color.Transparent;
			gPicture.Visible = SystemIcon(icon) != null;
			gBody.Controls.Add(gPicture);
			gBody.Controls.Add(gLabel);
			gBand.Paint += PaintBandLine;
			Controls.Add(gBody);
			Controls.Add(gBand);

			// ShowBox refuses the sets that are not drawn; an OK button keeps the box usable anyway.
			ButtonSpec[] specs = Buttons(buttons) ?? Buttons(MessageBoxButtons.OK);
			gButtonControls = new Button[specs.Length];
			DialogResult escape = EscapeResult(buttons);
			for(int i = 0; i < specs.Length; i++){
				Button b = new Button();
				b.Text = ButtonText(specs[i].Key);
				b.DialogResult = specs[i].Result;
				b.UseMnemonic = true;
				gButtonControls[i] = b;
				gBand.Controls.Add(b);
				if(specs[i].Result == escape){
					CancelButton = b;
				}
			}
			AcceptButton = gButtonControls[0];   // Button1 is the default, as before
			ApplyColors();
		}

		// The keys are written out, so the langdata tests see them as used.
		static string ButtonText(string key)
		{
			try{
				Language lang = MainForm.gLang;
				if(lang == null){
					return key;
				}
				switch(key){
					case "OK": return lang.GetText("OK");
					case "Cancel": return lang.GetText("Cancel");
					case "Yes": return lang.GetText("Yes");
					case "No": return lang.GetText("No");
					case "Retry": return lang.GetText("Retry");
					default: return lang.GetText(key);
				}
			}
			catch(Exception){
				return key;
			}
		}

		void ApplyColors()
		{
			bool dark = gTheme.IsDark;
			BackColor = dark ? gTheme.FormBack : gTheme.TabActive;
			gBody.BackColor = dark ? gTheme.FormBack : gTheme.TabActive;
			gBand.BackColor = gTheme.Bar;
			gLabel.ForeColor = dark ? gTheme.FormText : gTheme.BarText;
			foreach(Button b in gButtonControls){
				if(dark){
					b.FlatStyle = FlatStyle.Flat;
					// The default button is marked by the accent color, as the light buttons do.
					b.FlatAppearance.BorderColor = b == gButtonControls[0] ? gTheme.Accent : gTheme.BarLine;
					b.BackColor = gTheme.ButtonBack;
					b.ForeColor = gTheme.FormText;
				}
				else{
					b.UseVisualStyleBackColor = true;
				}
			}
		}

		void PaintBandLine(object sender, PaintEventArgs e)
		{
			using(Pen pen = new Pen(gTheme.BarLine)){
				e.Graphics.DrawLine(pen, 0, 0, gBand.Width, 0);
			}
		}

		// The no-close style for Yes/No, which (as in the system box) cannot be closed by
		// the close box, Alt+F4 or Esc.
		protected override CreateParams CreateParams
		{
			get{
				CreateParams cp = base.CreateParams;
				if(EscapeResult(gButtons) == DialogResult.None){
					cp.ClassStyle |= 0x0200; // CS_NOCLOSE
				}
				return cp;
			}
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			UiTheme.SetTitleBar(Handle, gTheme.IsDark);
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			// The close box gives the Esc result; for Yes/No it is disabled.
			if(DialogResult == DialogResult.None || (DialogResult == DialogResult.Cancel && !HasButton(DialogResult.Cancel))){
				DialogResult escape = EscapeResult(gButtons);
				if(escape == DialogResult.None){
					e.Cancel = true;
				}
				else{
					DialogResult = escape;
				}
			}
			base.OnFormClosing(e);
		}

		bool HasButton(DialogResult result)
		{
			foreach(Button b in gButtonControls){
				if(b.DialogResult == result){
					return true;
				}
			}
			return false;
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			// Ctrl+C copies the caption, text and buttons, like the system MessageBox.
			if(e.Control && e.KeyCode == Keys.C){
				try{
					string line = "---------------------------" + Environment.NewLine;
					string buttons = "";
					foreach(Button b in gButtonControls){
						buttons += b.Text.Replace("&", "") + "   ";
					}
					Clipboard.SetText(line + Text + Environment.NewLine + line + gText + Environment.NewLine + line + buttons.TrimEnd() + Environment.NewLine + line);
				}
				catch(Exception ee){
					System.Diagnostics.Debug.WriteLine(ee);
				}
				e.Handled = true;
			}
			base.OnKeyDown(e);
		}

		protected override void OnDpiChanged(DpiChangedEventArgs e)
		{
			base.OnDpiChanged(e);
			Relayout(e.DeviceDpiNew, false);
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			gButtonControls[0].Focus();
			SystemSound sound = Sound(gIcon);
			if(sound != null){
				sound.Play();
			}
		}

		int Px(int v96, int dpi)
		{
			return (int)Math.Round(v96 * dpi / 96.0);
		}

		/// <summary>
		/// Sizes and places everything for a DPI. The font is made in pixels for that DPI, so
		/// the measured text matches the monitor the box is on.
		/// </summary>
		// The size of a large icon at a DPI (32 at 100%), as Windows gives it.
		int IconSizeFor(int dpi)
		{
			try{
				int size = GetSystemMetricsForDpi(SM_CXICON, (uint)dpi);
				if(size > 0){
					return size;
				}
			}
			catch(EntryPointNotFoundException){
				// before Windows 10 1607
			}
			return Px(IconSize96, dpi);
		}

		void Relayout(int dpi, bool center)
		{
			// Every control gets the new font before the old one is disposed.
			Font old = gFont;
			gFont = AppFonts.Create(gFontName, gPointSize * dpi / 72f, FontStyle.Regular, GraphicsUnit.Pixel);
			Font = gFont;
			gLabel.Font = gFont;
			foreach(Button b in gButtonControls){
				b.Font = gFont;
			}
			if(old != null){
				old.Dispose();
			}

			int icon = IconId(gIcon) != 0 ? IconSizeFor(dpi) : 0;
			if(icon > 0){
				Image oldImage = gPicture.Image;
				gPicture.Image = IconBitmap(gIcon, icon);
				if(oldImage != null){
					oldImage.Dispose();
				}
			}

			int margin = Px(Margin96, dpi), gap = Px(Gap96, dpi);
			Rectangle area = gOwner != IntPtr.Zero ? Screen.FromHandle(gOwner).WorkingArea : Screen.PrimaryScreen.WorkingArea;
			int maxText = Math.Min(Px(MaxText96, dpi), area.Width * 3 / 5);
			// A line that cannot be broken (a long path) may make the box wider, up to most of the screen.
			int hardMax = Math.Max(maxText, area.Width * 9 / 10 - 2 * margin - icon - gap);

			TextFormatFlags flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix;
			if(gRtl){
				flags |= TextFormatFlags.RightToLeft;
			}
			Size text = TextRenderer.MeasureText(gText, gFont, new Size(maxText, int.MaxValue), flags);

			// The buttons: each at least the minimum width, wide enough for its text.
			int bh = Math.Max(Px(ButtonHeight96, dpi), gFont.Height + Px(10, dpi));
			int bgap = Px(ButtonGap96, dpi);
			int buttonsWidth = 0;
			int[] bw = new int[gButtonControls.Length];
			for(int i = 0; i < gButtonControls.Length; i++){
				Size t = TextRenderer.MeasureText(gButtonControls[i].Text, gFont);
				bw[i] = Math.Max(Px(ButtonMinWidth96, dpi), t.Width + Px(24, dpi));
				buttonsWidth += bw[i] + (i > 0 ? bgap : 0);
			}

			int textWidth = FitWidth(text.Width + 2, buttonsWidth - icon - gap, Px(MinText96, dpi), hardMax);
			if(text.Width > maxText){
				text = TextRenderer.MeasureText(gText, gFont, new Size(textWidth, int.MaxValue), flags);
			}
			int textHeight = text.Height;

			int bodyHeight = margin + Math.Max(icon, textHeight) + margin;
			int clientWidth = margin + (icon > 0 ? icon + gap : 0) + textWidth + margin;
			clientWidth = Math.Max(clientWidth, buttonsWidth + 2 * Px(BandPadding96, dpi));
			int bandHeight = bh + 2 * Px(BandPadding96, dpi);

			// Laid out left to right. RightToLeftLayout mirrors the form (its title bar and the
			// two panels) but not the controls inside the panels, so those are mirrored here.
			gBody.SetBounds(0, 0, clientWidth, bodyHeight);
			Place(gPicture, margin, margin, icon, icon, clientWidth);
			int textLeft = margin + (icon > 0 ? icon + gap : 0);
			// A one-line text sits on the middle of the icon.
			int textTop = icon > textHeight ? margin + (icon - textHeight) / 2 : margin;
			Place(gLabel, textLeft, textTop, clientWidth - textLeft - margin, textHeight, clientWidth);
			gLabel.TextAlign = ContentAlignment.TopLeft;

			gBand.SetBounds(0, bodyHeight, clientWidth, bandHeight);
			int x = clientWidth - Px(BandPadding96, dpi);
			for(int i = gButtonControls.Length - 1; i >= 0; i--){
				x -= bw[i];
				Place(gButtonControls[i], x, Px(BandPadding96, dpi), bw[i], bh, clientWidth);
				x -= bgap;
			}
			ClientSize = new Size(clientWidth, bodyHeight + bandHeight);

			if(center){
				CenterOnOwner(area);
			}
		}

		void Place(Control c, int x, int y, int width, int height, int parentWidth)
		{
			c.SetBounds(MirrorX(x, width, parentWidth, gRtl), y, width, height);
		}

		/// <summary>The left edge of a control at x, mirrored in its parent when rtl.</summary>
		internal static int MirrorX(int x, int width, int parentWidth, bool rtl)
		{
			return rtl ? parentWidth - x - width : x;
		}

		void CenterOnOwner(Rectangle area)
		{
			Rectangle o;
			RECT r;
			if(gOwner != IntPtr.Zero && !IsIconic(gOwner) && IsWindowVisible(gOwner) && GetWindowRect(gOwner, out r)){
				o = new Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
			}
			else{
				o = area;
			}
			Location = CenteredMessageBox.CenterIn(o, Size, area);
		}

		/// <summary>
		/// Shows the box (modal to owner when given) and returns the button chosen. Must be
		/// called on the owner's UI thread. Throws NotSupportedException for a button set it
		/// does not draw.
		/// </summary>
		internal static DialogResult ShowBox(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, bool rtl, UiTheme theme)
		{
			if(Buttons(buttons) == null){
				throw new NotSupportedException(buttons.ToString());
			}
			IntPtr ownerHandle = owner != null ? owner.Handle : IntPtr.Zero;
			using(ThemedMessageBox box = new ThemedMessageBox(ownerHandle, text, caption, buttons, icon, rtl, theme)){
				int dpi = ownerHandle != IntPtr.Zero ? WindowDpi(ownerHandle) : box.DeviceDpi;
				box.Relayout(dpi, true);
				DialogResult result = owner != null ? box.ShowDialog(owner) : box.ShowDialog();
				return result;
			}
		}

		// The DPI of the monitor a window is on (Windows 10 1607+), else the system DPI.
		static int WindowDpi(IntPtr hwnd)
		{
			try{
				int dpi = (int)GetDpiForWindow(hwnd);
				if(dpi > 0){
					return dpi;
				}
			}
			catch(EntryPointNotFoundException){
			}
			using(Graphics g = Graphics.FromHwnd(IntPtr.Zero)){
				return (int)g.DpiX;
			}
		}

		protected override void Dispose(bool disposing)
		{
			// The controls are disposed first; the image and font they used after them.
			Image image = disposing ? gPicture.Image : null;
			base.Dispose(disposing);
			if(disposing){
				if(image != null){
					image.Dispose();
				}
				if(gFont != null){
					gFont.Dispose();
					gFont = null;
				}
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		struct RECT { public int Left, Top, Right, Bottom; }

		[DllImport("user32.dll")]
		static extern uint GetDpiForWindow(IntPtr hwnd);
		[DllImport("user32.dll")]
		static extern int GetSystemMetricsForDpi(int nIndex, uint dpi);
		const int SM_CXICON = 11;
		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		static extern IntPtr GetModuleHandle(string lpModuleName);
		[DllImport("user32.dll")]
		static extern IntPtr LoadImage(IntPtr hinst, IntPtr name, uint type, int cx, int cy, uint fuLoad);
		const uint IMAGE_ICON = 1;
		[DllImport("user32.dll")]
		static extern bool DestroyIcon(IntPtr hIcon);
		[DllImport("user32.dll")]
		static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
		[DllImport("user32.dll")]
		static extern bool IsIconic(IntPtr hWnd);
		[DllImport("user32.dll")]
		static extern bool IsWindowVisible(IntPtr hWnd);
	}
}
